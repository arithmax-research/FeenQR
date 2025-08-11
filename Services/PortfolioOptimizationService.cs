using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using QuantResearchAgent.Core;
using MathNet.Numerics.Statistics;
using System.Text.Json;

namespace QuantResearchAgent.Services;

public class PortfolioOptimizationService
{
    private readonly ILogger<PortfolioOptimizationService> _logger;
    private readonly YahooFinanceService _yahooFinanceService;
    private readonly AlpacaService _alpacaService;
    private readonly PolygonService _polygonService;

    public PortfolioOptimizationService(
        ILogger<PortfolioOptimizationService> logger,
        YahooFinanceService yahooFinanceService,
        AlpacaService alpacaService,
        PolygonService polygonService)
    {
        _logger = logger;
        _yahooFinanceService = yahooFinanceService;
        _alpacaService = alpacaService;
        _polygonService = polygonService;
    }

    public async Task<PortfolioOptimizationResult> OptimizePortfolioAsync(string[] tickers, double[]? initialWeights = null, int lookbackDays = 252)
    {
        try
        {
            _logger.LogInformation("Starting portfolio optimization for {Tickers}", string.Join(", ", tickers));

            // Get current market data for all tickers
            var marketData = new Dictionary<string, YahooMarketData>();
            
            foreach (var ticker in tickers)
            {
                var data = await _yahooFinanceService.GetMarketDataAsync(ticker);
                if (data != null)
                {
                    marketData[ticker] = data;
                }
            }

            if (marketData.Count < 2)
            {
                throw new InvalidOperationException("Need at least 2 assets with valid data for optimization");
            }

            // Simple equal-weight optimization (since we don't have historical data for variance calculation)
            var optimizedWeights = new Dictionary<string, double>();
            var equalWeight = 1.0 / marketData.Count;
            
            foreach (var ticker in marketData.Keys)
            {
                optimizedWeights[ticker] = equalWeight;
            }

            // Calculate basic metrics using current prices
            var expectedReturns = new Dictionary<string, double>();
            foreach (var kvp in marketData)
            {
                // Use daily change as a proxy for expected return
                expectedReturns[kvp.Key] = (double)kvp.Value.ChangePercent24h / 100.0;
            }

            var result = new PortfolioOptimizationResult
            {
                Tickers = marketData.Keys.ToArray(),
                OptimizedWeights = optimizedWeights,
                ExpectedReturn = expectedReturns.Values.Average(),
                Risk = expectedReturns.Values.StandardDeviation(),
                SharpeRatio = expectedReturns.Values.Average() / Math.Max(expectedReturns.Values.StandardDeviation(), 0.001),
                ExpectedReturns = expectedReturns,
                LookbackDays = lookbackDays,
                OptimizationDate = DateTime.UtcNow
            };

            _logger.LogInformation("Portfolio optimization completed. Equal-weight allocation applied.");
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to optimize portfolio");
            throw;
        }
    }
}

public class PortfolioOptimizationResult
{
    public string[] Tickers { get; set; } = Array.Empty<string>();
    public Dictionary<string, double> OptimizedWeights { get; set; } = new();
    public double ExpectedReturn { get; set; }
    public double Risk { get; set; }
    public double SharpeRatio { get; set; }
    public Dictionary<string, double> ExpectedReturns { get; set; } = new();
    public int LookbackDays { get; set; }
    public DateTime OptimizationDate { get; set; }
}

public class HistoricalDataPoint
{
    public DateTime Date { get; set; }
    public double Open { get; set; }
    public double High { get; set; }
    public double Low { get; set; }
    public double Close { get; set; }
    public long Volume { get; set; }
}
