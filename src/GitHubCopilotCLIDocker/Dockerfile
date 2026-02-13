# Stage 1: Install GitHub Copilot CLI using the official install script
FROM debian:bookworm-slim AS installer
# Invalidate cache to force a fresh install
ARG CACHE_BUSTER

RUN apt-get update && \
    apt-get install -y --no-install-recommends \
        ca-certificates \
        curl \
        tar \
        coreutils \
    && rm -rf /var/lib/apt/lists/*

# Install copilot CLI to /usr/local/bin via the official install script
RUN curl -fsSL https://raw.githubusercontent.com/github/copilot-cli/refs/heads/main/install.sh | bash

# Final Stage: Create a clean, minimal image
FROM debian:bookworm-slim

# Install Node.js (for npx), sudo for dropping privileges, dos2unix for line endings, ca-certificates for TLS
RUN apt-get update && \
apt-get install -y --no-install-recommends \
    sudo \
    dos2unix \
    ca-certificates \
    curl \
    gnupg \
    git \
    && mkdir -p /etc/apt/keyrings \
    && curl -fsSL https://deb.nodesource.com/gpgkey/nodesource-repo.gpg.key | gpg --dearmor -o /etc/apt/keyrings/nodesource.gpg \
    && echo "deb [signed-by=/etc/apt/keyrings/nodesource.gpg] https://deb.nodesource.com/node_22.x nodistro main" > /etc/apt/sources.list.d/nodesource.list \
    && apt-get update \
    && apt-get install -y --no-install-recommends nodejs \
    && rm -rf /var/lib/apt/lists/*

# Copy the copilot binary from the installer stage
COPY --from=installer /usr/local/bin/copilot /usr/local/bin/copilot

# Copy and set up the entrypoint script
# TODO: improve security with entrypoint script that drops privileges to a non-root user, has the right env vars and handles signals properly.
# COPY --link entrypoint.sh /usr/local/bin/entrypoint.sh
# RUN dos2unix /usr/local/bin/entrypoint.sh && chmod +x /usr/local/bin/entrypoint.sh

# Set the working directory
WORKDIR /work

# Set the entrypoint to our custom script.
# The CMD will be passed as arguments to this script.
ENTRYPOINT ["copilot"]