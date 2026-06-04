#!/bin/bash
# Refresh YouTube cookies from browser and send to EC2
# Usage: ./refresh_youtube_cookies.sh [EC2_HOST]

set -euo pipefail

CYAN='\033[0;36m'
BOLD='\033[1m'
NC='\033[0m'
info()  { echo -e "${CYAN}${BOLD}[INFO]${NC}  ${CYAN}$*${NC}"; }
ok()    { echo -e "${CYAN}${BOLD}[ OK ]${NC} ${CYAN}$*${NC}"; }
err()   { echo -e "${CYAN}${BOLD}[FAIL]${NC} ${CYAN}$*${NC}" >&2; }

EC2_HOST="${1:-}"
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
LOCAL_COOKIES="/tmp/youtube_cookies_$$.txt"

# If no host provided, prompt for it
if [ -z "$EC2_HOST" ]; then
  echo ""
  info "Enter the EC2 hostname or IP:"
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
fi

info "Checking SSH connection..."
if ! ssh -o StrictHostKeyChecking=accept-new "ubuntu@$EC2_HOST" "echo ok" 2>/dev/null | grep -q ok; then
  err "Cannot SSH to ubuntu@$EC2_HOST"
  exit 1
fi
ok "SSH connection successful"

info "Exporting YouTube cookies from Chrome browser..."
python3 -c "
import browser_cookie3, os
cj = browser_cookie3.chrome(domain_name='.youtube.com')
with open('$LOCAL_COOKIES', 'w') as f:
    f.write('# Netscape HTTP Cookie File\n')
    for c in cj:
        if c.domain and ('youtube.com' in c.domain or 'google.com' in c.domain):
            f.write(f'{c.domain}\tTRUE\t{c.path}\tTRUE\t{int(c.expires) if c.expires else 0}\t{c.name}\t{c.value}\n')
count = len([c for c in cj if c.domain and 'youtube.com' in c.domain])
print(f'Exported {count} YouTube cookies')
" 2>&1

if [ ! -f "$LOCAL_COOKIES" ]; then
  err "Failed to export cookies"
  exit 1
fi

COOKIE_SIZE=$(wc -c < "$LOCAL_COOKIES" | tr -d ' ')
echo "  Cookie file size: ${COOKIE_SIZE} bytes"

info "Transferring cookies to EC2..."
REMOTE_PATH="/home/ubuntu/codechest/FeenQR/Scripts/youtube_cookies.txt"
scp -o StrictHostKeyChecking=accept-new "$LOCAL_COOKIES" "ubuntu@$EC2_HOST:$REMOTE_PATH" 2>&1

# Clean up local temp file
rm -f "$LOCAL_COOKIES"

# Verify on EC2
REMOTE_SIZE=$(ssh -o StrictHostKeyChecking=accept-new "ubuntu@$EC2_HOST" "wc -c < '$REMOTE_PATH'" 2>/dev/null | tr -d ' ')
if [ "$REMOTE_SIZE" = "$COOKIE_SIZE" ]; then
  ok "Cookies refreshed successfully ($REMOTE_SIZE bytes)"
  echo ""
  info "The app is already running — cookies are volume-mounted, so changes take effect immediately."
  echo ""
  echo -e "  ${CYAN}Test:${NC} ssh ubuntu@$EC2_HOST 'docker exec feenqr-web python3 /app/Scripts/get_youtube_transcript_ytdlp.py ikw9at76ukA | head -c 200'"
else
  warn "File sizes differ (local: $COOKIE_SIZE, remote: $REMOTE_SIZE). May need to check."
fi
