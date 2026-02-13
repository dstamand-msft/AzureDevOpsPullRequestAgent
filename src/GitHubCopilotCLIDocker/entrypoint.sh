#!/bin/bash
set -e

# Use defaults if variables are not set
HOST_USER_ID=${HOST_UID:-1000}
HOST_GROUP_ID=${HOST_GID:-1000}

# Create a group and user, redirecting stderr to /dev/null to suppress warnings.
# The '|| true' prevents the script from exiting on a failure we've silenced.
groupadd -f -g "$HOST_GROUP_ID" myuser 2>/dev/null || true
useradd --shell /bin/bash --uid "$HOST_USER_ID" --gid "$HOST_GROUP_ID" --non-unique --create-home myuser 2>/dev/null || true

# Execute the command passed to the container, dropping privileges to the new user.
# This will run "copilot" with any arguments you pass.
exec sudo -u myuser copilot "$@"
