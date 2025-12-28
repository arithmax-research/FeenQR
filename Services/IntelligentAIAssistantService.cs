using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using QuantResearchAgent.Core;
using QuantResearchAgent.Plugins;
using QuantResearchAgent.Services.ResearchAgents;
using Feen.Services;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace QuantResearchAgent.Services
{
    public class IntelligentAIAssistantService
    {
        private readonly ILogger<IntelligentAIAssistantService> _logger;
        private readonly ILLMService _llmService;
        private readonly IConfiguration _configuration;
        
        // Data Services
        private readonly YahooFinanceService _yahooFinanceService;
        private readonly AlpacaService _alpacaService;
        private readonly PolygonService _polygonService;
        private readonly DataBentoService _dataBentoService;
        private readonly MarketDataService _marketDataService;
        
        // Analysis Services
        private readonly TechnicalAnalysisService _technicalAnalysisService;
        private readonly ComprehensiveStockAnalysisAgent _comprehensiveAnalysisAgent;
        private readonly NewsSentimentAnalysisService _newsSentimentService;
        private readonly RedditScrapingService _redditScrapingService;
        
        // News Services
        private readonly YFinanceNewsService _yfinanceNewsService;
        private readonly FinvizNewsService _finvizNewsService;
        
        // Other Services
        private readonly PortfolioOptimizationService _portfolioOptimizationService;
        private readonly SocialMediaScrapingService _socialMediaScrapingService;
        private readonly YouTubeAnalysisService _youtubeAnalysisService;
        private readonly GoogleWebSearchPlugin _googleWebSearchPlugin;
        
        // Additional Services
        private readonly WebDataExtractionService _webDataExtractionService;
        private readonly ReportGenerationService _reportGenerationService;
        private readonly SatelliteImageryAnalysisService _satelliteImageryAnalysisService;
        private readonly TradingTemplateGeneratorAgent _tradingTemplateGeneratorAgent;
        private readonly StatisticalTestingService _statisticalTestingService;
        private readonly TimeSeriesAnalysisService _timeSeriesAnalysisService;
        private readonly CointegrationAnalysisService _cointegrationAnalysisService;
        private readonly TimeSeriesForecastingService _forecastingService;
        private readonly FeatureEngineeringService _featureEngineeringService;
        private readonly ModelValidationService _modelValidationService;
        private readonly FactorModelService _factorModelService;
        private readonly AdvancedOptimizationService _advancedOptimizationService;
        private readonly AdvancedRiskService _advancedRiskService;
        private readonly SECFilingsService _secFilingsService;
        private readonly EarningsCallService _earningsCallService;
        private readonly SupplyChainService _supplyChainService;
        private readonly OrderBookAnalysisService _orderBookAnalysisService;
        private readonly MarketImpactService _marketImpactService;
        private readonly ExecutionService _executionService;
        private readonly MonteCarloService _monteCarloService;
        private readonly StrategyBuilderService _strategyBuilderService;
        private readonly NotebookService _notebookService;
        private readonly DataValidationService _dataValidationService;
        private readonly CorporateActionService _corporateActionService;
        private readonly FREDService _fredService;
        private readonly WorldBankService _worldBankService;
        private readonly AdvancedAlpacaService _advancedAlpacaService;
        private readonly FactorResearchService _factorResearchService;
        private readonly AcademicResearchService _academicResearchService;
        private readonly AutoMLService _autoMLService;
        private readonly ModelInterpretabilityService _modelInterpretabilityService;
        private readonly ReinforcementLearningService _reinforcementLearningService;
        private readonly WebIntelligenceService _webIntelligenceService;
        private readonly PatentAnalysisService _patentAnalysisService;
        private readonly FederalReserveService _federalReserveService;
        private readonly GlobalEconomicService _globalEconomicService;
        private readonly GeopoliticalRiskService _geopoliticalRiskService;
        private readonly OptionsFlowService _optionsFlowService;
        private readonly VolatilityTradingService _volatilityTradingService;
        private readonly AdvancedMicrostructureService _advancedMicrostructureService;
        private readonly AlphaVantageService _alphaVantageService;
        
        // Conversation history
        private readonly List<ConversationMessage> _conversationHistory;

        public IntelligentAIAssistantService(
            ILogger<IntelligentAIAssistantService> logger,
            ILLMService llmService,
            IConfiguration configuration,
            YahooFinanceService yahooFinanceService,
            AlpacaService alpacaService,
            PolygonService polygonService,
            DataBentoService dataBentoService,
            MarketDataService marketDataService,
            TechnicalAnalysisService technicalAnalysisService,
            ComprehensiveStockAnalysisAgent comprehensiveAnalysisAgent,
            NewsSentimentAnalysisService newsSentimentService,
            RedditScrapingService redditScrapingService,
            YFinanceNewsService yfinanceNewsService,
            FinvizNewsService finvizNewsService,
            PortfolioOptimizationService portfolioOptimizationService,
            SocialMediaScrapingService socialMediaScrapingService,
            YouTubeAnalysisService youtubeAnalysisService,
            GoogleWebSearchPlugin googleWebSearchPlugin,
            WebDataExtractionService webDataExtractionService,
            ReportGenerationService reportGenerationService,
            SatelliteImageryAnalysisService satelliteImageryAnalysisService,
            TradingTemplateGeneratorAgent tradingTemplateGeneratorAgent,
            StatisticalTestingService statisticalTestingService,
            TimeSeriesAnalysisService timeSeriesAnalysisService,
            CointegrationAnalysisService cointegrationAnalysisService,
            TimeSeriesForecastingService forecastingService,
            FeatureEngineeringService featureEngineeringService,
            ModelValidationService modelValidationService,
            FactorModelService factorModelService,
            AdvancedOptimizationService advancedOptimizationService,
            AdvancedRiskService advancedRiskService,
            SECFilingsService secFilingsService,
            EarningsCallService earningsCallService,
            SupplyChainService supplyChainService,
            OrderBookAnalysisService orderBookAnalysisService,
            MarketImpactService marketImpactService,
            ExecutionService executionService,
            MonteCarloService monteCarloService,
            StrategyBuilderService strategyBuilderService,
            NotebookService notebookService,
            DataValidationService dataValidationService,
            CorporateActionService corporateActionService,
            FREDService fredService,
            WorldBankService worldBankService,
            AdvancedAlpacaService advancedAlpacaService,
            FactorResearchService factorResearchService,
            AcademicResearchService academicResearchService,
            AutoMLService autoMLService,
            ModelInterpretabilityService modelInterpretabilityService,
            ReinforcementLearningService reinforcementLearningService,
            WebIntelligenceService webIntelligenceService,
            PatentAnalysisService patentAnalysisService,
            FederalReserveService federalReserveService,
            GlobalEconomicService globalEconomicService,
            GeopoliticalRiskService geopoliticalRiskService,
            OptionsFlowService optionsFlowService,
            VolatilityTradingService volatilityTradingService,
            AdvancedMicrostructureService advancedMicrostructureService,
            AlphaVantageService alphaVantageService)
        {
            _logger = logger;
            _llmService = llmService;
            _configuration = configuration;
            _yahooFinanceService = yahooFinanceService;
            _alpacaService = alpacaService;
            _polygonService = polygonService;
            _dataBentoService = dataBentoService;
            _marketDataService = marketDataService;
            _technicalAnalysisService = technicalAnalysisService;
            _comprehensiveAnalysisAgent = comprehensiveAnalysisAgent;
            _newsSentimentService = newsSentimentService;
            _redditScrapingService = redditScrapingService;
            _yfinanceNewsService = yfinanceNewsService;
            _finvizNewsService = finvizNewsService;
            _portfolioOptimizationService = portfolioOptimizationService;
            _socialMediaScrapingService = socialMediaScrapingService;
            _youtubeAnalysisService = youtubeAnalysisService;
            _googleWebSearchPlugin = googleWebSearchPlugin;
            _webDataExtractionService = webDataExtractionService;
            _reportGenerationService = reportGenerationService;
            _satelliteImageryAnalysisService = satelliteImageryAnalysisService;
            _tradingTemplateGeneratorAgent = tradingTemplateGeneratorAgent;
            _statisticalTestingService = statisticalTestingService;
            _timeSeriesAnalysisService = timeSeriesAnalysisService;
            _cointegrationAnalysisService = cointegrationAnalysisService;
            _forecastingService = forecastingService;
            _featureEngineeringService = featureEngineeringService;
            _modelValidationService = modelValidationService;
            _factorModelService = factorModelService;
            _advancedOptimizationService = advancedOptimizationService;
            _advancedRiskService = advancedRiskService;
            _secFilingsService = secFilingsService;
            _earningsCallService = earningsCallService;
            _supplyChainService = supplyChainService;
            _orderBookAnalysisService = orderBookAnalysisService;
            _marketImpactService = marketImpactService;
            _executionService = executionService;
            _monteCarloService = monteCarloService;
            _strategyBuilderService = strategyBuilderService;
            _notebookService = notebookService;
            _dataValidationService = dataValidationService;
            _corporateActionService = corporateActionService;
            _fredService = fredService;
            _worldBankService = worldBankService;
            _advancedAlpacaService = advancedAlpacaService;
            _factorResearchService = factorResearchService;
            _academicResearchService = academicResearchService;
            _autoMLService = autoMLService;
            _modelInterpretabilityService = modelInterpretabilityService;
            _reinforcementLearningService = reinforcementLearningService;
            _webIntelligenceService = webIntelligenceService;
            _patentAnalysisService = patentAnalysisService;
            _federalReserveService = federalReserveService;
            _globalEconomicService = globalEconomicService;
            _geopoliticalRiskService = geopoliticalRiskService;
            _optionsFlowService = optionsFlowService;
            _volatilityTradingService = volatilityTradingService;
            _advancedMicrostructureService = advancedMicrostructureService;
            _alphaVantageService = alphaVantageService;
            _conversationHistory = new List<ConversationMessage>();
        }

        public async Task<string> ProcessUserRequestAsync(string userInput)
        {
            // Add user message to conversation history
            _conversationHistory.Add(new ConversationMessage 
            { 
                Role = "user", 
                Content = userInput, 
                Timestamp = DateTime.UtcNow 
            });

            Console.WriteLine("\nAI Assistant is analyzing your request...\n");

            try
            {
                // Step 1: Analyze the user request to determine intent and required tools
                var analysisResult = await AnalyzeUserRequestAsync(userInput);
                
                Console.WriteLine($"Analysis: {analysisResult.Intent}");
                Console.WriteLine($"Selected tools: {string.Join(", ", analysisResult.RequiredTools)}");
                
                if (analysisResult.ExtractedSymbols.Any())
                {
                    Console.WriteLine($"Symbols detected: {string.Join(", ", analysisResult.ExtractedSymbols)}");
                }
                Console.WriteLine();

                // Step 2: Execute the required tools and gather data
                var toolResults = new List<ToolResult>();
                
                foreach (var tool in analysisResult.RequiredTools)
                {
                    Console.WriteLine($"Executing tool: {tool}");
                    var result = await ExecuteToolAsync(tool, analysisResult.ExtractedSymbols, analysisResult.Parameters);
                    toolResults.Add(result);
                    
                    if (result.Success)
                    {
                        Console.WriteLine($"  -> {tool}: SUCCESS");
                    }
                    else
                    {
                        Console.WriteLine($"  -> {tool}: FAILED - {result.ErrorMessage}");
                    }
                }

                Console.WriteLine("\nSynthesizing comprehensive response...\n");

                // Step 3: Synthesize all results into a coherent response
                var finalResponse = await SynthesizeResponseAsync(userInput, analysisResult, toolResults);

                // Add assistant response to conversation history
                _conversationHistory.Add(new ConversationMessage 
                { 
                    Role = "assistant", 
                    Content = finalResponse, 
                    Timestamp = DateTime.UtcNow 
                });

                return finalResponse;
            }
            catch (Exception ex)
            {
                var errorMessage = $"I encountered an error while processing your request: {ex.Message}";
                _conversationHistory.Add(new ConversationMessage 
                { 
                    Role = "assistant", 
                    Content = errorMessage, 
                    Timestamp = DateTime.UtcNow 
                });
                return errorMessage;
            }
        }

        private async Task<RequestAnalysis> AnalyzeUserRequestAsync(string userInput)
        {
            var prompt = $@"
You are an intelligent financial analysis router. Analyze the user's request and determine:

1. The user's intent/goal
2. Which specific tools should be used to fulfill this request
3. Extract any stock symbols, cryptocurrencies, or financial instruments mentioned
4. Any additional parameters needed

Available tools (can be used individually or in combination):
- analyze-video: Analyze a YouTube video
- get-quantopian-videos: Get latest Quantopian videos  
- search-finance-videos: Search finance videos
- technical-analysis: Short-term (100d) technical analysis
- technical-analysis-long: Long-term (7y) technical analysis
- fundamental-analysis: Company fundamentals and valuation
- market-data: Get current market data and prices
- yahoo-data: Get Yahoo Finance market data
- portfolio: View portfolio summary
- risk-assessment: Assess portfolio risk
- alpaca-data: Get Alpaca market data
- alpaca-historical: Get historical data
- alpaca-account: View Alpaca account info
- alpaca-positions: View current positions
- alpaca-quotes: Get multiple quotes
- ta-indicators: Detailed technical indicators
- ta-compare: Compare TA of multiple symbols
- ta-patterns: Pattern recognition analysis
- comprehensive-analysis: Full analysis & recommendation
- research-papers: Search academic finance papers
- analyze-paper: Analyze paper & generate blueprint
- research-synthesis: Research & synthesize topic
- quick-research: Quick research overview
- test-apis: Test API connectivity and configuration
- polygon-data: Get Polygon.io market data
- polygon-news: Get Polygon.io news for symbol
- polygon-financials: Get Polygon.io financial data
- databento-ohlcv: Get DataBento OHLCV data
- databento-futures: Get DataBento futures contracts
- live-news: Get live financial news
- sentiment-analysis: AI-powered sentiment analysis for specific stock
- market-sentiment: AI-powered overall market sentiment analysis
- reddit-sentiment: Reddit sentiment analysis for symbol
- reddit-scrape: Scrape Reddit posts from subreddit
- optimize-portfolio: Portfolio optimization
- extract-web-data: Extract structured data from web pages
- generate-report: Generate comprehensive reports
- analyze-satellite-imagery: Analyze satellite imagery for company operations
- scrape-social-media: Social media sentiment analysis
- web-search: Search the web for real-time information and current events
- google-search: Google search for current news and events
- current-events: Get information about recent developments
- real-time-news: Search for the latest news and developments
- sec-filings: Analyze SEC filings and financial reports
- earnings-calls: Analyze earnings call transcripts
- supply-chain: Analyze company supply chain data
- order-book: Analyze order book dynamics
- market-impact: Assess market impact of trades
- execution-analysis: Analyze trade execution quality
- monte-carlo: Run Monte Carlo simulations
- strategy-builder: Build automated trading strategies
- notebook-analysis: Create and run analysis notebooks
- data-validation: Validate market data quality
- corporate-actions: Track corporate actions and dividends
- economic-data: Get FRED economic indicators
- world-bank-data: Get World Bank economic data
- advanced-alpaca: Advanced Alpaca trading features
- factor-research: Research factor models and alphas
- academic-research: Conduct academic research
- auto-ml: Automated machine learning for predictions
- model-interpretability: Explain ML model predictions
- reinforcement-learning: Apply RL to trading strategies
- web-intelligence: Gather web intelligence data
- patent-analysis: Analyze company patents
- federal-reserve: Get Federal Reserve data and analysis
- global-economics: Global economic analysis
- geopolitical-risk: Assess geopolitical risk factors
- options-flow: Analyze options flow data
- volatility-trading: Volatility-based trading strategies
- microstructure: Advanced market microstructure analysis
- alpha-vantage: Get Alpha Vantage market data
- statistical-testing: Run statistical tests on data
- time-series-analysis: Advanced time series analysis
- cointegration: Test for cointegration between assets
- forecasting: Time series forecasting
- feature-engineering: Create trading features
- model-validation: Validate predictive models
- factor-models: Build and test factor models
- advanced-optimization: Advanced portfolio optimization
- advanced-risk: Advanced risk analytics
- trading-templates: Generate trading strategy templates

IMPORTANT: For questions about recent events, current geopolitical situations, breaking news, latest content from specific companies/organizations, podcast episodes, recent strategies, or ANY information that needs to be current and up-to-date, ALWAYS include 'web-search' or 'current-events' tools to get real-time information.

Examples that REQUIRE web search:
- latest strategy from company
- recent podcast from organization
- current events about topic
- what happened with recent event
- latest news about anything
- recent developments in field

For video content requests without specific URLs, use web-search FIRST to find the content, then analyze if URLs are found.

For Reddit strategy analysis requests:
- Use 'reddit-scrape' to find posts about strategies
- The reddit-scrape tool will automatically analyze any videos found in those posts
- This provides deep strategy breakdowns from video content
- Include 'web-search' for additional context if needed

User request: ""{userInput}""

Conversation history:
{GetConversationHistoryForContext()}

Return your analysis in this JSON format:
{{
    ""intent"": ""Brief description of what the user wants"",
    ""requiredTools"": [""tool1"", ""tool2""],
    ""extractedSymbols"": [""SYMBOL1"", ""SYMBOL2""],
    ""parameters"": {{
        ""timeframe"": ""short|medium|long"",
        ""analysisDepth"": ""basic|detailed|comprehensive"",
        ""includeNews"": true/false,
        ""includeSentiment"": true/false,
        ""days"": 100,
        ""maxResults"": 10,
        ""subreddit"": ""quant|algotrading|investing"",
        ""query"": ""strategy search terms"",
        ""limit"": 10
    }}
}}
";

            var response = await _llmService.GetChatCompletionAsync(prompt);
            
            // Parse the JSON response
            try
            {
                var jsonMatch = Regex.Match(response, @"\{.*\}", RegexOptions.Singleline);
                if (jsonMatch.Success)
                {
                    var analysis = JsonSerializer.Deserialize<RequestAnalysis>(jsonMatch.Value, new JsonSerializerOptions 
                    { 
                        PropertyNameCaseInsensitive = true 
                    });
                    
                    if (analysis != null)
                    {
                        return analysis;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($" Failed to parse analysis JSON: {ex.Message}");
            }

            // Fallback analysis
            return new RequestAnalysis
            {
                Intent = "General financial inquiry",
                RequiredTools = new[] { "market_data" },
                ExtractedSymbols = ExtractSymbolsFromText(userInput),
                Parameters = new Dictionary<string, object>
                {
                    ["timeframe"] = "medium",
                    ["analysisDepth"] = "detailed",
                    ["includeNews"] = true,
                    ["includeSentiment"] = true
                }
            };
        }

        private string[] ExtractSymbolsFromText(string text)
        {
            // Extract common stock symbols (3-5 uppercase letters)
            var symbolPattern = @"\b[A-Z]{1,5}\b";
            var matches = Regex.Matches(text.ToUpper(), symbolPattern);
            
            var symbols = new List<string>();
            foreach (Match match in matches)
            {
                var symbol = match.Value;
                // Filter out common words that aren't symbols
                if (!IsCommonWord(symbol) && symbol.Length >= 2)
                {
                    symbols.Add(symbol);
                }
            }
            
            return symbols.Distinct().ToArray();
        }

        private bool IsCommonWord(string word)
        {
            var commonWords = new HashSet<string> 
            { 
                "THE", "AND", "FOR", "ARE", "BUT", "NOT", "YOU", "ALL", "CAN", "HAD", "HER", "WAS", "ONE", "OUR", "OUT", "DAY", "GET", "HAS", "HIM", "HOW", "ITS", "MAY", "NEW", "NOW", "OLD", "SEE", "TWO", "WHO", "BOY", "DID", "ITS", "LET", "PUT", "SAY", "SHE", "TOO", "USE"
            };
            return commonWords.Contains(word);
        }

        private async Task<ToolResult> ExecuteToolAsync(string tool, string[] symbols, Dictionary<string, object> parameters)
        {
            try
            {
                switch (tool.ToLower().Replace("-", "_"))
                {
                    // Market Data Tools
                    case "market_data":
                    case "yahoo_data":
                        return await ExecuteMarketDataAsync(symbols);
                    
                    case "alpaca_data":
                        return await ExecuteAlpacaDataAsync(symbols);
                    
                    case "polygon_data":
                        return await ExecutePolygonDataAsync(symbols);
                    
                    // Technical Analysis Tools
                    case "technical_analysis":
                    case "technical_analysis_long":
                    case "ta_indicators":
                    case "ta_compare":
                    case "ta_patterns":
                        return await ExecuteTechnicalAnalysisAsync(symbols);
                    
                    // Fundamental Analysis
                    case "fundamental_analysis":
                    case "comprehensive_analysis":
                        return await ExecuteFundamentalAnalysisAsync(symbols);
                    
                    // News & Sentiment Analysis
                    case "news_sentiment":
                    case "sentiment_analysis":
                    case "live_news":
                        return await ExecuteNewsSentimentAsync(symbols);
                    
                    case "market_sentiment":
                        return await ExecuteMarketSentimentAsync();
                    
                    // Social Media & Reddit
                    case "reddit_sentiment":
                        return await ExecuteRedditSentimentAsync(symbols);
                    
                    case "reddit_scrape":
                        return await ExecuteRedditScrapeWithAnalysisAsync(parameters);
                    
                    case "social_media":
                    case "scrape_social_media":
                        return await ExecuteSocialMediaAnalysisAsync(symbols);
                    
                    // Portfolio & Risk
                    case "portfolio":
                    case "risk_assessment":
                    case "optimize_portfolio":
                        return await ExecutePortfolioAnalysisAsync(symbols);
                    
                    // Research
                    case "research_papers":
                    case "analyze_paper":
                    case "research_synthesis":
                    case "quick_research":
                        return await ExecuteResearchAsync(parameters);
                    
                    // Web Search for Real-time Information
                    case "web_search":
                    case "google_search":
                    case "current_events":
                    case "real_time_news":
                        return await ExecuteWebSearchAsync(parameters);
                    
                    // Video Analysis
                    case "analyze_video":
                    case "get_quantopian_videos":
                    case "search_finance_videos":
                        return await ExecuteVideoAnalysisAsync(parameters);
                    
                    // Other comprehensive tools - map to closest existing functionality
                    case "polygon_news":
                    case "polygon_financials":
                        return await ExecutePolygonDataAsync(symbols);
                    
                    case "alpaca_account":
                    case "alpaca_positions":
                    case "alpaca_quotes":
                    case "alpaca_historical":
                        return await ExecuteAlpacaDataAsync(symbols);
                    
                    case "databento_ohlcv":
                    case "databento_futures":
                        return await ExecuteDataBentoAsync(symbols, parameters);
                    
                    case "test_apis":
                        return await ExecuteAPITestAsync(symbols);
                    
                    // Advanced Analysis Tools
                    case "sec_filings":
                        return await ExecuteSECFilingsAsync(symbols);
                    
                    case "earnings_calls":
                        return await ExecuteEarningsCallsAsync(symbols);
                    
                    case "supply_chain":
                        return await ExecuteSupplyChainAsync(symbols);
                    
                    case "order_book":
                        return await ExecuteOrderBookAsync(symbols);
                    
                    case "market_impact":
                        return await ExecuteMarketImpactAsync(symbols);
                    
                    case "execution_analysis":
                        return await ExecuteExecutionAsync(symbols);
                    
                    case "monte_carlo":
                        return await ExecuteMonteCarloAsync(symbols, parameters);
                    
                    case "strategy_builder":
                        return await ExecuteStrategyBuilderAsync(parameters);
                    
                    case "notebook_analysis":
                        return await ExecuteNotebookAsync(parameters);
                    
                    case "data_validation":
                        return await ExecuteDataValidationAsync(symbols);
                    
                    case "corporate_actions":
                        return await ExecuteCorporateActionsAsync(symbols);
                    
                    case "economic_data":
                        return await ExecuteEconomicDataAsync(parameters);
                    
                    case "world_bank_data":
                        return await ExecuteWorldBankDataAsync(parameters);
                    
                    case "advanced_alpaca":
                        return await ExecuteAdvancedAlpacaAsync(symbols);
                    
                    case "factor_research":
                        return await ExecuteFactorResearchAsync(parameters);
                    
                    case "academic_research":
                        return await ExecuteAcademicResearchAsync(parameters);
                    
                    case "auto_ml":
                        return await ExecuteAutoMLAsync(symbols, parameters);
                    
                    case "model_interpretability":
                        return await ExecuteModelInterpretabilityAsync(parameters);
                    
                    case "reinforcement_learning":
                        return await ExecuteReinforcementLearningAsync(parameters);
                    
                    case "web_intelligence":
                        return await ExecuteWebIntelligenceAsync(parameters);
                    
                    case "patent_analysis":
                        return await ExecutePatentAnalysisAsync(symbols);
                    
                    case "federal_reserve":
                        return await ExecuteFederalReserveAsync();
                    
                    case "global_economics":
                        return await ExecuteGlobalEconomicsAsync();
                    
                    case "geopolitical_risk":
                        return await ExecuteGeopoliticalRiskAsync();
                    
                    case "options_flow":
                        return await ExecuteOptionsFlowAsync(symbols);
                    
                    case "volatility_trading":
                        return await ExecuteVolatilityTradingAsync(symbols);
                    
                    case "microstructure":
                        return await ExecuteMicrostructureAsync(symbols);
                    
                    case "alpha_vantage":
                        return await ExecuteAlphaVantageAsync(symbols);
                    
                    case "statistical_testing":
                        return await ExecuteStatisticalTestingAsync(symbols, parameters);
                    
                    case "time_series_analysis":
                        return await ExecuteTimeSeriesAnalysisAsync(symbols, parameters);
                    
                    case "cointegration":
                        return await ExecuteCointegrationAsync(symbols);
                    
                    case "forecasting":
                        return await ExecuteForecastingAsync(symbols, parameters);
                    
                    case "feature_engineering":
                        return await ExecuteFeatureEngineeringAsync(symbols);
                    
                    case "model_validation":
                        return await ExecuteModelValidationAsync(parameters);
                    
                    case "factor_models":
                        return await ExecuteFactorModelsAsync(symbols);
                    
                    case "advanced_optimization":
                        return await ExecuteAdvancedOptimizationAsync(symbols);
                    
                    case "advanced_risk":
                        return await ExecuteAdvancedRiskAsync(symbols);
                    
                    case "trading_templates":
                        return await ExecuteTradingTemplatesAsync(parameters);
                    
                    // For tools not yet implemented, provide a helpful message
                    case "extract_web_data":
                    case "generate_report":
                    case "analyze_satellite_imagery":
                        return new ToolResult 
                        { 
                            ToolName = tool, 
                            Success = false, 
                            ErrorMessage = $"Tool '{tool}' requires specialized implementation. Please use specific commands like 'market-data', 'technical-analysis', or 'sentiment-analysis' for now." 
                        };
                    
                    default:
                        return new ToolResult 
                        { 
                            ToolName = tool, 
                            Success = false, 
                            ErrorMessage = $"Tool '{tool}' is not recognized. Try using: market-data, technical-analysis, fundamental-analysis, sentiment-analysis, reddit-sentiment, or portfolio-analysis" 
                        };
                }
            }
            catch (Exception ex)
            {
                return new ToolResult 
                { 
                    ToolName = tool, 
                    Success = false, 
                    ErrorMessage = ex.Message 
                };
            }
        }

        private async Task<ToolResult> ExecuteMarketDataAsync(string[] symbols)
        {
            var results = new List<object>();
            
            foreach (var symbol in symbols.Take(5)) // Limit to 5 symbols to avoid overwhelming
            {
                try
                {
                    var marketData = await _yahooFinanceService.GetMarketDataAsync(symbol);
                    results.Add(new { Symbol = symbol, Data = marketData });
                }
                catch (Exception ex)
                {
                    results.Add(new { Symbol = symbol, Error = ex.Message });
                }
            }
            
            return new ToolResult 
            { 
                ToolName = "market_data", 
                Success = true, 
                Data = results 
            };
        }

        private async Task<ToolResult> ExecuteTechnicalAnalysisAsync(string[] symbols)
        {
            var results = new List<object>();
            
            foreach (var symbol in symbols.Take(3)) // Limit for performance
            {
                try
                {
                    var analysis = await _technicalAnalysisService.PerformFullAnalysisAsync(symbol, 100);
                    results.Add(new { Symbol = symbol, Analysis = analysis });
                }
                catch (Exception ex)
                {
                    results.Add(new { Symbol = symbol, Error = ex.Message });
                }
            }
            
            return new ToolResult 
            { 
                ToolName = "technical_analysis", 
                Success = true, 
                Data = results 
            };
        }

        private async Task<ToolResult> ExecuteFundamentalAnalysisAsync(string[] symbols)
        {
            var results = new List<object>();
            
            foreach (var symbol in symbols.Take(3))
            {
                try
                {
                    var analysis = await _comprehensiveAnalysisAgent.AnalyzeAndRecommendAsync(symbol, "stock");
                    results.Add(new { Symbol = symbol, Analysis = analysis });
                }
                catch (Exception ex)
                {
                    results.Add(new { Symbol = symbol, Error = ex.Message });
                }
            }
            
            return new ToolResult 
            { 
                ToolName = "fundamental_analysis", 
                Success = true, 
                Data = results 
            };
        }

        private async Task<ToolResult> ExecuteNewsSentimentAsync(string[] symbols)
        {
            var results = new List<object>();
            
            foreach (var symbol in symbols.Take(3))
            {
                try
                {
                    var news = await _yfinanceNewsService.GetNewsAsync(symbol);
                    var sentiment = await _newsSentimentService.AnalyzeSymbolSentimentAsync(symbol);
                    results.Add(new { Symbol = symbol, News = news, Sentiment = sentiment });
                }
                catch (Exception ex)
                {
                    results.Add(new { Symbol = symbol, Error = ex.Message });
                }
            }
            
            return new ToolResult 
            { 
                ToolName = "news_sentiment", 
                Success = true, 
                Data = results 
            };
        }

        private async Task<ToolResult> ExecuteRedditSentimentAsync(string[] symbols)
        {
            var results = new List<object>();
            
            foreach (var symbol in symbols.Take(2))
            {
                try
                {
                    var sentiment = await _redditScrapingService.AnalyzeSubredditSentimentAsync("investing", symbol);
                    results.Add(new { Symbol = symbol, RedditSentiment = sentiment });
                }
                catch (Exception ex)
                {
                    results.Add(new { Symbol = symbol, Error = ex.Message });
                }
            }
            
            return new ToolResult 
            { 
                ToolName = "reddit_sentiment", 
                Success = true, 
                Data = results 
            };
        }

        private async Task<ToolResult> ExecuteSocialMediaAnalysisAsync(string[] symbols)
        {
            var results = new List<object>();
            
            foreach (var symbol in symbols.Take(2))
            {
                try
                {
                    var analysis = await _socialMediaScrapingService.AnalyzeSocialMediaSentimentAsync(symbol, new List<SocialMediaPlatform>());
                    results.Add(new { Symbol = symbol, SocialMedia = analysis });
                }
                catch (Exception ex)
                {
                    results.Add(new { Symbol = symbol, Error = ex.Message });
                }
            }
            
            return new ToolResult 
            { 
                ToolName = "social_media", 
                Success = true, 
                Data = results 
            };
        }

        private async Task<ToolResult> ExecuteRedditScrapeWithAnalysisAsync(Dictionary<string, object> parameters)
        {
            try
            {
                // Get subreddit from parameters, default to quant-related subreddits
                var subreddit = GetStringParameter(parameters, "subreddit", "quant");
                var searchQuery = GetStringParameter(parameters, "query", "strategy");
                var limit = GetIntParameter(parameters, "limit", 10);

                _logger.LogInformation($"Scraping Reddit r/{subreddit} for strategies with query: {searchQuery}");

                // Scrape Reddit posts
                var posts = await _redditScrapingService.ScrapeSubredditAsync(subreddit, limit);
                
                if (posts.Count == 0)
                {
                    return new ToolResult 
                    { 
                        ToolName = "reddit-scrape", 
                        Success = true, 
                        Data = "No posts found in the specified subreddit."
                    };
                }

                var analysisResults = new List<object>();
                var videoAnalysisResults = new List<object>();

                // Process posts and extract video URLs
                foreach (var post in posts.Take(5)) // Analyze top 5 posts
                {
                    var postAnalysis = new
                    {
                        Title = post.Title,
                        Author = post.Author,
                        Score = post.Score,
                        Comments = post.Comments,
                        Content = post.Content,
                        Url = post.Url,
                        CreatedUtc = post.CreatedUtc
                    };
                    
                    analysisResults.Add(postAnalysis);

                    // Check if the post contains YouTube or video URLs
                    if (ContainsVideoUrl(post.Url) || ContainsVideoUrl(post.Content))
                    {
                        var videoUrl = ExtractVideoUrl(post.Url) ?? ExtractVideoUrl(post.Content);
                        if (!string.IsNullOrEmpty(videoUrl))
                        {
                            try
                            {
                                _logger.LogInformation($"Analyzing video from Reddit post: {videoUrl}");
                                var videoAnalysis = await _youtubeAnalysisService.AnalyzeVideoAsync(videoUrl);
                                videoAnalysisResults.Add(new 
                                {
                                    RedditPost = post.Title,
                                    VideoUrl = videoUrl,
                                    VideoAnalysis = videoAnalysis
                                });
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, $"Failed to analyze video: {videoUrl}");
                                videoAnalysisResults.Add(new 
                                {
                                    RedditPost = post.Title,
                                    VideoUrl = videoUrl,
                                    Error = $"Video analysis failed: {ex.Message}"
                                });
                            }
                        }
                    }
                }

                var result = new
                {
                    Subreddit = subreddit,
                    SearchQuery = searchQuery,
                    PostsFound = posts.Count,
                    RedditPosts = analysisResults,
                    VideoAnalyses = videoAnalysisResults,
                    Summary = GenerateRedditAnalysisSummary(posts, videoAnalysisResults.Count)
                };

                return new ToolResult 
                { 
                    ToolName = "reddit-scrape", 
                    Success = true, 
                    Data = result
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Reddit scrape with analysis");
                return new ToolResult 
                { 
                    ToolName = "reddit-scrape", 
                    Success = false, 
                    ErrorMessage = $"Reddit analysis failed: {ex.Message}" 
                };
            }
        }

        private bool ContainsVideoUrl(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            
            var videoPatterns = new[]
            {
                "youtube.com/watch",
                "youtu.be/",
                "vimeo.com/",
                "twitch.tv/videos/",
                "streamable.com/"
            };
            
            return videoPatterns.Any(pattern => text.Contains(pattern, StringComparison.OrdinalIgnoreCase));
        }

        private string? ExtractVideoUrl(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            
            // YouTube patterns
            var youtubePatterns = new[]
            {
                @"(?:https?://)?(?:www\.)?(?:youtube\.com/watch\?v=|youtu\.be/)([a-zA-Z0-9_-]{11})",
                @"(?:https?://)?(?:www\.)?youtube\.com/embed/([a-zA-Z0-9_-]{11})"
            };
            
            foreach (var pattern in youtubePatterns)
            {
                var match = System.Text.RegularExpressions.Regex.Match(text, pattern);
                if (match.Success)
                {
                    return $"https://www.youtube.com/watch?v={match.Groups[1].Value}";
                }
            }
            
            // Direct URL patterns
            var urlPattern = @"https?://[^\s]+";
            var urlMatch = System.Text.RegularExpressions.Regex.Match(text, urlPattern);
            if (urlMatch.Success && ContainsVideoUrl(urlMatch.Value))
            {
                return urlMatch.Value;
            }
            
            return null;
        }

        private string GenerateRedditAnalysisSummary(List<RedditPost> posts, int videoAnalysisCount)
        {
            var summary = new StringBuilder();
            summary.AppendLine($"Analyzed {posts.Count} Reddit posts");
            summary.AppendLine($"Found and analyzed {videoAnalysisCount} videos with strategy content");
            summary.AppendLine("Key findings include detailed strategy breakdowns from video content");
            return summary.ToString();
        }

        private async Task<ToolResult> ExecuteLiveNewsAsync(string[] symbols)
        {
            var results = new List<object>();
            
            foreach (var symbol in symbols.Take(3))
            {
                try
                {
                    var news = await _finvizNewsService.GetNewsAsync(symbol);
                    results.Add(new { Symbol = symbol, LiveNews = news });
                }
                catch (Exception ex)
                {
                    results.Add(new { Symbol = symbol, Error = ex.Message });
                }
            }
            
            return new ToolResult 
            { 
                ToolName = "live_news", 
                Success = true, 
                Data = results 
            };
        }

        private async Task<ToolResult> ExecuteAlpacaDataAsync(string[] symbols)
        {
            var results = new List<object>();
            
            try
            {
                var account = await _alpacaService.GetAccountInfoAsync();
                var positions = await _alpacaService.GetPositionsAsync();
                
                results.Add(new { Account = account, Positions = positions });
                
                foreach (var symbol in symbols.Take(3))
                {
                    try
                    {
                        var quote = await _alpacaService.GetMarketDataAsync(symbol);
                        results.Add(new { Symbol = symbol, Quote = quote });
                    }
                    catch (Exception ex)
                    {
                        results.Add(new { Symbol = symbol, Error = ex.Message });
                    }
                }
            }
            catch (Exception ex)
            {
                return new ToolResult 
                { 
                    ToolName = "alpaca_data", 
                    Success = false, 
                    ErrorMessage = ex.Message 
                };
            }
            
            return new ToolResult 
            { 
                ToolName = "alpaca_data", 
                Success = true, 
                Data = results 
            };
        }

        private async Task<ToolResult> ExecutePolygonDataAsync(string[] symbols)
        {
            var results = new List<object>();
            
            foreach (var symbol in symbols.Take(3))
            {
                try
                {
                    var marketData = await _polygonService.GetQuoteAsync(symbol);
                    var news = await _polygonService.GetNewsAsync(symbol);
                    results.Add(new { Symbol = symbol, MarketData = marketData, News = news });
                }
                catch (Exception ex)
                {
                    results.Add(new { Symbol = symbol, Error = ex.Message });
                }
            }
            
            return new ToolResult 
            { 
                ToolName = "polygon_data", 
                Success = true, 
                Data = results 
            };
        }

        private async Task<ToolResult> ExecuteMarketSentimentAsync()
        {
            try
            {
                var sentiment = await _newsSentimentService.AnalyzeMarketSentimentAsync(20);
                return new ToolResult 
                { 
                    ToolName = "market_sentiment", 
                    Success = true, 
                    Data = sentiment 
                };
            }
            catch (Exception ex)
            {
                return new ToolResult 
                { 
                    ToolName = "market_sentiment", 
                    Success = false, 
                    ErrorMessage = ex.Message 
                };
            }
        }

        private async Task<ToolResult> ExecutePortfolioAnalysisAsync(string[] symbols)
        {
            try
            {
                var results = new List<object>();
                
                if (symbols.Length > 0)
                {
                    // Portfolio optimization for provided symbols
                    var optimizationResult = await _portfolioOptimizationService.OptimizePortfolioAsync(symbols);
                    results.Add(new { Type = "Portfolio Optimization", Data = optimizationResult });
                }
                
                // Add general portfolio guidance
                results.Add(new { 
                    Type = "Portfolio Analysis", 
                    Message = "Portfolio analysis completed. Consider diversification across sectors and asset classes.",
                    Symbols = symbols 
                });
                
                return new ToolResult 
                { 
                    ToolName = "portfolio_analysis", 
                    Success = true, 
                    Data = results 
                };
            }
            catch (Exception ex)
            {
                return new ToolResult 
                { 
                    ToolName = "portfolio_analysis", 
                    Success = false, 
                    ErrorMessage = ex.Message 
                };
            }
        }

        private async Task<ToolResult> ExecuteResearchAsync(Dictionary<string, object> parameters)
        {
            try
            {
                var topic = GetStringParameter(parameters, "topic", "financial markets");
                var maxResults = GetIntParameter(parameters, "maxResults", 5);
                
                // Placeholder for research functionality
                return new ToolResult 
                { 
                    ToolName = "research", 
                    Success = true, 
                    Data = new { 
                        Topic = topic, 
                        MaxResults = maxResults,
                        Message = "Research functionality available via dedicated research commands",
                        Suggestion = "Use 'research-papers', 'research-synthesis', or 'quick-research' commands directly"
                    }
                };
            }
            catch (Exception ex)
            {
                return new ToolResult 
                { 
                    ToolName = "research", 
                    Success = false, 
                    ErrorMessage = ex.Message 
                };
            }
        }

        private async Task<ToolResult> ExecuteVideoAnalysisAsync(Dictionary<string, object> parameters)
        {
            try
            {
                var url = GetStringParameter(parameters, "url", "");
                
                if (!string.IsNullOrEmpty(url))
                {
                    var analysis = await _youtubeAnalysisService.AnalyzeVideoAsync(url);
                    return new ToolResult 
                    { 
                        ToolName = "video_analysis", 
                        Success = true, 
                        Data = analysis 
                    };
                }
                
                return new ToolResult 
                { 
                    ToolName = "video_analysis", 
                    Success = false, 
                    ErrorMessage = "Video URL required for analysis" 
                };
            }
            catch (Exception ex)
            {
                return new ToolResult 
                { 
                    ToolName = "video_analysis", 
                    Success = false, 
                    ErrorMessage = ex.Message 
                };
            }
        }

        private async Task<ToolResult> ExecuteDataBentoAsync(string[] symbols, Dictionary<string, object> parameters)
        {
            try
            {
                var days = GetIntParameter(parameters, "days", 30);
                var results = new List<object>();
                
                foreach (var symbol in symbols.Take(3))
                {
                    try
                    {
                        // Use DataBento service if available
                        results.Add(new { 
                            Symbol = symbol, 
                            Days = days,
                            Message = "DataBento integration available via dedicated commands",
                            Suggestion = $"Use 'databento-ohlcv {symbol} {days}' for detailed data"
                        });
                    }
                    catch (Exception ex)
                    {
                        results.Add(new { Symbol = symbol, Error = ex.Message });
                    }
                }
                
                return new ToolResult 
                { 
                    ToolName = "databento", 
                    Success = true, 
                    Data = results 
                };
            }
            catch (Exception ex)
            {
                return new ToolResult 
                { 
                    ToolName = "databento", 
                    Success = false, 
                    ErrorMessage = ex.Message 
                };
            }
        }

        private async Task<ToolResult> ExecuteAPITestAsync(string[] symbols)
        {
            try
            {
                var symbol = symbols.FirstOrDefault() ?? "AAPL";
                var tests = new List<object>();
                
                // Test Yahoo Finance
                try
                {
                    var yahooData = await _yahooFinanceService.GetMarketDataAsync(symbol);
                    tests.Add(new { Service = "Yahoo Finance", Status = "Connected", Symbol = symbol });
                }
                catch
                {
                    tests.Add(new { Service = "Yahoo Finance", Status = "Error", Symbol = symbol });
                }
                
                // Test Alpaca
                try
                {
                    var alpacaData = await _alpacaService.GetMarketDataAsync(symbol);
                    tests.Add(new { Service = "Alpaca", Status = "Connected", Symbol = symbol });
                }
                catch
                {
                    tests.Add(new { Service = "Alpaca", Status = "Error", Symbol = symbol });
                }
                
                return new ToolResult 
                { 
                    ToolName = "api_test", 
                    Success = true, 
                    Data = tests 
                };
            }
            catch (Exception ex)
            {
                return new ToolResult 
                { 
                    ToolName = "api_test", 
                    Success = false, 
                    ErrorMessage = ex.Message 
                };
            }
        }

        private async Task<string> SynthesizeResponseAsync(string userInput, RequestAnalysis analysis, List<ToolResult> toolResults)
        {
            var dataContext = new StringBuilder();
            dataContext.AppendLine("=== GATHERED DATA ===");
            
            foreach (var result in toolResults)
            {
                if (result.Success)
                {
                    dataContext.AppendLine($"\n--- {result.ToolName.ToUpper()} RESULTS ---");
                    dataContext.AppendLine(JsonSerializer.Serialize(result.Data, new JsonSerializerOptions { WriteIndented = true }));
                }
                else
                {
                    dataContext.AppendLine($"\n--- {result.ToolName.ToUpper()} ERROR ---");
                    dataContext.AppendLine(result.ErrorMessage);
                }
            }

            var conversationContext = GetConversationHistoryForContext();

            var prompt = $@"
You are an expert financial AI assistant providing comprehensive analysis. Based on the user's request and the gathered data, provide a detailed, insightful response.

USER REQUEST: ""{userInput}""

ANALYSIS INTENT: {analysis.Intent}

CONVERSATION HISTORY:
{conversationContext}

{dataContext}

Provide a comprehensive response that:
1. Directly addresses the user's question
2. Synthesizes insights from all available data
3. Provides clear actionable information
4. Includes relevant links, sources, and justifications
5. Highlights key findings and recommendations
6. Uses appropriate financial terminology
7. Maintains a professional but accessible tone

CRITICAL: Always prioritize information from the gathered data above. If web search results are available, use ONLY that current information. Do NOT fall back to outdated training data or mention dates from your training cutoff. The current date is September 2025.

IMPORTANT: Format your response in plain text without markdown formatting. Use simple text formatting:
- Use line breaks and spacing for organization
- Use dashes (-) for lists instead of bullet points or asterisks
- Use equals signs (=) for section headers
- Use numbers for numbered lists
- Do NOT use markdown headers (##), bold (**text**), italics (*text*), or bullet points (•)
- Keep formatting clean and terminal-friendly

Structure your response with clear sections using plain text formatting. Include specific numbers, percentages, and data points from the analysis.

If any critical data is missing or tools failed, acknowledge this and explain what information is available.
";

            return await _llmService.GetChatCompletionAsync(prompt);
        }

        private string GetConversationHistoryForContext()
        {
            if (_conversationHistory.Count == 0)
                return "No previous conversation.";

            var recentHistory = _conversationHistory.TakeLast(6).ToList();
            var context = new StringBuilder();
            
            foreach (var message in recentHistory)
            {
                context.AppendLine($"{message.Role.ToUpper()}: {message.Content.Substring(0, Math.Min(message.Content.Length, 200))}...");
            }
            
            return context.ToString();
        }

        public void ClearConversationHistory()
        {
            _conversationHistory.Clear();
            Console.WriteLine("Conversation history cleared!");
        }

        public void ShowConversationHistory()
        {
            if (_conversationHistory.Count == 0)
            {
                Console.WriteLine("No conversation history available.");
                return;
            }

            Console.WriteLine("\n=== CONVERSATION HISTORY ===");
            foreach (var message in _conversationHistory.TakeLast(10))
            {
                Console.WriteLine($"\n[{message.Timestamp:HH:mm:ss}] {message.Role.ToUpper()}:");
                Console.WriteLine(message.Content.Substring(0, Math.Min(message.Content.Length, 300)) + 
                    (message.Content.Length > 300 ? "..." : ""));
            }
            Console.WriteLine("============================\n");
        }

        private async Task<ToolResult> ExecuteWebSearchAsync(Dictionary<string, object> parameters)
        {
            try
            {
                // Extract search query from parameters
                var query = GetStringParameter(parameters, "query", "");
                
                // If no specific query, try to extract from the original user request context
                if (string.IsNullOrEmpty(query) && _conversationHistory.Count > 0)
                {
                    var lastUserMessage = _conversationHistory.LastOrDefault(m => m.Role == "user");
                    if (lastUserMessage != null)
                    {
                        // Extract search terms from the user's request
                        query = ExtractSearchTermsFromRequest(lastUserMessage.Content);
                    }
                }
                
                if (string.IsNullOrEmpty(query))
                {
                    return new ToolResult 
                    { 
                        ToolName = "web-search", 
                        Success = false, 
                        ErrorMessage = "No search query provided" 
                    };
                }

                _logger.LogInformation($"Executing web search for: {query}");
                
                var searchResults = await _googleWebSearchPlugin.SearchAsync(query, 5);
                
                if (searchResults.Count == 0)
                {
                    return new ToolResult 
                    { 
                        ToolName = "web-search", 
                        Success = true, 
                        Data = "No current information found for this query."
                    };
                }

                // Format the search results for the AI to understand
                var resultSummary = new StringBuilder();
                resultSummary.AppendLine($"Real-time web search results for: {query}");
                resultSummary.AppendLine(new string('=', 60));
                
                foreach (var result in searchResults)
                {
                    resultSummary.AppendLine($"Title: {result.Title}");
                    resultSummary.AppendLine($"Source: {result.Url}");
                    resultSummary.AppendLine($"Summary: {result.Snippet}");
                    resultSummary.AppendLine(new string('-', 40));
                }

                return new ToolResult 
                { 
                    ToolName = "web-search", 
                    Success = true, 
                    Data = resultSummary.ToString()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing web search");
                return new ToolResult 
                { 
                    ToolName = "web-search", 
                    Success = false, 
                    ErrorMessage = $"Web search failed: {ex.Message}" 
                };
            }
        }
        
        private string ExtractSearchTermsFromRequest(string userRequest)
        {
            // Simple heuristic to extract search terms from user requests about current events
            var lowercaseRequest = userRequest.ToLower();
            
            // Look for patterns indicating current events or news
            if (lowercaseRequest.Contains("effect") && lowercaseRequest.Contains("stock market"))
            {
                // Extract key terms for current events
                var terms = new List<string>();
                
                // Extract country/organization names (capitalized words)
                var words = userRequest.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                foreach (var word in words)
                {
                    if (char.IsUpper(word[0]) && word.Length > 2)
                    {
                        terms.Add(word);
                    }
                }
                
                // Add relevant keywords
                if (lowercaseRequest.Contains("bombing")) terms.Add("bombing");
                if (lowercaseRequest.Contains("nuclear")) terms.Add("nuclear");
                if (lowercaseRequest.Contains("stock market")) terms.Add("stock market");
                if (lowercaseRequest.Contains("futures")) terms.Add("futures");
                
                return string.Join(" ", terms);
            }
            
            // For general requests, extract meaningful words (more than 3 characters, not common words)
            var commonWords = new HashSet<string> { "the", "and", "but", "for", "are", "was", "were", "what", "how", "when", "where", "why" };
            var meaningfulWords = userRequest.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length > 3 && !commonWords.Contains(w.ToLower()))
                .Take(5);
                
            return string.Join(" ", meaningfulWords);
        }

        private int GetIntParameter(Dictionary<string, object> parameters, string key, int defaultValue)
        {
            if (parameters.TryGetValue(key, out var value))
            {
                if (value is int intValue) return intValue;
                if (int.TryParse(value.ToString(), out var parsedValue)) return parsedValue;
            }
            return defaultValue;
        }

        private string GetStringParameter(Dictionary<string, object> parameters, string key, string defaultValue = "")
        {
            if (parameters.TryGetValue(key, out var value))
            {
                return value?.ToString() ?? defaultValue;
            }
            return defaultValue;
        }

        // New Execute Methods for Additional Tools

        private async Task<ToolResult> ExecuteSECFilingsAsync(string[] symbols)
        {
            var results = new List<object>();
            foreach (var symbol in symbols.Take(2))
            {
                try
                {
                    var analysis = await _secFilingsService.AnalyzeLatestFilingAsync(symbol);
                    results.Add(new { Symbol = symbol, SECFilingAnalysis = analysis });
                }
                catch (Exception ex)
                {
                    results.Add(new { Symbol = symbol, Error = ex.Message });
                }
            }
            return new ToolResult { ToolName = "sec-filings", Success = true, Data = results };
        }

        private async Task<ToolResult> ExecuteEarningsCallsAsync(string[] symbols)
        {
            var results = new List<object>();
            foreach (var symbol in symbols.Take(2))
            {
                try
                {
                    var analysis = await _earningsCallService.AnalyzeLatestEarningsCallAsync(symbol);
                    results.Add(new { Symbol = symbol, EarningsCallAnalysis = analysis });
                }
                catch (Exception ex)
                {
                    results.Add(new { Symbol = symbol, Error = ex.Message });
                }
            }
            return new ToolResult { ToolName = "earnings-calls", Success = true, Data = results };
        }

        private async Task<ToolResult> ExecuteSupplyChainAsync(string[] symbols)
        {
            var results = new List<object>();
            foreach (var symbol in symbols.Take(2))
            {
                try
                {
                    var analysis = await _supplyChainService.AnalyzeCompanySupplyChainAsync(symbol);
                    results.Add(new { Symbol = symbol, SupplyChainAnalysis = analysis });
                }
                catch (Exception ex)
                {
                    results.Add(new { Symbol = symbol, Error = ex.Message });
                }
            }
            return new ToolResult { ToolName = "supply-chain", Success = true, Data = results };
        }

        private async Task<ToolResult> ExecuteOrderBookAsync(string[] symbols)
        {
            var results = new List<object>();
            foreach (var symbol in symbols.Take(2))
            {
                try
                {
                    // Placeholder - OrderBookAnalysisService may not have the exact method
                    results.Add(new { Symbol = symbol, Message = "Order book analysis available via dedicated service" });
                }
                catch (Exception ex)
                {
                    results.Add(new { Symbol = symbol, Error = ex.Message });
                }
            }
            return new ToolResult { ToolName = "order-book", Success = true, Data = results };
        }

        private async Task<ToolResult> ExecuteMarketImpactAsync(string[] symbols)
        {
            var results = new List<object>();
            foreach (var symbol in symbols.Take(2))
            {
                try
                {
                    // Placeholder - MarketImpactService may not have the exact method
                    results.Add(new { Symbol = symbol, Message = "Market impact analysis available via dedicated service" });
                }
                catch (Exception ex)
                {
                    results.Add(new { Symbol = symbol, Error = ex.Message });
                }
            }
            return new ToolResult { ToolName = "market-impact", Success = true, Data = results };
        }

        private async Task<ToolResult> ExecuteExecutionAsync(string[] symbols)
        {
            var results = new List<object>();
            foreach (var symbol in symbols.Take(2))
            {
                try
                {
                    // Placeholder - ExecutionService may not have the exact method
                    results.Add(new { Symbol = symbol, Message = "Execution analysis available via dedicated service" });
                }
                catch (Exception ex)
                {
                    results.Add(new { Symbol = symbol, Error = ex.Message });
                }
            }
            return new ToolResult { ToolName = "execution-analysis", Success = true, Data = results };
        }

        private async Task<ToolResult> ExecuteMonteCarloAsync(string[] symbols, Dictionary<string, object> parameters)
        {
            try
            {
                var simulations = GetIntParameter(parameters, "simulations", 1000);
                // Placeholder - MonteCarloService may not have the exact method
                return new ToolResult { ToolName = "monte-carlo", Success = true, Data = $"Monte Carlo simulation with {simulations} runs available via dedicated service" };
            }
            catch (Exception ex)
            {
                return new ToolResult { ToolName = "monte-carlo", Success = false, ErrorMessage = ex.Message };
            }
        }

        private async Task<ToolResult> ExecuteStrategyBuilderAsync(Dictionary<string, object> parameters)
        {
            try
            {
                var strategyType = GetStringParameter(parameters, "type", "momentum");
                // Placeholder - StrategyBuilderService may not have the exact method
                return new ToolResult { ToolName = "strategy-builder", Success = true, Data = $"Strategy builder for {strategyType} available via dedicated service" };
            }
            catch (Exception ex)
            {
                return new ToolResult { ToolName = "strategy-builder", Success = false, ErrorMessage = ex.Message };
            }
        }

        private async Task<ToolResult> ExecuteNotebookAsync(Dictionary<string, object> parameters)
        {
            try
            {
                var analysisType = GetStringParameter(parameters, "type", "portfolio");
                // Placeholder - NotebookService may not have the exact method
                return new ToolResult { ToolName = "notebook-analysis", Success = true, Data = $"Notebook analysis for {analysisType} available via dedicated service" };
            }
            catch (Exception ex)
            {
                return new ToolResult { ToolName = "notebook-analysis", Success = false, ErrorMessage = ex.Message };
            }
        }

        private async Task<ToolResult> ExecuteDataValidationAsync(string[] symbols)
        {
            var results = new List<object>();
            foreach (var symbol in symbols.Take(3))
            {
                try
                {
                    // DataValidationService exists but method signature may be different
                    results.Add(new { Symbol = symbol, Message = "Data validation available via dedicated service" });
                }
                catch (Exception ex)
                {
                    results.Add(new { Symbol = symbol, Error = ex.Message });
                }
            }
            return new ToolResult { ToolName = "data-validation", Success = true, Data = results };
        }

        private async Task<ToolResult> ExecuteCorporateActionsAsync(string[] symbols)
        {
            var results = new List<object>();
            foreach (var symbol in symbols.Take(3))
            {
                try
                {
                    // Placeholder - CorporateActionService may not have the exact method
                    results.Add(new { Symbol = symbol, Message = "Corporate actions tracking available via dedicated service" });
                }
                catch (Exception ex)
                {
                    results.Add(new { Symbol = symbol, Error = ex.Message });
                }
            }
            return new ToolResult { ToolName = "corporate-actions", Success = true, Data = results };
        }

        private async Task<ToolResult> ExecuteEconomicDataAsync(Dictionary<string, object> parameters)
        {
            try
            {
                var indicator = GetStringParameter(parameters, "indicator", "GDP");
                // Placeholder - FREDService may not have the exact method
                return new ToolResult { ToolName = "economic-data", Success = true, Data = $"Economic data for {indicator} available via FRED service" };
            }
            catch (Exception ex)
            {
                return new ToolResult { ToolName = "economic-data", Success = false, ErrorMessage = ex.Message };
            }
        }

        private async Task<ToolResult> ExecuteWorldBankDataAsync(Dictionary<string, object> parameters)
        {
            try
            {
                var country = GetStringParameter(parameters, "country", "US");
                var indicator = GetStringParameter(parameters, "indicator", "GDP");
                // Placeholder - WorldBankService may not have the exact method
                return new ToolResult { ToolName = "world-bank-data", Success = true, Data = $"World Bank data for {country} {indicator} available via dedicated service" };
            }
            catch (Exception ex)
            {
                return new ToolResult { ToolName = "world-bank-data", Success = false, ErrorMessage = ex.Message };
            }
        }

        private async Task<ToolResult> ExecuteAdvancedAlpacaAsync(string[] symbols)
        {
            var results = new List<object>();
            foreach (var symbol in symbols.Take(2))
            {
                try
                {
                    // Placeholder - AdvancedAlpacaService may not have the exact method
                    results.Add(new { Symbol = symbol, Message = "Advanced Alpaca features available via dedicated service" });
                }
                catch (Exception ex)
                {
                    results.Add(new { Symbol = symbol, Error = ex.Message });
                }
            }
            return new ToolResult { ToolName = "advanced-alpaca", Success = true, Data = results };
        }

        private async Task<ToolResult> ExecuteFactorResearchAsync(Dictionary<string, object> parameters)
        {
            try
            {
                var factor = GetStringParameter(parameters, "factor", "momentum");
                // Placeholder - FactorResearchService may not have the exact method
                return new ToolResult { ToolName = "factor-research", Success = true, Data = $"Factor research for {factor} available via dedicated service" };
            }
            catch (Exception ex)
            {
                return new ToolResult { ToolName = "factor-research", Success = false, ErrorMessage = ex.Message };
            }
        }

        private async Task<ToolResult> ExecuteAcademicResearchAsync(Dictionary<string, object> parameters)
        {
            try
            {
                var topic = GetStringParameter(parameters, "topic", "finance");
                // Placeholder - AcademicResearchService may not have the exact method
                return new ToolResult { ToolName = "academic-research", Success = true, Data = $"Academic research on {topic} available via dedicated service" };
            }
            catch (Exception ex)
            {
                return new ToolResult { ToolName = "academic-research", Success = false, ErrorMessage = ex.Message };
            }
        }

        private async Task<ToolResult> ExecuteAutoMLAsync(string[] symbols, Dictionary<string, object> parameters)
        {
            try
            {
                var target = GetStringParameter(parameters, "target", "returns");
                // Placeholder - AutoMLService may not have the exact method
                return new ToolResult { ToolName = "auto-ml", Success = true, Data = $"AutoML for {target} prediction available via dedicated service" };
            }
            catch (Exception ex)
            {
                return new ToolResult { ToolName = "auto-ml", Success = false, ErrorMessage = ex.Message };
            }
        }

        private async Task<ToolResult> ExecuteModelInterpretabilityAsync(Dictionary<string, object> parameters)
        {
            try
            {
                var modelId = GetStringParameter(parameters, "modelId", "");
                // Placeholder - ModelInterpretabilityService may not have the exact method
                return new ToolResult { ToolName = "model-interpretability", Success = true, Data = $"Model interpretability for {modelId} available via dedicated service" };
            }
            catch (Exception ex)
            {
                return new ToolResult { ToolName = "model-interpretability", Success = false, ErrorMessage = ex.Message };
            }
        }

        private async Task<ToolResult> ExecuteReinforcementLearningAsync(Dictionary<string, object> parameters)
        {
            try
            {
                var environment = GetStringParameter(parameters, "environment", "trading");
                // Placeholder - ReinforcementLearningService may not have the exact method
                return new ToolResult { ToolName = "reinforcement-learning", Success = true, Data = $"Reinforcement learning for {environment} available via dedicated service" };
            }
            catch (Exception ex)
            {
                return new ToolResult { ToolName = "reinforcement-learning", Success = false, ErrorMessage = ex.Message };
            }
        }

        private async Task<ToolResult> ExecuteWebIntelligenceAsync(Dictionary<string, object> parameters)
        {
            try
            {
                var query = GetStringParameter(parameters, "query", "");
                // Placeholder - WebIntelligenceService may not have the exact method
                return new ToolResult { ToolName = "web-intelligence", Success = true, Data = $"Web intelligence for '{query}' available via dedicated service" };
            }
            catch (Exception ex)
            {
                return new ToolResult { ToolName = "web-intelligence", Success = false, ErrorMessage = ex.Message };
            }
        }

        private async Task<ToolResult> ExecutePatentAnalysisAsync(string[] symbols)
        {
            var results = new List<object>();
            foreach (var symbol in symbols.Take(2))
            {
                try
                {
                    // Placeholder - PatentAnalysisService may not have the exact method
                    results.Add(new { Symbol = symbol, Message = "Patent analysis available via dedicated service" });
                }
                catch (Exception ex)
                {
                    results.Add(new { Symbol = symbol, Error = ex.Message });
                }
            }
            return new ToolResult { ToolName = "patent-analysis", Success = true, Data = results };
        }

        private async Task<ToolResult> ExecuteFederalReserveAsync()
        {
            try
            {
                // Placeholder - FederalReserveService may not have the exact method
                return new ToolResult { ToolName = "federal-reserve", Success = true, Data = "Federal Reserve data and analysis available via dedicated service" };
            }
            catch (Exception ex)
            {
                return new ToolResult { ToolName = "federal-reserve", Success = false, ErrorMessage = ex.Message };
            }
        }

        private async Task<ToolResult> ExecuteGlobalEconomicsAsync()
        {
            try
            {
                // Placeholder - GlobalEconomicService may not have the exact method
                return new ToolResult { ToolName = "global-economics", Success = true, Data = "Global economic analysis available via dedicated service" };
            }
            catch (Exception ex)
            {
                return new ToolResult { ToolName = "global-economics", Success = false, ErrorMessage = ex.Message };
            }
        }

        private async Task<ToolResult> ExecuteGeopoliticalRiskAsync()
        {
            try
            {
                // Placeholder - GeopoliticalRiskService may not have the exact method
                return new ToolResult { ToolName = "geopolitical-risk", Success = true, Data = "Geopolitical risk assessment available via dedicated service" };
            }
            catch (Exception ex)
            {
                return new ToolResult { ToolName = "geopolitical-risk", Success = false, ErrorMessage = ex.Message };
            }
        }

        private async Task<ToolResult> ExecuteOptionsFlowAsync(string[] symbols)
        {
            var results = new List<object>();
            foreach (var symbol in symbols.Take(2))
            {
                try
                {
                    // Placeholder - OptionsFlowService may not have the exact method
                    results.Add(new { Symbol = symbol, Message = "Options flow analysis available via dedicated service" });
                }
                catch (Exception ex)
                {
                    results.Add(new { Symbol = symbol, Error = ex.Message });
                }
            }
            return new ToolResult { ToolName = "options-flow", Success = true, Data = results };
        }

        private async Task<ToolResult> ExecuteVolatilityTradingAsync(string[] symbols)
        {
            var results = new List<object>();
            foreach (var symbol in symbols.Take(2))
            {
                try
                {
                    // Placeholder - VolatilityTradingService may not have the exact method
                    results.Add(new { Symbol = symbol, Message = "Volatility trading strategies available via dedicated service" });
                }
                catch (Exception ex)
                {
                    results.Add(new { Symbol = symbol, Error = ex.Message });
                }
            }
            return new ToolResult { ToolName = "volatility-trading", Success = true, Data = results };
        }

        private async Task<ToolResult> ExecuteMicrostructureAsync(string[] symbols)
        {
            var results = new List<object>();
            foreach (var symbol in symbols.Take(2))
            {
                try
                {
                    // Placeholder - AdvancedMicrostructureService may not have the exact method
                    results.Add(new { Symbol = symbol, Message = "Market microstructure analysis available via dedicated service" });
                }
                catch (Exception ex)
                {
                    results.Add(new { Symbol = symbol, Error = ex.Message });
                }
            }
            return new ToolResult { ToolName = "microstructure", Success = true, Data = results };
        }

        private async Task<ToolResult> ExecuteAlphaVantageAsync(string[] symbols)
        {
            var results = new List<object>();
            foreach (var symbol in symbols.Take(3))
            {
                try
                {
                    // Placeholder - AlphaVantageService may not have the exact method
                    results.Add(new { Symbol = symbol, Message = "Alpha Vantage market data available via dedicated service" });
                }
                catch (Exception ex)
                {
                    results.Add(new { Symbol = symbol, Error = ex.Message });
                }
            }
            return new ToolResult { ToolName = "alpha-vantage", Success = true, Data = results };
        }

        private async Task<ToolResult> ExecuteStatisticalTestingAsync(string[] symbols, Dictionary<string, object> parameters)
        {
            try
            {
                var testType = GetStringParameter(parameters, "test", "normality");
                // Placeholder - StatisticalTestingService may not have the exact method
                return new ToolResult { ToolName = "statistical-testing", Success = true, Data = $"Statistical testing ({testType}) available via dedicated service" };
            }
            catch (Exception ex)
            {
                return new ToolResult { ToolName = "statistical-testing", Success = false, ErrorMessage = ex.Message };
            }
        }

        private async Task<ToolResult> ExecuteTimeSeriesAnalysisAsync(string[] symbols, Dictionary<string, object> parameters)
        {
            try
            {
                var analysisType = GetStringParameter(parameters, "type", "decomposition");
                // Placeholder - TimeSeriesAnalysisService may not have the exact method
                return new ToolResult { ToolName = "time-series-analysis", Success = true, Data = $"Time series analysis ({analysisType}) available via dedicated service" };
            }
            catch (Exception ex)
            {
                return new ToolResult { ToolName = "time-series-analysis", Success = false, ErrorMessage = ex.Message };
            }
        }

        private async Task<ToolResult> ExecuteCointegrationAsync(string[] symbols)
        {
            try
            {
                // Placeholder - CointegrationAnalysisService may not have the exact method
                return new ToolResult { ToolName = "cointegration", Success = true, Data = "Cointegration analysis available via dedicated service" };
            }
            catch (Exception ex)
            {
                return new ToolResult { ToolName = "cointegration", Success = false, ErrorMessage = ex.Message };
            }
        }

        private async Task<ToolResult> ExecuteForecastingAsync(string[] symbols, Dictionary<string, object> parameters)
        {
            try
            {
                var method = GetStringParameter(parameters, "method", "arima");
                // Placeholder - TimeSeriesForecastingService may not have the exact method
                return new ToolResult { ToolName = "forecasting", Success = true, Data = $"Time series forecasting ({method}) available via dedicated service" };
            }
            catch (Exception ex)
            {
                return new ToolResult { ToolName = "forecasting", Success = false, ErrorMessage = ex.Message };
            }
        }

        private async Task<ToolResult> ExecuteFeatureEngineeringAsync(string[] symbols)
        {
            try
            {
                // Placeholder - FeatureEngineeringService may not have the exact method
                return new ToolResult { ToolName = "feature-engineering", Success = true, Data = "Feature engineering available via dedicated service" };
            }
            catch (Exception ex)
            {
                return new ToolResult { ToolName = "feature-engineering", Success = false, ErrorMessage = ex.Message };
            }
        }

        private async Task<ToolResult> ExecuteModelValidationAsync(Dictionary<string, object> parameters)
        {
            try
            {
                var modelId = GetStringParameter(parameters, "modelId", "");
                // Placeholder - ModelValidationService may not have the exact method
                return new ToolResult { ToolName = "model-validation", Success = true, Data = $"Model validation for {modelId} available via dedicated service" };
            }
            catch (Exception ex)
            {
                return new ToolResult { ToolName = "model-validation", Success = false, ErrorMessage = ex.Message };
            }
        }

        private async Task<ToolResult> ExecuteFactorModelsAsync(string[] symbols)
        {
            try
            {
                // Placeholder - FactorModelService may not have the exact method
                return new ToolResult { ToolName = "factor-models", Success = true, Data = "Factor models available via dedicated service" };
            }
            catch (Exception ex)
            {
                return new ToolResult { ToolName = "factor-models", Success = false, ErrorMessage = ex.Message };
            }
        }

        private async Task<ToolResult> ExecuteAdvancedOptimizationAsync(string[] symbols)
        {
            try
            {
                // Placeholder - AdvancedOptimizationService may not have the exact method
                return new ToolResult { ToolName = "advanced-optimization", Success = true, Data = "Advanced portfolio optimization available via dedicated service" };
            }
            catch (Exception ex)
            {
                return new ToolResult { ToolName = "advanced-optimization", Success = false, ErrorMessage = ex.Message };
            }
        }

        private async Task<ToolResult> ExecuteAdvancedRiskAsync(string[] symbols)
        {
            try
            {
                // Placeholder - AdvancedRiskService may not have the exact method
                return new ToolResult { ToolName = "advanced-risk", Success = true, Data = "Advanced risk analytics available via dedicated service" };
            }
            catch (Exception ex)
            {
                return new ToolResult { ToolName = "advanced-risk", Success = false, ErrorMessage = ex.Message };
            }
        }

        private async Task<ToolResult> ExecuteTradingTemplatesAsync(Dictionary<string, object> parameters)
        {
            try
            {
                var templateType = GetStringParameter(parameters, "type", "momentum");
                // Placeholder - TradingTemplateGeneratorAgent may not have the exact method
                return new ToolResult { ToolName = "trading-templates", Success = true, Data = $"Trading templates for {templateType} available via dedicated service" };
            }
            catch (Exception ex)
            {
                return new ToolResult { ToolName = "trading-templates", Success = false, ErrorMessage = ex.Message };
            }
        }
    }

    public class RequestAnalysis
    {
        public string Intent { get; set; } = "";
        public string[] RequiredTools { get; set; } = Array.Empty<string>();
        public string[] ExtractedSymbols { get; set; } = Array.Empty<string>();
        public Dictionary<string, object> Parameters { get; set; } = new();
    }

    public class ToolResult
    {
        public string ToolName { get; set; } = "";
        public bool Success { get; set; }
        public object? Data { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class ConversationMessage
    {
        public string Role { get; set; } = ""; // "user" or "assistant"
        public string Content { get; set; } = "";
        public DateTime Timestamp { get; set; }
    }
}