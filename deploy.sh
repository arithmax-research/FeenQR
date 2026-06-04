#!/bin/bash
set -euo pipefail

# FeenQR EC2 Deploy Script
# Usage: ./deploy.sh [EC2_HOST]

CYAN='\033[0;36m'
BOLD='\033[1m'
NC='\033[0m'
info()  { echo -e "${CYAN}${BOLD}[INFO]${NC}  ${CYAN}$*${NC}"; }
ok()    { echo -e "${CYAN}${BOLD}[ OK ]${NC} ${CYAN}$*${NC}"; }
warn()  { echo -e "${CYAN}${BOLD}[WARN]${NC} ${CYAN}$*${NC}"; }
err()   { echo -e "${CYAN}${BOLD}[FAIL]${NC} ${CYAN}$*${NC}" >&2; }
header(){ echo ""; echo -e "${CYAN}════════════════════════════════════════${NC}"; echo -e "${CYAN}  $*${NC}"; echo -e "${CYAN}────────────────────────────────────${NC}"; }

# Config
SSH_USER="${SSH_USER:-ubuntu}"
SSH_OPTS="${SSH_OPTS:--o StrictHostKeyChecking=accept-new}"
TAR_FILE="/tmp/feenqr-deploy.tar.gz"
REMOTE_DIR="/home/$SSH_USER/codechest/FeenQR"
PROJECT_ROOT="$(cd "$(dirname "$0")" && pwd)"

# Get domain from Caddyfile
DOMAIN="feenqr.misango.me"
if [ -f "$PROJECT_ROOT/Caddyfile" ]; then
  DOMAIN=$(grep -m1 '^[a-zA-Z0-9._-]* {' "$PROJECT_ROOT/Caddyfile" | awk '{print $1}')
  DOMAIN="${DOMAIN:-feenqr.misango.me}"
fi

EC2_HOST="ec2-3-83-252-217.compute-1.amazonaws.com"

# If no host provided, prompt for it
if [ -z "$EC2_HOST" ]; then
  echo ""
  info "Enter the EC2 hostname or IP to deploy to:"
  echo -e "  ${CYAN}(e.g. ec2-3-83-252-217 or full hostname or IP)${NC}"
  echo -n -e "${CYAN}  EC2 host > ${NC}"
  read -r EC2_HOST
  echo ""
fi

if [ -z "$EC2_HOST" ]; then
  err "No EC2 host provided. Aborting."
  exit 1
fi

# Auto-complete short ec2-* hostnames
if echo "$EC2_HOST" | grep -qE '^ec2-[0-9]+-[0-9]+-[0-9]+-[0-9]+$'; then
  EC2_HOST="$EC2_HOST.compute-1.amazonaws.com"
  info "Auto-completed to: $EC2_HOST"
fi

header "Deploying FeenQR to $EC2_HOST"

# SSH check
info "Testing SSH connection..."
if ! ssh $SSH_OPTS "$SSH_USER@$EC2_HOST" "echo ok" 2>/dev/null | grep -q ok; then
  err "Cannot SSH to $SSH_USER@$EC2_HOST"
  err "Check the hostname and your SSH config."
  exit 1
fi
ok "SSH connection successful"

# DNS check
header "DNS Check: $DOMAIN"
DNS_IP="$(dig +short "$DOMAIN" 2>/dev/null || true)"
EC2_IP=$(echo "$EC2_HOST" | sed -n 's/^ec2-\([0-9]*\)-\([0-9]*\)-\([0-9]*\)-\([0-9]*\)\..*/\1.\2.\3.\4/p')
[ -z "$EC2_IP" ] && EC2_IP="$EC2_HOST"

if [ -n "$DNS_IP" ]; then
  if [ "$DNS_IP" = "$EC2_IP" ]; then
    ok "$DOMAIN resolves to $DNS_IP"
  else
    warn "$DOMAIN resolves to $DNS_IP but EC2 is $EC2_IP"
    warn "Update your A record if needed."
  fi
else
  warn "Could not resolve $DOMAIN"
fi

