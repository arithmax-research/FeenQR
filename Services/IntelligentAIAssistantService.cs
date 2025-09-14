using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using QuantResearchAgent.Core;
using QuantResearchAgent.Plugins;
using QuantResearchAgent.Services.ResearchAgents;
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
            GoogleWebSearchPlugin googleWebSearchPlugin)
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

IMPORTANT: For questions about recent events, current geopolitical situations, breaking news, latest content from specific companies/organizations, podcast episodes, recent strategies, or ANY information that needs to be current and up-to-date, ALWAYS include 'web-search' or 'current-events' tools to get real-time information.

Examples that REQUIRE web search:
- latest strategy from company
- recent podcast from organization
- current events about topic
- what happened with recent event
- latest news about anything
- recent developments in field

For video content requests without specific URLs, use web-search FIRST to find the content, then analyze if URLs are found.

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
        ""maxResults"": 10
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
                Console.WriteLine($"⚠️ Failed to parse analysis JSON: {ex.Message}");
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
                    case "reddit_scrape":
                        return await ExecuteRedditSentimentAsync(symbols);
                    
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