#!/bin/bash

# Arithmax Research Agent - Launcher
# Supports both CLI and Web modes

echo "Arithmax Research Agent"
echo "======================="
echo ""

# Check if we're in the right directory
if [ ! -f "QuantResearchAgent.csproj" ]; then
    echo " Error: Please run this script from the ArithmaxResearchChest root directory"
    exit 1
fi

# Check for mode argument
MODE="cli"
if [ "$1" = "web" ]; then
    MODE="web"
fi

echo " Building backend..."
dotnet build QuantResearchAgent.csproj

if [ $? -ne 0 ]; then
    echo " Backend build failed. Please check for errors."
    exit 1
fi

echo " Backend built successfully"
echo ""

if [ "$MODE" = "web" ]; then
    echo " Starting Web interface..."
    echo " Backend API will be available at http://localhost:5000"
    echo " Frontend will be available at http://localhost:5000"
    echo ""
    dotnet run --project QuantResearchAgent.csproj -- --web
else
    echo " Starting CLI interface..."
    echo ""
    dotnet run --project QuantResearchAgent.csproj
fi
