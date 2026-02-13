# Agent Directives

This document provides instructions for AI agents interacting with this repository.

## Project Goal

The primary goal of this project is to provide a self-updating Docker image for the GitHub Copilot CLI (`@github/copilot`). This allows users to run the CLI without worrying about local Node.js version conflicts.

The image is automatically built and published to the GitHub Container Registry (ghcr.io).

## Tech Stack

*   **Containerization**: Docker (`Dockerfile`)
*   **Base Image**: `node:22-alpine` or the latest stable Alpine version of Node.
*   **Automation**: GitHub Actions (`.github/workflows/docker-publish.yml`) for building and publishing the image.

## Conventions

*   **Language**: All code comments, documentation (`README.md`), and agent directives (`AGENTS.md`) must be in English.
*   **Dockerfile**: Keep the Dockerfile clean and commented. Use official and lightweight base images (like Alpine) where possible. The entrypoint is set to `copilot` to allow direct command passing.
*   **GitHub Actions**: The workflow is responsible for building and pushing the Docker image on pushes to `main` and on a daily schedule. The image is tagged with `latest` and the commit SHA.
*   **README**: The `README.md` should be clear, and concise, and provide easy-to-follow instructions for end-users, including the alias setup.
*   **Dependencies**: The core dependency is `@github/copilot`. The workflow is designed to automatically pick up new versions of this package upon its daily run.
