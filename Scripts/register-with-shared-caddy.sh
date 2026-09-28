#!/bin/bash
set -euo pipefail

# register-with-shared-caddy.sh
# Registers or updates a domain route in the shared Caddy instance.
# Usage: ./register-with-shared-caddy.sh <domain> <upstream_service> [docker_network]
#
# Example: ./register-with-shared-caddy.sh feenqr.misango.me feenqr-web:8080 feenqr_default

DOMAIN="${1:?Usage: $0 <domain> <upstream_service> [docker_network]}"
UPSTREAM="${2:?Usage: $0 <domain> <upstream_service> [docker_network]}"
NETWORK="${3:-$(docker compose ls -q 2>/dev/null || echo 'default')}"
SHARED_CADDY="holiday-effect-caddy"
SHARED_CADDYFILE="/mnt/shared-gp3/app-deployment/Caddyfile"

echo "=== Registering $DOMAIN → $UPSTREAM with shared Caddy ==="

# 1. Connect shared Caddy to the app's network
if docker ps --format '{{.Names}}' | grep -q "^$SHARED_CADDY$"; then
  docker network connect "$NETWORK" "$SHARED_CADDY" 2>/dev/null || {
    echo "  Already connected to $NETWORK or network not found"
  }
  echo "  ✓ Shared Caddy connected to network '$NETWORK'"
else
  echo "  ⚠ Shared Caddy container '$SHARED_CADDY' not found."
  echo "  Start it first or skip this step."
  exit 1
fi

# 2. Append Caddyfile block if not already present
if [ -f "$SHARED_CADDYFILE" ]; then
  if grep -q "^$DOMAIN " "$SHARED_CADDYFILE" 2>/dev/null; then
    echo "  ✓ $DOMAIN already registered — updating Caddy config"
  else
    tee -a "$SHARED_CADDYFILE" << CADDYEOF

# $DOMAIN (added $(date +%Y-%m-%d))
$DOMAIN {
    encode gzip
    reverse_proxy $UPSTREAM
}
CADDYEOF
    echo "  ✓ $DOMAIN appended to shared Caddyfile"
  fi

  # 3. Reload Caddy (zero-downtime)
  docker exec "$SHARED_CADDY" caddy reload --config /etc/caddy/Caddyfile
  echo "  ✓ Caddy reloaded — https://$DOMAIN should be live"
else
  echo "  ⚠ Shared Caddyfile not found at $SHARED_CADDYFILE"
  echo "  Create it manually or check the mount path."
  exit 1
fi

echo "=== Done ==="