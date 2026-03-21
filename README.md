# ADO Pull Request Agent

Automated, AI-powered code reviews for Azure DevOps pull requests — powered by Claude Code CLI.

[![.NET 10](https://img.shields.io/badge/.NET-10-512bd4?style=flat-square&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![Claude Code](https://img.shields.io/badge/Claude_Code-CLI-d97706?style=flat-square)](https://docs.anthropic.com/en/docs/claude-code)
[![Docker](https://img.shields.io/badge/Docker-ready-2496ED?style=flat-square&logo=docker&logoColor=white)](https://www.docker.com/)
[![License](https://img.shields.io/badge/License-MIT-yellow?style=flat-square)](LICENSE)

[Overview](#overview) · [How it works](#how-it-works) · [Prerequisites](#prerequisites) · [Getting started](#getting-started) · [Azure DevOps pipeline integration](#azure-devops-pipeline-integration)

## Demo

https://github.com/user-attachments/assets/cc22d11f-778d-48cf-9c46-00af6540b4c7

## Overview

ADO Pull Request Agent is a .NET console application that performs **automated code reviews** on Azure DevOps pull requests. It invokes [Claude Code CLI](https://docs.anthropic.com/en/docs/claude-code) in non-interactive mode and uses the [Azure DevOps MCP server](https://github.com/microsoft/azure-devops-mcp) to fetch PR diffs, then produces a structured security-first review covering:

- **Security** — injection vectors, auth/authz, secrets handling, dependency risks
- **Performance** — hot paths, I/O patterns, memory and concurrency issues
- **Maintainability** — API design, naming, error handling, testability

The review output is a Markdown report that can be saved to a file or posted directly as a PR comment through an Azure DevOps pipeline.

## How it works

```
┌──────────────┐      ┌─────────────────────┐
│  ADO Pull    │─────▶│  Claude Code CLI    │
│  Request     │      │  (claude -p)        │
│  Agent       │      └─────────────────────┘
│              │               │
│              │      ┌────────┴────────┐
│              │      │   MCP Servers   │
│              │      ├─────────────────┤
│              │      │ Azure DevOps    │──▶ PR diffs, work items
│              │      │ Microsoft Learn │──▶ Best practices docs
└──────────────┘      └─────────────────┘
```

1. The agent receives a pull request ID, ADO coordinates (org, project, repo), and a **local sources directory** via CLI arguments.
2. It invokes Claude Code CLI (`claude -p`) with two MCP servers configured:
   - **Azure DevOps MCP** — to retrieve PR details, diffs, and related work items
   - **Microsoft Learn MCP** — to look up relevant best practices documentation
3. A detailed [system prompt](src/ADOPullRequestAgent/pullreview.prompt) instructs the model to act as a senior staff/principal engineer performing a rigorous, structured code review. The prompt is configured with the local sources directory so the model can run **git CLI commands** on the cloned repository as its primary method for examining code changes.
4. The model's review is returned as a Markdown report with severity-tagged findings and suggested patches.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Node.js](https://nodejs.org/) (for the Azure DevOps MCP server via `npx`)
- [Claude Code CLI](https://docs.anthropic.com/en/docs/claude-code) installed (`npm install -g @anthropic-ai/claude-code`)
- An **Anthropic API key** (set as `ANTHROPIC_API_KEY` environment variable)
- An **Azure DevOps access token** with read access to the target repository and pull requests

## Getting started

For a local environment, follow the steps below:

### 1. Clone the repository

```bash
git clone https://github.com/<your-org>/ADOPullRequestAgent.git
cd ADOPullRequestAgent
```

### 2. Set environment variables

```bash
# Required by Claude Code CLI
export ANTHROPIC_API_KEY="<your-anthropic-api-key>"
```

### 3. Obtain an Azure DevOps token

Use the Azure CLI to get a token scoped to Azure DevOps:

```bash
az account get-access-token --resource 499b84ac-1321-427f-aa17-267ca6975798 --query accessToken -o tsv
```

### 4. Run the agent

```bash
dotnet run --project src/ADOPullRequestAgent -- \
  --ado-token "<ado-token>" \
  --pull-request-id <pr-id> \
  --organization-name <org> \
  --project-name <project> \
  --repository-name <repo> \
  --model "claude-sonnet-4-20250514" \
  --sources-directory /path/to/local/clone \
  --output-directory .
```

The `--sources-directory` option points to the local directory where the repository source code is cloned. The agent uses this path to run git commands and read source files during the review.

The `--output-directory` option writes the review to `pull_request_<id>_review.md` in the specified directory.

### CLI options

| Option | Alias | Description |
|---|---|---|
| `--ado-token` | `-at` | Azure DevOps access token (required) |
| `--pull-request-id` | `-id` | ID of the pull request to review (required) |
| `--organization-name` | `-o` | Azure DevOps organization name (required) |
| `--project-name` | `-p` | Azure DevOps project name (required) |
| `--repository-name` | `-r` | Azure DevOps repository name (required) |
| `--model` | `-m` | The Claude model to use for the review (required) |
| `--sources-directory` | `-sd` | The local directory path where the repository source code is cloned (required) |
| `--output-directory` | | The directory to save the review output to a file (optional) |
| `--max-turns` | | Maximum number of agentic turns for cost control (optional) |
| `--max-budget-usd` | | Maximum budget in USD for the review session (optional) |

### Docker

Build and run the agent as a container:

```bash
docker build -t ado-pr-agent .
docker run --rm \
  -e ANTHROPIC_API_KEY="<your-api-key>" \
  -v /path/to/local/clone:/sources \
  ado-pr-agent \
  --ado-token "<ado-token>" \
  --pull-request-id <pr-id> \
  --organization-name <org> \
  --project-name <project> \
  --repository-name <repo> \
  --model "claude-sonnet-4-20250514" \
  --sources-directory /sources
```

## Azure DevOps pipeline integration

The agent is designed to run as a **build validation policy** on pull requests. An [example pipeline](.azdo/azure-pipeline.yaml) is included that:

1. Runs the agent container against the triggering PR
2. Posts the review as a PR comment thread via the Azure DevOps REST API
3. Publishes the review as a pipeline artifact

> [!TIP]
> To set this up, go to **Repos > Branches > Branch Policies > Build Validation** and add the pipeline. The agent will run automatically on every new pull request.

> [!IMPORTANT]
> The build agent service account (e.g., `<Project Name> Build Service`) must have **Contribute to pull requests** permission on the target repository(ies). Go to **Repos > Repository Settings > Security** and grant this permission to enable the agent to post review comments.

### Required pipeline variables

These should be configured in a variable group (e.g., `Claude-Code`):

| Variable | Description |
|---|---|
| `ANTHROPIC_API_KEY` | Anthropic API key for Claude Code CLI |
| `ACR_NAME` | Azure Container Registry name |
| `DOCKER_SERVICECONNECTION_NAME` | ACR service connection |
| `ADO_ORGANIZATION_NAME` | Azure DevOps organization |
| `MODEL_NAME` | The Claude model to use (e.g., `claude-sonnet-4-20250514`) |

> [!NOTE]
> The pipeline uses `$(System.AccessToken)` for Azure DevOps authentication, so no additional ADO PAT is required when running within a pipeline.

## Project structure

```
├── src/
│   └── ADOPullRequestAgent/        # Main .NET console application
│       ├── Program.cs              # CLI entry point (System.CommandLine)
│       ├── PullRequestAgent.cs     # Core agent logic / Claude Code CLI invocation
│       ├── AgentOptions.cs         # Configuration options for the agent
│       └── pullreview.prompt       # System prompt for the code review
├── .azdo/
│   └── azure-pipeline.yaml         # Azure DevOps pipeline definition
├── Dockerfile                       # Multi-stage build for the agent
└── ADOPullRequestAgent.slnx         # Solution file
```
