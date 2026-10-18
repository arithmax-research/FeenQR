#!/bin/bash

# Arithmax Research Agent - Launcher
# Supports web mode (default), an archived CLI, and a container-scoped OpenVPN
# tunnel for the web application.
#
# Usage:
#   ./launch.sh [web] [--vpn]
#
# With --vpn, FeenQR is started with Docker Compose and OpenVPN runs *inside*
# the feenqr-web container. The host's traffic is never routed through the VPN.
# The profile is vpn/feenqr.ovpn (or the first vpn/*.ovpn).

set -u

echo "FeenQR"
echo "======================="
echo ""

PROJECT_ROOT="$(cd "$(dirname "$0")" && pwd)"
cd "$PROJECT_ROOT"

# Check if we're in the right directory
if [ ! -f "QuantResearchAgent.csproj" ]; then
    echo " Error: Please run this script from the FeenQR root directory"
    exit 1
fi

MODE="web"
USE_VPN=0
VPN_CONFIG_ARG=""
VPN_AUTH_ARG=""

usage() {
    cat <<'EOF'
Usage: ./launch.sh [web] [options]

Options:
  --vpn               Start FeenQR with Docker Compose and run OpenVPN inside
                      the feenqr-web container. The host is not routed through
                      the tunnel. Uses vpn/feenqr.ovpn or the first vpn/*.ovpn.
                      Web UI: http://localhost:8080
  -h, --help          Show this help.

Container VPN:
  Put the profile at vpn/feenqr.ovpn. If it uses auth-user-pass, put the
  credentials in vpn/auth.txt (username on line 1, password on line 2).
  The equivalent direct command is:

    docker compose -f docker-compose.yml -f docker-compose.vpn.yml up --build

  Only the feenqr-web container uses the tunnel; caddy, qdrant, and the host
  keep their normal network.

Examples:
  ./launch.sh web
  ./launch.sh web --vpn
EOF
}

while [ $# -gt 0 ]; do
    case "$1" in
        web|cli)
            MODE="$1"
            shift
            ;;
        --vpn)
            USE_VPN=1
            shift
            if [ $# -gt 0 ] && [ "${1#-}" = "$1" ]; then
                VPN_CONFIG_ARG="$1"
                shift
            fi
            ;;
        --vpn=*)
            USE_VPN=1
            VPN_CONFIG_ARG="${1#--vpn=}"
            shift
            ;;
        --vpn-config)
            USE_VPN=1
            if [ $# -lt 2 ]; then
                echo " Error: --vpn-config requires a file path"
                exit 2
            fi
            VPN_CONFIG_ARG="$2"
            shift 2
            ;;
        --vpn-auth)
            if [ $# -lt 2 ]; then
                echo " Error: --vpn-auth requires a file path"
                exit 2
            fi
            VPN_AUTH_ARG="$2"
            shift 2
            ;;
        -h|--help)
            usage
            exit 0
            ;;
        *)
            echo " Error: Unknown option: $1"
            echo ""
            usage
            exit 2
            ;;
    esac
done

# ── Container-scoped VPN ────────────────────────────────────────────────────
# OpenVPN must never run on the host here: a host tunnel would route every
# process on the machine, not only FeenQR. The supported mode starts the web
# application with Docker Compose and runs OpenVPN inside feenqr-web.
if [ "$USE_VPN" = "1" ]; then
    if [ "$MODE" != "web" ]; then
        echo " Error: --vpn is only supported for the web mode."
        exit 2
    fi

    if [ -n "$VPN_CONFIG_ARG" ]; then
        echo " Error: custom profile paths are not supported with --vpn."
        echo " Place the profile at $PROJECT_ROOT/vpn/feenqr.ovpn"
        echo " (or use the first vpn/*.ovpn)."
        exit 2
    fi

    if [ -n "$VPN_AUTH_ARG" ]; then
        echo " Error: custom auth paths are not supported with --vpn."
        echo " Place the credentials at $PROJECT_ROOT/vpn/auth.txt."
        exit 2
    fi

    if ! find "$PROJECT_ROOT/vpn" -maxdepth 1 -type f -name '*.ovpn' 2>/dev/null | grep -q .; then
        echo " Error: no OpenVPN profile found in $PROJECT_ROOT/vpn"
        echo " Place your provider's .ovpn at $PROJECT_ROOT/vpn/feenqr.ovpn"
        echo " (or add any single vpn/*.ovpn)."
        exit 1
    fi

    if ! command -v docker >/dev/null 2>&1; then
        echo " Error: Docker is required to run FeenQR with the container VPN."
        exit 1
    fi
    if ! docker compose version >/dev/null 2>&1; then
        echo " Error: Docker Compose v2 is required (docker compose)."
        exit 1
    fi

    echo " Starting FeenQR with OpenVPN inside the feenqr-web container."
    echo " Host traffic is not routed through the VPN."
    echo " Web UI: http://localhost:8080"
    echo ""
    exec docker compose \
        -f "$PROJECT_ROOT/docker-compose.yml" \
        -f "$PROJECT_ROOT/docker-compose.vpn.yml" \
        up --build
fi

# Function to check if Qdrant is running
check_qdrant() {
    if curl -s http://localhost:6333/health >/dev/null 2>&1; then
        return 0
    else
        return 1
    fi
}

# Function to start Qdrant
start_qdrant() {
    echo " Checking Qdrant vector database..."
    if check_qdrant; then
        echo " Qdrant is already running"
    else
        echo " Starting Qdrant container..."
        if command -v docker >/dev/null 2>&1; then
            # Check if container exists
            if docker ps -a --format '{{.Names}}' | grep -q "^qdrant$"; then
                docker start qdrant
            else
                docker run -d --name qdrant \
                    -p 6333:6333 -p 6334:6334 \
                    -v "$(pwd)/qdrant_storage:/qdrant/storage" \
                    qdrant/qdrant
            fi
            
            # Wait for Qdrant to be ready
            echo " Waiting for Qdrant to be ready..."
            for i in {1..30}; do
                if check_qdrant; then
                    echo " Qdrant is ready"
                    break
                fi
                sleep 1
            done
            
            if ! check_qdrant; then
                echo " Warning: Qdrant may not be ready. Continuing anyway..."
            fi
        else
            echo " Warning: Docker not found. Please start Qdrant manually:"
            echo " docker run -p 6333:6333 -p 6334:6334 -v \$(pwd)/qdrant_storage:/qdrant/storage qdrant/qdrant"
        fi
    fi
    echo ""
}

# Start Qdrant for the local (non-VPN) mode
start_qdrant

if [ "$MODE" = "web" ]; then
    echo " Starting Web interface..."
    echo " Checking for webapp..."
    if [ ! -d "WebApp" ]; then
        echo " Error: Webapp not found at WebApp"
        echo " Please ensure the webapp is built and available."
        exit 1
    fi
    echo " Webapp will be available at http://localhost:5228"
    echo ""
    cd WebApp/Server
    dotnet run
else
    echo " The CLI has been archived and is no longer built (see Archive/Cli/README.md)."
    echo " Use the web interface instead:  ./launch.sh web"
    echo ""
    echo " To restore the CLI, follow the 'How to restore it' steps in Archive/Cli/README.md."
    exit 1
fi
