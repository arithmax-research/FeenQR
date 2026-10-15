#!/bin/bash

# Arithmax Research Agent - Launcher
# Supports both CLI and Web modes

echo "FeenQR"
echo "======================="
echo ""

# Check if we're in the right directory
if [ ! -f "QuantResearchAgent.csproj" ]; then
    echo " Error: Please run this script from the FeenQR root directory"
    exit 1
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

# Start Qdrant for both modes
start_qdrant

# Check for mode argument (web is the default; the CLI is archived - see Archive/Cli/README.md)
MODE="web"
if [ "$1" = "cli" ]; then
    MODE="cli"
fi

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