# Create tarball
header "Creating tarball"
tar czf "$TAR_FILE" \
  --exclude='.git' --exclude='obj' --exclude='bin' --exclude='publish' \
  --exclude='logs' --exclude='node_modules' --exclude='__pycache__' \
  --exclude='.DS_Store' --exclude='.qodo' --exclude='.vscode' \
  --exclude='.github' --exclude='*.tar.gz' --exclude='.gitignore' \
  -C "$PROJECT_ROOT" .
ok "Tarball created ($(du -h "$TAR_FILE" | cut -f1))"

# Transfer
header "Transferring to EC2"
ssh $SSH_OPTS "$SSH_USER@$EC2_HOST" "mkdir -p '$REMOTE_DIR'"
scp $SSH_OPTS "$TAR_FILE" "$SSH_USER@$EC2_HOST:$REMOTE_DIR/"
ok "Transfer complete"

# Extract and cleanup on EC2
header "Extracting on EC2"
ssh $SSH_OPTS "$SSH_USER@$EC2_HOST" \
  "cd '$REMOTE_DIR' && \
   tar xzf feenqr-deploy.tar.gz && \
   find . -name '._*' -delete && \
   rm -f feenqr-deploy.tar.gz && \
   mkdir -p logs WebApp/Server/uploads"
rm -f "$TAR_FILE"
ok "Extracted and cleaned up (local + remote tarballs removed)"

# Docker deploy — start Qdrant first, wait for readiness, then the rest
header "Building and starting Qdrant (vector database)"
ssh $SSH_OPTS "$SSH_USER@$EC2_HOST" \
  "cd '$REMOTE_DIR' && docker compose down --remove-orphans && \
   docker compose up -d --build --force-recreate qdrant"

info "Waiting for Qdrant to be ready..."
ssh $SSH_OPTS "$SSH_USER@$EC2_HOST" \
  "cd '$REMOTE_DIR' && \
   for i in \$(seq 1 30); do
     if docker exec qdrant bash -c 'exec 3<>/dev/tcp/localhost/6333 && echo -e \"GET /healthz HTTP/1.1\r\nhost: localhost\r\nconnection: close\r\n\r\n\" >&3 && head -1 <&3' 2>/dev/null | grep -q 200; then
       echo 'Qdrant is ready'
       break
     fi
     echo \"Waiting for Qdrant... (\$i/30)\"
     sleep 2
   done"

info "Starting FeenQR web app and Caddy..."
ssh $SSH_OPTS "$SSH_USER@$EC2_HOST" \
  "cd '$REMOTE_DIR' && docker compose up -d --build --force-recreate feenqr-web caddy"
ok "Docker stack deployed"

# Verify
header "Verification"
ssh $SSH_OPTS "$SSH_USER@$EC2_HOST" \
  "docker ps --format 'table {{.Names}}\t{{.Status}}'"

HTTP_CODE=$(ssh $SSH_OPTS "$SSH_USER@$EC2_HOST" \
  "curl -s -o /dev/null -w '%{http_code}' http://localhost:80/" 2>/dev/null || echo "failed")
if [ "$HTTP_CODE" = "308" ] || [ "$HTTP_CODE" = "200" ] || [ "$HTTP_CODE" = "302" ]; then
  ok "Caddy responding with HTTP $HTTP_CODE"
elif [ "$HTTP_CODE" = "failed" ]; then
  warn "Caddy not responding yet (may still be starting)"
else
  warn "Unexpected response: $HTTP_CODE"
fi

# Summary
header "Done"
echo ""
echo -e "  ${CYAN}Site:${NC}       https://$DOMAIN"
echo -e "  ${CYAN}EC2:${NC}        ssh $SSH_USER@$EC2_HOST"
echo -e "  ${CYAN}Remote dir:${NC} $REMOTE_DIR"
echo ""
echo -e "  ${CYAN}Logs:${NC}       ssh $SSH_USER@$EC2_HOST 'cd $REMOTE_DIR && docker compose logs -f'"
echo -e "  ${CYAN}Redeploy:${NC}   ./deploy.sh $EC2_HOST"
echo ""
echo -e "${CYAN}────────────────────────────────────────────${NC}"
echo -e "${CYAN}  https://$DOMAIN should be live soon!${NC}"
echo -e "${CYAN}  (Caddy needs ~30s for TLS cert)${NC}"
echo -e "${CYAN}────────────────────────────────────────────${NC}"
echo ""
