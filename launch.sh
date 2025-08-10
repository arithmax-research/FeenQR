#!/bin/bash

# Arithmax Research Agent - Universal Launcher
# Supports both CLI and Modern UI modes

echo "🚀 Arithmax Research Agent - Universal Launcher"
echo "================================================="
echo ""

# Check if we're in the right directory
if [ ! -f "QuantResearchAgent.csproj" ]; then
    echo "❌ Error: Please run this script from the ArithmaxResearchChest root directory"
    exit 1
fi

# Function to kill background processes on exit
cleanup() {
    echo ""
    echo "🛑 Shutting down services..."
    if [ ! -z "$BACKEND_PID" ]; then
        kill $BACKEND_PID 2>/dev/null
    fi
    if [ ! -z "$FRONTEND_PID" ]; then
        kill $FRONTEND_PID 2>/dev/null
    fi
    exit 0
}

# Set up signal handlers
trap cleanup SIGINT SIGTERM

# Check if frontend dependencies are installed
check_frontend_deps() {
    if [ ! -d "frontend/node_modules" ]; then
        echo "📦 Installing frontend dependencies..."
        cd frontend
        npm install
        cd ..
        echo "✅ Frontend dependencies installed"
        echo ""
    fi
}

# Display menu
echo "Select launch mode:"
echo ""
echo "1. 🖥️  Modern Web UI (Recommended)"
echo "   - Beautiful dashboard with columns and sections"
echo "   - Point-and-click interface"
echo "   - Real-time analysis and charts"
echo "   - Professional appearance"
echo ""
echo "2. 💻 Original CLI Mode"
echo "   - Traditional command-line interface"
echo "   - All original functionality"
echo "   - Text-based interaction"
echo ""
echo "3. 🔧 API Server Only"
echo "   - Backend API server only"
echo "   - For integration or testing"
echo ""

read -p "Enter your choice (1-3): " choice

case $choice in
    1)
        echo ""
        echo "🎨 Launching Modern Web UI..."
        echo "================================"
        echo ""
        
        # Check frontend dependencies
        check_frontend_deps
        
        echo "📦 Building backend..."
        dotnet build QuantResearchAgent.csproj
        
        if [ $? -ne 0 ]; then
            echo "❌ Backend build failed. Please check for errors."
            exit 1
        fi
        
        echo "✅ Backend built successfully"
        echo ""
        
        echo "🌐 Starting backend API server..."
        dotnet run --project QuantResearchAgent.csproj -- --web &
        BACKEND_PID=$!
        
        # Wait a moment for backend to start
        sleep 3
        
        echo "🎨 Starting frontend development server..."
        cd frontend
        npm run dev &
        FRONTEND_PID=$!
        cd ..
        
        echo ""
        echo "🎉 Modern Web UI started successfully!"
        echo ""
        echo "📍 Backend API:  http://localhost:5000"
        echo "🌐 Frontend UI:  http://localhost:4321"
        echo ""
        echo "� Quick Start:"
        echo "   1. Open http://localhost:4321 in your browser"
        echo "   2. Use the sidebar to navigate between sections"
        echo "   3. Enter a stock symbol (e.g., AAPL, TSLA) and click 'Analyze'"
        echo "   4. Explore different analysis types and features"
        echo ""
        echo "📝 Available Features:"
        echo "   • Market Overview & Status"
        echo "   • Technical Analysis with Charts"
        echo "   • AI-Powered Sentiment Analysis"
        echo "   • Comprehensive Stock Analysis"
        echo "   • Portfolio Overview & Risk Assessment"
        echo "   • Market Sentiment Monitoring"
        echo "   • Academic Research Paper Search"
        echo ""
        echo "Press Ctrl+C to stop all services"
        echo ""
        
        # Wait for user to stop
        wait
        ;;
        
    2)
        echo ""
        echo "💻 Launching Original CLI Mode..."
        echo "=================================="
        echo ""
        
        echo "📦 Building backend..."
        dotnet build QuantResearchAgent.csproj
        
        if [ $? -ne 0 ]; then
            echo "❌ Backend build failed. Please check for errors."
            exit 1
        fi
        
        echo "✅ Backend built successfully"
        echo ""
        echo "🚀 Starting CLI interface..."
        echo ""
        
        # Run in CLI mode (no --web flag)
        dotnet run --project QuantResearchAgent.csproj
        ;;
        
    3)
        echo ""
        echo "🔧 Launching API Server Only..."
        echo "================================"
        echo ""
        
        echo "📦 Building backend..."
        dotnet build QuantResearchAgent.csproj
        
        if [ $? -ne 0 ]; then
            echo "❌ Backend build failed. Please check for errors."
            exit 1
        fi
        
        echo "✅ Backend built successfully"
        echo ""
        
        echo "🌐 Starting API server..."
        dotnet run --project QuantResearchAgent.csproj -- --web &
        BACKEND_PID=$!
        
        echo ""
        echo "🎉 API Server started successfully!"
        echo ""
        echo "📍 API Endpoint: http://localhost:5000"
        echo ""
        echo "📋 Available API endpoints:"
        echo "   GET  /api/health"
        echo "   GET  /api/market-data/{symbol}"
        echo "   GET  /api/technical-analysis/{symbol}"
        echo "   GET  /api/sentiment-analysis/{symbol}"
        echo "   GET  /api/comprehensive-analysis/{symbol}"
        echo "   GET  /api/portfolio"
        echo "   GET  /api/risk-assessment"
        echo "   GET  /api/market-sentiment"
        echo "   POST /api/research-papers"
        echo ""
        echo "Press Ctrl+C to stop the API server"
        echo ""
        
        # Wait for user to stop
        wait
        ;;
        
    *)
        echo "❌ Invalid choice. Please run the script again and select 1, 2, or 3."
        exit 1
        ;;
esac
