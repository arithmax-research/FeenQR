# Arithmax Research Quantitative Research 1.0.0

An intelligent agentic AI system built with Microsoft Semantic Kernel that performs quantitative research by analyzing YouTube videos, Market trends and generating trading signals. This system combines the power of AI-driven content analysis with sophisticated trading algorithms.

## Features

### Core Capabilities
- **YouTube Video Analysis**: Automatically fetches and analyzes financial videos from YouTube (especially Quantopian channel)
- **Trading Signal Generation**: Creates actionable trading signals using AI and technical analysis
- **Risk Management**: Comprehensive portfolio risk assessment and position sizing
- **Market Data Integration**: Real-time market data from Binance and other sources
- **Portfolio Management**: Track positions, calculate metrics, and manage trades
- **Agent Orchestration**: Autonomous job scheduling and execution

### Advanced Research Agents
- **Market Sentiment Agent**: Multi-source sentiment analysis (news, social media, fear/greed index, technical)
- **Statistical Pattern Agent**: Deep mathematical analysis to detect exploitable market patterns

### AI-Powered Analysis
- **Sentiment Analysis**: Extract market sentiment from video content and internet sources
- **Technical Insights**: Identify trading concepts and strategies from video content
- **Signal Generation**: Convert insights into executable trading signals
- **Risk Assessment**: AI-driven portfolio risk evaluation
- **Academic Research**: Extract trading strategies from research papers

## Architecture

### Semantic Kernel Integration
The system is built around Microsoft Semantic Kernel, providing:
- **Plugin Architecture**: Modular AI functions for different domains
- **Natural Language Processing**: Advanced content analysis capabilities
- **Function Calling**: Structured AI interactions with trading systems
- **Orchestration**: Intelligent job scheduling and execution

### Core Components

#### Services Layer
- `YouTubeAnalysisService`: YouTube integration and content analysis
- `TradingSignalService`: Signal generation and management
- `MarketDataService`: Real-time and historical market data
- `RiskManagementService`: Portfolio risk assessment
- `PortfolioService`: Position and performance tracking

##### Research Agent Services
- `MarketSentimentAgentService`: Multi-source sentiment analysis and market direction
- `StatisticalPatternAgentService`: Mathematical pattern detection and exploitability analysis

#### Plugins Layer (Semantic Kernel Functions)
- `PodcastAnalysisPlugin`: AI functions for YouTube video content analysis
- `YouTubeAnalysisPlugin`: Dedicated YouTube video analysis functions
- `TradingPlugin`: Trading operations and signal management
- `MarketDataPlugin`: Market data retrieval and analysis
- `RiskManagementPlugin`: Risk assessment and portfolio management

##### Research Agent Plugins
- `MarketSentimentPlugin`: Sentiment analysis and trading signal generation
- `StatisticalPatternPlugin`: Pattern detection and statistical analysis functions

#### Core Layer
- `AgentOrchestrator`: Main orchestration engine
- `Models`: Data structures and business entities

## Setup and Configuration

### Prerequisites
- .NET 8.0 SDK
- OpenAI API Key
- YouTube Data API Key (v3)
- Binance API Keys (optional, for live trading)

### Installation

1. **Clone the repository**
   ```bash
   cd /Users/misango/codechest/ArithmaxResearchChest
   ```

2. **Configure Settings**
   Update `appsettings.json` with your API keys:
   ```json
   {
     "OpenAI": {
       "ApiKey": "your-openai-api-key",
       "ModelId": "gpt-4o"
     },
     "Spotify": {
       "ClientId": "your-spotify-client-id",
       "ClientSecret": "your-spotify-client-secret"
     },
     "Binance": {
       "ApiKey": "your-binance-api-key",
       "SecretKey": "your-binance-secret-key",
       "TestMode": true
     }
   }
   ```

3. **Install Dependencies**
   ```bash
   dotnet restore
   ```

4. **Build the Project**
   ```bash
   dotnet build
   ```

5. **Run the Agent**
   ```bash
   dotnet run
   ```

## Usage Examples

### Podcast Analysis
```csharp
// Analyze a Spotify podcast episode
var episode = await podcastService.AnalyzePodcastAsync("https://open.spotify.com/episode/69tcEMbTyOEcPfgEJ95xos");
```

### Trading Signal Generation
```csharp
// Generate signals for all tracked symbols
var signals = await signalService.GenerateSignalsAsync();

// Generate signal for specific symbol
var btcSignal = await signalService.GenerateSignalAsync("BTCUSDT");
```

### Risk Management
```csharp
// Assess portfolio risk
var riskAssessment = await riskService.AssessPortfolioRiskAsync();

// Calculate position size
var positionSize = await riskService.CalculatePositionSizeAsync(signal);
```

### Portfolio Management
```csharp
// Get portfolio metrics
var metrics = await portfolioService.CalculateMetricsAsync();

// Execute trading signal
var executed = await portfolioService.ExecuteSignalAsync(signal, quantity);
```

## Semantic Kernel Plugin Functions

The system exposes AI functions through Semantic Kernel plugins:

### Core Trading Plugins

#### PodcastAnalysisPlugin
- `AnalyzePodcastAsync()`: Full podcast analysis with insights and signals
- `GetPodcastSentimentAsync()`: Extract sentiment score from podcast
- `ExtractTradingInsightsAsync()`: Get technical trading insights

#### TradingPlugin
- `GenerateTradingSignalsAsync()`: Generate signals for symbols
- `ExecuteTradingSignalAsync()`: Execute a signal with risk management
- `GetActiveSignalsAsync()`: Retrieve current active signals
- `AnalyzeTradingPerformanceAsync()`: Performance analysis and metrics

