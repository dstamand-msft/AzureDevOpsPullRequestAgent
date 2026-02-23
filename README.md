# ADO Pull Request Agent

Automated, AI-powered code reviews for Azure DevOps pull requests — powered by GitHub Copilot SDK.

[![.NET 10](https://img.shields.io/badge/.NET-10-512bd4?style=flat-square&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![GitHub Copilot SDK](https://img.shields.io/badge/GitHub_Copilot-SDK-000?style=flat-square&logo=github&logoColor=white)](https://github.com/github/copilot-sdk)
[![Docker](https://img.shields.io/badge/Docker-ready-2496ED?style=flat-square&logo=docker&logoColor=white)](https://www.docker.com/)
[![License](https://img.shields.io/badge/License-MIT-yellow?style=flat-square)](LICENSE)

[Overview](#overview) · [How it works](#how-it-works) · [Prerequisites](#prerequisites) · [Getting started](#getting-started) · [Azure DevOps pipeline integration](#azure-devops-pipeline-integration) · [Blog post](https://www.domstamand.com/building-an-ai-pull-request-agent-for-azure-devops-using-github-copilot-sdk/)

## Demo

https://github.com/user-attachments/assets/cc22d11f-778d-48cf-9c46-00af6540b4c7

## Overview

ADO Pull Request Agent is a .NET console application that performs **automated code reviews** on Azure DevOps pull requests. It connects to the [GitHub Copilot SDK](https://github.com/github/copilot-sdk) and uses the [Azure DevOps MCP server](https://github.com/microsoft/azure-devops-mcp) to fetch PR diffs, then produces a structured security-first review covering:

- **Security** — injection vectors, auth/authz, secrets handling, dependency risks
- **Performance** — hot paths, I/O patterns, memory and concurrency issues
- **Maintainability** — API design, naming, error handling, testability

The review output is a Markdown report that can be saved to a file or posted directly as a PR comment through an Azure DevOps pipeline.

## How it works

```
┌──────────────┐      ┌─────────────────────┐      ┌──────────────────┐
│  ADO Pull    │─────▶│  GitHub Copilot SDK ─────▶│  AI Model        │
│  Request     │      │  (Copilot CLI)      │      │                  │
│  Agent       │      └─────────────────────┘      └──────────────────┘
│              │               │
│              │      ┌────────┴────────┐
│              │      │   MCP Servers   │
│              │      ├─────────────────┤
│              │      │ Azure DevOps    │──▶ PR diffs, work items
│              │      │ Microsoft Learn │──▶ Best practices docs
└──────────────┘      └─────────────────┘
```

1. The agent receives a pull request ID and ADO coordinates (org, project, repo) via CLI arguments.
2. It starts a local [GitHub Copilot CLI](https://github.com/github/copilot-cli) session and configures two MCP servers:
   - **Azure DevOps MCP** — to retrieve PR details, diffs, and related work items
   - **Microsoft Learn MCP** — to look up relevant best practices documentation
3. A detailed [system prompt](src/ADOPullRequestAgent/pullreview.prompt) instructs the model to act as a senior staff/principal engineer performing a rigorous, structured code review.
4. The model's review is returned as a Markdown report with severity-tagged findings and suggested patches.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Node.js](https://nodejs.org/) (for the Azure DevOps MCP server via `npx`)
- [GitHub Copilot CLI](https://github.com/github/copilot-cli) running in headless mode on port `4321`
- A **GitHub personal access token** with the `Copilot` scope (set as `COPILOT_GITHUB_TOKEN` environment variable)
- An **Azure DevOps access token** with read access to the target repository and pull requests

## Getting started

For a local environment, follow the steps below:

### 1. Clone the repository

```bash
git clone https://github.com/<your-org>/ADOPullRequestAgent.git
cd ADOPullRequestAgent
```

### 2. Start the Copilot CLI

In a separate terminal, start the Copilot CLI in headless mode:

```bash
copilot --headless --port 4321
```

### 3. Set environment variables

```bash
# Required by the GitHub Copilot SDK
export COPILOT_GITHUB_TOKEN="<your-github-pat>"
```

### 4. Obtain an Azure DevOps token

Use the Azure CLI to get a token scoped to Azure DevOps:

```bash
az account get-access-token --resource 499b84ac-1321-427f-aa17-267ca6975798 --query accessToken -o tsv
```

### 5. Run the agent

```bash
dotnet run --project src/ADOPullRequestAgent -- \
  --ado-token "<ado-token>" \
  --pull-request-id <pr-id> \
  --organization-name <org> \
  --project-name <project> \
  --repository-name <repo> \
  --model <model (i.e. "claude-sonnet-4.5")> \
  --cli-os-platform <windows|unix|osx> \
  --output-directory .
```

The `--output-directory` option writes the review to `pull_request_<id>_review.md` in the specified directory.

### CLI options

| Option | Alias | Description |
|---|---|---|
| `--ado-token` | `-at` | Azure DevOps access token (required) |
| `--pull-request-id` | `-id` | ID of the pull request to review (required) |
| `--organization-name` | `-o` | Azure DevOps organization name (required) |
| `--project-name` | `-p` | Azure DevOps project name (required) |
| `--repository-name` | `-r` | Azure DevOps repository name (required) |
| `--model` | `-m` | The model to use for the agent (required) |
| `--cli-port` | `-cp` | The port number for the CLI (defaults to 4321) |
| `--cli-os-platform` | | The OS platform where the CLI is running: `windows`, `unix`, or `osx` (defaults to `unix`) |
| `--output-directory` | | The directory to save review output to a Markdown file (optional) |

### Docker

Build and run the agent as a container:

```bash
docker build -t ado-pr-agent .
docker run --rm \
  ado-pr-agent \
  --ado-token "<ado-token>" \
  --pull-request-id <pr-id> \
  --organization-name <org> \
  --project-name <project> \
  --repository-name <repo> \
  --model <model (i.e. "claude-sonnet-4.5")> \
  --cli-os-platform unix
```

## Azure DevOps pipeline integration

The agent is designed to run as a **build validation policy** on pull requests. An [example pipeline](.azdo/azure-pipeline.yaml) is included that:

1. Starts a GitHub Copilot CLI sidecar container
2. Runs the agent container against the triggering PR
3. Posts the review as a PR comment thread via the Azure DevOps REST API
4. Publishes the review as a pipeline artifact

> [!TIP]
> To set this up, go to **Repos > Branches > Branch Policies > Build Validation** and add the pipeline. The agent will run automatically on every new pull request.

> [!IMPORTANT]
> The build agent service account (e.g., `<Project Name> Build Service`) must have **Contribute to pull requests** permission on the target repository(ies). Go to **Repos > Repository Settings > Security** and grant this permission to enable the agent to post review comments.

### Required pipeline variables

These should be configured in a variable group (e.g., `GitHub-Copilot`):

| Variable | Description |
|---|---|
| `COPILOT_GITHUB_TOKEN` | GitHub PAT with Copilot scope |
| `ACR_NAME` | Azure Container Registry name |
| `ACR_SERVICECONNECTION_NAME` | ACR service connection |
| `ADO_ORGANIZATION_NAME` | Azure DevOps organization |

> [!NOTE]
> The pipeline uses `$(System.AccessToken)` for Azure DevOps authentication, so no additional ADO PAT is required when running within a pipeline.

## Project structure

```
├── src/
│   ├── ADOPullRequestAgent/        # Main .NET console application
│   │   ├── Program.cs              # CLI entry point (System.CommandLine)
│   │   ├── PullRequestAgent.cs     # Core agent logic / Copilot SDK orchestration
│   │   └── pullreview.prompt       # System prompt for the code review
│   └── GitHubCopilotCLIDocker/     # Docker image for GitHub Copilot CLI
├── .azdo/
│   └── azure-pipeline.yaml         # Azure DevOps pipeline definition
├── .github/
│   └── workflows/
│       └── docker-buildandpush.yaml # GitHub Actions: build & push Copilot CLI image
├── Dockerfile                       # Multi-stage build for the agent
└── ADOPullRequestAgent.slnx         # Solution file
```
