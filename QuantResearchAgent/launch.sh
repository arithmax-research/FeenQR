#!/bin/bash

# Quant Research Agent Launcher Script

echo "🤖 Quant Research Agent Launcher"
echo "================================"
echo ""

# Check if .NET is installed
if ! command -v dotnet &> /dev/null; then
    echo "❌ .NET SDK is not installed. Please install .NET 8.0 SDK first."
    exit 1
fi

# Check if the project file exists
if [ ! -f "QuantResearchAgent.csproj" ]; then
    echo "❌ Project file not found. Please run this script from the project directory."
    exit 1
fi

# Check if configuration exists
if [ ! -f "appsettings.json" ]; then
    echo "❌ Configuration file (appsettings.json) not found."
    exit 1
fi

# Build the project
echo "🔨 Building the project..."
dotnet build --configuration Release

if [ $? -ne 0 ]; then
    echo "❌ Build failed. Please fix the compilation errors."
    exit 1
fi

echo "✅ Build successful!"
echo ""

# Check for environment variables or prompt user
if [ -z "$OPENAI_API_KEY" ]; then
    echo "⚠️  Warning: OPENAI_API_KEY environment variable not set."
    echo "   Please update appsettings.json with your API key."
    echo ""
fi

if [ -z "$SPOTIFY_CLIENT_ID" ]; then
    echo "⚠️  Warning: SPOTIFY_CLIENT_ID environment variable not set."
    echo "   Please update appsettings.json with your Spotify credentials."
    echo ""
fi

# Ask user for run mode
echo "Select run mode:"
echo "1. Interactive CLI mode (recommended for testing)"
echo "2. Background agent mode"
echo "3. Test sequence only"
echo ""
read -p "Enter your choice (1-3): " choice

case $choice in
    1)
        echo "🚀 Starting in interactive CLI mode..."
        dotnet run --configuration Release -- --interactive
        ;;
    2)
        echo "🚀 Starting in background agent mode..."
        dotnet run --configuration Release
        ;;
    3)
        echo "🧪 Running test sequence..."
        dotnet run --configuration Release -- --interactive &
        sleep 3
        echo "test" | nc localhost 8080 2>/dev/null || echo "Test completed"
        ;;
    *)
        echo "❌ Invalid choice. Defaulting to interactive mode..."
        dotnet run --configuration Release -- --interactive
        ;;
esac

echo ""
echo "👋 Quant Research Agent stopped. Goodbye!"
