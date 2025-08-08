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
        FinvizNewsService finvizNewsService)
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
        Console.WriteLine(" 23. test-apis [symbol] - Test API connectivity and configuration");
        Console.WriteLine(" 24. polygon-data [symbol] - Get Polygon.io market data");
        Console.WriteLine(" 25. polygon-news [symbol] - Get Polygon.io news for symbol");
        Console.WriteLine(" 26. polygon-financials [symbol] - Get Polygon.io financial data");
        Console.WriteLine(" 27. databento-ohlcv [symbol] [days] - Get DataBento OHLCV data");
        Console.WriteLine(" 28. databento-futures [symbol] - Get DataBento futures contracts");
        Console.WriteLine(" 29. live-news [symbol/keyword] - Get live financial news");
        Console.WriteLine(" 30. clear - Clear terminal and show menu");
        Console.WriteLine(" 31. help - Show available functions");
        Console.WriteLine(" 32. quit - Exit the application");
        Console.WriteLine();

        while (true)
        {
            Console.Write("agent> ");
            var input = Console.ReadLine()?.Trim();

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
            Console.WriteLine("❌ Invalid function format. Use: PluginName.FunctionName [param=value]");
            return;
        }

        var functionParts = parts[0].Split('.');
        if (functionParts.Length != 2)
        {
            Console.WriteLine("❌ Invalid function format. Use: PluginName.FunctionName [param=value]");
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
            Console.WriteLine($"❌ Function execution failed: {ex.Message}");
        }
    }

    private async Task RunTestSequence()
    {
        Console.WriteLine("🧪 Running test sequence...");
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

        return Task.FromResult(new InteractiveCLI(kernel, orchestrator, logger, comprehensiveAgent, researchAgent, yahooFinanceService, alpacaService, polygonService, dataBentoService, yfinanceNewsService, finvizNewsService));
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
        Console.WriteLine(" 23. test-apis [symbol] - Test API connectivity and configuration");
        Console.WriteLine(" 24. polygon-data [symbol] - Get Polygon.io market data");
        Console.WriteLine(" 25. polygon-news [symbol] - Get Polygon.io news for symbol");
        Console.WriteLine(" 26. polygon-financials [symbol] - Get Polygon.io financial data");
        Console.WriteLine(" 27. databento-ohlcv [symbol] [days] - Get DataBento OHLCV data");
        Console.WriteLine(" 28. databento-futures [symbol] - Get DataBento futures contracts");
        Console.WriteLine(" 29. live-news [symbol/keyword] - Get live financial news");
        Console.WriteLine(" 30. clear - Clear terminal and show menu");
        Console.WriteLine(" 31. help - Show available functions");
        Console.WriteLine(" 32. quit - Exit the application");
        Console.WriteLine();
        
        // Since this is an async method, we need to return a completed task
        await Task.CompletedTask;
    }

    private async Task TestApisCommand(string[] parts)
    {
        var symbol = parts.Length > 1 ? parts[1] : "AAPL";
        
        PrintSectionHeader($"API Connectivity Test - {symbol}");
        
        try
        {
            // Test Yahoo Finance
            Console.WriteLine("Testing Yahoo Finance API...");
            var yahooData = await _yahooFinanceService.GetMarketDataAsync(symbol);
            Console.WriteLine(yahooData != null ? "✓ Yahoo Finance: Connected" : "✗ Yahoo Finance: Failed");
            
            // Test Alpaca
            Console.WriteLine("Testing Alpaca API...");
            var alpacaData = await _alpacaService.GetMarketDataAsync(symbol);
            Console.WriteLine(alpacaData != null ? "✓ Alpaca: Connected" : "✗ Alpaca: Failed");
            
            // Test Polygon
            Console.WriteLine("Testing Polygon.io API...");
            var polygonData = await _polygonService.GetQuoteAsync(symbol);
            Console.WriteLine(polygonData != null ? "✓ Polygon.io: Connected" : "✗ Polygon.io: Failed");
            
            // Test DataBento
            Console.WriteLine("Testing DataBento API...");
            var start = DateTime.Now.AddDays(-7);
            var end = DateTime.Now;
            var dataBentoData = await _dataBentoService.GetOHLCVAsync(symbol, start, end);
            Console.WriteLine(dataBentoData != null && dataBentoData.Any() ? "✓ DataBento: Connected" : "✗ DataBento: Failed");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error testing APIs: {ex.Message}");
        }
        
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
                    Console.WriteLine($"📰 {article.Title}");
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
                    
                    Console.WriteLine($"   🔗 {article.Link}");
                    Console.WriteLine();
                }
            }
            
            // Display Finviz News
            if (finvizNews.Any())
            {
                Console.WriteLine("\n=== Finviz News ===");
                foreach (var article in finvizNews.Take(6))
                {
                    Console.WriteLine($"📈 {article.Title}");
                    Console.WriteLine($"   Publisher: {article.Publisher} | {article.PublishedDate:MMM dd, HH:mm}");
                    
                    if (!string.IsNullOrEmpty(article.Summary))
                    {
                        var summary = article.Summary.Length > 150 ? 
                            article.Summary.Substring(0, 150) + "..." : 
                            article.Summary;
                        Console.WriteLine($"   {summary}");
                    }
                    
                    Console.WriteLine($"   🔗 {article.Link}");
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
}

