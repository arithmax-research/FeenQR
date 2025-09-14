#!/bin/bash

echo "Quant Research Agent Setup"
echo "============================="
echo ""

# Check if .NET 8 is installed
if ! command -v dotnet &> /dev/null; then
    echo "   .NET 8 SDK is not installed. Please install it first:"
    echo "   https://dotnet.microsoft.com/download/dotnet/8.0"
    exit 1
fi

echo ".NET SDK found: $(dotnet --version)"


# Check for required environment variables
echo ""
echo "  Checking API Keys..."

# Check OpenAI API key (environment variable or appsettings.json)
OPENAI_KEY_ENV="$OPENAI_API_KEY"
OPENAI_KEY_CONFIG=$(grep -A 3 '"OpenAI"' appsettings.json | grep '"ApiKey"' | cut -d'"' -f4)

if [ -n "$OPENAI_KEY_ENV" ]; then
    echo "OpenAI API Key found in environment variable"
elif [ -n "$OPENAI_KEY_CONFIG" ] && [ "$OPENAI_KEY_CONFIG" != "" ]; then
    echo "OpenAI API Key found in appsettings.json"
else
    echo "   OpenAI API Key not configured"
    echo "   Set environment variable: export OPENAI_API_KEY=your_api_key"
    echo "   OR add to appsettings.json under OpenAI:ApiKey"
fi

# Check YouTube API key
YOUTUBE_API_KEY=$(grep -A 3 '"YouTube"' appsettings.json | grep '"ApiKey"' | cut -d'"' -f4)
if [ -n "$YOUTUBE_API_KEY" ] && [ "$YOUTUBE_API_KEY" != "" ]; then
    echo " YouTube API Key found in appsettings.json"
else
    echo "   YouTube API Key is not configured in appsettings.json"
    echo "   1. Go to https://console.developers.google.com/"
    echo "   2. Create a new project or select existing"
    echo "   3. Enable YouTube Data API v3"
    echo "   4. Create credentials (API Key)"
    echo "   5. Add the key to appsettings.json under YouTube:ApiKey"
fi

echo ""
echo "   Setup complete! You can now run the agent with:"
echo "   dotnet run"
echo ""
echo "   Available commands once running:"
echo "   • analyze-video [youtube_url] - Analyze YouTube videos"
echo "   • get-quantopian-videos - Get latest Quantopian channel videos"
echo "   • search-finance-videos [query] - Search for finance videos"
echo "   • generate-signals [symbol] - Generate trading signals"
echo "   • market-data [symbol] - Get market data"
echo "   • portfolio - View portfolio summary"
echo "   • risk-assessment - Assess portfolio risk"
echo "   • test - Run test sequence"
echo "   • help - Show all available functions"
echo ""
echo "   Example usage:"
echo "   analyze-video https://www.youtube.com/watch?v=EXAMPLE"
echo "   search-finance-videos algorithmic trading"
echo "   generate-signals AAPL"
echo ""
