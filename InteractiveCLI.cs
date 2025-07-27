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
        Console.WriteLine("Quant Research Agent - Interactive CLI");
        Console.WriteLine("=========================================");
        Console.WriteLine();
        Console.WriteLine("Available commands:");
        Console.WriteLine("  1. analyze-video [url] - Analyze a YouTube video");
        Console.WriteLine("  2. get-quantopian-videos - Get latest Quantopian videos");
        Console.WriteLine("  3. search-finance-videos [query] - Search finance videos");
        Console.WriteLine("  4. generate-signals [symbol] - Generate trading signals");
        Console.WriteLine("  5. market-data [symbol] - Get market data");
        Console.WriteLine("  6. yahoo-data [symbol] - Get Yahoo Finance market data");
        Console.WriteLine("  6. portfolio - View portfolio summary");
        Console.WriteLine("  7. risk-assessment - Assess portfolio risk");
        Console.WriteLine("  8. alpaca-data [symbol] - Get Alpaca market data");
        Console.WriteLine("  9. alpaca-historical [symbol] [days] - Get historical data");
        Console.WriteLine(" 10. alpaca-account - View Alpaca account info");
        Console.WriteLine(" 11. alpaca-positions - View current positions");
        Console.WriteLine(" 12. alpaca-quotes [symbols] - Get multiple quotes");
        Console.WriteLine(" 13. technical-analysis [symbol] - Comprehensive TA");
        Console.WriteLine(" 14. ta-indicators [symbol] [category] - Detailed indicators");
        Console.WriteLine(" 15. ta-compare [symbols] - Compare TA of multiple symbols");
        Console.WriteLine(" 16. ta-patterns [symbol] - Pattern recognition analysis");
        Console.WriteLine(" 17. help - Show available functions");
        Console.WriteLine(" 18. quit - Exit the application");
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
                case "generate-signals":
                    await GenerateSignalsCommand(parts);
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
                case "technical-analysis":
                    await TechnicalAnalysisCommand(parts);
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
                    // Try to execute as a direct Semantic Kernel function call
                    await ExecuteSemanticFunction(input);
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            _logger.LogError(ex, "Error processing command: {Command}", input);
        }
    }

    private async Task AnalyzeVideoCommand(string[] parts)
    {
        var url = parts.Length > 1 ? parts[1] : "https://www.youtube.com/watch?v=dQw4w9WgXcQ";
        
        Console.WriteLine($"Analyzing YouTube video: {url}");
        
        var function = _kernel.Plugins["YouTubeAnalysisPlugin"]["AnalyzeVideo"];
        var result = await _kernel.InvokeAsync(function, new() { ["videoUrl"] = url });
        
        Console.WriteLine(result.ToString());
    }

    private async Task GetQuantopianVideosCommand()
    {
        Console.WriteLine("Getting latest Quantopian videos...");
        
        var function = _kernel.Plugins["YouTubeAnalysisPlugin"]["GetLatestQuantopianVideos"];
        var result = await _kernel.InvokeAsync(function, new() { ["maxResults"] = 5 });
        
        Console.WriteLine(result.ToString());
    }

    private async Task SearchFinanceVideosCommand(string[] parts)
    {
        var query = parts.Length > 1 ? string.Join(" ", parts.Skip(1)) : "quantitative trading";
        
        Console.WriteLine($"🔍 Searching finance videos for: {query}");
        
        var function = _kernel.Plugins["YouTubeAnalysisPlugin"]["SearchFinanceVideos"];
        var result = await _kernel.InvokeAsync(function, new() { ["query"] = query, ["maxResults"] = 5 });
        
        Console.WriteLine(result.ToString());
    }

    private async Task GenerateSignalsCommand(string[] parts)
    {
        var symbol = parts.Length > 1 ? parts[1] : null;
        
        Console.WriteLine(symbol != null ? $"Generating signals for {symbol}..." : "Generating signals for all symbols...");
        
        var function = _kernel.Plugins["TradingPlugin"]["GenerateTradingSignals"];
        var result = await _kernel.InvokeAsync(function, new() { ["symbol"] = symbol });
        
        Console.WriteLine(result.ToString());
    }

    private async Task MarketDataCommand(string[] parts)
    {
        var symbol = parts.Length > 1 ? parts[1] : "BTCUSDT";
        
        Console.WriteLine($"Getting market data for {symbol}...");
        
        var function = _kernel.Plugins["MarketDataPlugin"]["GetMarketData"];
        var result = await _kernel.InvokeAsync(function, new() { ["symbol"] = symbol });
        
        Console.WriteLine(result.ToString());
    }

    private async Task PortfolioCommand()
    {
        Console.WriteLine("Getting portfolio summary...");
        
        var function = _kernel.Plugins["RiskManagementPlugin"]["GetPortfolioSummary"];
        var result = await _kernel.InvokeAsync(function);
        
        Console.WriteLine(result.ToString());
    }

    private async Task RiskAssessmentCommand()
    {
        Console.WriteLine("Assessing portfolio risk...");
        
        var function = _kernel.Plugins["RiskManagementPlugin"]["AssessPortfolioRisk"];
        var result = await _kernel.InvokeAsync(function);
        
        Console.WriteLine(result.ToString());
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

        // Test 3: Generate Signals
        Console.WriteLine("3. Testing signal generation...");
        await GenerateSignalsCommand(new[] { "generate-signals", "BTCUSDT" });
        Console.WriteLine();

        // Test 4: Portfolio Summary
        Console.WriteLine("4. Testing portfolio summary...");
        await PortfolioCommand();
        Console.WriteLine();

        // Test 5: Risk Assessment
        Console.WriteLine("5. Testing risk assessment...");
        await RiskAssessmentCommand();
        Console.WriteLine();

        Console.WriteLine("Test sequence completed!");
    }

    private async Task YahooDataCommand(string[] parts)
    {
        var symbol = parts.Length > 1 ? parts[1] : "AAPL";
        Console.WriteLine($"Getting Yahoo Finance data for {symbol}...");
        var function = _kernel.Plugins["MarketDataPlugin"]["GetYahooMarketData"];
        var result = await _kernel.InvokeAsync(function, new() { ["symbol"] = symbol });
        Console.WriteLine(result.ToString());
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
        
        Console.WriteLine($"Getting Alpaca market data for {symbol}...");
        
        var function = _kernel.Plugins["AlpacaPlugin"]["GetMarketData"];
        var result = await _kernel.InvokeAsync(function, new() { ["symbol"] = symbol });
        
        Console.WriteLine(result.ToString());
    }

    private async Task AlpacaHistoricalCommand(string[] parts)
    {
        var symbol = parts.Length > 1 ? parts[1] : "AAPL";
        var days = parts.Length > 2 && int.TryParse(parts[2], out var d) ? d : 30;
        
        Console.WriteLine($"Getting Alpaca historical data for {symbol} ({days} days)...");
        
        var function = _kernel.Plugins["AlpacaPlugin"]["GetHistoricalData"];
        var result = await _kernel.InvokeAsync(function, new() { ["symbol"] = symbol, ["days"] = days });
        
        Console.WriteLine(result.ToString());
    }

    private async Task AlpacaAccountCommand()
    {
        Console.WriteLine("Getting Alpaca account information...");
        
        var function = _kernel.Plugins["AlpacaPlugin"]["GetAccountInfo"];
        var result = await _kernel.InvokeAsync(function);
        
        Console.WriteLine(result.ToString());
    }

    private async Task AlpacaPositionsCommand()
    {
        Console.WriteLine("Getting Alpaca positions...");
        
        var function = _kernel.Plugins["AlpacaPlugin"]["GetPositions"];
        var result = await _kernel.InvokeAsync(function);
        
        Console.WriteLine(result.ToString());
    }

    private async Task AlpacaQuotesCommand(string[] parts)
    {
        var symbols = parts.Length > 1 ? parts[1] : "AAPL,MSFT,GOOGL";
        
        Console.WriteLine($"Getting multiple quotes for: {symbols}...");
        
        var function = _kernel.Plugins["AlpacaPlugin"]["GetMultipleQuotes"];
        var result = await _kernel.InvokeAsync(function, new() { ["symbols"] = symbols });
        
        Console.WriteLine(result.ToString());
    }

    // Technical Analysis Commands
    private async Task TechnicalAnalysisCommand(string[] parts)
    {
        var symbol = parts.Length > 1 ? parts[1] : "AAPL";
        
        Console.WriteLine($"🔍 Performing comprehensive technical analysis for {symbol}...");
        
        var function = _kernel.Plugins["TechnicalAnalysisPlugin"]["AnalyzeSymbol"];
        var result = await _kernel.InvokeAsync(function, new() { ["symbol"] = symbol });
        
        Console.WriteLine(result.ToString());
    }

    private async Task TechnicalIndicatorsCommand(string[] parts)
    {
        var symbol = parts.Length > 1 ? parts[1] : "AAPL";
        var category = parts.Length > 2 ? parts[2] : "all";
        
        Console.WriteLine($"📊 Getting detailed {category} indicators for {symbol}...");
        
        var function = _kernel.Plugins["TechnicalAnalysisPlugin"]["GetDetailedIndicators"];
        var result = await _kernel.InvokeAsync(function, new() { ["symbol"] = symbol, ["category"] = category });
        
        Console.WriteLine(result.ToString());
    }

    private async Task TechnicalCompareCommand(string[] parts)
    {
        var symbols = parts.Length > 1 ? parts[1] : "AAPL,MSFT,GOOGL";
        
        Console.WriteLine($"📈 Comparing technical analysis for: {symbols}...");
        
        var function = _kernel.Plugins["TechnicalAnalysisPlugin"]["CompareSymbols"];
        var result = await _kernel.InvokeAsync(function, new() { ["symbols"] = symbols });
        
        Console.WriteLine(result.ToString());
    }

    private async Task TechnicalPatternsCommand(string[] parts)
    {
        var symbol = parts.Length > 1 ? parts[1] : "AAPL";
        
        Console.WriteLine($"🔍 Analyzing patterns for {symbol}...");
        
        var function = _kernel.Plugins["TechnicalAnalysisPlugin"]["GetPatternAnalysis"];
        var result = await _kernel.InvokeAsync(function, new() { ["symbol"] = symbol });
        
        Console.WriteLine(result.ToString());
    }

}
