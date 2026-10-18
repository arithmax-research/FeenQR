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

# ── Optional OpenVPN tunnel ──────────────────────────────────────────────────
# Enabled with VPN_ENABLED=true. The profile is mounted at /app/vpn (or set
# VPN_CONFIG to an explicit .ovpn path).
VPN_PID=""
VPN_AUTH_TEMP=""
VPN_CONFIG_TEMP=""
RESOLV_BACKUP=""
APP_PID=""

cleanup_app() {
    if [ -n "$APP_PID" ] && kill -0 "$APP_PID" 2>/dev/null; then
        echo "Stopping application..."
        kill "$APP_PID" 2>/dev/null || true
        for _ in {1..10}; do
            kill -0 "$APP_PID" 2>/dev/null || break
            sleep 1
        done
        kill -9 "$APP_PID" 2>/dev/null || true
        wait "$APP_PID" 2>/dev/null || true
    fi
}

cleanup_vpn() {
    if [ -n "$VPN_PID" ] && kill -0 "$VPN_PID" 2>/dev/null; then
        echo "Stopping OpenVPN..."
        kill "$VPN_PID" 2>/dev/null || true
        for _ in {1..10}; do
            kill -0 "$VPN_PID" 2>/dev/null || break
            sleep 1
        done
        kill -9 "$VPN_PID" 2>/dev/null || true
        wait "$VPN_PID" 2>/dev/null || true
    fi
    if [ -n "$VPN_AUTH_TEMP" ] && [ -f "$VPN_AUTH_TEMP" ]; then
        rm -f "$VPN_AUTH_TEMP"
    fi
    if [ -n "$VPN_CONFIG_TEMP" ] && [ -f "$VPN_CONFIG_TEMP" ]; then
        rm -f "$VPN_CONFIG_TEMP"
    fi
    if [ -n "$RESOLV_BACKUP" ] && [ -f "$RESOLV_BACKUP" ]; then
        cat "$RESOLV_BACKUP" > /etc/resolv.conf 2>/dev/null || true
    fi
}

cleanup_all() {
    cleanup_app
    cleanup_vpn
}

trap cleanup_all EXIT
trap 'exit 129' HUP
trap 'exit 130' INT
trap 'exit 143' TERM

