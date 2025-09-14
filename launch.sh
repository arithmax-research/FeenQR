#!/bin/bash

# Arithmax Research Agent - CLI Launcher
# Direct CLI mode - simplified launcher

echo "Arithmax Research Agent - CLI Mode"
echo "==================================="
echo ""

# Check if we're in the right directory
if [ ! -f "QuantResearchAgent.csproj" ]; then
    echo " Error: Please run this script from the ArithmaxResearchChest root directory"
    exit 1
fi

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

# Run in CLI mode (no --web flag)
dotnet run --project QuantResearchAgent.csproj
