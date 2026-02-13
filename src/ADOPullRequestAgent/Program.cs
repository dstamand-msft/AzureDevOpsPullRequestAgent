using GHCPAgent;
using System.CommandLine;
using System.Diagnostics;
using System.IO.Abstractions;

namespace ADOPullRequestAgent
{
    internal class Program
    {
        /// <summary>
        /// Entry point for the Azure DevOps pull request agent application. Parses command-line arguments, initiates a
        /// code review for the specified pull request, and optionally saves the review output to a file.
        /// </summary>
        /// <remarks>This method validates command-line input and requires valid values for authentication
        /// and pull request identification. If parsing fails, error messages are written to the standard error stream
        /// and the application exits. The review output can be saved to a file if the corresponding option is
        /// specified.</remarks>
        /// <param name="args">An array of command-line arguments that configure authentication, pull request details, organization,
        /// project, repository, and output options.</param>
        /// <returns>A task that represents the asynchronous execution of the application.</returns>
        static async Task Main(string[] args)
        {
            Option<string> adoTokenOption = new("--ado-token", "-at")
            {
                Description = "The token to use for authentication. To use this agent locally using your identity, use the Az CLI: az account get-access-token --resource 499b84ac-1321-427f-aa17-267ca6975798"
            };

            Option<int> pullRequestIdOption = new("--pull-request-id", "-id")
            {
                Description = "The ID of the pull request to review."
            };
            Option<string> organizationNameOption = new("--organization-name", "-o")
            {
                Description = "The name of the Azure DevOps organization."
            };
            Option<string> projectNameOption = new("--project-name", "-p")
            {
                Description = "The name of the Azure DevOps project."
            };
            Option<string> repositoryNameOption = new("--repository-name", "-r")
            {
                Description = "The name of the Azure DevOps repository."
            };
            Option<bool> saveOutputOption = new Option<bool>("--save-output")
            {
                Description = "Whether to save the agent work steps output to a file."
            };

            var rootCommand = new RootCommand("A pull request agent for Azure DevOps. It is responsible to provide code reviewing for the pull request and focuses on security, performance, and maintainability.");
            rootCommand.Options.Add(adoTokenOption);
            rootCommand.Options.Add(pullRequestIdOption);
            rootCommand.Options.Add(organizationNameOption);
            rootCommand.Options.Add(projectNameOption);
            rootCommand.Options.Add(repositoryNameOption);
            rootCommand.Options.Add(saveOutputOption);

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
            var saveOutput = parseResult.GetValue<bool>(saveOutputOption);

            var fileSystem = new FileSystem();
            var agent = new PullRequestAgent(fileSystem, token);
            var response = await agent.RunAsync(pullRequestId, organizationName, projectName, repositoryName);

            Console.WriteLine(response);

            if (saveOutput)
            {
                using (var stream = fileSystem.File.CreateText($"pull_request_{pullRequestId}_review.md"))
                {
                    await stream.WriteAsync(response);
                }
            }
        }
    }
}
