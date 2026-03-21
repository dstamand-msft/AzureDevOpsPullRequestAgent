# AGENTS.md — AI Coding Assistant Instructions

This file provides context and rules for AI-assisted coding platforms (GitHub Copilot, Cursor, Windsurf, Cline, etc.) working in this repository.

## Project Overview

**ADO Pull Request Agent** is a .NET 10 console application that performs automated, AI-powered code reviews on Azure DevOps pull requests. It invokes the Claude Code CLI (`claude -p`) in non-interactive mode and connects to Azure DevOps via an MCP (Model Context Protocol) server to fetch PR diffs, then produces a structured Markdown review report.

## Tech Stack

- **Language:** C# (.NET 10)
- **Build system:** MSBuild / `dotnet` CLI
- **Solution file:** `ADOPullRequestAgent.slnx` (XML-based solution format)
- **Key dependencies:**
  - `System.CommandLine` — CLI argument parsing
  - `System.IO.Abstractions` — filesystem abstraction for testability
  - `Microsoft.Extensions.Logging.Console` — structured console logging
- **External tools (in Docker image):**
  - `claude` CLI — Claude Code non-interactive mode for AI-powered review
  - `npx @azure-devops/mcp@latest` — Azure DevOps MCP server
  - `git` — local repository operations
- **Container:** Multi-stage Dockerfile using `mcr.microsoft.com/dotnet/sdk:10.0` (build) and `mcr.microsoft.com/dotnet/runtime:10.0` (runtime) with Node.js, Claude Code CLI, and git
- **CI/CD:** Azure DevOps pipeline (`.azdo/azure-pipeline.yaml`)

## Repository Structure

```
├── AGENTS.md                        # This file
├── ADOPullRequestAgent.slnx         # Solution file
├── Dockerfile                       # Multi-stage Docker build for the agent
├── README.md                        # Project documentation
├── .azdo/
│   └── azure-pipeline.yaml          # Azure DevOps pipeline definition
└── src/
    └── ADOPullRequestAgent/         # Main console application
        ├── Program.cs               # Entry point — CLI parsing & orchestration
        ├── PullRequestAgent.cs      # Core agent logic — Claude Code CLI invocation
        ├── AgentOptions.cs          # Configuration POCO (model, cost controls)
        ├── pullreview.prompt        # System prompt for the AI code reviewer
        └── ADOPullRequestAgent.csproj
```

## Coding Conventions

- **Nullable reference types** are enabled (`<Nullable>enable</Nullable>`). All reference types must be annotated; do not suppress warnings without justification.
- **Implicit usings** are enabled. Do not add redundant `using` statements for `System`, `System.Collections.Generic`, `System.Linq`, `System.Threading.Tasks`, etc.
- **File-scoped namespaces** are preferred for new files (e.g., `namespace ADOPullRequestAgent;`). Existing files may use block-scoped namespaces — follow the style of the file being edited.
- Use **XML documentation comments** (`<summary>`, `<param>`, `<returns>`, `<remarks>`) on all public and internal members.
- Follow standard C# naming conventions: `PascalCase` for types, methods, and properties; `camelCase` for local variables and parameters; `_camelCase` for private fields.
- Constructor injection for dependencies; use `IFileSystem` (from `System.IO.Abstractions`) instead of direct `System.IO` calls for testability.
- Prefer `async`/`await` throughout. Do not use `.Result` or `.Wait()` on tasks.
- Use `ArgumentNullException` for null-check guard clauses on constructor parameters.

## Build & Run

```bash
# Restore and build
dotnet build src/ADOPullRequestAgent/ADOPullRequestAgent.csproj

# Run (requires ANTHROPIC_API_KEY env var set)
dotnet run --project src/ADOPullRequestAgent -- \
  --ado-token "<token>" \
  --pull-request-id <id> \
  --organization-name <org> \
  --project-name <project> \
  --repository-name <repo> \
  --model "claude-sonnet-4-20250514" \
  --sources-directory /path/to/clone

# Docker build
docker build -t ado-pr-agent .
```

## Environment Variables

| Variable | Required | Description |
|---|---|---|
| `ANTHROPIC_API_KEY` | Yes | Anthropic API key — used by Claude Code CLI for authentication |
| `ADO_MCP_AUTH_TOKEN` | Runtime | Azure DevOps access token — set internally by `PullRequestAgent` for the MCP server; passed via `--ado-token` CLI arg |

## Key Architecture Decisions

- **Claude Code CLI invocation:** `PullRequestAgent.RunAsync()` shells out to `claude -p` (non-interactive mode) via `System.Diagnostics.Process`. It builds an MCP config JSON at runtime with two MCP servers (Azure DevOps for PR data, Microsoft Learn for best practices) and passes the system prompt via `--system-prompt-file`.
- **System prompt:** The review persona and checklist live in `pullreview.prompt`, which is loaded at runtime. Edits to the review behavior should target this file, not C# code.
- **MCP server configuration:** The Azure DevOps MCP server is configured in a JSON file passed to `--mcp-config`. The ADO authentication token is passed through the `env` field in the MCP config, keeping it out of command-line arguments.
- **Output:** The review is written to stdout and optionally to `pull_request_<id>_review.md` in the specified output directory. Claude also saves structured review files (`review/code_review.md` and optionally `review/code_review_summary.md`) as instructed by the system prompt.

## Guidelines for AI Assistants

1. **Do not modify `pullreview.prompt` unless explicitly asked.** This file defines the AI reviewer persona and is carefully tuned.
2. **Never hardcode secrets or tokens.** All credentials flow through CLI arguments or environment variables.
3. **Maintain filesystem abstraction.** Use `IFileSystem` for any new file I/O — never use `System.IO.File` directly in production code.
4. **Preserve the CLI contract.** Changing or removing existing CLI options is a breaking change; add new options with defaults.
5. **Docker considerations:** The runtime image includes .NET runtime, Node.js, Claude Code CLI, and git. If new runtime dependencies are needed, update the Dockerfile accordingly.
6. **Security first:** This project handles authentication tokens and interacts with external services. Follow the security-first review priority (Security > Correctness > Performance > Maintainability) defined in the prompt file.
7. **No tests exist yet.** When adding features, consider adding unit tests using `System.IO.Abstractions.TestingHelpers` for filesystem mocking and standard xUnit/NUnit patterns.