if [ "${VPN_ENABLED:-false}" = "true" ]; then
    VPN_DIR="${VPN_DIR:-/app/vpn}"
    VPN_CONFIG_PATH="${VPN_CONFIG:-}"

    if [ -z "$VPN_CONFIG_PATH" ]; then
        VPN_CONFIG_PATH="$(find "$VPN_DIR" -maxdepth 1 -type f -name '*.ovpn' 2>/dev/null | LC_ALL=C sort | head -n 1 || true)"
    fi
    if [ -z "$VPN_CONFIG_PATH" ] || [ ! -f "$VPN_CONFIG_PATH" ]; then
        echo "ERROR: VPN_ENABLED=true but no .ovpn profile was found in $VPN_DIR" >&2
        echo "       Mount your profile at $VPN_DIR/feenqr.ovpn (see docker-compose.vpn.yml)." >&2
        exit 1
    fi

    case "$VPN_CONFIG_PATH" in
        /*) ;;

        *) VPN_CONFIG_PATH="$(pwd)/$VPN_CONFIG_PATH" ;;
    esac

    if ! command -v openvpn >/dev/null 2>&1; then
        echo "ERROR: VPN_ENABLED=true but the openvpn binary is not installed." >&2
        exit 1
    fi

    # Keep the tunnel private to this container. Some container runtimes
    # (including Docker Desktop on macOS) do not expose the host's
    # /dev/net/tun inside the container, so create a private TUN device if the
    # compose override granted MKNOD + the cgroup rule.
    if [ ! -e /dev/net/tun ]; then
        mkdir -p /dev/net
        if ! mknod /dev/net/tun c 10 200 2>/dev/null; then
            echo "ERROR: cannot create /dev/net/tun in the container." >&2
            echo "       The feenqr-web container needs NET_ADMIN, MKNOD, and" >&2
            echo "       device_cgroup_rules: ['c 10:200 rwm'] (see docker-compose.vpn.yml)." >&2
            exit 1
        fi
        chmod 600 /dev/net/tun
    fi

    VPN_AUTH="${VPN_AUTH_FILE:-}"
    if [ -z "$VPN_AUTH" ] && [ -f "$VPN_DIR/auth.txt" ]; then
        VPN_AUTH="$VPN_DIR/auth.txt"
    fi
    if [ -z "$VPN_AUTH" ] && [ -n "${VPN_USERNAME:-}" ] && [ -n "${VPN_PASSWORD:-}" ]; then
        VPN_AUTH="/tmp/feenqr-vpn-auth.txt"
        printf '%s\n%s\n' "$VPN_USERNAME" "$VPN_PASSWORD" > "$VPN_AUTH"
        chmod 600 "$VPN_AUTH"
        VPN_AUTH_TEMP="$VPN_AUTH"
    fi

    if [ -n "$VPN_AUTH" ]; then
        case "$VPN_AUTH" in
            /*) ;;

            *) VPN_AUTH="$(pwd)/$VPN_AUTH" ;;
        esac
    fi

    if [ -n "$VPN_AUTH" ] && [ ! -f "$VPN_AUTH" ]; then
        echo "ERROR: VPN auth file not found: $VPN_AUTH" >&2
        exit 1
    fi

    if [ -z "$VPN_AUTH" ] && grep -qiE '^[[:space:]]*auth-user-pass[[:space:]]*$' "$VPN_CONFIG_PATH"; then
        echo "ERROR: $VPN_CONFIG_PATH requires a username/password." >&2
        echo "       Provide $VPN_DIR/auth.txt or set VPN_USERNAME/VPN_PASSWORD." >&2
        exit 1
    fi

    CONFIG_TO_USE="$VPN_CONFIG_PATH"
    if [ -n "$VPN_AUTH" ]; then
        CONFIG_TO_USE="/tmp/feenqr-openvpn.ovpn"
        VPN_CONFIG_TEMP="$CONFIG_TO_USE"
        sed -E "s|^[[:space:]]*auth-user-pass.*$|auth-user-pass \"$VPN_AUTH\"|I" \
            "$VPN_CONFIG_PATH" > "$CONFIG_TO_USE"
        chmod 600 "$CONFIG_TO_USE"
    fi

    VPN_LOG="/tmp/feenqr-openvpn.log"
    : > "$VPN_LOG"

    # Remember the container's original Docker route before OpenVPN replaces
    # or splits the default route. It is needed for Docker Desktop's published
    # ports after the tunnel is up.
    DOCKER_DEFAULT_GW="$(ip route 2>/dev/null | awk '$1 == "default" {print $3; exit}')"
    DOCKER_DEFAULT_DEV="$(ip route 2>/dev/null | awk '$1 == "default" {print $5; exit}')"

    echo "Starting OpenVPN with $VPN_CONFIG_PATH ..."
    openvpn \
        --cd "$(dirname "$VPN_CONFIG_PATH")" \
        --config "$CONFIG_TO_USE" \
        --log "$VPN_LOG" \
        --auth-nocache \
        --script-security 2 \
        --verb 4 &
    VPN_PID=$!

    VPN_READY=0
    for _ in {1..90}; do
        if grep -q "Initialization Sequence Completed" "$VPN_LOG" 2>/dev/null; then
            VPN_READY=1
            break
        fi
        if ! kill -0 "$VPN_PID" 2>/dev/null; then
            break
        fi
        sleep 1
    done

    if [ "$VPN_READY" != "1" ]; then
        echo "ERROR: OpenVPN did not connect." >&2
        tail -n 40 "$VPN_LOG" >&2 || true
        if grep -q "AUTH_FAILED" "$VPN_LOG" 2>/dev/null; then
            echo "HINT: ProtonVPN/OpenVPN rejected the credentials. Use the provider's" >&2
            echo "      OpenVPN username/password, check the connection limit, or" >&2
            echo "      regenerate the credentials in the provider dashboard." >&2
        fi
        exit 1
    fi

    echo "OpenVPN connected."

    # Docker Desktop forwards published ports from its VM network
    # (192.168.65.0/24). OpenVPN's redirect-gateway installs 0.0.0.0/1 and
    # 128.0.0.0/1 via tun0, which also captures that VM subnet. Keep the VM
    # subnet on the container's original Docker interface so inbound port
    # forwarding (http://localhost:8080) still works. host.docker.internal is
    # only present on Docker Desktop; Linux uses the bridge gateway directly.
    if getent hosts host.docker.internal >/dev/null 2>&1 && \
       [ -n "$DOCKER_DEFAULT_GW" ] && [ -n "$DOCKER_DEFAULT_DEV" ]; then
        ip route replace 192.168.65.0/24 via "$DOCKER_DEFAULT_GW" dev "$DOCKER_DEFAULT_DEV" 2>/dev/null || \
            echo "WARNING: could not preserve the Docker Desktop port-forwarding route." >&2
    fi

    # Use DNS pushed by the VPN, while preserving the container's original
    # Docker resolver first so compose service names such as "qdrant" keep
    # working. This edits only the container's /etc/resolv.conf; the host DNS
    # configuration is untouched.
    if [ "${VPN_SET_DNS:-false}" = "true" ]; then
        VPN_DNS="$(grep -oE 'dhcp-option DNS [0-9a-fA-F:.]+' "$VPN_LOG" \
            | awk '{print $3}' | awk '!seen[$0]++' | head -n 3 || true)"
        if [ -z "$VPN_DNS" ]; then
            VPN_DNS="1.1.1.1 8.8.8.8"
        fi
        RESOLV_BACKUP="/tmp/feenqr-resolv.conf"
        cp /etc/resolv.conf "$RESOLV_BACKUP" 2>/dev/null || true
        DOCKER_DNS="$(awk '/^[[:space:]]*nameserver[[:space:]]/{print $2; exit}' "$RESOLV_BACKUP" 2>/dev/null || true)"
        [ -n "$DOCKER_DNS" ] || DOCKER_DNS="127.0.0.11"
        {
            echo "# Generated by FeenQR VPN entrypoint"
            echo "nameserver $DOCKER_DNS"
            for ns in $VPN_DNS; do
                [ "$ns" = "$DOCKER_DNS" ] || echo "nameserver $ns"
            done
            # Keep fallback from waiting too long if the Docker resolver is not
            # reachable through the tunnel.
            echo "options timeout:1 attempts:1"
        } > /etc/resolv.conf 2>/dev/null || \
            echo "WARNING: could not update /etc/resolv.conf" >&2
    fi

    if command -v curl >/dev/null 2>&1; then
        VPN_EGRESS_IP="$(curl -fsS --max-time 10 https://api.ipify.org 2>/dev/null || true)"
        if [ -n "$VPN_EGRESS_IP" ]; then
            echo "VPN public egress IP: $VPN_EGRESS_IP"
        else
            echo "WARNING: could not verify the VPN egress IP." >&2
        fi
    fi
fi

# Start the application as a child so signals are forwarded and cleanup runs.
dotnet Server.dll &
APP_PID=$!
STATUS=0
wait "$APP_PID" || STATUS=$?
APP_PID=""
exit "$STATUS"
