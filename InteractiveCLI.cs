using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using QuantResearchAgent.Core;
using QuantResearchAgent.Services;
using QuantResearchAgent.Services.ResearchAgents;
using QuantResearchAgent.Plugins;
using System.Text.Json;

namespace QuantResearchAgent;

/// <summary>
/// Interactive CLI for testing the Quant Research Agent
/// </summary>
public class InteractiveCLI
{
    private readonly Kernel _kernel;
    private readonly AgentOrchestrator _orchestrator;
    private readonly ILogger<InteractiveCLI> _logger;
    private readonly ComprehensiveStockAnalysisAgent _comprehensiveAgent;
    private readonly AcademicResearchPaperAgent _researchAgent;
    private readonly YahooFinanceService _yahooFinanceService;
    private readonly AlpacaService _alpacaService;
    private readonly PolygonService _polygonService;
    private readonly DataBentoService _dataBentoService;
    private readonly YFinanceNewsService _yfinanceNewsService;
    private readonly FinvizNewsService _finvizNewsService;
    private readonly NewsSentimentAnalysisService _newsSentimentService;
    private readonly RedditScrapingService _redditScrapingService;
    private readonly PortfolioOptimizationService _portfolioOptimizationService;
    private readonly SocialMediaScrapingService _socialMediaScrapingService;
    private readonly WebDataExtractionService _webDataExtractionService;
    private readonly ReportGenerationService _reportGenerationService;
    private readonly SatelliteImageryAnalysisService _satelliteImageryAnalysisService;
    private readonly ILLMService _llmService;
    
    public InteractiveCLI(
        Kernel kernel, 
        AgentOrchestrator orchestrator, 
        ILogger<InteractiveCLI> logger,
        ComprehensiveStockAnalysisAgent comprehensiveAgent,
        AcademicResearchPaperAgent researchAgent,
        YahooFinanceService yahooFinanceService,
        AlpacaService alpacaService,
        PolygonService polygonService,
        DataBentoService dataBentoService,
        YFinanceNewsService yfinanceNewsService,
        FinvizNewsService finvizNewsService,
        NewsSentimentAnalysisService newsSentimentService,
        RedditScrapingService redditScrapingService,
        PortfolioOptimizationService portfolioOptimizationService,
        SocialMediaScrapingService socialMediaScrapingService,
        WebDataExtractionService webDataExtractionService,
        ReportGenerationService reportGenerationService,
        SatelliteImageryAnalysisService satelliteImageryAnalysisService,
        ILLMService llmService)
    {
        _kernel = kernel;
        _orchestrator = orchestrator;
        _logger = logger;
        _comprehensiveAgent = comprehensiveAgent;
        _researchAgent = researchAgent;
        _yahooFinanceService = yahooFinanceService;
        _alpacaService = alpacaService;
        _polygonService = polygonService;
        _dataBentoService = dataBentoService;
        _yfinanceNewsService = yfinanceNewsService;
        _finvizNewsService = finvizNewsService;
        _newsSentimentService = newsSentimentService;
        _redditScrapingService = redditScrapingService;
        _portfolioOptimizationService = portfolioOptimizationService;
        _socialMediaScrapingService = socialMediaScrapingService;
        _webDataExtractionService = webDataExtractionService;
        _reportGenerationService = reportGenerationService;
        _satelliteImageryAnalysisService = satelliteImageryAnalysisService;
        _llmService = llmService;
    }

    public async Task RunAsync()
    {
        Console.WriteLine("Arithmax CLI Quantitative Research Agent");
        Console.WriteLine("=========================================");
        Console.WriteLine();
        Console.WriteLine("Available commands:");
        Console.WriteLine("  1. analyze-video [url] - Analyze a YouTube video");
        Console.WriteLine("  2. get-quantopian-videos - Get latest Quantopian videos");
        Console.WriteLine("  3. search-finance-videos [query] - Search finance videos");
        Console.WriteLine("  4. technical-analysis [symbol] - Short-term (100d) technical analysis");
        Console.WriteLine("  5. technical-analysis-long [symbol] - Long-term (7y) technical analysis");
        Console.WriteLine("  6. fundamental-analysis [symbol] - Fundamental & sentiment analysis");
        Console.WriteLine("  7. market-data [symbol] - Get market data");
        Console.WriteLine("  8. yahoo-data [symbol] - Get Yahoo Finance market data");
        Console.WriteLine("  9. portfolio - View portfolio summary");
        Console.WriteLine(" 10. risk-assessment - Assess portfolio risk");
        Console.WriteLine(" 11. alpaca-data [symbol] - Get Alpaca market data");
        Console.WriteLine(" 12. alpaca-historical [symbol] [days] - Get historical data");
        Console.WriteLine(" 13. alpaca-account - View Alpaca account info");
        Console.WriteLine(" 14. alpaca-positions - View current positions");
        Console.WriteLine(" 15. alpaca-quotes [symbols] - Get multiple quotes");
        Console.WriteLine(" 16. ta-indicators [symbol] [category] - Detailed indicators");
        Console.WriteLine(" 17. ta-compare [symbols] - Compare TA of multiple symbols");
        Console.WriteLine(" 18. ta-patterns [symbol] - Pattern recognition analysis");
        Console.WriteLine(" 19. comprehensive-analysis [symbol] [asset_type] - Full analysis & recommendation");
        Console.WriteLine(" 20. research-papers [topic] - Search academic finance papers");
        Console.WriteLine(" 21. analyze-paper [url] [focus_area] - Analyze paper & generate blueprint");
        Console.WriteLine(" 22. research-synthesis [topic] [max_papers] - Research & synthesize topic");
        Console.WriteLine(" 23. quick-research [topic] [max_papers] - Quick research overview (faster)");
        Console.WriteLine(" 24. test-apis [symbol] - Test API connectivity and configuration");
        Console.WriteLine(" 25. polygon-data [symbol] - Get Polygon.io market data");
        Console.WriteLine(" 26. polygon-news [symbol] - Get Polygon.io news for symbol");
        Console.WriteLine(" 27. polygon-financials [symbol] - Get Polygon.io financial data");
        Console.WriteLine(" 28. databento-ohlcv [symbol] [days] - Get DataBento OHLCV data");
        Console.WriteLine(" 29. databento-futures [symbol] - Get DataBento futures contracts");
        Console.WriteLine(" 30. live-news [symbol/keyword] - Get live financial news");
        Console.WriteLine(" 31. sentiment-analysis [symbol] - AI-powered sentiment analysis for specific stock");
        Console.WriteLine(" 32. market-sentiment - AI-powered overall market sentiment analysis");
        Console.WriteLine(" 33. reddit-sentiment [subreddit] [symbol] - Reddit sentiment analysis for symbol");
        Console.WriteLine(" 34. reddit-scrape [subreddit] - Scrape Reddit posts from subreddit");
        Console.WriteLine(" 35. optimize-portfolio [tickers] - Portfolio optimization (equal weight)");
        Console.WriteLine(" 36. extract-web-data [url] - Extract structured data from web pages");
        Console.WriteLine(" 37. generate-report [symbol/portfolio] [report_type] - Generate comprehensive reports");
        Console.WriteLine(" 38. analyze-satellite-imagery [symbol] - Analyze satellite imagery for company operations");
        Console.WriteLine(" 39. scrape-social-media [symbol] - Social media sentiment analysis");
        Console.WriteLine(" 40. clear - Clear terminal and show menu");
        Console.WriteLine(" 41. help - Show available functions");
        Console.WriteLine(" 42. quit - Exit the application");
        Console.WriteLine();

        while (true)
        {
            Console.Write("agent> ");
            var input = Console.ReadLine()?.Trim();

            // Handle null input (EOF from pipe or Ctrl+D)
            if (input == null)
            {
                Console.WriteLine();
                break;
            }

            if (string.IsNullOrEmpty(input))
                continue;

            if (input.ToLower() == "quit" || input.ToLower() == "exit")
                break;

            await ProcessCommandAsync(input);
            Console.WriteLine();
        }
    }


    private async Task ProcessCommandAsync(string input)
    {
        try
        {
            var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var command = parts[0].ToLower();

            switch (command)
            {
                case "analyze-video":
                    await AnalyzeVideoCommand(parts);
                    break;
                case "get-quantopian-videos":
                    await GetQuantopianVideosCommand();
                    break;
                case "search-finance-videos":
                    await SearchFinanceVideosCommand(parts);
                    break;
                case "technical-analysis":
                    await TechnicalAnalysisCommand(parts); // short-term (100d)
                    break;
                case "technical-analysis-long":
                    await TechnicalAnalysisLongCommand(parts); // long-term (7y)
                    break;
                case "fundamental-analysis":
                    await FundamentalAnalysisCommand(parts);
                    break;
                case "market-data":
                    await MarketDataCommand(parts);
                    break;
                case "yahoo-data":
                    await YahooDataCommand(parts);
                    break;
                case "portfolio":
                    await PortfolioCommand();
                    break;
                case "risk-assessment":
                    await RiskAssessmentCommand();
                    break;
                case "alpaca-data":
                    await AlpacaDataCommand(parts);
                    break;
                case "alpaca-historical":
                    await AlpacaHistoricalCommand(parts);
                    break;
                case "alpaca-account":
                    await AlpacaAccountCommand();
                    break;
                case "alpaca-positions":
                    await AlpacaPositionsCommand();
                    break;
                case "alpaca-quotes":
                    await AlpacaQuotesCommand(parts);
                    break;
                case "ta-indicators":
                    await TechnicalIndicatorsCommand(parts);
                    break;
                case "ta-compare":
                    await TechnicalCompareCommand(parts);
                    break;
                case "ta-patterns":
                    await TechnicalPatternsCommand(parts);
                    break;
                case "comprehensive-analysis":
                    await ComprehensiveAnalysisCommand(parts);
                    break;
                case "research-papers":
                    await ResearchPapersCommand(parts);
                    break;
                case "analyze-paper":
                    await AnalyzePaperCommand(parts);
                    break;
                case "research-synthesis":
                    await ResearchSynthesisCommand(parts);
                    break;
                case "quick-research":
                    await QuickResearchCommand(parts);
                    break;
                case "test-apis":
                    await TestApisCommand(parts);
                    break;
                case "polygon-data":
                    await PolygonDataCommand(parts);
                    break;
                case "polygon-news":
                    await PolygonNewsCommand(parts);
                    break;
                case "polygon-financials":
                    await PolygonFinancialsCommand(parts);
                    break;
                case "databento-ohlcv":
                    await DataBentoOhlcvCommand(parts);
                    break;
                case "databento-futures":
                    await DataBentoFuturesCommand(parts);
                    break;
                case "live-news":
                    await LiveNewsCommand(parts);
                    break;
                case "sentiment-analysis":
                    await SentimentAnalysisCommand(parts);
                    break;
                case "market-sentiment":
                    await MarketSentimentCommand(parts);
                    break;
                case "reddit-sentiment":
                    await RedditSentimentCommand(parts);
                    break;
                case "reddit-scrape":
                    await RedditScrapeCommand(parts);
                    break;
                case "optimize-portfolio":
                    await OptimizePortfolioCommand(parts);
                    break;
                case "extract-web-data":
                    await ExtractWebDataCommand(parts);
                    break;
                case "generate-report":
                    await GenerateReportCommand(parts);
                    break;
                case "analyze-satellite-imagery":
                    await AnalyzeSatelliteImageryCommand(parts);
                    break;
                case "scrape-social-media":
                    await ScrapeSocialMediaCommand(parts);
                    break;
                case "clear":
                    await ClearCommand();
                    break;
                case "help":
                    await ShowAvailableFunctions();
                    break;
                case "test":
                    await RunTestSequence();
                    break;
                default:
                    await ExecuteSemanticFunction(input);
                    break;
            }
        }
        catch (Exception ex)
        {
            // Only show a minimal error message, no logs
            PrintSectionHeader("Error");
            Console.WriteLine($"{ex.Message}");
            PrintSectionFooter();
        }
    }

