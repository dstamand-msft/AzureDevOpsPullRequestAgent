# GitHub Copilot CLI — Docker

A containerized [GitHub Copilot CLI](https://github.com/github/copilot-cli) image, automatically built and published to Azure Container Registry.

[![Docker](https://img.shields.io/badge/Docker-ready-2496ED?style=flat-square&logo=docker&logoColor=white)](https://www.docker.com/)
[![Node.js 22](https://img.shields.io/badge/Node.js-22-3c873a?style=flat-square&logo=node.js&logoColor=white)](https://nodejs.org/)
[![License](https://img.shields.io/badge/License-MIT-yellow?style=flat-square)](../../LICENSE)

[Overview](#overview) · [How it works](#how-it-works) · [Usage](#usage) · [CI/CD](#cicd)

</div>

## Overview

This project provides a minimal Docker image with the GitHub Copilot CLI pre-installed. It allows you to run the Copilot CLI without managing local Node.js versions or native dependencies — useful both as a standalone tool and as a background container in CI/CD pipelines (e.g., for the [ADO Pull Request Agent](../../README.md)).

## How it works

The [Dockerfile](Dockerfile) uses a multi-stage build:

1. **Installer stage** — Uses the official Copilot CLI install script on a `debian:bookworm-slim` base to download the `copilot` binary.
2. **Final stage** — Starts from a clean `debian:bookworm-slim`, installs Node.js 22 (required for `npx` / MCP servers), copies the `copilot` binary, and sets it as the entrypoint.

The resulting image is lightweight and self-contained.

## Usage

### Authentication

The container requires a GitHub fine-grained Personal Access Token (PAT) with the **Copilot Requests** permission.

To create one:

1. Go to https://github.com/settings/personal-access-tokens/new
2. Under "Permissions", click **Add permissions** and select **Copilot Requests**
3. Generate the token

See the [official Copilot CLI docs](https://github.com/github/copilot-cli/blob/main/README.md#authenticate-with-a-personal-access-token-pat) for details.

### Run as a background daemon

```bash
docker run -d \
  -e COPILOT_GITHUB_TOKEN="<your-github-pat>" \
  -p 4321:4321 \
  --name copilot-cli \
  -v /local/path/to/files:/work/copilot \
  <acr-login-server>/github-copilot/cli:latest \
  --headless --port 4321
```

The Copilot CLI will be available at `localhost:4321`. To stop and remove the container:

```bash
docker stop copilot-cli && docker rm copilot-cli
```

## CI/CD

Two pipeline flavors are available:

| Platform | Path |
|---|---|
| GitHub Actions | [`.github/workflows/docker-buildandpush.yaml`](../../.github/workflows/docker-buildandpush.yaml) |
| Azure DevOps | [`.azdo/copilotcli-buildandpush-pipeline.yaml`](../../.azdo/copilotcli-buildandpush-pipeline.yaml) |

Both pipelines share the same workflow:

- **Trigger** — Manual dispatch (with an optional daily schedule)
- **Version detection** — Automatically fetches the latest Copilot CLI release tag from GitHub
- **Skip logic** — Skips the build if the version already exists in ACR
- **Tags** — Images are tagged with `latest`, the Copilot CLI version, and the commit SHA
- **Registry** — Pushed to Azure Container Registry via OIDC authentication

## Project structure

```
├── Dockerfile          # Multi-stage build for the Copilot CLI image
├── .dockerignore       # Docker build exclusions
└── .gitignore          # Git exclusions
```
