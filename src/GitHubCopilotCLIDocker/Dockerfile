# Stage 1: Base with a potentially cached installation
FROM node:22-slim AS base
RUN npm install -g @github/copilot

# Stage 2: Updater to fetch the latest version
FROM base AS updater
ARG CACHE_BUSTER
RUN npm update -g @github/copilot

# Final Stage: Create a clean, final image
FROM node:22-slim

# Install sudo for dropping privileges, then clean up
RUN apt-get update && \
    apt-get install -y --no-install-recommends sudo && \
    rm -rf /var/lib/apt/lists/*

# Copy the application files from the updater stage
COPY --from=updater --link /usr/local/lib/node_modules /usr/local/lib/node_modules
COPY --from=updater --link /usr/local/bin /usr/local/bin

# Copy and set up the entrypoint script
COPY --chmod=755 --link entrypoint.sh /usr/local/bin/entrypoint.sh

# Set the working directory
WORKDIR /work

# Set the entrypoint to our custom script.
# The CMD will be passed as arguments to this script.
ENTRYPOINT ["entrypoint.sh"]