    private async Task AnalyzeVideoCommand(string[] parts)
    {
        var url = parts.Length > 1 ? parts[1] : "https://www.youtube.com/watch?v=dQw4w9WgXcQ";
        
        PrintSectionHeader("YouTube Video Analysis");
        Console.WriteLine($"Video URL: {url}");
        var function = _kernel.Plugins["YouTubeAnalysisPlugin"]["AnalyzeVideo"];
        var result = await _kernel.InvokeAsync(function, new() { ["videoUrl"] = url });
        Console.WriteLine(result.ToString());
        PrintSectionFooter();
    }

    private async Task GetQuantopianVideosCommand()
    {
        PrintSectionHeader("Latest Quantopian Videos");
        var function = _kernel.Plugins["YouTubeAnalysisPlugin"]["GetLatestQuantopianVideos"];
        var result = await _kernel.InvokeAsync(function, new() { ["maxResults"] = 5 });
        Console.WriteLine(result.ToString());
        PrintSectionFooter();
    }

    private async Task SearchFinanceVideosCommand(string[] parts)
    {
        var query = parts.Length > 1 ? string.Join(" ", parts.Skip(1)) : "quantitative trading";
        
        PrintSectionHeader("Finance Video Search");
        Console.WriteLine($"Query: {query}");
        var function = _kernel.Plugins["YouTubeAnalysisPlugin"]["SearchFinanceVideos"];
        var result = await _kernel.InvokeAsync(function, new() { ["query"] = query, ["maxResults"] = 5 });
        Console.WriteLine(result.ToString());
        PrintSectionFooter();
    }



    private async Task MarketDataCommand(string[] parts)
    {
        var symbol = parts.Length > 1 ? parts[1] : "BTCUSDT";
        
        PrintSectionHeader("Market Data");
        Console.WriteLine($"Symbol: {symbol}");
        var function = _kernel.Plugins["MarketDataPlugin"]["GetMarketData"];
        var result = await _kernel.InvokeAsync(function, new() { ["symbol"] = symbol });
        Console.WriteLine(result.ToString());
        PrintSectionFooter();
    }

    private async Task PortfolioCommand()
    {
        PrintSectionHeader("Portfolio Summary");
        var function = _kernel.Plugins["RiskManagementPlugin"]["GetPortfolioSummary"];
        var result = await _kernel.InvokeAsync(function);
        Console.WriteLine(result.ToString());
        PrintSectionFooter();
    }

    private async Task RiskAssessmentCommand()
    {
        PrintSectionHeader("Portfolio Risk Assessment");
        var function = _kernel.Plugins["RiskManagementPlugin"]["AssessPortfolioRisk"];
        var result = await _kernel.InvokeAsync(function);
        Console.WriteLine(result.ToString());
        PrintSectionFooter();
    }

    private async Task ShowAvailableFunctions()
    {
        Console.WriteLine("Available Semantic Kernel Functions:");
        Console.WriteLine();

        foreach (var plugin in _kernel.Plugins)
        {
            Console.WriteLine($"{plugin.Name}:");
            
            foreach (var function in plugin)
            {
                Console.WriteLine($"  • {function.Name} - {function.Description}");
            }
            Console.WriteLine();
        }

        await Task.CompletedTask;
    }

    private async Task ExecuteSemanticFunction(string input)
    {
        // Parse function call format: PluginName.FunctionName [param1=value1] [param2=value2]
        var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        
        if (parts.Length == 0 || !parts[0].Contains('.'))
        {
            Console.WriteLine("ERROR: Invalid function format. Use: PluginName.FunctionName [param=value]");
            return;
        }

        var functionParts = parts[0].Split('.');
        if (functionParts.Length != 2)
        {
            Console.WriteLine("ERROR: Invalid function format. Use: PluginName.FunctionName [param=value]");
            return;
        }

        var pluginName = functionParts[0];
        var functionName = functionParts[1];

        // Parse parameters
        var parameters = new Dictionary<string, object?>();
        for (int i = 1; i < parts.Length; i++)
        {
            var paramParts = parts[i].Split('=', 2);
            if (paramParts.Length == 2)
            {
                parameters[paramParts[0]] = paramParts[1];
            }
        }

        try
        {
            var function = _kernel.Plugins[pluginName][functionName];
            var result = await _kernel.InvokeAsync(function, new KernelArguments(parameters));
            Console.WriteLine(result.ToString());
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: Function execution failed: {ex.Message}");
        }
    }

    private async Task RunTestSequence()
    {
        Console.WriteLine("Running test sequence...");
        Console.WriteLine();

        // Test 1: Get Quantopian Videos
        Console.WriteLine("1. Testing Quantopian video retrieval...");
        await GetQuantopianVideosCommand();
        Console.WriteLine();

        // Test 2: Market Data
        Console.WriteLine("2. Testing market data retrieval...");
        await MarketDataCommand(new[] { "market-data", "BTCUSDT" });
        Console.WriteLine();

        // Test 3: Portfolio Summary
        Console.WriteLine("3. Testing portfolio summary...");
        await PortfolioCommand();
        Console.WriteLine();

        // Test 4: Risk Assessment
        Console.WriteLine("4. Testing risk assessment...");
        await RiskAssessmentCommand();
        Console.WriteLine();

        Console.WriteLine("Test sequence completed!");
    }

    private async Task YahooDataCommand(string[] parts)
    {
        var symbol = parts.Length > 1 ? parts[1] : "AAPL";
        PrintSectionHeader("Yahoo Finance Data");
        Console.WriteLine($"Symbol: {symbol}");
        var function = _kernel.Plugins["MarketDataPlugin"]["GetYahooMarketData"];
        var result = await _kernel.InvokeAsync(function, new() { ["symbol"] = symbol });
        Console.WriteLine(result.ToString());
        PrintSectionFooter();
    }

    public static Task<InteractiveCLI> CreateAsync(IServiceProvider serviceProvider)
    {
        var kernel = serviceProvider.GetRequiredService<Kernel>();
        var orchestrator = serviceProvider.GetRequiredService<AgentOrchestrator>();
        var logger = serviceProvider.GetRequiredService<ILogger<InteractiveCLI>>();
        var comprehensiveAgent = serviceProvider.GetRequiredService<ComprehensiveStockAnalysisAgent>();
        var researchAgent = serviceProvider.GetRequiredService<AcademicResearchPaperAgent>();
        var yahooFinanceService = serviceProvider.GetRequiredService<YahooFinanceService>();
        var alpacaService = serviceProvider.GetRequiredService<AlpacaService>();
        var polygonService = serviceProvider.GetRequiredService<PolygonService>();
        var dataBentoService = serviceProvider.GetRequiredService<DataBentoService>();
        var yfinanceNewsService = serviceProvider.GetRequiredService<YFinanceNewsService>();
        var finvizNewsService = serviceProvider.GetRequiredService<FinvizNewsService>();
        var newsSentimentService = serviceProvider.GetRequiredService<NewsSentimentAnalysisService>();
        var redditScrapingService = serviceProvider.GetRequiredService<RedditScrapingService>();
        var portfolioOptimizationService = serviceProvider.GetRequiredService<PortfolioOptimizationService>();
        var socialMediaScrapingService = serviceProvider.GetRequiredService<SocialMediaScrapingService>();
        var webDataExtractionService = serviceProvider.GetRequiredService<WebDataExtractionService>();
        var reportGenerationService = serviceProvider.GetRequiredService<ReportGenerationService>();
        var satelliteImageryAnalysisService = serviceProvider.GetRequiredService<SatelliteImageryAnalysisService>();

        var llmService = serviceProvider.GetRequiredService<ILLMService>();
        return Task.FromResult(new InteractiveCLI(kernel, orchestrator, logger, comprehensiveAgent, researchAgent, yahooFinanceService, alpacaService, polygonService, dataBentoService, yfinanceNewsService, finvizNewsService, newsSentimentService, redditScrapingService, portfolioOptimizationService, socialMediaScrapingService, webDataExtractionService, reportGenerationService, satelliteImageryAnalysisService, llmService));
    }

    // Alpaca Commands
    private async Task AlpacaDataCommand(string[] parts)
    {
        var symbol = parts.Length > 1 ? parts[1] : "AAPL";
        
        PrintSectionHeader("Alpaca Market Data");
        Console.WriteLine($"Symbol: {symbol}");
        var function = _kernel.Plugins["AlpacaPlugin"]["GetMarketData"];
        var result = await _kernel.InvokeAsync(function, new() { ["symbol"] = symbol });
        Console.WriteLine(result.ToString());
        PrintSectionFooter();
    }

    private async Task AlpacaHistoricalCommand(string[] parts)
    {
        var symbol = parts.Length > 1 ? parts[1] : "AAPL";
        var days = parts.Length > 2 && int.TryParse(parts[2], out var d) ? d : 30;
        
        PrintSectionHeader("Alpaca Historical Data");
        Console.WriteLine($"Symbol: {symbol} | Days: {days}");
        var function = _kernel.Plugins["AlpacaPlugin"]["GetHistoricalData"];
        var result = await _kernel.InvokeAsync(function, new() { ["symbol"] = symbol, ["days"] = days });
        Console.WriteLine(result.ToString());
        PrintSectionFooter();
    }

    private async Task AlpacaAccountCommand()
    {
        PrintSectionHeader("Alpaca Account Info");
        var function = _kernel.Plugins["AlpacaPlugin"]["GetAccountInfo"];
        var result = await _kernel.InvokeAsync(function);
        Console.WriteLine(result.ToString());
        PrintSectionFooter();
    }

