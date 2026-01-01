# CLI vs Web App Feature Gap Analysis

## Currently Implemented in Web App ✅

### AskFeen Floating Chat Widget
- AI-powered conversational assistant
- Navigation helper for FeenQR features
- Context-aware suggestions
- Quick access to common tasks
- Smart routing to relevant sections

### Market Data Controller
- Basic quotes (AAPL, NVDA, TSLA, MSFT, GOOGL)
- Individual symbol quote lookup
- Yahoo Finance data
- Polygon quote, daily bars, aggregates, financials
- DataBento data
- Multi-symbol quotes

### News Controller
- Basic news feed
- Symbol-specific news (Polygon integration)

### Chat Controller
- DeepSeek chat
- OpenAI chat

### Research Controller ✅ NEW
- Quick research (conversational AI research)
- Deep research with LLM
- Research query building
- YouTube video analysis  
- Quantopian video library
- Finance video search
- Academic paper search
- Paper analysis and strategy extraction
- Research synthesis and citation networks
- Comprehensive research reports
- Report library

---

## Missing from Web App (Available in CLI) ❌

### 1. **AI & Research Features** ✅ IMPLEMENTED
- `ai-assistant` / `chat` / `assistant` - ✅ Fully implemented (Chat Controller + AskFeen widget)
- `analyze-video` - ✅ YouTube video analysis (ResearchController)
- `get-quantopian-videos` - ✅ Quantopian video retrieval (ResearchController)
- `search-finance-videos` - ✅ Finance video search (ResearchController)
- `research-papers` - ✅ Academic paper search (ResearchController)
- `analyze-paper` - ✅ Paper analysis (ResearchController)
- `research` / `research-synthesis` - ✅ Research synthesis (ResearchController)
- `quick-research` - ✅ Quick research queries (ResearchController)
- `research-llm` - ✅ LLM-powered research (ResearchController)
- `research-query` - ✅ Research queries (ResearchController)
- `research-report` - ✅ Generate research reports (ResearchController)

### 2. **Technical Analysis**
- `ta` / `technical-analysis` - Technical analysis with indicators
- `ta-long` - Long-term technical analysis
- `ta-indicators` - Specific TA indicators
- `ta-compare` - Compare TA across symbols
- `ta-patterns` - Pattern recognition
- `ta-full` - Full technical analysis suite

### 3. **Fundamental Analysis**
- `fundamental` / `fundamental-analysis` - Fundamental analysis
- `enhanced-analysis` - Enhanced fundamental analysis
- `alpha-vantage` - Alpha Vantage fundamental data
- `iex` / `iex-data` - IEX Cloud data
- `fmp` / `fmp-data` - Financial Modeling Prep data

### 4. **Comprehensive Analysis**
- `analyze` / `comprehensive-analysis` - Full stock analysis combining multiple data sources

### 5. **Portfolio Management**
- `portfolio` - Portfolio summary and allocation visualization
- `risk-assessment` - Portfolio risk assessment
- `portfolio-analytics` - Advanced portfolio analytics
- `portfolio-strategy` - Portfolio strategy builder
- `portfolio-monte-carlo` - Monte Carlo portfolio simulation
- `optimize-portfolio` - Portfolio optimization

### 6. **Trading & Orders (Alpaca)**
- `alpaca` / `alpaca-data` - Alpaca market data
- `historical` / `alpaca-historical` - Historical data
- `alpaca-account` - Account information
- `alpaca-positions` - Current positions
- `bracket-order` - Bracket orders
- `oco-order` - OCO orders
- `trailing-stop` - Trailing stop orders
- `iceberg-order` - Iceberg orders
- `smart-routing` - Smart order routing

