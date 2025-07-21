using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace QuantResearchAgent.Plugins;

/// <summary>
/// Semantic Kernel plugin for Trading Strategy Library Service
/// Provides access to consolidated trading strategies from crypto_research folder
/// </summary>
public class TradingStrategyLibraryPlugin
{
    private readonly Services.TradingStrategyLibraryService _strategyService;

    public TradingStrategyLibraryPlugin(Services.TradingStrategyLibraryService strategyService)
    {
        _strategyService = strategyService;
    }

    [KernelFunction]
    [Description("Execute a specific trading strategy with backtesting")]
    public async Task<string> ExecuteStrategy(
        [Description("Strategy name (SMA, EMA, Bollinger, RSI, MACD, MeanReversion, Momentum, Pairs, Scalping, Swing)")] string strategyName,
        [Description("Trading symbol")] string symbol,
        [Description("Strategy parameters in JSON format")] string parameters = "{}",
        [Description("Backtesting period in days")] int backtestDays = 30)
    {
        return await _strategyService.ExecuteStrategyAsync(strategyName, symbol, parameters, backtestDays);
    }

    [KernelFunction]
    [Description("Compare performance of multiple trading strategies")]
    public async Task<string> CompareStrategies(
        [Description("Comma-separated strategy names")] string strategies,
        [Description("Trading symbol")] string symbol,
        [Description("Comparison period in days")] int comparisonDays = 90)
    {
        return await _strategyService.CompareStrategiesAsync(strategies, symbol, comparisonDays);
    }

    [KernelFunction]
    [Description("Generate adaptive trading signals based on market conditions")]
    public async Task<string> GenerateAdaptiveSignals(
        [Description("Trading symbol")] string symbol,
        [Description("Market regime (trending, ranging, volatile, calm, auto)")] string marketRegime = "auto",
        [Description("Signal strength threshold (0.0-1.0)")] double threshold = 0.6)
    {
        return await _strategyService.GenerateAdaptiveSignalsAsync(symbol, marketRegime, threshold);
    }

    [KernelFunction]
    [Description("Optimize portfolio allocation across multiple strategies")]
    public async Task<string> OptimizePortfolioStrategies(
        [Description("Comma-separated list of symbols")] string symbols,
        [Description("Risk tolerance (conservative, moderate, aggressive)")] string riskTolerance = "moderate",
        [Description("Optimization period in days")] int optimizationDays = 60)
    {
        return await _strategyService.OptimizePortfolioStrategiesAsync(symbols, riskTolerance, optimizationDays);
    }
}
