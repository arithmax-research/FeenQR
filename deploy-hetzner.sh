#!/usr/bin/env bash
set -euo pipefail

# ═══════════════════════════════════════════════════════════════════════════════
#  FeenQR → Hetzner incremental deploy (46.225.46.174 / arithmaxbase)
#
#  Two modes:
#
#  1) DEFAULT — incremental hot-swap (seconds, no image rebuild)
#       dotnet publish locally → hash-diff against the RUNNING container's /app
#       → stream only the changed files into the container → restart.
#       Nothing outside the `feenqr-web` container is touched: Caddy
#       (caddy-central), the TLS certs and the qdrant volume are never touched.
#
#  2) --rebuild — clean server-side image rebuild (slower, 5-15 min on 2 vCPU)
#       git pull on the server → `docker compose build && up -d feenqr-web`.
#       Also leaves Caddy/certs/qdrant untouched, but regenerates the image so
#       any stale files inside /app are gone.
#
#  Safety: the hot-swap only ADDS/OVERWRITES files. appsettings.json,
#  Scripts/, logs/ and uploads/ are never overwritten (the container holds the
#  live production appsettings.json). Roll back at any time with:
#      ssh <user>@<host> 'cd <remote-dir> && docker compose up -d --force-recreate feenqr-web'
#
#  Usage:
#      ./deploy-hetzner.sh                      # incremental hot-swap
#      ./deploy-hetzner.sh --dry-run            # show what would be uploaded
#      ./deploy-hetzner.sh --rebuild            # full docker image rebuild on server
#      ./deploy-hetzner.sh --with-appsettings   # also push publish/appsettings.json
#
#  Env overrides: SSH_HOST, SSH_USER, REMOTE_DIR, CONTAINER, DOMAIN, SKIP_PUBLISH=1
# ═══════════════════════════════════════════════════════════════════════════════

CYAN='\033[0;36m'; BOLD='\033[1m'; NC='\033[0m'
info()  { printf "${CYAN}${BOLD}[INFO]${NC}  ${CYAN}%s${NC}\n" "$*"; }
ok()    { printf "${CYAN}${BOLD}[ OK ]${NC} ${CYAN}%s${NC}\n" "$*"; }
warn()  { printf "${CYAN}${BOLD}[WARN]${NC} ${CYAN}%s${NC}\n" "$*"; }
err()   { printf "${CYAN}${BOLD}[FAIL]${NC} ${CYAN}%s${NC}\n" "$*" >&2; }
header(){ printf "\n${CYAN}════════════════════════════════════════${NC}\n${CYAN}  %s${NC}\n${CYAN}────────────────────────────────────────${NC}\n" "$*"; }

# ── Config ────────────────────────────────────────────────────────────────────
SSH_HOST="${SSH_HOST:-46.225.46.174}"
SSH_USER="${SSH_USER:-misango}"
REMOTE_DIR="${REMOTE_DIR:-/home/$SSH_USER/codechest/FeenQR}"
CONTAINER="${CONTAINER:-feenqr-web}"
DOMAIN="${DOMAIN:-feenqr.misango.me}"
PROXY_NETWORK="${PROXY_NETWORK:-caddy-central_proxy}"

SSH_OPTS="-o BatchMode=yes -o StrictHostKeyChecking=accept-new -o ServerAliveInterval=30 -o ServerAliveCountMax=20 -o ConnectTimeout=15"

PROJECT_ROOT="$(cd "$(dirname "$0")" && pwd)"
PUBLISH_DIR="$PROJECT_ROOT/publish"
WORK_DIR="$PROJECT_ROOT/.deploy"

MODE="hotswap"
DRY_RUN=0
WITH_APPSETTINGS=0

for arg in "$@"; do
  case "$arg" in
    --rebuild)          MODE="rebuild" ;;
    --dry-run)          DRY_RUN=1 ;;
    --with-appsettings) WITH_APPSETTINGS=1 ;;
    -h|--help)          sed -n '4,32p' "$0"; exit 0 ;;
    *) err "Unknown option: $arg"; exit 2 ;;
  esac
done

ssh_run() { ssh $SSH_OPTS "$SSH_USER@$SSH_HOST" "$@"; }

# md5 helper: macOS `md5 -r`, Linux `md5sum`
if command -v md5sum >/dev/null 2>&1; then MD5="md5sum"; else MD5="md5 -r"; fi
if stat -f '%z' "$0" >/dev/null 2>&1; then STAT_SIZE="stat -f %z"; else STAT_SIZE="stat -c %s"; fi
# macOS bsdtar embeds xattrs (LIBARCHIVE.xattr.*) that GNU tar warns about on extract
TAR_OPTS=""
if tar --version 2>/dev/null | grep -qi bsdtar; then TAR_OPTS="--no-mac-metadata"; fi

mkdir -p "$WORK_DIR"