### 7. **Risk Analytics**
- `risk-metrics` - Risk metrics calculation
- `calculate-var` - Value at Risk
- `stress-test` / `stress-test-portfolio` - Portfolio stress testing
- `risk-attribution` - Risk attribution analysis
- `risk-report` - Comprehensive risk reports
- `compare-risk-measures` - Compare different risk measures
- `advanced-risk` - Advanced risk analytics (CVaR, Expected Shortfall, etc.)
- `counterparty-risk` - Counterparty risk analysis
- `tail-risk` - Tail risk analysis

### 8. **Performance Analytics**
- `performance-metrics` - Performance metrics
- `performance-attribution` - Performance attribution analysis
- `benchmarking` - Benchmark comparison
- `tax-lots` - Tax lot tracking

### 9. **News & Sentiment Analysis**
- `sentiment` / `sentiment-analysis` - ✅ Partially (news exists, sentiment analysis missing)
- `market-sentiment` - Market-wide sentiment
- `reddit-sentiment` - Reddit sentiment
- `reddit-scrape` - Reddit data scraping
- `reddit-finance-trending` - Trending finance topics on Reddit
- `reddit-finance-search` - Search Reddit finance communities
- `reddit-finance-sentiment` - Reddit finance sentiment
- `reddit-market-pulse` - Market pulse from Reddit

### 10. **Economic Data**
- `fred-series` - FRED economic data series
- `fred-search` - Search FRED indicators
- `fred-popular` - Popular FRED indicators
- `worldbank-series` - World Bank data
- `worldbank-search` - Search World Bank indicators
- `worldbank-popular` - Popular World Bank indicators
- `worldbank-indicator` - World Bank indicators
- `oecd-series` - OECD data
- `oecd-search` - Search OECD data
- `oecd-popular` - Popular OECD indicators
- `oecd-indicator` - OECD indicators
- `imf-series` - IMF data
- `imf-search` - Search IMF data
- `imf-popular` - Popular IMF indicators
- `imf-indicator` - IMF indicators

### 11. **Statistical Analysis**
- `statistical-test` - Statistical hypothesis testing
- `hypothesis-test` - Hypothesis testing
- `power-analysis` - Statistical power analysis
- `time-series-analysis` - Time series analysis
- `stationarity-test` - Stationarity testing
- `autocorrelation-analysis` - Autocorrelation analysis
- `seasonal-decomposition` - Seasonal decomposition
- `ts-analysis` / `time-series-stock` - Stock time series analysis

### 12. **Forecasting**
- `forecast` - Time series forecasting
- `forecast-compare` - Compare forecast models
- `forecast-accuracy` - Forecast accuracy metrics

### 13. **Machine Learning**
- `feature-engineer` - Feature engineering
- `feature-importance` - Feature importance analysis
- `feature-select` / `feature-selection` - Feature selection
- `validate-model` - Model validation
- `cross-validate` / `cross-validation` - Cross-validation
- `model-metrics` - Model performance metrics
- `automl-pipeline` - AutoML pipeline
- `model-selection` - Model selection
- `ensemble-prediction` - Ensemble predictions
- `hyperparameter-opt` - Hyperparameter optimization

### 14. **Model Interpretability**
- `shap-analysis` - SHAP value analysis
- `partial-dependence` - Partial dependence plots
- `feature-interactions` - Feature interaction analysis
- `explain-prediction` - Prediction explanations
- `permutation-importance` - Permutation importance
- `model-fairness` - Model fairness analysis
- `interpretability-report` - Interpretability reports

### 15. **Reinforcement Learning**
- `train-q-learning` - Q-learning training
- `train-policy-gradient` - Policy gradient training
- `train-actor-critic` - Actor-critic training
- `adapt-strategy` - Strategy adaptation
- `bandit-optimization` - Multi-armed bandit optimization
- `contextual-bandit` - Contextual bandit algorithms
- `evaluate-rl-agent` - RL agent evaluation
- `rl-strategy-report` - RL strategy reports

### 16. **Portfolio Optimization**
- `black-litterman` - Black-Litterman optimization
- `risk-parity` - Risk parity portfolio
- `hierarchical-risk-parity` - Hierarchical Risk Parity (HRP)
- `minimum-variance` - Minimum variance portfolio
- `compare-optimization` - Compare optimization methods

