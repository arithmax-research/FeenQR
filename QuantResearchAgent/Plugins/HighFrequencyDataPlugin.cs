using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace QuantResearchAgent.Plugins;

/// <summary>
/// Semantic Kernel plugin for High-Frequency Data Service
/// Provides market microstructure analysis and HFT capabilities
/// </summary>
public class HighFrequencyDataPlugin
{
    private readonly Services.HighFrequencyDataService _hfDataService;

    public HighFrequencyDataPlugin(Services.HighFrequencyDataService hfDataService)
    {
        _hfDataService = hfDataService;
    }

    [KernelFunction]
    [Description("Start collecting high-frequency market data for analysis")]
    public async Task<string> StartHFDataCollection(
        [Description("Comma-separated list of trading symbols")] string symbols,
        [Description("Data collection duration in minutes")] int durationMinutes = 60,
        [Description("Data aggregation interval in milliseconds")] int intervalMs = 100)
    {
        return await _hfDataService.StartHFDataCollectionAsync(symbols, durationMinutes, intervalMs);
    }

    [KernelFunction]
    [Description("Analyze market microstructure patterns for trading opportunities")]
    public async Task<string> AnalyzeMicrostructure(
        [Description("Trading symbol to analyze")] string symbol,
        [Description("Analysis window in minutes")] int windowMinutes = 30)
    {
        return await _hfDataService.AnalyzeMicrostructureAsync(symbol, windowMinutes);
    }

    [KernelFunction]
    [Description("Execute Avellaneda-Stoikov market making strategy")]
    public async Task<string> RunAvellanedaStoikovStrategy(
        [Description("Trading symbol")] string symbol,
        [Description("Risk aversion parameter (higher = more conservative)")] double riskAversion = 0.1,
        [Description("Inventory target (shares to maintain)")] double inventoryTarget = 0.0,
        [Description("Strategy duration in minutes")] int durationMinutes = 60)
    {
        return await _hfDataService.RunAvellanedaStoikovStrategyAsync(symbol, riskAversion, inventoryTarget, durationMinutes);
    }

    [KernelFunction]
    [Description("Detect hidden order flow patterns in the market")]
    public async Task<string> DetectHiddenOrders(
        [Description("Trading symbol")] string symbol,
        [Description("Detection sensitivity (1-10, higher = more sensitive)")] int sensitivity = 5)
    {
        return await _hfDataService.DetectHiddenOrdersAsync(symbol, sensitivity);
    }

    [KernelFunction]
    [Description("Calculate real-time volatility metrics and predictions")]
    public async Task<string> CalculateVolatilityMetrics(
        [Description("Trading symbol")] string symbol,
        [Description("Volatility model type (GARCH, EWMA, historical)")] string modelType = "GARCH")
    {
        return await _hfDataService.CalculateVolatilityMetricsAsync(symbol, modelType);
    }
}
