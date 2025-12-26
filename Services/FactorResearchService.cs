using Microsoft.Extensions.Logging;
using QuantResearchAgent.Core;
using QuantResearchAgent.Services;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.Statistics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace QuantResearchAgent.Services;

/// <summary>
/// Advanced factor research service for creating and analyzing custom factors
/// </summary>
public class FactorResearchService
{
    private readonly ILogger<FactorResearchService> _logger;
    private readonly FactorModelService _factorModelService;
    private readonly MarketDataService _marketDataService;

    public FactorResearchService(
        ILogger<FactorResearchService> logger,
        FactorModelService factorModelService,
        MarketDataService marketDataService)
    {
        _logger = logger;
        _factorModelService = factorModelService;
        _marketDataService = marketDataService;
    }

    /// <summary>
    /// Create a custom factor from fundamental data
    /// </summary>
    public async Task<CustomFactor> CreateFundamentalFactorAsync(
        string factorName,
        string description,
        List<string> symbols,
        DateTime startDate,
        DateTime endDate,
        Func<Dictionary<string, object>, double> factorCalculation)
    {
        try
        {
            _logger.LogInformation("Creating fundamental factor: {FactorName}", factorName);

            var factorData = new List<FactorDataPoint>();

            foreach (var symbol in symbols)
            {
                // Get fundamental data for the symbol
                var fundamentals = await GetFundamentalDataAsync(symbol, startDate, endDate);

                foreach (var fundamental in fundamentals)
                {
                    var factorValue = factorCalculation(fundamental.Data);
                    factorData.Add(new FactorDataPoint
                    {
                        Date = fundamental.Date,
                        Symbol = symbol,
                        Value = factorValue
                    });
                }
            }

            var factor = new CustomFactor
            {
                Name = factorName,
                Description = description,
                Type = FactorType.Fundamental,
                DataPoints = factorData,
                CreationDate = DateTime.UtcNow
            };

            // Calculate factor statistics
            factor.Statistics = CalculateFactorStatistics(factorData);

            _logger.LogInformation("Created fundamental factor {FactorName} with {Count} data points",
                factorName, factorData.Count);

            return factor;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create fundamental factor {FactorName}", factorName);
            throw;
        }
    }

    /// <summary>
    /// Create a technical factor from price/volume data
    /// </summary>
    public async Task<CustomFactor> CreateTechnicalFactorAsync(
        string factorName,
        string description,
        List<string> symbols,
        DateTime startDate,
        DateTime endDate,
        Func<List<MarketData>, double> factorCalculation,
        int lookbackPeriod = 252)
    {
        try
        {
            _logger.LogInformation("Creating technical factor: {FactorName}", factorName);

            var factorData = new List<FactorDataPoint>();

            foreach (var symbol in symbols)
            {
                // Get price data for the symbol using available API and filter by date
                var rawData = await _marketDataService.GetHistoricalDataAsync(symbol, 5000) ?? new List<MarketData>();
                var priceData = rawData.Where(d => d.Timestamp >= startDate && d.Timestamp <= endDate)
                                       .OrderBy(d => d.Timestamp)
                                       .ToList();

                // Calculate factor for each date
                for (int i = lookbackPeriod; i < priceData.Count; i++)
                {
                    var window = priceData.Skip(i - lookbackPeriod).Take(lookbackPeriod).ToList();
                    var factorValue = factorCalculation(window);

                    factorData.Add(new FactorDataPoint
                    {
                        Date = priceData[i].Timestamp,
                        Symbol = symbol,
                        Value = factorValue
                    });
                }
            }

            var factor = new CustomFactor
            {
                Name = factorName,
                Description = description,
                Type = FactorType.Technical,
                DataPoints = factorData,
                CreationDate = DateTime.UtcNow
            };

            // Calculate factor statistics
            factor.Statistics = CalculateFactorStatistics(factorData);

            _logger.LogInformation("Created technical factor {FactorName} with {Count} data points",
                factorName, factorData.Count);

            return factor;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create technical factor {FactorName}", factorName);
            throw;
        }
    }