#### MarketDataPlugin
- `GetMarketDataAsync()`: Current market data for symbols
- `GetHistoricalDataAsync()`: Historical price data
- `CompareSymbolsAsync()`: Compare multiple symbols performance
- `GetMarketOverviewAsync()`: Overall market summary

#### RiskManagementPlugin
- `AssessPortfolioRiskAsync()`: Comprehensive risk assessment
- `GetPortfolioSummaryAsync()`: Portfolio positions and metrics
- `CalculatePositionSizeAsync()`: Optimal position sizing
- `AnalyzeDiversificationAsync()`: Portfolio diversification analysis

### Research Agent Plugins

#### MarketSentimentPlugin
- `AnalyzeMarketSentimentAsync()`: Multi-source sentiment analysis
- `CompareSentimentAcrossAssetsAsync()`: Cross-asset sentiment comparison
- `GenerateSentimentTradingSignalsAsync()`: Sentiment-based trading signals
- `GetSentimentAlertsAsync()`: Extreme sentiment condition alerts
- `GetSentimentTrendAsync()`: Historical sentiment trend analysis

#### StatisticalPatternPlugin
- `DetectPatternsAsync()`: Comprehensive pattern detection
- `DetectSpecificPatternAsync()`: Target specific pattern types
- `ComparePatternAcrossSymbolsAsync()`: Multi-symbol pattern comparison
- `DetectAnomaliesAsync()`: Statistical anomaly detection
- `AnalyzeVolatilityPatternsAsync()`: Volatility clustering analysis
- `GenerateImplementationRoadmapAsync()`: Pattern implementation guidance

## Integration with Existing Strategies

This system builds upon your existing Python quantitative strategies:

### Similar Patterns
- **EMA/SMA Strategies**: Technical indicator integration
- **Scalping Strategy**: High-frequency signal generation
- **Swing Trading**: Medium-term position management
- **Risk Management**: Drawdown control and position sizing
- **Portfolio Optimization**: Multi-asset strategy execution

### Enhanced Capabilities
- **AI-Driven Analysis**: Semantic understanding of market content
- **Automated Orchestration**: Self-managing agent workflows
- **Natural Language Interface**: Conversational trading interactions
- **Multi-Source Integration**: Podcast, market data, and news analysis

## 🔄 Agent Workflow

1. **Initialization**: Load configuration and register plugins
2. **Scheduled Jobs**: Podcast analysis, market data updates
3. **Signal Generation**: AI-powered trading signal creation
4. **Risk Validation**: Comprehensive risk checks before execution
5. **Portfolio Management**: Position tracking and performance monitoring
6. **Continuous Learning**: Adaptation based on performance feedback

## Performance Monitoring

## 🧠 Research Agent Capabilities

### Market Sentiment Agent
**Multi-Source Sentiment Analysis & Prediction**
- Analyzes news sentiment from financial news sources
- Processes social media sentiment (Twitter, Reddit, Discord)
- Monitors Fear & Greed Index and volatility indicators
- Performs technical sentiment analysis
- Generates market direction predictions

**Key Features:**
- Real-time sentiment aggregation from multiple sources
- Confidence-weighted sentiment scoring
- Contrarian vs momentum signal generation
- Extreme sentiment alert system
- Historical sentiment trend analysis

### Statistical Pattern Agent
**Deep Mathematical Market Analysis**
- Detects mean reversion patterns with statistical significance
- Identifies momentum and trend patterns
- Analyzes seasonal and calendar effects
- Performs volatility clustering analysis (GARCH modeling)
- Detects statistical anomalies and outliers
- Analyzes fractal patterns and self-similarity
- Identifies potential arbitrage opportunities
- Performs regime change detection
- Analyzes market microstructure patterns

**Supported Pattern Types:**
- **Mean Reversion**: ADF tests, Hurst exponent, half-life calculations
- **Momentum**: Autocorrelation analysis, trend strength measurement
- **Seasonality**: Intraday, day-of-week, monthly effects
- **Volatility**: GARCH parameters, volatility clustering
- **Correlation**: Cross-asset correlation breakdowns
- **Anomaly**: Outlier detection, jump detection
- **Fractal**: Self-similarity analysis, fractal dimension
- **Arbitrage**: Price discrepancy identification
- **Regime**: Market regime change detection
- **Microstructure**: Order flow analysis, volume patterns

The system tracks comprehensive metrics:
- Total return and P&L
- Sharpe ratio and volatility
- Maximum drawdown
- Win rate and profit factor
- Portfolio diversification
- Risk-adjusted returns

## Risk Management

Built-in risk controls include:
- Maximum drawdown limits
- Position size constraints
- Volatility targeting
- Diversification requirements
- Stop-loss and take-profit management

## 🤖 AI Agent Capabilities

- **Autonomous Operation**: Self-managing job execution
- **Adaptive Learning**: Performance-based strategy adjustment
- **Multi-Modal Analysis**: Text, audio, and numerical data processing
- **Contextual Decision Making**: Market regime awareness
- **Natural Language Interaction**: Conversational AI interface

## 🔗 Next Steps

1. **Audio Processing**: Integrate speech-to-text for actual podcast transcription
2. **Live Trading**: Connect to real exchange APIs
3. **Advanced Strategies**: Implement your complex Python strategies
4. **UI Dashboard**: Web interface for monitoring and control
5. **Machine Learning**: Enhanced prediction models
6. **Multi-Asset Support**: Expand beyond crypto to stocks, forex, etc.

This system provides a solid foundation for building a comprehensive quantitative research platform with AI-driven analysis and autonomous trading capabilities.