# ── Preflight ─────────────────────────────────────────────────────────────────
header "Preflight → $SSH_USER@$SSH_HOST"

if ! ssh_run 'echo ok' 2>/dev/null | grep -q ok; then
  err "Cannot SSH to $SSH_USER@$SSH_HOST (check ~/.ssh/config / key)."
  exit 1
fi
ok "SSH reachable"

ssh_run "docker network inspect $PROXY_NETWORK >/dev/null" \
  || { err "External Caddy network '$PROXY_NETWORK' not found. Start caddy-central first."; exit 1; }
ok "Shared Caddy network present (left untouched)"

if [ "$MODE" = "rebuild" ]; then
  ssh_run "test -d '$REMOTE_DIR' && test -f '$REMOTE_DIR/docker-compose.yml' && test -f '$REMOTE_DIR/appsettings.json'" \
    || { err "Remote dir / docker-compose.yml / appsettings.json missing in $REMOTE_DIR"; exit 1; }
  ok "Remote project layout OK"
else
  STATE="$(ssh_run "docker inspect -f '{{.State.Running}}' $CONTAINER" 2>/dev/null || echo missing)"
  if [ "$STATE" != "true" ]; then
    err "Container '$CONTAINER' is not running on $SSH_HOST (state: $STATE)."
    err "Start it first:  ssh $SSH_USER@$SSH_HOST 'cd $REMOTE_DIR && docker compose up -d feenqr-web'"
    exit 1
  fi
  ok "Container '$CONTAINER' is running"
fi

# ═══════════════════════════════════════════════════════════════════════════════
#  MODE 2: full rebuild on the server
# ═══════════════════════════════════════════════════════════════════════════════
if [ "$MODE" = "rebuild" ]; then
  header "Server-side rebuild of $CONTAINER"

  info "git pull on the server (keeps the checkout authoritative)..."
  ssh_run "cd '$REMOTE_DIR' && git pull --ff-only 2>&1 | tail -5"

  info "Building the image. BuildKit cache is warm, so the apt/pip/node layer is reused."
  info "This is the slow path on 2 vCPU — the running container keeps serving until cutover."
  ssh_run "cd '$REMOTE_DIR' && docker compose build $CONTAINER"

  info "Recreating $CONTAINER only (qdrant + caddy untouched, certs untouched)..."
  ssh_run "cd '$REMOTE_DIR' && docker compose up -d $CONTAINER"

  ssh_run "cd '$REMOTE_DIR' && docker compose ps" || true
  sleep 5
  HTTPS_CODE="$(curl -s -o /dev/null -w '%{http_code}' --max-time 25 "https://$DOMAIN/" || echo failed)"
  if [ "$HTTPS_CODE" = "200" ]; then ok "https://$DOMAIN → 200"; else warn "https://$DOMAIN → $HTTPS_CODE"; fi
  info "Recent logs:"
  ssh_run "docker logs --tail 15 $CONTAINER 2>&1 | tail -15" | sed 's/^/        /' || true
  exit 0
fi

# ═══════════════════════════════════════════════════════════════════════════════
#  MODE 1: incremental hot-swap
# ═══════════════════════════════════════════════════════════════════════════════
header "Local publish → $PUBLISH_DIR"
if [ "${SKIP_PUBLISH:-0}" = "1" ]; then
  warn "SKIP_PUBLISH=1 → reusing the existing publish/ output"
else
  command -v dotnet >/dev/null || { err "dotnet SDK not found locally"; exit 1; }
  info "dotnet publish (incremental, UseAppHost=false → portable IL only)"
  ( cd "$PROJECT_ROOT" && dotnet publish WebApp/Server/Server.csproj -c Release -o "$PUBLISH_DIR" \
      /p:UseAppHost=false /p:UseSharedCompilation=true 2>&1 | tail -3 )
fi
[ -f "$PUBLISH_DIR/Server.dll" ] || { err "publish/Server.dll not found — publish failed"; exit 1; }
ok "publish/ ready ($(du -sh "$PUBLISH_DIR" | awk '{print $1}'))"

header "Collecting hashes"
info "Local  : $PUBLISH_DIR"
( cd "$PUBLISH_DIR" && find . -type f -print0 | xargs -0 $MD5 ) \
  | awk '{h=$1; sub(/^[0-9a-f]+ +/,""); sub(/^\.\//,""); print h"\t"$0}' \
  | LC_ALL=C sort > "$WORK_DIR/local.sums"
ok "Local manifest    : $(wc -l < "$WORK_DIR/local.sums" | tr -d ' ') files"