    private async Task AlpacaPositionsCommand()
    {
        PrintSectionHeader("Alpaca Positions");
        var function = _kernel.Plugins["AlpacaPlugin"]["GetPositions"];
        var result = await _kernel.InvokeAsync(function);
        Console.WriteLine(result.ToString());
        PrintSectionFooter();
    }

    private async Task AlpacaQuotesCommand(string[] parts)
    {
        var symbols = parts.Length > 1 ? parts[1] : "AAPL,MSFT,GOOGL";
        
        PrintSectionHeader("Alpaca Multiple Quotes");
        Console.WriteLine($"Symbols: {symbols}");
        var function = _kernel.Plugins["AlpacaPlugin"]["GetMultipleQuotes"];
        var result = await _kernel.InvokeAsync(function, new() { ["symbols"] = symbols });
        Console.WriteLine(result.ToString());
        PrintSectionFooter();
    }

    // Technical Analysis Commands
    private async Task TechnicalAnalysisCommand(string[] parts)
    {
        var symbol = parts.Length > 1 ? parts[1] : "AAPL";
        PrintSectionHeader("Technical Analysis");
        Console.WriteLine($"Symbol: {symbol}");
        var function = _kernel.Plugins["TechnicalAnalysisPlugin"]["AnalyzeSymbol"];
        var result = await _kernel.InvokeAsync(function, new() { ["symbol"] = symbol });
        Console.WriteLine(result.ToString());

        // --- News Sentiment & Outlook ---
        PrintSectionHeader("News Sentiment & Outlook");
        try
        {
            var sentimentFunction = _kernel.Plugins["MarketSentimentPlugin"]?["AnalyzeMarketSentiment"];
            if (sentimentFunction != null)
            {
                var sentimentResult = await _kernel.InvokeAsync(sentimentFunction, new() { ["assetClass"] = "stocks", ["specificAsset"] = symbol });
                Console.WriteLine(sentimentResult.ToString());
            }
            else
            {
                Console.WriteLine("(No sentiment function available)");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"(Sentiment analysis unavailable: {ex.Message})");
        }
        PrintSectionFooter();
    }

