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

# Check for mode argument
MODE="cli"
if [ "$1" = "web" ]; then
    MODE="web"
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
    echo " Building backend..."
    dotnet build QuantResearchAgent.csproj

    if [ $? -ne 0 ]; then
        echo " Backend build failed. Please check for errors."
        exit 1
    fi

    echo " Backend built successfully"
    echo ""
    echo " Starting CLI interface..."
    echo ""
    dotnet run --project QuantResearchAgent.csproj
fi
