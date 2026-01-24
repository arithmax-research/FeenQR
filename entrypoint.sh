#!/bin/bash
set -e

# Install Python and yt-dlp on first run (runtime, not build time)
if [ ! -f /usr/bin/python3 ]; then
    echo "Installing Python3 and yt-dlp..."
    apt-get update > /dev/null 2>&1 && \
    apt-get install -y python3 python3-pip > /dev/null 2>&1 && \
    pip3 install --no-cache-dir yt-dlp > /dev/null 2>&1 && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/* || \
    echo "Warning: Failed to install Python/yt-dlp. Video analysis may not work."
fi

# Start the application
exec dotnet Server.dll
