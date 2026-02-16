#!/bin/bash

# Setup script for News Scraper with DeepSeek Analytics
# This script installs Python dependencies required for news scraping

echo "=========================================="
echo "News Scraper Setup"
echo "=========================================="
echo ""

# Check if Python is installed
if ! command -v python3 &> /dev/null; then
    echo "❌ Python 3 is not installed. Please install Python 3.8 or higher."
    exit 1
fi

echo "✅ Python 3 is installed"
python3 --version
echo ""

# Check if pip is installed
if ! command -v pip3 &> /dev/null; then
    echo "❌ pip3 is not installed. Please install pip."
    exit 1
fi

echo "✅ pip3 is installed"
echo ""

# Install required Python packages
echo "📦 Installing Python dependencies..."
echo ""

pip3 install --upgrade requests beautifulsoup4 lxml

echo ""
echo "✅ Python dependencies installed successfully"
echo ""

# Make the scraper script executable
chmod +x Scripts/url_news_scraper.py

echo "✅ News scraper script is now executable"
echo ""

# Test Python environment
echo "🧪 Testing Python environment..."
python3 -c "import requests, bs4; print('✅ All required packages are available')"

echo ""
echo "=========================================="
echo "Setup Complete!"
echo "=========================================="
echo ""
echo "📋 Next Steps:"
echo "1. Configure DeepSeek API key in appsettings.json"
echo "2. Build and run the application"
echo "3. Navigate to Research → News Scraper & Analytics"
echo ""
echo "For detailed documentation, see:"
echo "  New_Features/NEWS_SCRAPER_FEATURE.md"
echo ""
