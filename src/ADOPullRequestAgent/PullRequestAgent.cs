using System.Diagnostics;
using System.IO.Abstractions;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace ADOPullRequestAgent
{
    public class PullRequestAgent
    {
        private readonly IFileSystem _fileSystem;
        private readonly string _adoMcpAuthenticationToken;
        private readonly AgentOptions _agentOptions;

        /// <summary>
        /// Timeout for the Claude Code CLI process. PR reviews with MCP tool calls can be slow.
        /// </summary>
        private static readonly TimeSpan ProcessTimeout = TimeSpan.FromMinutes(30);

        public PullRequestAgent(IFileSystem fileSystem, string adoMcpAuthenticationToken, AgentOptions agentOptions)
        {
            _fileSystem = fileSystem;
            _adoMcpAuthenticationToken = adoMcpAuthenticationToken ??
                                         throw new ArgumentNullException(nameof(adoMcpAuthenticationToken), "ADO_MCP_AUTH_TOKEN cannot be null");
            _agentOptions = agentOptions;
        }

        /// <summary>
        /// Runs an AI-powered code review on the specified pull request using Claude Code CLI.
        /// </summary>
        /// <param name="pullRequestId">The ID of the pull request to review.</param>
        /// <param name="organizationName">The name of the Azure DevOps organization.</param>
        /// <param name="projectName">The name of the Azure DevOps project.</param>
        /// <param name="repositoryName">The name of the Azure DevOps repository.</param>
        /// <returns>The review output as a string.</returns>
        public async Task<string> RunAsync(int pullRequestId, string organizationName, string projectName, string repositoryName)
        {
            // Load the system prompt and inject the sources directory
            var systemInstructions = await _fileSystem.File.ReadAllTextAsync("pullreview.prompt");
            systemInstructions = systemInstructions.Replace("{{SOURCES_DIRECTORY}}", _agentOptions.SourcesDirectory);

            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.SetMinimumLevel(LogLevel.Information);
                builder.AddConsole();
            });
            var logger = loggerFactory.CreateLogger<PullRequestAgent>();

            // Write system prompt to a temp file for --system-prompt-file
            var tempDir = _fileSystem.Path.GetTempPath();
            var systemPromptPath = _fileSystem.Path.Combine(tempDir, _fileSystem.Path.GetRandomFileName());
            await _fileSystem.File.WriteAllTextAsync(systemPromptPath, systemInstructions);

            // Build and write MCP config to a temp file for --mcp-config
            var mcpConfigPath = _fileSystem.Path.Combine(tempDir, _fileSystem.Path.GetRandomFileName());
            var mcpConfig = BuildMcpConfig(organizationName);
            await _fileSystem.File.WriteAllTextAsync(mcpConfigPath, mcpConfig);

            try
            {
                var userPrompt = $"Review the pull request number {pullRequestId} in Azure DevOps project {projectName} for the repository {repositoryName}. The repository source code is cloned locally at: {_agentOptions.SourcesDirectory}";

                var arguments = BuildClaudeArguments(systemPromptPath, mcpConfigPath);

                logger.LogInformation("Starting Claude Code review for PR #{PullRequestId} in {Project}/{Repository}", pullRequestId, projectName, repositoryName);
                logger.LogInformation("Using model: {Model}", _agentOptions.Model);

                var reviewStopwatch = Stopwatch.StartNew();
                var (exitCode, stdout, stderr) = await RunClaudeProcessAsync(arguments, userPrompt, logger);
                reviewStopwatch.Stop();

                logger.LogInformation("[Metrics] Pull request review: {Elapsed}", reviewStopwatch.Elapsed);

                if (exitCode != 0)
                {
                    throw new Exception($"Claude Code CLI exited with code {exitCode}.{(string.IsNullOrWhiteSpace(stderr) ? string.Empty : $"{Environment.NewLine}stderr: {stderr}")}");
                }

                return stdout;
            }
            finally
            {
                // Clean up temp files
                TryDeleteFile(systemPromptPath);
                TryDeleteFile(mcpConfigPath);
            }
        }

        /// <summary>
        /// Builds the MCP server configuration JSON for Claude Code CLI.
        /// </summary>
        private string BuildMcpConfig(string organizationName)
        {
            var config = new
            {
                mcpServers = new Dictionary<string, object>
                {
                    ["azure-devops"] = new
                    {
                        command = "npx",
                        args = new[]
                        {
                            "-y", "@azure-devops/mcp", organizationName,
                            "--domains", "core", "repositories", "search", "work", "work-items",
                            "--authentication", "envvar"
                        },
                        env = new Dictionary<string, string>
                        {
                            ["ADO_MCP_AUTH_TOKEN"] = _adoMcpAuthenticationToken
                        }
                    },
                    ["microsoft-learn"] = new
                    {
                        type = "url",
                        url = "https://learn.microsoft.com/api/mcp"
                    }
                }
            };

            return JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        }

        /// <summary>
        /// Builds the command-line arguments for the claude CLI invocation.
        /// </summary>
        private List<string> BuildClaudeArguments(string systemPromptPath, string mcpConfigPath)
        {
            var args = new List<string>
            {
                "-p",
                "--output-format", "text",
                "--model", _agentOptions.Model,
                "--system-prompt-file", systemPromptPath,
                "--mcp-config", mcpConfigPath,
                "--no-session-persistence"
            };

            if (_agentOptions.MaxTurns.HasValue)
            {
                args.Add("--max-turns");
                args.Add(_agentOptions.MaxTurns.Value.ToString());
            }

            if (_agentOptions.MaxBudgetUsd.HasValue)
            {
                args.Add("--max-budget-usd");
                args.Add(_agentOptions.MaxBudgetUsd.Value.ToString("F2"));
            }

            return args;
        }

        /// <summary>
        /// Executes the claude CLI process, piping the user prompt via stdin and capturing output.
        /// </summary>
        private async Task<(int ExitCode, string Stdout, string Stderr)> RunClaudeProcessAsync(
            List<string> arguments, string userPrompt, ILogger logger)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "claude",
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = _agentOptions.SourcesDirectory
            };

            // Pass ANTHROPIC_API_KEY from current environment
            var anthropicApiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
            if (string.IsNullOrWhiteSpace(anthropicApiKey))
            {
                throw new InvalidOperationException("ANTHROPIC_API_KEY environment variable is not set. This is required for Claude Code CLI.");
            }
            startInfo.Environment["ANTHROPIC_API_KEY"] = anthropicApiKey;

            // Pass ADO token for the MCP server
            startInfo.Environment["ADO_MCP_AUTH_TOKEN"] = _adoMcpAuthenticationToken;

            foreach (var arg in arguments)
            {
                startInfo.ArgumentList.Add(arg);
            }

            using var process = new Process { StartInfo = startInfo };

            process.Start();

            // Start reading stdout and stderr concurrently before writing stdin to prevent
            // buffer deadlocks. ReadToEndAsync guarantees all output is drained before the
            // tasks complete, unlike BeginOutputReadLine which can leave data in the buffer
            // after WaitForExitAsync returns.
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();

            // Pipe the user prompt to stdin and close it
            await process.StandardInput.WriteLineAsync(userPrompt);
            process.StandardInput.Close();

            // Wait for process completion with timeout
            using var cts = new CancellationTokenSource(ProcessTimeout);
            try
            {
                await process.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                    }
                }
                catch (InvalidOperationException)
                {
                    // Process may have already exited; ignore to keep timeout handling reliable
                }
                throw new TimeoutException($"Claude Code CLI did not complete within {ProcessTimeout.TotalMinutes} minutes. The process was terminated.");
            }

            // Await stream reads after exit to ensure all buffered output has been captured
            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            foreach (var line in stderr.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                logger.LogDebug("[claude stderr] {Line}", line.TrimEnd('\r'));
            }

            return (process.ExitCode, stdout, stderr);
        }

        /// <summary>
        /// Attempts to delete a file, suppressing any exceptions.
        /// </summary>
        private void TryDeleteFile(string path)
        {
            try
            {
                if (_fileSystem.File.Exists(path))
                {
                    _fileSystem.File.Delete(path);
                }
            }
            catch
            {
                // Best-effort cleanup; ignore failures
            }
        }
    }
}