info "Remote : /app inside $CONTAINER (ignores logs/, uploads/, Scripts/)"
ssh_run "CONTAINER='$CONTAINER' bash -s" <<'REMOTE' > "$WORK_DIR/container.raw"
docker exec "$CONTAINER" sh -c 'find /app -type f -not -path "/app/logs/*" -not -path "/app/uploads/*" -not -path "/app/Scripts/*" -print0 | xargs -0 md5sum'
REMOTE
awk '{h=$1; sub(/^[0-9a-f]+ +/,""); sub(/^\/app\//,""); print h"\t"$0}' "$WORK_DIR/container.raw" \
  | LC_ALL=C sort > "$WORK_DIR/container.sums"
ok "Container manifest: $(wc -l < "$WORK_DIR/container.sums" | tr -d ' ') files"

# changed-or-new = present locally but not (hash-identical) inside the container
comm -23 "$WORK_DIR/local.sums" "$WORK_DIR/container.sums" | cut -f2- > "$WORK_DIR/delta.raw"

# ── Filters: never clobber live state; drop macOS/nested-publish junk ────────
#  Scripts/ is excluded from the remote manifest too, so it must be excluded
#  here as well (otherwise it would show up as a permanent false delta).
FILTER='^publish/|^Scripts/|^Server$|\.DS_Store$|(^|/)\._'
if [ "$WITH_APPSETTINGS" = "0" ]; then
  FILTER="$FILTER"'|^appsettings\.json$|^appsettings\..*\.json$'
fi
grep -Ev "$FILTER" "$WORK_DIR/delta.raw" > "$WORK_DIR/delta.txt" || true

DELTA_N="$(wc -l < "$WORK_DIR/delta.txt" | tr -d ' ')"
DELTA_BYTES=0
if [ "$DELTA_N" -gt 0 ]; then
  DELTA_BYTES="$( ( cd "$PUBLISH_DIR" && tr '\n' '\0' < "$WORK_DIR/delta.txt" | xargs -0 $STAT_SIZE ) \
    | awk '{s+=$1} END {print s+0}')"
fi

header "Delta"
info "Files to upload: $DELTA_N  ($(awk -v b="$DELTA_BYTES" 'BEGIN{printf "%.1f MB", b/1048576}'))"
[ "$WITH_APPSETTINGS" = "0" ] && warn "appsettings*.json SKIPPED (container holds the live prod config)"
if [ "$DELTA_N" -gt 0 ]; then
  info "Sample:"; head -8 "$WORK_DIR/delta.txt" | sed 's/^/        /'
  [ "$DELTA_N" -gt 8 ] && info "        … and $((DELTA_N - 8)) more files"
fi

if [ "$DELTA_N" -eq 0 ]; then
  ok "Container already matches this build — nothing to upload."
  exit 0
fi

if [ "$DRY_RUN" = "1" ]; then
  header "Dry run — stopping before upload"
  info "Re-run without --dry-run to push these files."
  exit 0
fi

header "Uploading into $CONTAINER"
UPLOAD_START="$(date +%s)"
( cd "$PUBLISH_DIR" && tar czf - $TAR_OPTS -T "$WORK_DIR/delta.txt" ) \
  | ssh $SSH_OPTS "$SSH_USER@$SSH_HOST" "docker exec -i $CONTAINER tar xzf - -C /app"
ok "Uploaded in $(( $(date +%s) - UPLOAD_START ))s"

header "Restarting $CONTAINER"
ssh_run "docker restart $CONTAINER >/dev/null && sleep 4 && docker ps --format 'table {{.Names}}\t{{.Status}}' | grep -E 'NAMES|$CONTAINER'"
ok "Container restarted"

header "Verification"
sleep 4
HTTP_CODE="$(curl -s -o /dev/null -w '%{http_code}' --max-time 25 "http://$DOMAIN/" || echo failed)"
HTTPS_CODE="$(curl -s -o /dev/null -w '%{http_code}' --max-time 25 "https://$DOMAIN/" || echo failed)"
if [ "$HTTPS_CODE" = "200" ]; then ok "https://$DOMAIN → 200"; else warn "https://$DOMAIN → $HTTPS_CODE"; fi
info "http://$DOMAIN → $HTTP_CODE (308 = expected Caddy redirect)"

info "Recent container logs:"
ssh_run "docker logs --tail 15 $CONTAINER 2>&1 | tail -15" | sed 's/^/        /' || true

printf "\n"
printf "  ${CYAN}Site:${NC}         https://%s\n" "$DOMAIN"
printf "  ${CYAN}Host:${NC}         ssh %s@%s\n" "$SSH_USER" "$SSH_HOST"
printf "  ${CYAN}Logs:${NC}         ssh %s@%s 'docker logs -f %s'\n" "$SSH_USER" "$SSH_HOST" "$CONTAINER"
printf "  ${CYAN}Rollback:${NC}     ssh %s@%s 'cd %s && docker compose up -d --force-recreate %s'\n" "$SSH_USER" "$SSH_HOST" "$REMOTE_DIR" "$CONTAINER"
printf "  ${CYAN}Full rebuild:${NC} %s --rebuild\n\n" "$(basename "$0")"
