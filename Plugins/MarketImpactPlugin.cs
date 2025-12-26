using Microsoft.SemanticKernel;
using QuantResearchAgent.Services;
using System.ComponentModel;
using System.Text.Json;

namespace QuantResearchAgent.Plugins;

public class MarketImpactPlugin
{
    private readonly MarketImpactService _marketImpactService;

    public MarketImpactPlugin(MarketImpactService marketImpactService)
    {
        _marketImpactService = marketImpactService;
    }

    [KernelFunction, Description("Calculate optimal execution using Almgren-Chriss model")]
    public async Task<string> CalculateAlmgrenChriss(
        [Description("Total shares to trade")] double totalShares,
        [Description("Time horizon in trading days")] double timeHorizon,
        [Description("Asset volatility (annualized)")] double volatility,
        [Description("Risk aversion parameter (default 1e-6)")] double lambda = 1e-6,
        [Description("Permanent impact parameter (default 1.0)")] double gamma = 1.0,
        [Description("Temporary impact parameter (default 0.1)")] double eta = 0.1)
    {
        try
        {
            var result = _marketImpactService.CalculateAlmgrenChriss(
                totalShares, timeHorizon, volatility, lambda, gamma, eta);

            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return $"Error calculating Almgren-Chriss model: {ex.Message}";
        }
    }

    [KernelFunction, Description("Calculate implementation shortfall for a trade")]
    public async Task<string> CalculateImplementationShortfall(
        [Description("Benchmark price at start of trading")] double benchmarkPrice,
        [Description("Execution prices as comma-separated values")] string executionPrices,
        [Description("Execution volumes as comma-separated values")] string executionVolumes,
        [Description("Total planned volume")] double totalVolume)
    {
        try
        {
            var prices = executionPrices.Split(',').Select(double.Parse).ToList();
            var volumes = executionVolumes.Split(',').Select(double.Parse).ToList();

            var result = _marketImpactService.CalculateImplementationShortfall(
                benchmarkPrice, prices, volumes, totalVolume, DateTime.Now, DateTime.Now);

            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return $"Error calculating implementation shortfall: {ex.Message}";
        }
    }

    [KernelFunction, Description("Estimate price impact for a trade")]
    public async Task<string> EstimatePriceImpact(
        [Description("Trade size in shares")] double tradeSize,
        [Description("Average daily volume")] double averageDailyVolume,
        [Description("Asset volatility")] double volatility,
        [Description("Market capitalization (optional)")] double marketCap = 0,
        [Description("Beta coefficient (default 1.0)")] double beta = 1.0)
    {
        try
        {
            var result = _marketImpactService.EstimatePriceImpact(
                tradeSize, averageDailyVolume, volatility, marketCap, beta);

            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return $"Error estimating price impact: {ex.Message}";
        }
    }

    [KernelFunction, Description("Calculate optimal execution strategy")]
    public async Task<string> CalculateOptimalExecution(
        [Description("Total shares to trade")] double totalShares,
        [Description("Time horizon in days")] double timeHorizon,
        [Description("Asset volatility")] double volatility,
        [Description("Current price")] double currentPrice,
        [Description("Average daily volume")] double averageVolume)
    {
        try
        {
            var result = _marketImpactService.CalculateOptimalExecution(
                totalShares, timeHorizon, volatility, currentPrice, averageVolume);

            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return $"Error calculating optimal execution: {ex.Message}";
        }
    }

    [KernelFunction, Description("Analyze historical market impact from trades")]
    public async Task<string> AnalyzeHistoricalImpact(
        [Description("Trade prices as comma-separated values")] string tradePrices,
        [Description("Trade volumes as comma-separated values")] string tradeVolumes,
        [Description("Benchmark prices as comma-separated values")] string benchmarkPrices,
        [Description("Average daily volume")] double averageVolume)
    {
        try
        {
            var prices = tradePrices.Split(',').Select(double.Parse).ToList();
            var volumes = tradeVolumes.Split(',').Select(double.Parse).ToList();
            var benchmarks = benchmarkPrices.Split(',').Select(double.Parse).ToList();

            var result = _marketImpactService.AnalyzeHistoricalImpact(
                prices, volumes, benchmarks, averageVolume);

            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return $"Error analyzing historical impact: {ex.Message}";
        }
    }
}