    private async Task TechnicalIndicatorsCommand(string[] parts)
    {
        var symbol = parts.Length > 1 ? parts[1] : "AAPL";
        var category = parts.Length > 2 ? parts[2] : "all";
        PrintSectionHeader("Technical Indicators");
        Console.WriteLine($"Symbol: {symbol} | Category: {category}");
        var function = _kernel.Plugins["TechnicalAnalysisPlugin"]["GetDetailedIndicators"];
        var result = await _kernel.InvokeAsync(function, new() { ["symbol"] = symbol, ["category"] = category });
        Console.WriteLine(result.ToString());
        // AI-powered summary/analysis
        try
        {
            var aiSummaryFunction = _kernel.Plugins["TechnicalAnalysisPlugin"].Contains("AISummary")
                ? _kernel.Plugins["TechnicalAnalysisPlugin"]["AISummary"]
                : null;
            if (aiSummaryFunction != null)
            {
                Console.WriteLine("\nAI Analysis:");
                var aiSummary = await _kernel.InvokeAsync(aiSummaryFunction, new() { ["symbol"] = symbol, ["indicatorsText"] = result.ToString() });
                Console.WriteLine(aiSummary.ToString());
            }
            else
            {
                var prompt = $"Given the following technical indicator results for {symbol}, provide a concise, actionable summary and highlight any notable trends, risks, or opportunities.\n\n{result.ToString()}";
                var summaryFunction = _kernel.Plugins["GeneralAIPlugin"]?["Summarize"];
                if (summaryFunction != null)
                {
                    Console.WriteLine("\nAI Analysis:");
                    var aiSummary = await _kernel.InvokeAsync(summaryFunction, new() { ["input"] = prompt });
                    Console.WriteLine(aiSummary.ToString());
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"(AI analysis unavailable: {ex.Message})");
        }
        PrintSectionFooter();
    }

    private async Task TechnicalCompareCommand(string[] parts)
    {
        var symbols = parts.Length > 1 ? string.Join(",", parts.Skip(1)) : "AAPL,MSFT,GOOGL";
        PrintSectionHeader("Technical Analysis Comparison");
        Console.WriteLine($"Symbols: {symbols}");
        var function = _kernel.Plugins["TechnicalAnalysisPlugin"]["CompareSymbols"];
        var result = await _kernel.InvokeAsync(function, new() { ["symbols"] = symbols });
        Console.WriteLine(result.ToString());
        PrintSectionFooter();
    }

    private async Task TechnicalPatternsCommand(string[] parts)
    {
        var symbol = parts.Length > 1 ? parts[1] : "AAPL";
        PrintSectionHeader("Pattern Recognition Analysis");
        Console.WriteLine($"Symbol: {symbol}");
        var function = _kernel.Plugins["TechnicalAnalysisPlugin"]["GetPatternAnalysis"];
        var result = await _kernel.InvokeAsync(function, new() { ["symbol"] = symbol });
        Console.WriteLine(result.ToString());
        PrintSectionFooter();
    }

    // Long-term technical analysis (7 years)
    private async Task TechnicalAnalysisLongCommand(string[] parts)
    {
        var symbol = parts.Length > 1 ? parts[1] : "AAPL";
        PrintSectionHeader("Long-Term Technical Analysis");
        Console.WriteLine($"Symbol: {symbol} | Lookback: 7 years");
        var function = _kernel.Plugins["TechnicalAnalysisPlugin"]["AnalyzeSymbol"];
        var result = await _kernel.InvokeAsync(function, new() { ["symbol"] = symbol, ["lookbackDays"] = 2555 });
        Console.WriteLine(result.ToString());

        // --- News Sentiment & Outlook ---
        PrintSectionHeader("News Sentiment & Outlook");
        try
        {
            var sentimentFunction = _kernel.Plugins["MarketSentimentPlugin"]?["AnalyzeMarketSentiment"];
            if (sentimentFunction != null)
            {
                var sentimentResult = await _kernel.InvokeAsync(sentimentFunction, new() { ["assetClass"] = "stocks", ["specificAsset"] = symbol });
                Console.WriteLine(sentimentResult.ToString());
            }
            else
            {
                Console.WriteLine("(No sentiment function available)");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"(Sentiment analysis unavailable: {ex.Message})");
        }
        PrintSectionFooter();
    }

    // Fundamental & sentiment analysis
    private async Task FundamentalAnalysisCommand(string[] parts)
    {
        var symbol = parts.Length > 1 ? parts[1] : "AAPL";
        PrintSectionHeader("Fundamental & Sentiment Analysis");
        Console.WriteLine($"Symbol: {symbol}");
        try
        {
            var function = _kernel.Plugins["CompanyValuationPlugin"]?["AnalyzeStock"];
            if (function != null)
            {
                var result = await _kernel.InvokeAsync(function, new() { ["ticker"] = symbol });
                Console.WriteLine(result.ToString());
            }
            else
            {
                Console.WriteLine("(No fundamental analysis function available)");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error analyzing stock {symbol}: {ex.Message}");
        }
        PrintSectionFooter();
    }
    // --- UI Section Helpers ---
    private void PrintSectionHeader(string title)
    {
        Console.WriteLine();
        Console.WriteLine(new string('=', 60));
        Console.WriteLine($"{title.ToUpper(),-60}");
        Console.WriteLine(new string('-', 60));
    }


    private void PrintSectionFooter()
    {
        Console.WriteLine(new string('=', 60));
        Console.WriteLine();
    }

    // --- New Research Commands ---
    
    private async Task ComprehensiveAnalysisCommand(string[] parts)
    {
        try
        {
            var symbol = parts.Length > 1 ? parts[1].ToUpper() : "AAPL";
            var assetType = parts.Length > 2 ? parts[2].ToLower() : "stock";
            
            PrintSectionHeader($"Comprehensive Analysis: {symbol}");
            Console.WriteLine($"Asset Type: {assetType}");
            Console.WriteLine("Performing comprehensive analysis including web search, sentiment, technical, and fundamental analysis...");
            Console.WriteLine();
            
            var result = await _comprehensiveAgent.AnalyzeAndRecommendAsync(symbol, assetType);
            Console.WriteLine(result);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error performing comprehensive analysis: {ex.Message}");
        }
        PrintSectionFooter();
    }
    
    private async Task ResearchPapersCommand(string[] parts)
    {
        try
        {
            var topic = parts.Length > 1 ? string.Join(" ", parts[1..]) : "algorithmic trading";
            
            PrintSectionHeader($"Academic Papers Search: {topic}");
            Console.WriteLine("Searching for academic finance/quantitative research papers...");
            Console.WriteLine();
            
            var result = await _researchAgent.SearchAcademicPapersAsync(topic, 5);
            Console.WriteLine(result);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error searching academic papers: {ex.Message}");
        }
        PrintSectionFooter();
    }
    
    private async Task AnalyzePaperCommand(string[] parts)
    {
        try
        {
            if (parts.Length < 2)
            {
                Console.WriteLine("Usage: analyze-paper [url] [optional_focus_area]");
                return;
            }
            
            var paperUrl = parts[1];
            var focusArea = parts.Length > 2 ? string.Join(" ", parts[2..]) : "";
            
            PrintSectionHeader($"Paper Analysis & Blueprint Generation");
            Console.WriteLine($"Paper URL: {paperUrl}");
            if (!string.IsNullOrEmpty(focusArea))
            {
                Console.WriteLine($"Focus Area: {focusArea}");
            }
            Console.WriteLine("Analyzing paper and generating implementation blueprint...");
            Console.WriteLine();
            
            var result = await _researchAgent.AnalyzePaperAndGenerateBlueprintAsync(paperUrl, focusArea);
            Console.WriteLine(result);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error analyzing paper: {ex.Message}");
        }
        PrintSectionFooter();
    }
    
    private async Task ResearchSynthesisCommand(string[] parts)
    {
        try
        {
            var topic = parts.Length > 1 ? string.Join(" ", parts[1..^1]) : "momentum trading";
            var maxPapers = 3;
            
            if (parts.Length > 1 && int.TryParse(parts[^1], out var parsedMaxPapers))
            {
                maxPapers = parsedMaxPapers;
                if (parts.Length > 2)
                {
                    topic = string.Join(" ", parts[1..^1]);
                }
            }
            else if (parts.Length > 1)
            {
                topic = string.Join(" ", parts[1..]);
            }
            
            PrintSectionHeader($"Research Synthesis: {topic}");
            Console.WriteLine($"Max Papers: {maxPapers}");
            Console.WriteLine("Researching topic and synthesizing findings from multiple papers...");
            Console.WriteLine("This may take several minutes...");
            Console.WriteLine();
            
            var result = await _researchAgent.ResearchTopicAndSynthesizeAsync(topic, maxPapers);
            Console.WriteLine(result);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error performing research synthesis: {ex.Message}");
        }
        PrintSectionFooter();
    }

    private async Task QuickResearchCommand(string[] parts)
    {
        try
        {
            var topic = parts.Length > 1 ? string.Join(" ", parts[1..^1]) : "machine learning trading";
            var maxPapers = 3;
            
            if (parts.Length > 1 && int.TryParse(parts[^1], out var parsedMaxPapers))
            {
                maxPapers = parsedMaxPapers;
                if (parts.Length > 2)
                {
                    topic = string.Join(" ", parts[1..^1]);
                }
            }
            else if (parts.Length > 1)
            {
                topic = string.Join(" ", parts[1..]);
            }
            
            PrintSectionHeader($"Quick Research Overview: {topic}");
            Console.WriteLine($"Max Papers: {maxPapers}");
            Console.WriteLine("Performing quick research scan...");
            Console.WriteLine("This should complete in under a minute...");
            Console.WriteLine();
            
            var result = await _researchAgent.GetQuickResearchOverviewAsync(topic, maxPapers);
            Console.WriteLine(result);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error performing quick research: {ex.Message}");
        }
        PrintSectionFooter();
    }

    private async Task ClearCommand()
    {
        // Clear the console
        Console.Clear();
        
        // Show the welcome message and menu again
        Console.WriteLine("Arithmax CLI Quantitative Research Agent");
        Console.WriteLine("=========================================");
        Console.WriteLine();
        Console.WriteLine("Available commands:");
        Console.WriteLine("  1. analyze-video [url] - Analyze a YouTube video");
        Console.WriteLine("  2. get-quantopian-videos - Get latest Quantopian videos");
        Console.WriteLine("  3. search-finance-videos [query] - Search finance videos");
        Console.WriteLine("  4. technical-analysis [symbol] - Short-term (100d) technical analysis");
        Console.WriteLine("  5. technical-analysis-long [symbol] - Long-term (7y) technical analysis");
        Console.WriteLine("  6. fundamental-analysis [symbol] - Fundamental & sentiment analysis");
        Console.WriteLine("  7. market-data [symbol] - Get market data");
        Console.WriteLine("  8. yahoo-data [symbol] - Get Yahoo Finance market data");
        Console.WriteLine("  9. portfolio - View portfolio summary");
        Console.WriteLine(" 10. risk-assessment - Assess portfolio risk");
        Console.WriteLine(" 11. alpaca-data [symbol] - Get Alpaca market data");
        Console.WriteLine(" 12. alpaca-historical [symbol] [days] - Get historical data");
        Console.WriteLine(" 13. alpaca-account - View Alpaca account info");
        Console.WriteLine(" 14. alpaca-positions - View current positions");
        Console.WriteLine(" 15. alpaca-quotes [symbols] - Get multiple quotes");
        Console.WriteLine(" 16. ta-indicators [symbol] [category] - Detailed indicators");
        Console.WriteLine(" 17. ta-compare [symbols] - Compare TA of multiple symbols");
        Console.WriteLine(" 18. ta-patterns [symbol] - Pattern recognition analysis");
        Console.WriteLine(" 19. comprehensive-analysis [symbol] [asset_type] - Full analysis & recommendation");
        Console.WriteLine(" 20. research-papers [topic] - Search academic finance papers");
        Console.WriteLine(" 21. analyze-paper [url] [focus_area] - Analyze paper & generate blueprint");
        Console.WriteLine(" 22. research-synthesis [topic] [max_papers] - Research & synthesize topic");
        Console.WriteLine(" 23. quick-research [topic] [max_papers] - Quick research overview (faster)");
        Console.WriteLine(" 24. test-apis [symbol] - Test API connectivity and configuration");
        Console.WriteLine(" 25. polygon-data [symbol] - Get Polygon.io market data");
        Console.WriteLine(" 26. polygon-news [symbol] - Get Polygon.io news for symbol");
        Console.WriteLine(" 27. polygon-financials [symbol] - Get Polygon.io financial data");
        Console.WriteLine(" 28. databento-ohlcv [symbol] [days] - Get DataBento OHLCV data");
        Console.WriteLine(" 29. databento-futures [symbol] - Get DataBento futures contracts");
        Console.WriteLine(" 30. live-news [symbol/keyword] - Get live financial news");
        Console.WriteLine(" 31. sentiment-analysis [symbol] - AI-powered sentiment analysis for specific stock");
        Console.WriteLine(" 32. market-sentiment - AI-powered overall market sentiment analysis");
        Console.WriteLine(" 33. reddit-sentiment [subreddit] [symbol] - Reddit sentiment analysis for symbol");
        Console.WriteLine(" 34. reddit-scrape [subreddit] - Scrape Reddit posts from subreddit");
        Console.WriteLine(" 35. optimize-portfolio [tickers] - Portfolio optimization (equal weight)");
        Console.WriteLine(" 36. extract-web-data [url] - Extract structured data from web pages");
        Console.WriteLine(" 37. generate-report [symbol/portfolio] [report_type] - Generate comprehensive reports");
        Console.WriteLine(" 38. analyze-satellite-imagery [symbol] - Analyze satellite imagery for company operations");
        Console.WriteLine(" 39. scrape-social-media [symbol] - Social media sentiment analysis");
        Console.WriteLine(" 40. clear - Clear terminal and show menu");
        Console.WriteLine(" 41. help - Show available functions");
        Console.WriteLine(" 42. quit - Exit the application");
        Console.WriteLine();
        
        // Since this is an async method, we need to return a completed task
        await Task.CompletedTask;
    }

    private async Task TestApisCommand(string[] parts)
    {
        var symbol = parts.Length > 1 ? parts[1] : "AAPL";
        
        PrintSectionHeader($"API Connectivity Test - {symbol}");
        
        // Test Yahoo Finance
        Console.WriteLine("Testing Yahoo Finance API...");
        try
        {
            var yahooData = await _yahooFinanceService.GetMarketDataAsync(symbol);
            if (yahooData != null)
            {
                Console.WriteLine("SUCCESS: Yahoo Finance: Connected");
                Console.WriteLine($"  Price: ${yahooData.CurrentPrice:F2}, Volume: {yahooData.Volume:N0}");
            }
            else
            {
                Console.WriteLine("FAILED: Yahoo Finance: Failed");
                Console.WriteLine("  Possible issues: Plugin not loaded, invalid symbol, or API rate limits");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("FAILED: Yahoo Finance: Failed");
            Console.WriteLine($"  Error: {ex.Message}");
        }
        
        // Test Alpaca
        Console.WriteLine("\nTesting Alpaca API...");
        try
        {
            var alpacaData = await _alpacaService.GetMarketDataAsync(symbol);
            if (alpacaData != null)
            {
                Console.WriteLine("SUCCESS: Alpaca: Connected");
                Console.WriteLine($"  Price: ${alpacaData.Price:F2}, Volume: {alpacaData.Volume:N0}");
            }
            else
            {
                Console.WriteLine("FAILED: Alpaca: Failed");
                Console.WriteLine("  Possible issues: Invalid credentials, market closed, or invalid symbol");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("FAILED: Alpaca: Failed");
            Console.WriteLine($"  Error: {ex.Message}");
        }
        
        // Test Polygon
        Console.WriteLine("\nTesting Polygon.io API...");
        try
        {
            var polygonData = await _polygonService.GetQuoteAsync(symbol);
            if (polygonData != null)
            {
                Console.WriteLine("SUCCESS: Polygon.io: Connected");
                Console.WriteLine($"  Price: ${polygonData.Price:F2}, Volume: {polygonData.Size:N0}");
            }
            else
            {
                Console.WriteLine("FAILED: Polygon.io: Failed");
                Console.WriteLine("  Possible issues: Invalid API key, rate limits, or free tier restrictions");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("FAILED: Polygon.io: Failed");
            Console.WriteLine($"  Error: {ex.Message}");
        }
        
        // Test DataBento
        Console.WriteLine("\nTesting DataBento API...");
        try
        {
            var start = DateTime.Now.AddDays(-7);
            var end = DateTime.Now;
            var dataBentoData = await _dataBentoService.GetOHLCVAsync(symbol, start, end);
            if (dataBentoData != null && dataBentoData.Any())
            {
                Console.WriteLine("SUCCESS: DataBento: Connected");
                Console.WriteLine($"  Retrieved {dataBentoData.Count} data points");
            }
            else
            {
                Console.WriteLine("FAILED: DataBento: Failed");
                Console.WriteLine("  Possible issues: Invalid credentials, subscription limits, or data availability");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("FAILED: DataBento: Failed");
            Console.WriteLine($"  Error: {ex.Message}");
        }
        
        Console.WriteLine("\nTroubleshooting Tips:");
        Console.WriteLine("1. Check API keys in appsettings.json");
        Console.WriteLine("2. Verify your internet connection");
        Console.WriteLine("3. Some APIs have rate limits or require subscriptions");
        Console.WriteLine("4. Market data may not be available outside trading hours");
        Console.WriteLine("5. Try different symbols (e.g., TSLA, MSFT, GOOGL)");
        
        PrintSectionFooter();
    }

    private async Task PolygonDataCommand(string[] parts)
    {
        var symbol = parts.Length > 1 ? parts[1] : "AAPL";
        
        PrintSectionHeader($"Polygon.io Market Data - {symbol}");
        
        try
        {
            // Get previous day's data (available on free tier)
            var quote = await _polygonService.GetQuoteAsync(symbol);
            if (quote != null)
            {
                Console.WriteLine($"Symbol: {quote.Symbol}");
                Console.WriteLine($"Previous Close: ${quote.Price:F2}");
                Console.WriteLine($"Volume: {quote.Size:N0}");
                Console.WriteLine($"Date: {quote.Timestamp:yyyy-MM-dd}");
                Console.WriteLine();
                Console.WriteLine("Note: This shows previous day's data (free tier). For real-time data, upgrade to a paid plan.");
            }
            else
            {
                Console.WriteLine("No data available from Polygon.io");
                Console.WriteLine("This might be due to:");
                Console.WriteLine("- Invalid symbol");
                Console.WriteLine("- API rate limiting");
                Console.WriteLine("- Free tier limitations");
            }
            
            // Also get market status
            var marketStatus = await _polygonService.GetMarketStatusAsync();
            if (marketStatus != null)
            {
                Console.WriteLine();
                Console.WriteLine($"Market Status: {marketStatus.Market}");
                Console.WriteLine($"Server Time: {marketStatus.ServerTime:yyyy-MM-dd HH:mm:ss}");
                if (marketStatus.Exchanges != null)
                {
                    Console.WriteLine($"NYSE: {marketStatus.Exchanges.Nyse}");
                    Console.WriteLine($"NASDAQ: {marketStatus.Exchanges.Nasdaq}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error retrieving Polygon data: {ex.Message}");
            if (ex.Message.Contains("NOT_AUTHORIZED"))
            {
                Console.WriteLine("Your current API key has limited access. Consider upgrading your Polygon.io plan.");
            }
        }
        
        PrintSectionFooter();
    }

    private async Task PolygonNewsCommand(string[] parts)
    {
        var symbol = parts.Length > 1 ? parts[1] : "AAPL";
        
        PrintSectionHeader($"Financial News - {symbol}");
        
        try
        {
            Console.WriteLine("=== Polygon.io News ===");
            var polygonNews = await _polygonService.GetNewsAsync(symbol, 3);
            if (polygonNews.Any())
            {
                foreach (var article in polygonNews.Take(3))
                {
                    Console.WriteLine($"Title: {article.Title}");
                    Console.WriteLine($"Published: {article.PublishedUtc:yyyy-MM-dd HH:mm:ss}");
                    Console.WriteLine($"Author: {article.Author}");
                    Console.WriteLine($"URL: {article.ArticleUrl}");
                    Console.WriteLine();
                }
            }
            else
            {
                Console.WriteLine("No news available from Polygon.io (may require paid subscription)");
            }
            
            Console.WriteLine("\n=== Yahoo Finance News (Live) ===");
            var yfinanceNews = await _yfinanceNewsService.GetNewsAsync(symbol, 3);
            if (yfinanceNews.Any())
            {
                foreach (var article in yfinanceNews.Take(3))
                {
                    Console.WriteLine($"Title: {article.Title}");
                    Console.WriteLine($"Published: {article.PublishedDate:yyyy-MM-dd HH:mm:ss}");
                    Console.WriteLine($"Publisher: {article.Publisher}");
                    Console.WriteLine($"Summary: {(article.Summary.Length > 100 ? article.Summary.Substring(0, 100) + "..." : article.Summary)}");
                    Console.WriteLine($"URL: {article.Link}");
                    Console.WriteLine();
                }
            }
            else
            {
                Console.WriteLine("No news available from Yahoo Finance");
            }
            
            Console.WriteLine("\n=== Finviz News ===");
            var finvizNews = await _finvizNewsService.GetNewsAsync(symbol, 3);
            if (finvizNews.Any())
            {
                foreach (var article in finvizNews.Take(3))
                {
                    Console.WriteLine($"Title: {article.Title}");
                    Console.WriteLine($"Published: {article.PublishedDate:yyyy-MM-dd HH:mm:ss}");
                    Console.WriteLine($"Publisher: {article.Publisher}");
                    if (!string.IsNullOrEmpty(article.Summary))
                    {
                        Console.WriteLine($"Summary: {(article.Summary.Length > 100 ? article.Summary.Substring(0, 100) + "..." : article.Summary)}");
                    }
                    Console.WriteLine($"URL: {article.Link}");
                    Console.WriteLine();
                }
            }
            else
            {
                Console.WriteLine("No news available from Finviz");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error retrieving news: {ex.Message}");
        }
        
        PrintSectionFooter();
    }

    private async Task PolygonFinancialsCommand(string[] parts)
    {
        var symbol = parts.Length > 1 ? parts[1] : "AAPL";
        
        PrintSectionHeader($"Polygon.io Financial Data - {symbol}");
        
        try
        {
            var financials = await _polygonService.GetFinancialsAsync(symbol);
            if (financials != null)
            {
                Console.WriteLine($"Ticker: {financials.Ticker}");
                Console.WriteLine($"Period: {financials.PeriodOfReportDate:yyyy-MM-dd}");
                
                if (financials.Financials?.IncomeStatement?.Revenues?.Value != null)
                {
                    Console.WriteLine($"Revenue: ${financials.Financials.IncomeStatement.Revenues.Value:N0}");
                }
                
                if (financials.Financials?.IncomeStatement?.NetIncomeLoss?.Value != null)
                {
                    Console.WriteLine($"Net Income: ${financials.Financials.IncomeStatement.NetIncomeLoss.Value:N0}");
                }
                
                if (financials.Financials?.BalanceSheet?.Assets?.Value != null)
                {
                    Console.WriteLine($"Assets: ${financials.Financials.BalanceSheet.Assets.Value:N0}");
                }
                
                if (financials.Financials?.BalanceSheet?.Liabilities?.Value != null)
                {
                    Console.WriteLine($"Liabilities: ${financials.Financials.BalanceSheet.Liabilities.Value:N0}");
                }
                
                if (financials.Financials?.BalanceSheet?.Equity?.Value != null)
                {
                    Console.WriteLine($"Equity: ${financials.Financials.BalanceSheet.Equity.Value:N0}");
                }
            }
            else
            {
                Console.WriteLine("No financial data available from Polygon.io");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error retrieving Polygon financials: {ex.Message}");
        }
        
        PrintSectionFooter();
    }

    private async Task DataBentoOhlcvCommand(string[] parts)
    {
        var symbol = parts.Length > 1 ? parts[1] : "AAPL";
        var days = parts.Length > 2 && int.TryParse(parts[2], out var d) ? d : 5;
        
        PrintSectionHeader($"DataBento OHLCV Data - {symbol} ({days} days)");
        
        try
        {
            var start = DateTime.Now.AddDays(-days);
            var end = DateTime.Now;
            var ohlcvData = await _dataBentoService.GetOHLCVAsync(symbol, start, end);
            
            if (ohlcvData.Any())
            {
                Console.WriteLine($"{"Date",-12} {"Open",-10} {"High",-10} {"Low",-10} {"Close",-10} {"Volume",-12}");
                Console.WriteLine(new string('-', 70));
                
                foreach (var bar in ohlcvData.Take(10))
                {
                    Console.WriteLine($"{bar.EventTime:yyyy-MM-dd} ${bar.Open,-9:F2} ${bar.High,-9:F2} ${bar.Low,-9:F2} ${bar.Close,-9:F2} {bar.Volume,-12:N0}");
                }
            }
            else
            {
                Console.WriteLine("No OHLCV data available from DataBento");
                Console.WriteLine();
                Console.WriteLine("This might be due to:");
                Console.WriteLine("- DataBento API authentication issues");
                Console.WriteLine("- Invalid API key or insufficient subscription");
                Console.WriteLine("- Symbol not available in the dataset");
                Console.WriteLine();
                Console.WriteLine("DataBento requires a paid subscription for most endpoints.");
                Console.WriteLine("Visit https://databento.com for more information.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error retrieving DataBento OHLCV: {ex.Message}");
            Console.WriteLine("DataBento requires valid API credentials and subscription.");
        }
        
        PrintSectionFooter();
    }

    private async Task DataBentoFuturesCommand(string[] parts)
    {
        var symbol = parts.Length > 1 ? parts[1] : "ES";
        
        PrintSectionHeader($"DataBento Futures Contracts - {symbol}");
        
        try
        {
            var futures = await _dataBentoService.GetFuturesContractsAsync(symbol);
            if (futures.Any())
            {
                Console.WriteLine($"{"Symbol",-15} {"Description",-30} {"Start Date",-12} {"End Date",-12} {"Days to Expiry",-15}");
                Console.WriteLine(new string('-', 85));
                
                foreach (var contract in futures.Take(10))
                {
                    Console.WriteLine($"{contract.Symbol,-15} {contract.Description,-30} {contract.StartDate:yyyy-MM-dd} {contract.EndDate:yyyy-MM-dd} {contract.DaysToExpiry,-15}");
                }
            }
            else
            {
                Console.WriteLine("No futures contracts available from DataBento");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error retrieving DataBento futures: {ex.Message}");
        }
        
        PrintSectionFooter();
    }

    private async Task LiveNewsCommand(string[] parts)
    {
        var query = parts.Length > 1 ? parts[1] : "";
        
        PrintSectionHeader($"Live Financial News{(string.IsNullOrEmpty(query) ? " - Market Overview" : $" - {query}")}");
        
        try
        {
            // Fetch from both sources concurrently
            Task<List<YFinanceNewsItem>> yahooTask;
            Task<List<FinvizNewsItem>> finvizTask;
            
            if (string.IsNullOrEmpty(query))
            {
                Console.WriteLine("Getting market overview news from Yahoo Finance and Finviz...");
                yahooTask = _yfinanceNewsService.GetMarketNewsAsync(8);
                finvizTask = _finvizNewsService.GetMarketNewsAsync(8);
            }
            else if (query.Length <= 5 && query.All(char.IsLetterOrDigit))
            {
                // Treat as symbol
                Console.WriteLine($"Getting news for symbol: {query} from Yahoo Finance and Finviz...");
                yahooTask = _yfinanceNewsService.GetNewsAsync(query, 6);
                finvizTask = _finvizNewsService.GetNewsAsync(query, 6);
            }
            else
            {
                // Treat as keyword search
                Console.WriteLine($"Searching news for: {query} from Yahoo Finance and Finviz...");
                yahooTask = _yfinanceNewsService.SearchNewsAsync(query, 6);
                finvizTask = _finvizNewsService.SearchNewsAsync(query, 6);
            }
            
            // Wait for both sources
            var yahooNews = await yahooTask;
            var finvizNews = await finvizTask;
            
            // Display Yahoo Finance News
            if (yahooNews.Any())
            {
                Console.WriteLine("\n=== Yahoo Finance News ===");
                foreach (var article in yahooNews.Take(6))
                {
                    Console.WriteLine($"NEWS: {article.Title}");
                    Console.WriteLine($"   Publisher: {article.Publisher} | {article.PublishedDate:MMM dd, HH:mm}");
                    
                    if (!string.IsNullOrEmpty(article.Summary))
                    {
                        var summary = article.Summary.Length > 150 ? 
                            article.Summary.Substring(0, 150) + "..." : 
                            article.Summary;
                        Console.WriteLine($"   {summary}");
                    }
                    
                    if (article.RelatedTickers?.Any() == true)
                    {
                        Console.WriteLine($"   Tickers: {string.Join(", ", article.RelatedTickers.Take(5))}");
                    }
                    
                    Console.WriteLine($"   Link: {article.Link}");
                    Console.WriteLine();
                }
            }
            
            // Display Finviz News
            if (finvizNews.Any())
            {
                Console.WriteLine("\n=== Finviz News ===");
                foreach (var article in finvizNews.Take(6))
                {
                    Console.WriteLine($"MARKET: {article.Title}");
                    Console.WriteLine($"   Publisher: {article.Publisher} | {article.PublishedDate:MMM dd, HH:mm}");
                    
                    if (!string.IsNullOrEmpty(article.Summary))
                    {
                        var summary = article.Summary.Length > 150 ? 
                            article.Summary.Substring(0, 150) + "..." : 
                            article.Summary;
                        Console.WriteLine($"   {summary}");
                    }
                    
                    Console.WriteLine($"   Link: {article.Link}");
                    Console.WriteLine();
                }
            }
            
            var totalCount = yahooNews.Count + finvizNews.Count;
            if (totalCount > 0)
            {
                Console.WriteLine($"Found {totalCount} articles ({yahooNews.Count} from Yahoo Finance, {finvizNews.Count} from Finviz)");
            }
            else
            {
                Console.WriteLine("No news articles found from either source");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error retrieving live news: {ex.Message}");
        }
        
        PrintSectionFooter();
    }

    private async Task SentimentAnalysisCommand(string[] parts)
    {
        if (parts.Length < 2)
        {
            Console.WriteLine("ERROR: Please provide a stock symbol. Usage: sentiment-analysis [SYMBOL]");
            return;
        }

        var symbol = parts[1].ToUpper();
        PrintSectionHeader($"AI-Powered Quantitative Sentiment Analysis for {symbol}");

        try
        {
            var analysis = await _newsSentimentService.AnalyzeSymbolSentimentAsync(symbol);
            
            if (analysis != null)
            {
                // Display overall sentiment
                var sentimentEmoji = analysis.OverallSentiment switch
                {
                    "Very Positive" => "[VERY+]",
                    "Positive" => "[POS]",
                    "Negative" => "[NEG]",
                    "Very Negative" => "[VERY-]",
                    "Neutral" => "[NEUT]",
                    _ => "[UNKNOWN]"
                };
                
                Console.WriteLine($"\nOverall Sentiment: {sentimentEmoji} {analysis.OverallSentiment} (Score: {analysis.SentimentScore:F3})");
                Console.WriteLine($"Confidence Level: {analysis.Confidence:F1}%");
                Console.WriteLine($"Trend Direction: {analysis.TrendDirection}");
                
                // Display quantitative insights
                Console.WriteLine($"\nQUANTITATIVE INSIGHTS:");
                Console.WriteLine($"   Trading Signal: {analysis.TradingSignal}");
                Console.WriteLine($"   Volatility Indicator: {analysis.VolatilityIndicator}");
                Console.WriteLine($"   Price Target Bias: {analysis.PriceTargetBias}");
                Console.WriteLine($"   Institutional Sentiment: {analysis.InstitutionalSentiment}");
                Console.WriteLine($"   Retail Sentiment: {analysis.RetailSentiment}");
                Console.WriteLine($"   Analyst Consensus: {analysis.AnalystConsensus}");
                Console.WriteLine($"   Earnings Impact: {analysis.EarningsImpact}");
                Console.WriteLine($"   Sector Comparison: {analysis.SectorComparison}");
                Console.WriteLine($"   Momentum Signal: {analysis.MomentumSignal}");
                
                // Display key themes
                if (analysis.KeyThemes?.Any() == true)
                {
                    Console.WriteLine($"\nKey Themes:");
                    foreach (var theme in analysis.KeyThemes)
                    {
                        Console.WriteLine($"   • {theme}");
                    }
                }
                
                // Display risk factors
                if (analysis.RiskFactors?.Any() == true)
                {
                    Console.WriteLine($"\nRisk Factors:");
                    foreach (var risk in analysis.RiskFactors)
                    {
                        Console.WriteLine($"   • {risk}");
                    }
                }
                
                // Display analysis summary
                if (!string.IsNullOrEmpty(analysis.Summary))
                {
                    Console.WriteLine($"\nQuantitative Analysis Summary:");
                    Console.WriteLine($"   {analysis.Summary}");
                }
                
                // Display analyzed news articles with links
                if (analysis.NewsItems?.Any() == true)
                {
                    Console.WriteLine($"\nAnalyzed News Articles ({analysis.NewsItems.Count} articles):");
                    foreach (var article in analysis.NewsItems.Take(10))
                    {
                        var articleSentiment = article.SentimentLabel switch
                        {
                            "Very Positive" => "[VERY+]",
                            "Positive" => "[POS]",
                            "Negative" => "[NEG]",
                            "Very Negative" => "[VERY-]",
                            "Neutral" => "[NEUT]",
                            _ => "[UNKNOWN]"
                        };
                        
                        Console.WriteLine($"\n   {articleSentiment} {article.Title}");
                        Console.WriteLine($"      Date: {article.PublishedDate:MMM dd, HH:mm} | Score: {article.SentimentScore:F2} | Publisher: {article.Publisher}");
                        
                        if (!string.IsNullOrEmpty(article.Summary) && article.Summary.Length > 50)
                        {
                            var summary = article.Summary.Length > 120 ? 
                                article.Summary.Substring(0, 120) + "..." : 
                                article.Summary;
                            Console.WriteLine($"      Summary: {summary}");
                        }
                        
                        if (article.KeyTopics?.Any() == true)
                        {
                            Console.WriteLine($"      Topics: {string.Join(", ", article.KeyTopics.Take(3))}");
                        }
                        
                        Console.WriteLine($"      Link: {article.Link}");
                    }
                    
                    Console.WriteLine($"\nAnalysis Time: {analysis.AnalysisDate:MMM dd, yyyy HH:mm} UTC");
                }
            }
            else
            {
                Console.WriteLine("ERROR: Unable to perform sentiment analysis. No news data available.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: Error performing sentiment analysis: {ex.Message}");
        }
        
        PrintSectionFooter();
    }

    private async Task MarketSentimentCommand(string[] parts)
    {
        PrintSectionHeader("AI-Powered Overall Market Sentiment Analysis");

        try
        {
            var analysis = await _newsSentimentService.AnalyzeMarketSentimentAsync();
            
            if (analysis != null)
            {
                // Display overall market sentiment
                var sentimentEmoji = analysis.OverallSentiment switch
                {
                    "POSITIVE" => "[POS]",
                    "NEGATIVE" => "[NEG]",
                    "NEUTRAL" => "[NEUT]",
                    _ => "[UNKNOWN]"
                };
                
                Console.WriteLine($"\nOverall Market Sentiment: {sentimentEmoji} {analysis.OverallSentiment} (Score: {analysis.SentimentScore:F2})");
                Console.WriteLine($"📈 Confidence Level: {analysis.Confidence:F1}%");
                
                // Display key market themes
                if (analysis.KeyThemes?.Any() == true)
                {
                    Console.WriteLine($"\n🔍 Key Market Themes:");
                    foreach (var theme in analysis.KeyThemes)
                    {
                        Console.WriteLine($"   • {theme}");
                    }
                }
                
                // Display market analysis summary
                if (!string.IsNullOrEmpty(analysis.Summary))
                {
                    Console.WriteLine($"\n📝 Market Analysis Summary:");
                    Console.WriteLine($"   {analysis.Summary}");
                }
                
                // Display analysis scope
                if (analysis.NewsItems?.Any() == true)
                {
                    Console.WriteLine($"\n📰 Analyzed {analysis.NewsItems.Count} market news articles");
                    Console.WriteLine($"⏰ Analysis Time: {analysis.AnalysisDate:MMM dd, yyyy HH:mm} UTC");
                }
                
                Console.WriteLine($"\n💡 This analysis combines news from multiple financial sources");
                Console.WriteLine($"   and uses AI to provide comprehensive market sentiment insights.");
            }
            else
            {
                Console.WriteLine("❌ Unable to perform market sentiment analysis. No market news data available.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error performing market sentiment analysis: {ex.Message}");
        }
        
        PrintSectionFooter();
    }

    // Reddit Commands
    private async Task RedditSentimentCommand(string[] parts)
    {
        var subreddit = parts.Length > 1 ? parts[1] : "wallstreetbets";
        var symbol = parts.Length > 2 ? parts[2] : "";
        
        PrintSectionHeader($"Reddit Sentiment Analysis - r/{subreddit}" + (string.IsNullOrEmpty(symbol) ? "" : $" for {symbol}"));

        try
        {
            var sentiment = await _redditScrapingService.AnalyzeSubredditSentimentAsync(subreddit, symbol);
            
            if (sentiment != null)
            {
                var sentimentEmoji = sentiment.OverallSentiment switch
                {
                    "BULLISH" => "[📈]",
                    "BEARISH" => "[📉]", 
                    "NEUTRAL" => "[➡️]",
                    _ => "[❓]"
                };
                
                Console.WriteLine($"Overall Sentiment: {sentimentEmoji} {sentiment.OverallSentiment}");
                Console.WriteLine($"Sentiment Score: {sentiment.SentimentScore:F2}");
                Console.WriteLine($"Posts Analyzed: {sentiment.TotalPosts}");
                Console.WriteLine($"Analysis Time: {sentiment.AnalysisDate:MMM dd, yyyy HH:mm} UTC");
                Console.WriteLine($"Average Score: {sentiment.AverageScore:F1}");
                Console.WriteLine($"Total Upvotes: {sentiment.TotalUpvotes:N0}");
                Console.WriteLine($"Total Comments: {sentiment.TotalComments:N0}");
            }
            else
            {
                Console.WriteLine("❌ Unable to analyze Reddit sentiment. No data available.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error analyzing Reddit sentiment: {ex.Message}");
        }
        
        PrintSectionFooter();
    }

    private async Task RedditScrapeCommand(string[] parts)
    {
        var subreddit = parts.Length > 1 ? parts[1] : "wallstreetbets";
        
        PrintSectionHeader($"Reddit Scraping - r/{subreddit}");

        try
        {
            var posts = await _redditScrapingService.ScrapeSubredditAsync(subreddit, 25);
            
            if (posts?.Any() == true)
            {
                Console.WriteLine($"Found {posts.Count} posts from r/{subreddit}:\n");
                
                foreach (var post in posts.Take(10)) // Show first 10
                {
                    Console.WriteLine($"🔥 {post.Score} | {post.Title}");
                    Console.WriteLine($"   💬 {post.Comments} comments | Posted: {post.CreatedUtc:MMM dd, HH:mm}");
                    Console.WriteLine($"   🔗 {post.Url}");
                    Console.WriteLine();
                }
                
                if (posts.Count > 10)
                {
                    Console.WriteLine($"... and {posts.Count - 10} more posts");
                }
            }
            else
            {
                Console.WriteLine("❌ No posts found or unable to scrape subreddit.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error scraping Reddit: {ex.Message}");
        }
        
        PrintSectionFooter();
    }

    // Portfolio Optimization Commands
    private async Task OptimizePortfolioCommand(string[] parts)
    {
        if (parts.Length < 2)
        {
            Console.WriteLine("Usage: optimize-portfolio AAPL,MSFT,GOOGL,TSLA");
            return;
        }

        var tickers = parts[1].Split(',', StringSplitOptions.RemoveEmptyEntries);
        
        PrintSectionHeader($"Portfolio Optimization - {string.Join(", ", tickers)}");

        try
        {
            var result = await _portfolioOptimizationService.OptimizePortfolioAsync(tickers);
            
            if (result != null)
            {
                Console.WriteLine($"📊 Optimization Results (as of {result.OptimizationDate:MMM dd, yyyy HH:mm} UTC):");
                Console.WriteLine($"Expected Return: {result.ExpectedReturn:P2}");
                Console.WriteLine($"Risk (Volatility): {result.Risk:P2}");
                Console.WriteLine($"Sharpe Ratio: {result.SharpeRatio:F4}");
                Console.WriteLine();
                
                Console.WriteLine("🎯 Optimized Portfolio Weights:");
                foreach (var kvp in result.OptimizedWeights)
                {
                    Console.WriteLine($"   {kvp.Key}: {kvp.Value:P1}");
                }
                
                Console.WriteLine();
                Console.WriteLine("📈 Expected Individual Returns:");
                foreach (var kvp in result.ExpectedReturns)
                {
                    Console.WriteLine($"   {kvp.Key}: {kvp.Value:P2}");
                }
                
                Console.WriteLine($"\n💡 Note: This uses equal weighting due to limited historical data access.");
                Console.WriteLine($"   For production use, implement proper mean-variance optimization.");
            }
            else
            {
                Console.WriteLine("❌ Unable to optimize portfolio. Insufficient data.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error optimizing portfolio: {ex.Message}");
        }
        
        PrintSectionFooter();
    }

    // Advanced Data Extraction Commands
    private async Task ExtractWebDataCommand(string[] parts)
    {
        if (parts.Length < 2)
        {
            Console.WriteLine("Usage: extract-web-data [url]");
            return;
        }

        var url = parts[1];
        PrintSectionHeader($"Web Data Extraction - {url}");

        try
        {
            var extractedData = await _webDataExtractionService.ExtractStructuredDataAsync(url);
            
            if (extractedData != null)
            {
                Console.WriteLine($"🌐 Data extracted from: {extractedData.Url}");
                Console.WriteLine($"📄 Page Title: {extractedData.Title}");
                Console.WriteLine($"📅 Extraction Date: {extractedData.ExtractedAt:MMM dd, yyyy HH:mm} UTC");
                Console.WriteLine($"� Data Type: {extractedData.DataType}");
                Console.WriteLine($"�🔢 Structured Data: {extractedData.StructuredData?.Count ?? 0} items");
                Console.WriteLine($"📊 Tables: {extractedData.Tables?.Count ?? 0}");
                Console.WriteLine($"💰 Financial Data: {extractedData.FinancialData?.Count ?? 0} items");

                // Special handling for academic papers
                if (extractedData.DataType == "PDF" && extractedData.StructuredData?.ContainsKey("document_type") == true && 
                    extractedData.StructuredData["document_type"]?.ToString() == "arxiv_paper")
                {
                    await DisplayAcademicPaperAnalysis(extractedData);
                }
                else
                {
                    // Standard web content display
                    await DisplayStandardWebContent(extractedData);
                }
            }
            else
            {
                Console.WriteLine("❌ Unable to extract data from the specified URL.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error extracting web data: {ex.Message}");
        }
        
        PrintSectionFooter();
    }

    private async Task DisplayAcademicPaperAnalysis(WebDataExtractionResult extractedData)
    {
        Console.WriteLine("\n🎓 ACADEMIC PAPER ANALYSIS");
        Console.WriteLine("=" + new string('=', 50));

        // Show basic paper information
        if (extractedData.StructuredData?.ContainsKey("abstract") == true)
        {
            Console.WriteLine("\n📝 Abstract:");
            Console.WriteLine($"   {extractedData.StructuredData["abstract"]}");
        }

        if (extractedData.StructuredData?.ContainsKey("keywords") == true)
        {
            Console.WriteLine("\n🏷️  Keywords:");
            var keywords = extractedData.StructuredData["keywords"] as List<object> ?? new List<object>();
            Console.WriteLine($"   {string.Join(", ", keywords.Take(10))}");
        }

        if (extractedData.StructuredData?.ContainsKey("sections") == true)
        {
            Console.WriteLine("\n📚 Paper Sections:");
            var sections = extractedData.StructuredData["sections"] as List<object> ?? new List<object>();
            foreach (var section in sections.Take(8))
            {
                Console.WriteLine($"   • {section}");
            }
        }

        // Show AI analysis results
        if (extractedData.StructuredData?.ContainsKey("ai_analysis") == true)
        {
            var aiAnalysis = extractedData.StructuredData["ai_analysis"] as Dictionary<string, object>;
            if (aiAnalysis != null)
            {
                await DisplayAIAnalysisResults(aiAnalysis);
            }
        }
        else
        {
            Console.WriteLine("\n⚠️  AI analysis not available - this may indicate an analysis error or missing OpenAI API key.");
        }
    }

    private async Task DisplayAIAnalysisResults(Dictionary<string, object> aiAnalysis)
    {
        Console.WriteLine("\n🤖 AI-POWERED DEEP ANALYSIS");
        Console.WriteLine("=" + new string('=', 50));

        // Summary
        if (aiAnalysis.ContainsKey("summary"))
        {
            Console.WriteLine("\n📋 EXECUTIVE SUMMARY");
            Console.WriteLine(new string('-', 30));
            Console.WriteLine(FormatAnalysisText(aiAnalysis["summary"]?.ToString()));
        }

        // Strategy Blueprint
        if (aiAnalysis.ContainsKey("strategy_blueprint"))
        {
            Console.WriteLine("\n🎯 STRATEGY BLUEPRINT");
            Console.WriteLine(new string('-', 30));
            Console.WriteLine(FormatAnalysisText(aiAnalysis["strategy_blueprint"]?.ToString()));
        }

        // Implementation Guide
        if (aiAnalysis.ContainsKey("implementation"))
        {
            Console.WriteLine("\n⚙️  IMPLEMENTATION PSEUDOCODE");
            Console.WriteLine(new string('-', 30));
            Console.WriteLine(FormatAnalysisText(aiAnalysis["implementation"]?.ToString()));
        }

        // Methodology
        if (aiAnalysis.ContainsKey("methodology"))
        {
            Console.WriteLine("\n🔬 METHODOLOGY BREAKDOWN");
            Console.WriteLine(new string('-', 30));
            Console.WriteLine(FormatAnalysisText(aiAnalysis["methodology"]?.ToString()));
        }

        // Key Contributions
        if (aiAnalysis.ContainsKey("key_contributions"))
        {
            Console.WriteLine("\n💡 KEY CONTRIBUTIONS");
            Console.WriteLine(new string('-', 30));
            Console.WriteLine(FormatAnalysisText(aiAnalysis["key_contributions"]?.ToString()));
        }

        // Practical Applications
        if (aiAnalysis.ContainsKey("practical_applications"))
        {
            Console.WriteLine("\n🏦 PRACTICAL APPLICATIONS");
            Console.WriteLine(new string('-', 30));
            Console.WriteLine(FormatAnalysisText(aiAnalysis["practical_applications"]?.ToString()));
        }

        // Limitations
        if (aiAnalysis.ContainsKey("limitations"))
        {
            Console.WriteLine("\n⚠️  LIMITATIONS & CHALLENGES");
            Console.WriteLine(new string('-', 30));
            Console.WriteLine(FormatAnalysisText(aiAnalysis["limitations"]?.ToString()));
        }

        // Future Work
        if (aiAnalysis.ContainsKey("future_work"))
        {
            Console.WriteLine("\n� FUTURE RESEARCH DIRECTIONS");
            Console.WriteLine(new string('-', 30));
            Console.WriteLine(FormatAnalysisText(aiAnalysis["future_work"]?.ToString()));
        }
    }

    private async Task DisplayStandardWebContent(WebDataExtractionResult extractedData)
    {
        if (!string.IsNullOrEmpty(extractedData.Content) && extractedData.Content.Length > 50)
        {
            Console.WriteLine($"\n📝 Content Preview:");
            var preview = extractedData.Content.Length > 200 ? 
                extractedData.Content.Substring(0, 200) + "..." : 
                extractedData.Content;
            Console.WriteLine($"   {preview}");
        }
        
        if (extractedData.StructuredData?.Any() == true)
        {
            Console.WriteLine($"\n🔍 Structured Data:");
            foreach (var kvp in extractedData.StructuredData.Take(10))
            {
                Console.WriteLine($"   • {kvp.Key}: {kvp.Value}");
            }
        }
        
        if (extractedData.FinancialData?.Any() == true)
        {
            Console.WriteLine($"\n💰 Financial Data:");
            foreach (var kvp in extractedData.FinancialData.Take(5))
            {
                Console.WriteLine($"   • {kvp.Key}: {kvp.Value}");
            }
        }
        
        if (extractedData.Tables?.Any() == true)
        {
            Console.WriteLine($"\n📊 Extracted Tables:");
            foreach (var table in extractedData.Tables.Take(3))
            {
                Console.WriteLine($"   Table with {table.Headers?.Count ?? 0} columns and {table.Rows?.Count ?? 0} rows");
                if (table.Headers?.Any() == true)
                {
                    Console.WriteLine($"   Headers: {string.Join(", ", table.Headers.Take(5))}");
                }
            }
        }
    }

    private string FormatAnalysisText(string? text)
    {
        if (string.IsNullOrEmpty(text)) return "   Analysis not available.";

        // Split into paragraphs and add proper indentation
        var paragraphs = text.Split(new[] { "\n\n", "\r\n\r\n" }, StringSplitOptions.RemoveEmptyEntries);
        var formatted = new List<string>();

        foreach (var paragraph in paragraphs)
        {
            var cleanParagraph = paragraph.Replace("\n", " ").Replace("\r", " ").Trim();
            if (!string.IsNullOrEmpty(cleanParagraph))
            {
                // Wrap long lines
                var wrapped = WrapText(cleanParagraph, 90);
                formatted.Add("   " + wrapped.Replace("\n", "\n   "));
            }
        }

        return string.Join("\n\n", formatted);
    }

    private string WrapText(string text, int maxWidth)
    {
        if (text.Length <= maxWidth) return text;

        var words = text.Split(' ');
        var lines = new List<string>();
        var currentLine = "";

        foreach (var word in words)
        {
            if ((currentLine + " " + word).Length <= maxWidth)
            {
                currentLine += (currentLine.Length > 0 ? " " : "") + word;
            }
            else
            {
                if (currentLine.Length > 0)
                {
                    lines.Add(currentLine);
                    currentLine = word;
                }
                else
                {
                    lines.Add(word); // Word is longer than maxWidth
                }
            }
        }

        if (currentLine.Length > 0)
        {
            lines.Add(currentLine);
        }

        return string.Join("\n", lines);
    }

    private async Task GenerateReportCommand(string[] parts)
    {
        if (parts.Length < 2)
        {
            Console.WriteLine("Usage: generate-report [symbol/portfolio] [optional_report_type]");
            Console.WriteLine("Report types: stock-analysis, earnings-analysis, technical-analysis, portfolio-analysis, market-sector");
            return;
        }

        var target = parts[1];
        var reportTypeStr = parts.Length > 2 ? parts[2] : "stock-analysis";
        
        // Convert string to enum
        var reportType = reportTypeStr.ToLower() switch
        {
            "stock-analysis" => ReportType.StockAnalysis,
            "earnings-analysis" => ReportType.EarningsAnalysis,
            "technical-analysis" => ReportType.TechnicalAnalysis,
            "portfolio-analysis" => ReportType.PortfolioAnalysis,
            "market-sector" => ReportType.MarketSector,
            _ => ReportType.StockAnalysis
        };
        
        PrintSectionHeader($"Report Generation - {target} ({reportType})");

        try
        {
            var report = await _reportGenerationService.GenerateComprehensiveReportAsync(target, reportType);
            
            if (report != null)
            {
                Console.WriteLine($"📊 Report Generated: {report.Title}");
                Console.WriteLine($"🎯 Symbol: {report.Symbol}");
                Console.WriteLine($"📋 Type: {report.ReportType}");
                Console.WriteLine($"📅 Generated: {report.GeneratedAt:MMM dd, yyyy HH:mm} UTC");
                Console.WriteLine($"📏 Sections: {report.Sections?.Count ?? 0}");
                
                if (!string.IsNullOrEmpty(report.ExecutiveSummary))
                {
                    Console.WriteLine($"\n📝 Executive Summary:");
                    Console.WriteLine($"   {report.ExecutiveSummary}");
                }
                
                if (report.Sections?.Any() == true)
                {
                    Console.WriteLine($"\n� Report Sections:");
                    foreach (var section in report.Sections.Take(5))
                    {
                        Console.WriteLine($"   • {section.Title} ({section.SectionType})");
                        if (section.Charts?.Any() == true)
                        {
                            Console.WriteLine($"     Charts: {section.Charts.Count}");
                        }
                        if (section.Tables?.Any() == true)
                        {
                            Console.WriteLine($"     Tables: {section.Tables.Count}");
                        }
                    }
                }
                
                Console.WriteLine($"\n📄 Full report generated with {report.Sections?.Count ?? 0} detailed sections");
                Console.WriteLine($"📁 Export formats available: HTML, Markdown, JSON");
                if (!string.IsNullOrEmpty(report.HtmlContent))
                {
                    Console.WriteLine($"📄 HTML content: {report.HtmlContent.Length} characters");
                }
                if (!string.IsNullOrEmpty(report.MarkdownContent))
                {
                    Console.WriteLine($"📄 Markdown content: {report.MarkdownContent.Length} characters");
                }
                
                // Display saved file paths
                if (report.SavedFilePaths?.Any() == true)
                {
                    Console.WriteLine($"\n💾 Report saved to files:");
                    foreach (var (format, path) in report.SavedFilePaths)
                    {
                        Console.WriteLine($"   📄 {format.ToUpper()}: {path}");
                    }
                    Console.WriteLine($"📂 Reports directory: {Path.Combine(Directory.GetCurrentDirectory(), "reports")}");
                };
            }
            else
            {
                Console.WriteLine("❌ Unable to generate report. Insufficient data.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error generating report: {ex.Message}");
        }
        
        PrintSectionFooter();
    }

    private async Task AnalyzeSatelliteImageryCommand(string[] parts)
    {
        if (parts.Length < 2)
        {
            Console.WriteLine("Usage: analyze-satellite-imagery [symbol]");
            return;
        }

        var symbol = parts[1].ToUpper();
        PrintSectionHeader($"Satellite Imagery Analysis - {symbol}");

        try
        {
            var analysis = await _satelliteImageryAnalysisService.AnalyzeCompanyOperationsAsync(symbol);
            
            if (analysis != null)
            {
                Console.WriteLine($"🛰️ Satellite Analysis for: {analysis.Symbol}");
                Console.WriteLine($"🏢 Company: {analysis.CompanyName}");
                Console.WriteLine($"📅 Analysis Date: {analysis.AnalysisDate:MMM dd, yyyy HH:mm} UTC");
                Console.WriteLine($"🏭 Facilities Analyzed: {analysis.Facilities?.Count ?? 0}");
                
                if (analysis.Metrics != null)
                {
                    Console.WriteLine($"\n📊 Satellite Metrics:");
                    Console.WriteLine($"   Total Facilities: {analysis.Metrics.TotalFacilities}");
                    Console.WriteLine($"   Average Activity Level: {analysis.Metrics.AverageActivityLevel:F2}");
                    Console.WriteLine($"   Capacity Utilization: {analysis.Metrics.AverageCapacityUtilization:F2}");
                    Console.WriteLine($"   Operational Efficiency: {analysis.Metrics.OperationalEfficiencyScore:F2}");
                }
                
                if (analysis.Facilities?.Any() == true)
                {
                    Console.WriteLine($"\n🏭 Facility Analyses:");
                    foreach (var facility in analysis.Facilities.Take(5))
                    {
                        Console.WriteLine($"   📍 {facility.Facility.Name} ({facility.Facility.Address})");
                        Console.WriteLine($"      Activity Level: {facility.ActivityLevel:F2}");
                        Console.WriteLine($"      Capacity Utilization: {facility.CapacityUtilization:F2}");
                        Console.WriteLine($"      Vehicle Count: {facility.VehicleCount}");
                    }
                }
                
                if (analysis.OperationalInsights?.Any() == true)
                {
                    Console.WriteLine($"\n🔍 Operational Insights:");
                    foreach (var insight in analysis.OperationalInsights.Take(5))
                    {
                        Console.WriteLine($"   • {insight.Category}: {insight.Insight}");
                        Console.WriteLine($"     Confidence: {insight.Confidence:F1}, Impact: {insight.ImpactLevel}");
                    }
                }
                
                if (!string.IsNullOrEmpty(analysis.AnalysisSummary))
                {
                    Console.WriteLine($"\n� Analysis Summary:");
                    Console.WriteLine($"   {analysis.AnalysisSummary}");
                }
                
                Console.WriteLine($"\n📡 Note: Satellite imagery analysis provides operational insights");
                Console.WriteLine($"   that may not be reflected in traditional financial metrics.");
            }
            else
            {
                Console.WriteLine("❌ Unable to analyze satellite imagery. Company facilities not found.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error analyzing satellite imagery: {ex.Message}");
        }
        
        PrintSectionFooter();
    }

    private async Task ScrapeSocialMediaCommand(string[] parts)
    {
        if (parts.Length < 2)
        {
            Console.WriteLine("Usage: scrape-social-media [symbol]");
            return;
        }

        var symbol = parts[1].ToUpper();
        var platforms = new List<SocialMediaPlatform> 
        { 
            SocialMediaPlatform.Twitter, 
            SocialMediaPlatform.Reddit, 
            SocialMediaPlatform.StockTwits 
        };
        
        PrintSectionHeader($"Social Media Sentiment Analysis - {symbol}");

        try
        {
            var analysis = await _socialMediaScrapingService.AnalyzeSocialMediaSentimentAsync(symbol, platforms);
            
            if (analysis != null)
            {
                var sentimentEmoji = analysis.OverallMetrics.OverallSentimentScore switch
                {
                    > 0.7 => "🚀",
                    > 0.6 => "📈",
                    > 0.4 => "➡️",
                    > 0.3 => "📉",
                    _ => "💥"
                };
                
                Console.WriteLine($"📱 Social Media Analysis for: {analysis.Symbol}");
                Console.WriteLine($"📅 Analysis Date: {analysis.AnalysisDate:MMM dd, yyyy HH:mm} UTC");
                Console.WriteLine($"⏰ Time Range: {analysis.TimeRange.Days} days");
                Console.WriteLine($"📊 Platforms: {analysis.PlatformAnalyses.Count}");
                
                Console.WriteLine($"\n{sentimentEmoji} Overall Sentiment: {analysis.OverallMetrics.OverallSentimentScore:F2} (Score: 0-1)");
                Console.WriteLine($"📈 Total Posts: {analysis.OverallMetrics.TotalPosts:N0}");
                Console.WriteLine($"💬 Total Engagement: {analysis.OverallMetrics.TotalEngagement:N0}");
                Console.WriteLine($"📊 Post Frequency: {analysis.OverallMetrics.AveragePostFrequency:F1} posts/day");
                
                if (analysis.OverallMetrics.SentimentDistribution?.Any() == true)
                {
                    Console.WriteLine($"\n📊 Sentiment Distribution:");
                    foreach (var kvp in analysis.OverallMetrics.SentimentDistribution)
                    {
                        Console.WriteLine($"   {kvp.Key}: {kvp.Value:P1}");
                    }
                }
                
                if (analysis.PlatformAnalyses?.Any() == true)
                {
                    Console.WriteLine($"\n📱 Platform Breakdown:");
                    foreach (var platform in analysis.PlatformAnalyses)
                    {
                        var platformEmoji = platform.Platform switch
                        {
                            SocialMediaPlatform.Twitter => "🐦",
                            SocialMediaPlatform.Reddit => "🤖",
                            SocialMediaPlatform.StockTwits => "📊",
                            _ => "📱"
                        };
                        
                        Console.WriteLine($"   {platformEmoji} {platform.Platform}:");
                        Console.WriteLine($"      Posts: {platform.PostCount:N0}");
                        Console.WriteLine($"      Sentiment: {platform.SentimentScore:F2}");
                        Console.WriteLine($"      Engagement: {platform.EngagementMetrics.TotalLikes + platform.EngagementMetrics.TotalComments:N0}");
                    }
                }
                
                if (analysis.TrendingTopics?.Any() == true)
                {
                    Console.WriteLine($"\n🔥 Trending Topics:");
                    foreach (var topic in analysis.TrendingTopics.Take(5))
                    {
                        Console.WriteLine($"   #{topic.Topic} (Mentions: {topic.MentionCount})");
                    }
                }
                
                if (analysis.AIInsights?.Any() == true)
                {
                    Console.WriteLine($"\n🤖 AI Insights:");
                    foreach (var insight in analysis.AIInsights.Take(3))
                    {
                        Console.WriteLine($"   • {insight}");
                    }
                }
                
                Console.WriteLine($"\n💡 Note: Social media sentiment can be highly volatile and may not");
                Console.WriteLine($"   directly correlate with stock price movements.");
            }
            else
            {
                Console.WriteLine("❌ Unable to analyze social media sentiment. No data available.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error analyzing social media: {ex.Message}");
        }
        
        PrintSectionFooter();
    }
}
