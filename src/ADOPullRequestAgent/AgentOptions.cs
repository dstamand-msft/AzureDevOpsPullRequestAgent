namespace ADOPullRequestAgent;

public class AgentOptions
{
    /// <summary>
    /// Gets or sets the TCP port number used for CLI (Command-Line Interface) connections.
    /// </summary>
    public int CliPort { get; set; }

    public PlatformID CliOsPlatform { get; set; } = PlatformID.Unix;

    /// <summary>
    /// Gets or sets the name or identifier of the model.
    /// </summary>
    public string Model { get; set; }
}