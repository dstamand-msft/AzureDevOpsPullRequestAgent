# syntax=docker/dockerfile:1

# ── Build stage ──────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project file and restore as a distinct layer for caching
COPY src/ADOPullRequestAgent/ADOPullRequestAgent.csproj src/ADOPullRequestAgent/
RUN dotnet restore src/ADOPullRequestAgent/ADOPullRequestAgent.csproj

# Copy remaining source and publish
COPY src/ src/
RUN dotnet publish src/ADOPullRequestAgent/ADOPullRequestAgent.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# ── Final stage ──────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app

# Install Node.js 22, git, and supporting tools
RUN apt-get update && \
    apt-get install -y --no-install-recommends \
        ca-certificates \
        curl \
        gnupg \
        git \
    && mkdir -p /etc/apt/keyrings \
    && curl -fsSL https://deb.nodesource.com/gpgkey/nodesource-repo.gpg.key \
        | gpg --dearmor -o /etc/apt/keyrings/nodesource.gpg \
    && echo "deb [signed-by=/etc/apt/keyrings/nodesource.gpg] https://deb.nodesource.com/node_22.x nodistro main" \
        > /etc/apt/sources.list.d/nodesource.list \
    && apt-get update \
    && apt-get install -y --no-install-recommends nodejs \
    && rm -rf /var/lib/apt/lists/*

# Install Claude Code CLI globally
RUN npm install -g @anthropic-ai/claude-code

# Pre-install the Azure DevOps MCP package globally so npx doesn't download it at runtime
RUN npm install -g @azure-devops/mcp@latest

COPY --from=build /app/publish .

# Create writable directories for the non-root user
# /sources is the mount point for the local repository clone
# /tmp is needed for temporary system prompt and MCP config files
RUN mkdir -p /output /sources /tmp && \
    chown "$APP_UID":"$APP_UID" /output /sources /tmp && \
    chmod 755 /output /sources /tmp

# Run as non-root user for security
USER $APP_UID

ENTRYPOINT ["dotnet", "ADOPullRequestAgent.dll"]
