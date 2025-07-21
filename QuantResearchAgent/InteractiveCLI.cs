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

    public InteractiveCLI(Kernel kernel, AgentOrchestrator orchestrator, ILogger<InteractiveCLI> logger)
    {
        _kernel = kernel;
        _orchestrator = orchestrator;
        _logger = logger;
    }

    public async Task RunAsync()
    {
        Console.WriteLine("🤖 Quant Research Agent - Interactive CLI");
        Console.WriteLine("=========================================");
        Console.WriteLine();
        Console.WriteLine("Available commands:");
        Console.WriteLine("  1. analyze-podcast [url] - Analyze a Spotify podcast");
        Console.WriteLine("  2. generate-signals [symbol] - Generate trading signals");
        Console.WriteLine("  3. market-data [symbol] - Get market data");
        Console.WriteLine("  4. portfolio - View portfolio summary");
        Console.WriteLine("  5. risk-assessment - Assess portfolio risk");
        Console.WriteLine("  6. help - Show available functions");
        Console.WriteLine("  7. quit - Exit the application");
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
                case "analyze-podcast":
                    await AnalyzePodcastCommand(parts);
                    break;

                case "generate-signals":
                    await GenerateSignalsCommand(parts);
                    break;

                case "market-data":
                    await MarketDataCommand(parts);
                    break;

                case "portfolio":
                    await PortfolioCommand();
                    break;

                case "risk-assessment":
                    await RiskAssessmentCommand();
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
            Console.WriteLine($"❌ Error: {ex.Message}");
            _logger.LogError(ex, "Error processing command: {Command}", input);
        }
    }

    private async Task AnalyzePodcastCommand(string[] parts)
    {
        var url = parts.Length > 1 ? parts[1] : "https://open.spotify.com/episode/69tcEMbTyOEcPfgEJ95xos";
        
        Console.WriteLine($"🎧 Analyzing podcast: {url}");
        
        var function = _kernel.Plugins["PodcastAnalysisPlugin"]["AnalyzePodcastAsync"];
        var result = await _kernel.InvokeAsync(function, new() { ["podcastUrl"] = url });
        
        Console.WriteLine(result.ToString());
    }

    private async Task GenerateSignalsCommand(string[] parts)
    {
        var symbol = parts.Length > 1 ? parts[1] : null;
        
        Console.WriteLine(symbol != null ? $"📊 Generating signals for {symbol}..." : "📊 Generating signals for all symbols...");
        
        var function = _kernel.Plugins["TradingPlugin"]["GenerateTradingSignalsAsync"];
        var result = await _kernel.InvokeAsync(function, new() { ["symbol"] = symbol });
        
        Console.WriteLine(result.ToString());
    }

    private async Task MarketDataCommand(string[] parts)
    {
        var symbol = parts.Length > 1 ? parts[1] : "BTCUSDT";
        
        Console.WriteLine($"📈 Getting market data for {symbol}...");
        
        var function = _kernel.Plugins["MarketDataPlugin"]["GetMarketDataAsync"];
        var result = await _kernel.InvokeAsync(function, new() { ["symbol"] = symbol });
        
        Console.WriteLine(result.ToString());
    }

    private async Task PortfolioCommand()
    {
        Console.WriteLine("💰 Getting portfolio summary...");
        
        var function = _kernel.Plugins["RiskManagementPlugin"]["GetPortfolioSummaryAsync"];
        var result = await _kernel.InvokeAsync(function);
        
        Console.WriteLine(result.ToString());
    }

    private async Task RiskAssessmentCommand()
    {
        Console.WriteLine("🛡️ Assessing portfolio risk...");
        
        var function = _kernel.Plugins["RiskManagementPlugin"]["AssessPortfolioRiskAsync"];
        var result = await _kernel.InvokeAsync(function);
        
        Console.WriteLine(result.ToString());
    }

    private async Task ShowAvailableFunctions()
    {
        Console.WriteLine("🔧 Available Semantic Kernel Functions:");
        Console.WriteLine();

        foreach (var plugin in _kernel.Plugins)
        {
            Console.WriteLine($"📦 {plugin.Name}:");
            
            foreach (var function in plugin)
            {
                Console.WriteLine($"  • {function.Name} - {function.Description}");
            }
            Console.WriteLine();
        }
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
        var parameters = new Dictionary<string, object>();
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

        // Test 1: Market Data
        Console.WriteLine("1. Testing market data retrieval...");
        await MarketDataCommand(new[] { "market-data", "BTCUSDT" });
        Console.WriteLine();

        // Test 2: Generate Signals
        Console.WriteLine("2. Testing signal generation...");
        await GenerateSignalsCommand(new[] { "generate-signals", "BTCUSDT" });
        Console.WriteLine();

        // Test 3: Portfolio Summary
        Console.WriteLine("3. Testing portfolio summary...");
        await PortfolioCommand();
        Console.WriteLine();

        // Test 4: Risk Assessment
        Console.WriteLine("4. Testing risk assessment...");
        await RiskAssessmentCommand();
        Console.WriteLine();

        Console.WriteLine("✅ Test sequence completed!");
    }

    public static async Task<InteractiveCLI> CreateAsync(IServiceProvider serviceProvider)
    {
        var kernel = serviceProvider.GetRequiredService<Kernel>();
        var orchestrator = serviceProvider.GetRequiredService<AgentOrchestrator>();
        var logger = serviceProvider.GetRequiredService<ILogger<InteractiveCLI>>();

        return new InteractiveCLI(kernel, orchestrator, logger);
    }
}
