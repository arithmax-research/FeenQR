using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using QuantResearchAgent.Core;
using QuantResearchAgent.Services;
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
    
    // ... existing code ...
    public InteractiveCLI(Kernel kernel, AgentOrchestrator orchestrator, ILogger<InteractiveCLI> logger)
    {
        _kernel = kernel;
        _orchestrator = orchestrator;
        _logger = logger;
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
        Console.WriteLine("  5. market-data [symbol] - Get market data");
        Console.WriteLine("  7. yahoo-data [symbol] - Get Yahoo Finance market data");
        Console.WriteLine("  8. portfolio - View portfolio summary");
        Console.WriteLine("  9. risk-assessment - Assess portfolio risk");
        Console.WriteLine(" 10. alpaca-data [symbol] - Get Alpaca market data");
        Console.WriteLine(" 11. alpaca-historical [symbol] [days] - Get historical data");
        Console.WriteLine(" 12. alpaca-account - View Alpaca account info");
        Console.WriteLine(" 13. alpaca-positions - View current positions");
        Console.WriteLine(" 14. alpaca-quotes [symbols] - Get multiple quotes");
        Console.WriteLine(" 15. ta-indicators [symbol] [category] - Detailed indicators");
        Console.WriteLine(" 16. ta-compare [symbols] - Compare TA of multiple symbols");
        Console.WriteLine(" 17. ta-patterns [symbol] - Pattern recognition analysis");
        Console.WriteLine(" 18. help - Show available functions");
        Console.WriteLine(" 19. quit - Exit the application");
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

        return Task.FromResult(new InteractiveCLI(kernel, orchestrator, logger));
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
}

