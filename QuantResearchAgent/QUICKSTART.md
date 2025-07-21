# 🚀 Quick Start Guide - Quant Research Agent

## Prerequisites Setup

### 1. API Keys Required
Before running the agent, you'll need the following API keys:

- **OpenAI API Key**: For AI analysis and signal generation
  - Get from: https://platform.openai.com/api-keys
  
- **Spotify Developer Credentials**: For podcast analysis
  - Get from: https://developer.spotify.com/dashboard
  - You'll need Client ID and Client Secret
  
- **Binance API Keys** (Optional): For live crypto trading
  - Get from: https://www.binance.com/en/my/settings/api-management
  - Set to testnet mode initially

### 2. Configure API Keys

Edit `appsettings.json` and add your API keys:

```json
{
  "OpenAI": {
    "ApiKey": "sk-your-openai-api-key-here",
    "ModelId": "gpt-4o"
  },
  "Spotify": {
    "ClientId": "your-spotify-client-id-here",
    "ClientSecret": "your-spotify-client-secret-here"
  },
  "Binance": {
    "ApiKey": "your-binance-api-key-here",
    "SecretKey": "your-binance-secret-key-here",
    "TestMode": true
  }
}
```

## 🏃‍♂️ Running the Agent

### Option 1: Using the Launch Script (Recommended)
```bash
cd /Users/misango/codechest/ArithmaxResearchChest/QuantResearchAgent
./launch.sh
```

### Option 2: Direct .NET Commands
```bash
# Build the project
dotnet build

# Run in interactive mode
dotnet run -- --interactive

# Run in background mode
dotnet run
```

## 🎯 First Steps

### 1. Test Market Data
```
agent> market-data BTCUSDT
```

### 2. Generate Trading Signals
```
agent> generate-signals BTCUSDT
```

### 3. Analyze a Podcast
```
agent> analyze-podcast https://open.spotify.com/episode/69tcEMbTyOEcPfgEJ95xos
```

### 4. Check Portfolio
```
agent> portfolio
```

### 5. Run Risk Assessment
```
agent> risk-assessment
```

## 🔧 Available Commands

### Basic Commands
- `analyze-podcast [url]` - Analyze Spotify podcast for trading insights
- `generate-signals [symbol]` - Generate AI trading signals
- `market-data [symbol]` - Get current market data
- `portfolio` - View portfolio summary and positions
- `risk-assessment` - Assess current portfolio risk
- `test` - Run complete test sequence
- `help` - Show all available functions
- `quit` - Exit the application

### Advanced Function Calls
You can also call Semantic Kernel functions directly:

```
agent> TradingPlugin.GenerateTradingSignalsAsync symbol=ETHUSDT
agent> MarketDataPlugin.CompareSymbolsAsync symbols=BTCUSDT,ETHUSDT,BNBUSDT
agent> RiskManagementPlugin.CalculatePositionSizeAsync symbol=BTCUSDT signalStrength=0.8 currentPrice=45000
```

## 📊 Example Workflow

1. **Start the Agent**
   ```bash
   ./launch.sh
   # Choose option 1 for interactive mode
   ```

2. **Check Market Overview**
   ```
   agent> MarketDataPlugin.GetMarketOverviewAsync
   ```

3. **Analyze Recent Podcast**
   ```
   agent> analyze-podcast https://open.spotify.com/episode/YOUR_EPISODE_ID
   ```

4. **Generate Signals Based on Analysis**
   ```
   agent> generate-signals
   ```

5. **Review Portfolio Risk**
   ```
   agent> risk-assessment
   ```

6. **Execute a Signal** (if valid)
   ```
   agent> TradingPlugin.ExecuteTradingSignalAsync symbol=BTCUSDT signalType=BUY strength=0.7 price=45000 reasoning="Strong podcast sentiment"
   ```

## 🎮 Interactive Features

### Real-time Data
- Market data refreshes automatically every 15 minutes
- Podcast analysis runs every 6 hours
- Risk assessment updates continuously

### AI-Powered Analysis
- Sentiment analysis of podcast content
- Technical indicator calculation
- Risk-adjusted position sizing
- Portfolio optimization recommendations

### Comprehensive Risk Management
- Maximum drawdown protection
- Position size limits
- Volatility targeting
- Stop-loss and take-profit automation

## 🛠️ Troubleshooting

### Common Issues

1. **API Key Errors**
   - Verify your API keys are correct in `appsettings.json`
   - Check API key permissions and rate limits

2. **Spotify Podcast Access**
   - Ensure the podcast episode is publicly available
   - Check Spotify API quota limits

3. **Market Data Issues**
   - Binance API may have rate limits
   - Check internet connectivity
   - Verify symbol names are correct (e.g., "BTCUSDT" not "BTC")

4. **Build Errors**
   - Ensure .NET 8.0 SDK is installed
   - Run `dotnet restore` to update packages

### Getting Help

1. **View Available Functions**
   ```
   agent> help
   ```

2. **Check System Status**
   ```
   agent> TradingPlugin.AnalyzeTradingPerformanceAsync
   ```

3. **Test All Components**
   ```
   agent> test
   ```

## 🔄 Next Steps

1. **Customize Risk Parameters**: Edit the risk management settings in `appsettings.json`
2. **Add More Symbols**: Extend the tracked symbols list in `TradingSignalService.cs`
3. **Integrate Live Trading**: Configure real Binance API keys (disable test mode)
4. **Create Custom Strategies**: Add your own trading logic based on the existing Python strategies
5. **Monitor Performance**: Use the portfolio analytics to track strategy effectiveness

## 📚 Learn More

- Read the full `README.md` for detailed architecture information
- Explore the source code in the `/Services` and `/Plugins` directories
- Check out the existing Python strategies for integration ideas
- Review the Semantic Kernel documentation for advanced AI features