    /// <summary>
    /// Test factor efficacy by running portfolio analysis
    /// </summary>
    public async Task<FactorEfficacyTest> TestFactorEfficacyAsync(
        CustomFactor factor,
        DateTime startDate,
        DateTime endDate,
        int portfolioCount = 5)
    {
        try
        {
            _logger.LogInformation("Testing factor efficacy for {FactorName}", factor.Name);

            // Create portfolios based on factor rankings
            var portfolios = await CreateFactorPortfoliosAsync(factor, startDate, endDate, portfolioCount);

            // Calculate portfolio returns
            var portfolioReturns = new Dictionary<string, List<double>>();
            foreach (var portfolio in portfolios)
            {
                var returns = await CalculatePortfolioReturnsAsync(portfolio.Symbols, startDate, endDate);
                portfolioReturns[portfolio.Name] = returns;
            }

            // Calculate factor returns (long-short)
            var longPortfolio = portfolios.Last();
            var shortPortfolio = portfolios.First();
            var factorReturns = CalculateLongShortReturns(
                portfolioReturns[longPortfolio.Name],
                portfolioReturns[shortPortfolio.Name]);

            var test = new FactorEfficacyTest
            {
                FactorName = factor.Name,
                TestPeriodStart = startDate,
                TestPeriodEnd = endDate,
                PortfolioCount = portfolioCount,
                Portfolios = portfolios,
                FactorReturns = factorReturns,
                SharpeRatio = CalculateSharpeRatio(factorReturns),
                MaxDrawdown = CalculateMaxDrawdown(factorReturns),
                WinRate = CalculateWinRate(factorReturns)
            };

            _logger.LogInformation("Factor efficacy test completed for {FactorName}", factor.Name);
            return test;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to test factor efficacy for {FactorName}", factor.Name);
            throw;
        }
    }

    /// <summary>
    /// Combine multiple factors into a composite factor
    /// </summary>
    public CustomFactor CreateCompositeFactor(
        string factorName,
        string description,
        List<CustomFactor> factors,
        List<double> weights)
    {
        try
        {
            _logger.LogInformation("Creating composite factor: {FactorName}", factorName);

            if (factors.Count != weights.Count)
            {
                throw new ArgumentException("Number of factors must match number of weights");
            }

            // Normalize weights
            var totalWeight = weights.Sum();
            weights = weights.Select(w => w / totalWeight).ToList();

            // Group data points by date and symbol
            var groupedData = factors
                .SelectMany(f => f.DataPoints)
                .GroupBy(dp => new { dp.Date, dp.Symbol })
                .ToDictionary(g => g.Key, g => g.ToList());

            var compositeData = new List<FactorDataPoint>();

            foreach (var group in groupedData)
            {
                if (group.Value.Count == factors.Count)
                {
                    // All factors have data for this date/symbol
                    var compositeValue = 0.0;
                    for (int i = 0; i < factors.Count; i++)
                    {
                        compositeValue += group.Value[i].Value * weights[i];
                    }

                    compositeData.Add(new FactorDataPoint
                    {
                        Date = group.Key.Date,
                        Symbol = group.Key.Symbol,
                        Value = compositeValue
                    });
                }
            }

            var compositeFactor = new CustomFactor
            {
                Name = factorName,
                Description = description,
                Type = FactorType.Composite,
                DataPoints = compositeData,
                CreationDate = DateTime.UtcNow
            };

            compositeFactor.Statistics = CalculateFactorStatistics(compositeData);

            _logger.LogInformation("Created composite factor {FactorName} with {Count} data points",
                factorName, compositeData.Count);

            return compositeFactor;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create composite factor {FactorName}", factorName);
            throw;
        }
    }

    private async Task<List<FundamentalData>> GetFundamentalDataAsync(string symbol, DateTime startDate, DateTime endDate)
    {
        // Simplified - in practice would integrate with financial data providers
        var fundamentals = new List<FundamentalData>();
        var currentDate = startDate;

        while (currentDate <= endDate)
        {
            fundamentals.Add(new FundamentalData
            {
                Date = currentDate,
                Symbol = symbol,
                Data = new Dictionary<string, object>
                {
                    ["MarketCap"] = 1000000000.0, // Example data
                    ["PE"] = 20.0,
                    ["PB"] = 2.5,
                    ["ROE"] = 0.15,
                    ["DebtToEquity"] = 0.5
                }
            });
            currentDate = currentDate.AddMonths(1);
        }

        return fundamentals;
    }