### 17. **Factor Models**
- `fama-french-3factor` - Fama-French 3-factor model
- `carhart-4factor` - Carhart 4-factor model
- `custom-factor-model` - Custom factor models
- `factor-attribution` - Factor attribution
- `compare-factor-models` - Compare factor models
- `factor-research` - Factor research
- `factor-portfolio` - Factor-based portfolio construction
- `factor-efficacy` - Factor efficacy testing
- `dynamic-factors` - Dynamic factor analysis

### 18. **Cointegration & Causality**
- `engle-granger-test` - Engle-Granger cointegration test
- `johansen-test` - Johansen cointegration test
- `granger-causality` - Granger causality test
- `lead-lag-analysis` - Lead-lag analysis
- `cointegration-analysis` - Cointegration analysis
- `granger-stock-test` - Granger test for stocks

### 19. **SEC Filings & Earnings**
- `sec` / `sec-analysis` - SEC filing analysis
- `sec-filing-history` - SEC filing history
- `sec-risk-factors` - Risk factors from 10-K
- `sec-management-discussion` - MD&A analysis
- `sec-comprehensive` - Comprehensive SEC analysis
- `earnings` / `earnings-analysis` - Earnings analysis
- `earnings-history` - Earnings history
- `earnings-sentiment` - Earnings call sentiment
- `earnings-strategic` - Strategic insights from earnings
- `earnings-risks` - Earnings risks
- `earnings-comprehensive` - Comprehensive earnings analysis

### 20. **Supply Chain Analysis**
- `supply-chain-analysis` - Supply chain analysis
- `supply-chain-risks` - Supply chain risk assessment
- `supply-chain-geography` - Geographic supply chain analysis
- `supply-chain-diversification` - Supply chain diversification
- `supply-chain-resilience` - Supply chain resilience
- `supply-chain-comprehensive` - Comprehensive supply chain analysis

### 21. **Market Microstructure**
- `order-book-analysis` - Order book analysis
- `market-depth` - Market depth analysis
- `liquidity-analysis` - Liquidity analysis
- `spread-analysis` - Bid-ask spread analysis
- `orderbook-reconstruction` - Order book reconstruction
- `hft-analysis` - HFT analysis
- `manipulation-detection` - Market manipulation detection

### 22. **Execution Algorithms**
- `almgren-chriss` - Almgren-Chriss model
- `implementation-shortfall` - Implementation shortfall
- `price-impact` - Price impact analysis
- `vwap-schedule` - VWAP execution schedule
- `twap-schedule` - TWAP execution schedule
- `execution-optimization` - Execution optimization
- `optimal-execution` - Optimal execution strategies

### 23. **Options & Derivatives**
- `option-monte-carlo` - Option Monte Carlo pricing
- `options-flow` - Options flow analysis
- `unusual-options` - Unusual options activity
- `gamma-exposure` - Gamma exposure analysis
- `options-orderbook` - Options order book
- `volatility-surface` - Volatility surface
- `vix-analysis` - VIX analysis
- `volatility-strategy` - Volatility trading strategies
- `volatility-monitor` - Volatility monitoring
- `vol-surface` - Volatility surface visualization

### 24. **Latency & HFT**
- `latency-arbitrage` - Latency arbitrage analysis
- `colocation-analysis` - Co-location analysis
- `order-routing` - Order routing analysis
- `market-data-feeds` - Market data feed analysis
- `arbitrage-profitability` - Arbitrage profitability
- `latency-scan` - Latency scanning

### 25. **Strategy Development**
- `scenario-analysis` - Scenario analysis
- `build-strategy` - Strategy builder
- `optimize-strategy` - Strategy optimization
- `generate-trading-template` - Generate trading templates
- `trading-template` - Trading templates
- `live-strategy` / `deploy-strategy` - Live strategy deployment
- `event-trading` / `event-driven` - Event-driven trading

