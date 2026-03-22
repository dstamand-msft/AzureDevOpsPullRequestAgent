using System.CommandLine;
using System.Diagnostics;
using System.IO.Abstractions;

namespace ADOPullRequestAgent
{
    internal class Program
    {
        /// <summary>
        /// Entry point for the Azure DevOps pull request agent application. Parses command-line arguments, initiates a
        /// code review for the specified pull request using Claude Code CLI, and optionally saves the review output to a file.
        /// </summary>
        /// <param name="args">
        /// An array of command-line arguments that configure authentication, pull request details, organization,
        /// project, repository, and output options.
        /// </param>
        /// <returns>A task that represents the asynchronous execution of the application.</returns>
        static async Task Main(string[] args)
        {
            Option<string> adoTokenOption = new("--ado-token", "-at")
            {
                Description = "The token to use for authentication. To use this agent locally using your identity, use the Az CLI: az account get-access-token --resource 499b84ac-1321-427f-aa17-267ca6975798",
                Required = true
            };

            Option<int> pullRequestIdOption = new("--pull-request-id", "-id")
            {
                Description = "The ID of the pull request to review.",
                Required = true
            };
            Option<string> organizationNameOption = new("--organization-name", "-o")
            {
                Description = "The name of the Azure DevOps organization.",
                Required = true
            };
            Option<string> projectNameOption = new("--project-name", "-p")
            {
                Description = "The name of the Azure DevOps project.",
                Required = true
            };
            Option<string> repositoryNameOption = new("--repository-name", "-r")
            {
                Description = "The name of the Azure DevOps repository.",
                Required = true
            };
            Option<string> modelOption = new("--model", "-m")
            {
                Description = "The Claude model to use for the review (e.g., claude-sonnet-4-20250514)",
                Required = true
            };
            Option<string> outputDirectoryOption = new Option<string>("--output-directory")
            {
                Description = "The directory to save the review output to a file (optional)."
            };
            Option<string> sourcesDirectoryOption = new Option<string>("--sources-directory", "-sd")
            {
                Description = "The local directory path where the repository source code is cloned. The agent uses this path to run git commands and read source files during the review.",
                Required = true
            };
            Option<int?> maxTurnsOption = new("--max-turns")
            {
                Description = "Maximum number of agentic turns for Claude Code CLI. Used for cost control (optional)."
            };
            Option<decimal?> maxBudgetUsdOption = new("--max-budget-usd")
            {
                Description = "Maximum budget in USD for the Claude Code session. Used for cost control (optional)."
            };

            var rootCommand = new RootCommand("A pull request agent for Azure DevOps. Uses Claude Code CLI to perform AI-powered code reviews focusing on security, performance, and maintainability.");
            rootCommand.Options.Add(adoTokenOption);
            rootCommand.Options.Add(pullRequestIdOption);
            rootCommand.Options.Add(organizationNameOption);
            rootCommand.Options.Add(projectNameOption);
            rootCommand.Options.Add(repositoryNameOption);
            rootCommand.Options.Add(modelOption);
            rootCommand.Options.Add(outputDirectoryOption);
            rootCommand.Options.Add(sourcesDirectoryOption);
            rootCommand.Options.Add(maxTurnsOption);
            rootCommand.Options.Add(maxBudgetUsdOption);

            var parseResult = rootCommand.Parse(args);
            if (parseResult.Errors.Count != 0)
            {
                foreach (var parseError in parseResult.Errors)
                {
                    await Console.Error.WriteLineAsync(parseError.Message);
                }

                if (Debugger.IsAttached)
                {
                    Console.ReadKey();
                }

                return;
            }

            var token = parseResult.GetRequiredValue<string>(adoTokenOption);
            var pullRequestId = parseResult.GetRequiredValue<int>(pullRequestIdOption);
            var organizationName = parseResult.GetRequiredValue<string>(organizationNameOption);
            var projectName = parseResult.GetRequiredValue<string>(projectNameOption);
            var repositoryName = parseResult.GetRequiredValue<string>(repositoryNameOption);
            var model = parseResult.GetRequiredValue<string>(modelOption);
            var outputDirectory = parseResult.GetValue<string>(outputDirectoryOption);
            var sourcesDirectory = parseResult.GetRequiredValue<string>(sourcesDirectoryOption);
            var maxTurns = parseResult.GetValue<int?>(maxTurnsOption);
            var maxBudgetUsd = parseResult.GetValue<decimal?>(maxBudgetUsdOption);

            var agentOptions = new AgentOptions
            {
                Model = model,
                SourcesDirectory = sourcesDirectory,
                OutputDirectory = outputDirectory,
                MaxTurns = maxTurns,
                MaxBudgetUsd = maxBudgetUsd
            };

            var fileSystem = new FileSystem();
            var agent = new PullRequestAgent(fileSystem, token, agentOptions);

            var totalStopwatch = Stopwatch.StartNew();
            var response = await agent.RunAsync(pullRequestId, organizationName, projectName, repositoryName);
            totalStopwatch.Stop();

            Console.WriteLine(response);

            Console.WriteLine();
            Console.WriteLine("== Metrics ==");
            Console.WriteLine($"Total processing time: {totalStopwatch.Elapsed.TotalSeconds:F2}s ({totalStopwatch.Elapsed})");

            if (!string.IsNullOrWhiteSpace(outputDirectory))
            {
                using (var writer = new StreamWriter(fileSystem.FileStream.New(Path.Combine(outputDirectory, $"pull_request_{pullRequestId}_review.md"), FileMode.Create)))
                {
                    await writer.WriteLineAsync(response);
                }
            }
        }
    }
}
