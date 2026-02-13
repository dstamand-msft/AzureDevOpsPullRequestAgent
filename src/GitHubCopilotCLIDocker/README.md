# Copilot CLI - Docker
**This is a community-maintained project whose repository is located at [https://github.com/mentordosnerds/github-copilot-cli](https://github.com/mentordosnerds/github-copilot-cli). A copy and modifications were made in this repository for demo purposes.**

This repository contains a `Dockerfile` to build a Docker image with the latest version of Node.js (v22+) and the GitHub Copilot CLI (`@github/copilot`).

The goal is to allow the use of the Copilot CLI without the need to manage or change the Node.js version on your local machine.

## Automation with GitHub Actions

A GitHub Actions workflow is configured in `.github/workflows/docker-publish.yml` to automate the Docker image build and publication process.

The automation is triggered on the following events:
1.  **Push to the `main` branch**: On every new commit, the image is rebuilt and published with the `latest` and commit `SHA` tags.
2.  **Daily**: Every day, the workflow runs to ensure the `latest` image contains the most recent version of the `@github/copilot` dependency, in case it has been updated on NPM.

The image is published to the GitHub Container Registry (ghcr.io) and can be found at:
`ghcr.io/mentordosnerds/github-copilot-cli`

## How to Use

To run the Copilot CLI using this Docker image, the command below is designed for a seamless experience. An entrypoint script inside the container will dynamically create a user that matches your local user's UID/GID, solving file permission issues and system compatibility errors.

It works by:
- Passing your host UID/GID as environment variables.
- Mapping your current directory into the container.
- Mapping your local `.config` and `.gitconfig` to the dynamically created user's home directory inside the container.
- Mapping your SSH agent socket to allow authentication with services like Git.

### Advanced Command

```bash
docker run -it --rm --pull=always \
  -e HOST_UID=$(id -u) \
  -e HOST_GID=$(id -g) \
  -v "$(pwd):/work" \
  -w /work \
  -v "$HOME:/home/myuser/" \
  -e SSH_AUTH_SOCK=$SSH_AUTH_SOCK \
  -v $SSH_AUTH_SOCK:$SSH_AUTH_SOCK \
  ghcr.io/mentordosnerds/github-copilot-cli:latest [COMMANDS]
```
Replace `[COMMANDS]` with the arguments you want to pass to the Copilot CLI (e.g., `auth`, `explain`, etc.).

**Note:** The SSH agent forwarding (`SSH_AUTH_SOCK`) will only work if your SSH agent is running and the environment variable is set.

### Alias for a Native Experience

For the best experience, create a shell alias. This will make the `copilot` command transparently execute the fully configured Docker container.

```bash
alias copilot='docker run -it --rm --pull=always \
  -e HOST_UID=$(id -u) \
  -e HOST_GID=$(id -g) \
  -v "$(pwd):/work" \
  -w /work \
  -v "$HOME:/home/myuser/" \
  -e SSH_AUTH_SOCK=$SSH_AUTH_SOCK \
  -v $SSH_AUTH_SOCK:$SSH_AUTH_SOCK \
  ghcr.io/mentordosnerds/github-copilot-cli:latest'
```

#### How to make the alias permanent:

1.  Open your shell's configuration file (`~/.bashrc`, `~/.zshrc`, or `~/.config/fish/config.fish`).
2.  Add the alias line to the end of the file.
3.  Save the file and restart your terminal or run `source ~/.bashrc` (or the corresponding file) to apply the changes.

After setting up the alias, you can use the Copilot CLI as if it were installed locally, with your user permissions, SSH keys, and Git identity intact:
```bash
copilot --help
```
