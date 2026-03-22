namespace ADOPullRequestAgent;

public class AgentOptions
{
    /// <summary>
    /// Gets or sets the name or identifier of the Claude model to use for the review.
    /// </summary>
    public required string Model { get; set; }

    /// <summary>
    /// Gets or sets the local directory path where the repository source code is cloned.
    /// The agent uses this path to run git commands and read source files during the review.
    /// </summary>
    public required string SourcesDirectory { get; set; }

    /// <summary>
    /// Gets or sets the output directory path where Claude will save the review files (e.g., /output).
    /// When specified, review files are written to <c>&lt;OutputDirectory&gt;/review/</c>.
    /// When null, empty, or whitespace, the sources directory is used as the base for the review output folder.
    /// </summary>
    public string? OutputDirectory { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of agentic turns Claude Code will execute.
    /// Used for cost control. When null, Claude Code uses its default.
    /// </summary>
    public int? MaxTurns { get; set; }

    /// <summary>
    /// Gets or sets the maximum budget in USD for the Claude Code session.
    /// Used for cost control. When null, no budget limit is applied.
    /// </summary>
    public decimal? MaxBudgetUsd { get; set; }
}
