#!/bin/bash
set -e

# Default UMASK if unset to prevent errors with set -e
UMASK="${UMASK:-022}"
CURRENT_UID=$(id -u)

# If not running as root, skip all user/permission management.
# This supports docker-compose `user: PUID:PGID` (rootless mode).
if [ "$CURRENT_UID" != "0" ]; then
    umask "$UMASK"

    # In rootless mode, the app uses /config for all writable state.
    # A non-root user cannot create directories under /, so validate early.
    if [ ! -d /config ]; then
        echo "ERROR: /config does not exist and the container is running as non-root (UID $CURRENT_UID)." >&2
        echo "Please mount a writable volume at /config." >&2
        exit 1
    fi

    if [ ! -w /config ]; then
        echo "ERROR: /config is not writable by UID $CURRENT_UID." >&2
        echo "Please adjust permissions or mount /config as a writable volume." >&2
        exit 1
    fi

    exec "$@"
fi

# Running as root — use PUID/PGID to create user and drop privileges

# Create group if it doesn't exist
if ! getent group "$PGID" > /dev/null 2>&1; then
    echo "Creating group with GID $PGID"
    groupadd -g "$PGID" appgroup
fi

# Create user if it doesn't exist
if ! getent passwd "$PUID" > /dev/null 2>&1; then
    echo "Creating user with UID $PUID"
    useradd -u "$PUID" -g "$PGID" -s /bin/bash -M appuser
fi

# Set umask
umask "$UMASK"

# Ensure /config is writable by the target user
if [ "$PUID" != "0" ] || [ "$PGID" != "0" ]; then
    mkdir -p /config
    chown -R "$PUID:$PGID" /config
fi

# Execute as the specified user (or root if PUID=0)
if [ "$PUID" = "0" ] && [ "$PGID" = "0" ]; then
    exec "$@"
else
    # Use gosu to drop privileges
    exec gosu "$PUID:$PGID" "$@"
fi