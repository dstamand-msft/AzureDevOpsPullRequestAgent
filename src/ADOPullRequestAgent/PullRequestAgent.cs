using System.IO.Abstractions;
using System.Text;
using GitHub.Copilot.SDK;
using Microsoft.Extensions.Logging;

namespace ADOPullRequestAgent
{
    public class PullRequestAgent
    {
        private readonly IFileSystem _fileSystem;
        private readonly string _adoMcpAuthenticationToken;
        private readonly AgentOptions _agentOptions;

        public PullRequestAgent(IFileSystem fileSystem, string adoMcpAuthenticationToken, AgentOptions agentOptions)
        {
            _fileSystem = fileSystem;
            _adoMcpAuthenticationToken = adoMcpAuthenticationToken ??
                                         throw new ArgumentNullException(nameof(adoMcpAuthenticationToken), "ADO_MCP_AUTH_TOKEN cannot be null");
            _agentOptions = agentOptions;
        }

        /// <summary>
        /// Executes an asynchronous operation for the specified organization, project, and repository.
        /// </summary>
        /// <param name="pullRequestId">The ID of the pull request to operate on.</param>
        /// <param name="organizationName">The name of the organization to operate on.</param>
        /// <param name="projectName">The name of the project within the organization.</param>
        /// <param name="repositoryName">The name of the repository within the project.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public async Task<string> RunAsync(int pullRequestId, string organizationName, string projectName, string repositoryName)
        {
            // should optimize to not load everytime, but once, but considering this is run per pull request, the performance impact should be minimal
            var systemInstructions = await _fileSystem.File.ReadAllTextAsync("pullreview.prompt");

            using (ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
                   {
                       builder.SetMinimumLevel(LogLevel.Trace);
                       builder.AddConsole();
                   }))
            {
                var logger = loggerFactory.CreateLogger<CopilotClient>();
                var copilotOptions = new CopilotClientOptions
                {
                    Logger = logger,
                    LogLevel = nameof(LogLevel.Trace),
                    // https://github.com/github/copilot-sdk/blob/main/docs/getting-started.md#connecting-to-an-external-cli-server
                    // ex: copilot --headless --port 4321
                    CliUrl = $"localhost:{_agentOptions.CliPort}",
                    UseStdio = false
                };

                var adoMcpNpxCmd = $"npx -y @azure-devops/mcp@latest {organizationName} --domains core repositories search work work-items --authentication envvar";

                var cliRunningOnWindows = false;
                List<string> cliWindowsArgs = [
                    "/c",
                    $"set ADO_MCP_AUTH_TOKEN={_adoMcpAuthenticationToken} && {adoMcpNpxCmd}"
                ];
                List<string> cliNixOsXArgs = [
                    "-c",
                    $"export ADO_MCP_AUTH_TOKEN={_adoMcpAuthenticationToken} && {adoMcpNpxCmd}"
                ];

                // NOTE: The CopilotClient requires the environment variable COPILOT_GITHUB_TOKEN to be set with a GitHub token.
                // This token is used by the client to authenticate with GitHub services.
                // If the token is not set, the client _will_ not function properly and will throw an authentication error.
                // The token needs Copilot Requests on the account scope (fine-grained).
                await using (var client = new CopilotClient(copilotOptions))
                {
                    await client.StartAsync();
                    var sessionConfig = new SessionConfig
                    {
                        Model = _agentOptions.Model,
                        OnPermissionRequest = (_, _) => Task.FromResult(new PermissionRequestResult { Kind = "approved" }),
                        SystemMessage = new SystemMessageConfig
                        {
                            Content = systemInstructions,
                            Mode = SystemMessageMode.Append
                        },
                        McpServers = new Dictionary<string, object>
                        {
                            // workaround until https://github.com/github/copilot-sdk/issues/163 is fixed
                            ["azure-devops"] = new McpLocalServerConfig
                            {
                                Type = "local",
                                Command = cliRunningOnWindows ? "cmd" : "sh",
                                // token is set in the env var ADO_MCP_AUTH_TOKEN
                                // see: https://github.com/microsoft/azure-devops-mcp/blob/main/docs/GETTINGSTARTED.md#using-token-authentication-via-environment-variables
                                Args = cliRunningOnWindows ? cliWindowsArgs : cliNixOsXArgs,
                                Env = new Dictionary<string, string>
                                {
                                    ["ADO_MCP_AUTH_TOKEN"] = _adoMcpAuthenticationToken
                                },
                                Tools = ["*"]
                            },
                            ["microsoft-learn"] = new McpRemoteServerConfig
                            {
                                Type = "http",
                                Url = "https://learn.microsoft.com/api/mcp",
                                Tools = ["*"]
                            }
                        }
                    };

                    await using (var session = await client.CreateSessionAsync(sessionConfig))
                    {
                        var done = new TaskCompletionSource<object?>();
                        var sb = new StringBuilder();

                        session.On(evt =>
                        {
                            switch (evt)
                            {
                                case AssistantMessageEvent msg:
                                    logger.LogInformation(msg.Data.Content);
                                    sb.AppendLine(msg.Data.Content);
                                    break;
                                case AssistantReasoningEvent msg:
                                    logger.LogInformation(msg.Data.Content);
                                    sb.AppendLine(msg.Data.Content);
                                    break;
                                case SessionIdleEvent:
                                    done.SetResult(null);
                                    break;
                                case SessionErrorEvent msg:
                                    done.SetResult(msg.Data);
                                    break;
                            }
                        });

                        await session.SendAsync(new MessageOptions
                        {
                            Prompt = $"Review the pull request number {pullRequestId} in Azure DevOps project {projectName} for the repository {repositoryName}"
                        });

                        var res = await done.Task;
                        if (res is SessionErrorEvent errorEvent)
                        {
                            throw new Exception($"An error occured while processing the request{(errorEvent.Data.StatusCode != null ? $" [Status Code {errorEvent.Data.StatusCode}]" : string.Empty)}: {errorEvent.Data.Message}.{(errorEvent.Data.Stack != null ? $"{Environment.NewLine}{errorEvent.Data.Stack}" : string.Empty)}");
                        }
                        return sb.ToString();
                    }
                }
            }
        }
    }
}