    private FactorStatistics CalculateFactorStatistics(List<FactorDataPoint> dataPoints)
    {
        var values = dataPoints.Select(dp => dp.Value).ToList();

        return new FactorStatistics
        {
            Mean = values.Mean(),
            StandardDeviation = values.StandardDeviation(),
            Skewness = values.Skewness(),
            Kurtosis = values.Kurtosis(),
            Min = values.Min(),
            Max = values.Max(),
            Count = values.Count,
            DataPoints = dataPoints
        };
    }

    private async Task<List<FactorPortfolio>> CreateFactorPortfoliosAsync(
        CustomFactor factor,
        DateTime startDate,
        DateTime endDate,
        int portfolioCount)
    {
        var portfolios = new List<FactorPortfolio>();

        // Group factor data by date
        var dateGroups = factor.DataPoints.GroupBy(dp => dp.Date).OrderBy(g => g.Key);

        foreach (var dateGroup in dateGroups)
        {
            // Sort symbols by factor value
            var sortedSymbols = dateGroup
                .OrderBy(dp => dp.Value)
                .Select(dp => dp.Symbol)
                .Distinct()
                .ToList();

            var symbolsPerPortfolio = sortedSymbols.Count / portfolioCount;

            for (int i = 0; i < portfolioCount; i++)
            {
                var portfolioName = $"Portfolio_{i + 1}";
                var startIndex = i * symbolsPerPortfolio;
                var endIndex = (i == portfolioCount - 1) ? sortedSymbols.Count : (i + 1) * symbolsPerPortfolio;

                var portfolioSymbols = sortedSymbols.Skip(startIndex).Take(endIndex - startIndex).ToList();

                if (!portfolios.Any(p => p.Name == portfolioName))
                {
                    portfolios.Add(new FactorPortfolio
                    {
                        Name = portfolioName,
                        Symbols = portfolioSymbols,
                        FactorValues = new Dictionary<string, double>()
                    });
                }

                // Update portfolio with current factor values
                var portfolio = portfolios.First(p => p.Name == portfolioName);
                foreach (var symbol in portfolioSymbols)
                {
                    var factorValue = dateGroup.FirstOrDefault(dp => dp.Symbol == symbol)?.Value ?? 0;
                    portfolio.FactorValues[symbol] = factorValue;
                }
            }
        }

        return portfolios;
    }

    private async Task<List<double>> CalculatePortfolioReturnsAsync(List<string> symbols, DateTime startDate, DateTime endDate)
    {
        var portfolioReturns = new List<double>();

        // Simplified - equal weight portfolio
        var weight = 1.0 / symbols.Count;

        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            var dailyReturn = 0.0;
            foreach (var symbol in symbols)
            {
                // Simplified return calculation
                dailyReturn += weight * (new Random().NextDouble() - 0.5) * 0.02;
            }
            portfolioReturns.Add(dailyReturn);
        }

        return portfolioReturns;
    }

    private List<double> CalculateLongShortReturns(List<double> longReturns, List<double> shortReturns)
    {
        var factorReturns = new List<double>();
        var minLength = Math.Min(longReturns.Count, shortReturns.Count);

        for (int i = 0; i < minLength; i++)
        {
            factorReturns.Add(longReturns[i] - shortReturns[i]);
        }

        return factorReturns;
    }

    private double CalculateSharpeRatio(List<double> returns)
    {
        var mean = returns.Mean();
        var std = returns.StandardDeviation();
        return std > 0 ? mean / std : 0;
    }

    private double CalculateMaxDrawdown(List<double> returns)
    {
        var cumulative = new List<double>();
        var runningSum = 0.0;
        var peak = 0.0;
        var maxDrawdown = 0.0;

        foreach (var ret in returns)
        {
            runningSum += ret;
            cumulative.Add(runningSum);

            if (runningSum > peak)
            {
                peak = runningSum;
            }

            var drawdown = peak - runningSum;
            if (drawdown > maxDrawdown)
            {
                maxDrawdown = drawdown;
            }
        }

        return maxDrawdown;
    }

    private double CalculateWinRate(List<double> returns)
    {
        var winningDays = returns.Count(r => r > 0);
        return (double)winningDays / returns.Count;
    }
}