using GitHub.Copilot.SDK;
using Microsoft.Extensions.Logging;
using System.IO.Abstractions;
using System.Text;

namespace GHCPAgent;

public class PullRequestAgent
{
    private readonly IFileSystem _fileSystem;
    private readonly string _adoMcpAuthenticationToken;

    public PullRequestAgent(IFileSystem fileSystem, string adoMcpAuthenticationToken)
    {
        _fileSystem = fileSystem;
        _adoMcpAuthenticationToken = adoMcpAuthenticationToken ??
                                     throw new ArgumentNullException(nameof(adoMcpAuthenticationToken), "ADO_MCP_AUTH_TOKEN cannot be null");

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
                LogLevel = nameof(LogLevel.Error),
                // https://github.com/github/copilot-sdk/blob/main/docs/getting-started.md#connecting-to-an-external-cli-server
                // copilot --headless --port 4321
                CliUrl = "localhost:4321",
                UseStdio = false
            };

            var adoMcpNpxCmd = $"npx -y @azure-devops/mcp@latest {organizationName} --domains core repositories search work work-items --authentication envvar";

            // NOTE: The CopilotClient requires the environment variable COPILOT_GITHUB_TOKEN to be set with a GitHub token.
            // This token is used by the client to authenticate with GitHub services.
            // If the token is not set, the client _will_ not function properly and will throw an authentication error.
            // The token needs Copilot Requests on the account scope (fine-grained).
            await using (var client = new CopilotClient(copilotOptions))
            {
                await client.StartAsync();
                try
                {
                    var sessionConfig = new SessionConfig
                    {
                        Model = "claude-sonnet-4.5",
                        Streaming = false,
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
                                Command = "cmd",
                                // token is set in the env var ADO_MCP_AUTH_TOKEN
                                // see: https://github.com/microsoft/azure-devops-mcp/blob/main/docs/GETTINGSTARTED.md#using-token-authentication-via-environment-variables
                                Args =
                                [
                                    "/c",
                                    $"set ADO_MCP_AUTH_TOKEN={_adoMcpAuthenticationToken} && {adoMcpNpxCmd}"
                                ],
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
                        var done = new TaskCompletionSource();
                        var sb = new StringBuilder();

                        session.On(evt =>
                        {
                            switch (evt)
                            {
                                case AssistantMessageEvent msg:
                                    sb.AppendLine(msg.Data.Content);
                                    break;
                                case SessionIdleEvent:
                                    done.SetResult();
                                    break;
                            }
                        });

                        await session.SendAsync(new MessageOptions
                        {
                            Prompt = $"Review the pull request number {pullRequestId} in Azure DevOps project {projectName} for the repository {repositoryName}"
                        });

                        await done.Task;
                        return sb.ToString();
                    }
                }
                finally
                {
                    await client.StopAsync();
                }
            }
        }
    }
}