### 26. **Monitoring & Compliance**
- `alerts` / `check-alerts` - Real-time alerts
- `compliance` / `compliance-check` - Compliance monitoring
- `data-validation` - Data validation
- `corporate-action` / `corp-action` - Corporate actions
- `timezone` - Timezone handling

### 27. **Research Tools**
- `academic-research` - Academic research tools
- `replicate-study` - Study replication
- `citation-network` - Citation network analysis
- `quantitative-model` - Quantitative modeling
- `literature-review` - Literature review generation

### 28. **Advanced Data Sources**
- `extract-web-data` - Web data extraction
- `scrape-social-media` - Social media scraping
- `analyze-satellite-imagery` - Satellite imagery analysis
- `geo-satellite` - Geospatial satellite data
- `consumer-pulse` - Consumer sentiment pulse

### 29. **FIX Protocol**
- `fix-connect` - FIX protocol connection
- `fix-disconnect` - FIX disconnect
- `fix-order` - FIX order submission
- `fix-cancel` - FIX order cancellation
- `fix-market-data` - FIX market data
- `fix-heartbeat` - FIX heartbeat
- `fix-status` - FIX connection status
- `fix-info` - FIX session info

### 30. **Alternative Data**
- `web-scrape-earnings` - Web scraping for earnings
- `web-monitor-communications` - Monitor company communications
- `web-analyze-sentiment` - Web sentiment analysis
- `web-analyze-social` - Social media analysis
- `web-monitor-darkweb` - Dark web monitoring
- `patent-search` - Patent search
- `patent-innovation` - Patent innovation analysis
- `patent-citations` - Patent citation analysis
- `patent-value` - Patent valuation

### 31. **Federal Reserve & Global Economics**
- `fed-fomc-announcements` - FOMC announcements
- `fed-interest-rates` - Federal Reserve interest rates
- `fed-economic-projections` - Fed economic projections
- `fed-speeches` - Fed speeches analysis
- `global-economic-indicators` - Global economic indicators
- `global-supply-chain` - Global supply chain data
- `global-trade-data` - Global trade data
- `global-currency-data` - Global currency data
- `global-commodity-prices` - Global commodity prices

### 32. **Notebook & Report Generation**
- `create-notebook` - Create Jupyter notebooks
- `execute-notebook` - Execute notebooks
- `generate-report` - Generate reports
- `generate-research-report` - Generate research reports

### 33. **Market Analysis**
- `detect-market-regime` - Market regime detection
- `detect-anomalies` / `anomaly-scan` - Anomaly detection
- `test-apis` - API connectivity testing

### 34. **Other Specialized Features**
- `prime-connect` - Prime broker connectivity
- `esg-footprint` - ESG footprint analysis

---

## Summary Statistics

**Total CLI Commands:** ~220+ unique commands
**Web App Endpoints:** ~12 endpoints
**Feature Coverage:** ~5-7%

## Priority Recommendations for Web App Implementation

### High Priority (Core Functionality)
1. **Technical Analysis** - Essential for traders
2. **Portfolio Management** - Core functionality
3. **Comprehensive Analysis** - Main value proposition
4. **Alpaca Account/Positions** - Trading interface
5. **Risk Metrics** - Risk management

### Medium Priority (Enhanced Features)
1. **Sentiment Analysis** - Already have news, add sentiment
2. **Statistical Testing** - Quant research
3. **SEC Filings** - Fundamental research
4. **Earnings Analysis** - Fundamental research
5. **FRED Economic Data** - Macro analysis

### Lower Priority (Advanced Features)
1. **ML/AutoML** - Advanced quant features
2. **FIX Protocol** - Professional trading
3. **Alternative Data** - Specialized use cases
4. **Notebooks** - Development tools
5. **Geopolitical/Patent Analysis** - Niche features
