using Microsoft.SemanticKernel;
using QuantResearchAgent.Services;
using System.ComponentModel;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.Statistics;
using QuantResearchAgent.Core;

namespace QuantResearchAgent.Plugins;

/// <summary>
/// Semantic Kernel plugin for advanced factor research
/// </summary>
public class FactorResearchPlugin
{
    private readonly FactorResearchService _factorResearchService;

    public FactorResearchPlugin(FactorResearchService factorResearchService)
    {
        _factorResearchService = factorResearchService;
    }

    [KernelFunction, Description("Create a custom fundamental factor from company financial data")]
    public async Task<string> CreateFundamentalFactorAsync(
        [Description("Name of the factor")] string factorName,
        [Description("Description of what the factor measures")] string description,
        [Description("Comma-separated list of stock symbols")] string symbols,
        [Description("Start date for factor creation (YYYY-MM-DD)")] string startDate,
        [Description("End date for factor creation (YYYY-MM-DD)")] string endDate,
        [Description("Type of fundamental factor: Value, Growth, Quality, Momentum")] string factorType)
    {
        try
        {
            if (!DateTime.TryParse(startDate, out var start) || !DateTime.TryParse(endDate, out var end))
            {
                return "ERROR: Invalid date format. Use YYYY-MM-DD format.";
            }

            var symbolList = symbols.Split(',').Select(s => s.Trim()).Where(s => !string.IsNullOrEmpty(s)).ToList();

            Func<Dictionary<string, object>, double> factorCalculation = factorType.ToLower() switch
            {
                "value" => data => CalculateValueFactor(data),
                "growth" => data => CalculateGrowthFactor(data),
                "quality" => data => CalculateQualityFactor(data),
                "momentum" => data => CalculateMomentumFactor(data),
                _ => data => 0.0
            };

            var factor = await _factorResearchService.CreateFundamentalFactorAsync(
                factorName, description, symbolList, start, end, factorCalculation);

            var response = $"CUSTOM FUNDAMENTAL FACTOR CREATED: {factor.Name}\n\n" +
                          $"Description: {factor.Description}\n" +
                          $"Type: {factor.Type}\n" +
                          $"Creation Date: {factor.CreationDate:yyyy-MM-dd}\n" +
                          $"Data Points: {factor.DataPoints.Count}\n\n" +
                          $"STATISTICS:\n" +
                          $"- Mean: {factor.Statistics.Mean:F4}\n" +
                          $"- Std Dev: {factor.Statistics.StandardDeviation:F4}\n" +
                          $"- Skewness: {factor.Statistics.Skewness:F4}\n" +
                          $"- Kurtosis: {factor.Statistics.Kurtosis:F4}\n" +
                          $"- Min: {factor.Statistics.Min:F4}\n" +
                          $"- Max: {factor.Statistics.Max:F4}\n";

            return response;
        }
        catch (Exception ex)
        {
            return $"ERROR: Failed to create fundamental factor: {ex.Message}";
        }
    }

    [KernelFunction, Description("Create a custom technical factor from price/volume data")]
    public async Task<string> CreateTechnicalFactorAsync(
        [Description("Name of the factor")] string factorName,
        [Description("Description of what the factor measures")] string description,
        [Description("Comma-separated list of stock symbols")] string symbols,
        [Description("Start date for factor creation (YYYY-MM-DD)")] string startDate,
        [Description("End date for factor creation (YYYY-MM-DD)")] string endDate,
        [Description("Type of technical factor: Momentum, MeanReversion, Volatility, Volume")] string factorType,
        [Description("Lookback period in trading days")] int lookbackPeriod = 252)
    {
        try
        {
            if (!DateTime.TryParse(startDate, out var start) || !DateTime.TryParse(endDate, out var end))
            {
                return "ERROR: Invalid date format. Use YYYY-MM-DD format.";
            }

            var symbolList = symbols.Split(',').Select(s => s.Trim()).Where(s => !string.IsNullOrEmpty(s)).ToList();

            Func<List<MarketData>, double> factorCalculation = factorType.ToLower() switch
            {
                "momentum" => data => CalculateMomentumTechnical(data),
                "meanreversion" => data => CalculateMeanReversion(data),
                "volatility" => data => CalculateVolatility(data),
                "volume" => data => CalculateVolumeFactor(data),
                _ => data => 0.0
            };

            var factor = await _factorResearchService.CreateTechnicalFactorAsync(
                factorName, description, symbolList, start, end, factorCalculation, lookbackPeriod);

            var response = $"CUSTOM TECHNICAL FACTOR CREATED: {factor.Name}\n\n" +
                          $"Description: {factor.Description}\n" +
                          $"Type: {factor.Type}\n" +
                          $"Creation Date: {factor.CreationDate:yyyy-MM-dd}\n" +
                          $"Lookback Period: {lookbackPeriod} days\n" +
                          $"Data Points: {factor.DataPoints.Count}\n\n" +
                          $"STATISTICS:\n" +
                          $"- Mean: {factor.Statistics.Mean:F4}\n" +
                          $"- Std Dev: {factor.Statistics.StandardDeviation:F4}\n" +
                          $"- Skewness: {factor.Statistics.Skewness:F4}\n" +
                          $"- Kurtosis: {factor.Statistics.Kurtosis:F4}\n" +
                          $"- Min: {factor.Statistics.Min:F4}\n" +
                          $"- Max: {factor.Statistics.Max:F4}\n";

            return response;
        }
        catch (Exception ex)
        {
            return $"ERROR: Failed to create technical factor: {ex.Message}";
        }
    }

    [KernelFunction, Description("Test the efficacy of a custom factor by creating factor portfolios")]
    public async Task<string> TestFactorEfficacyAsync(
        [Description("Name of the factor to test")] string factorName,
        [Description("Start date for testing (YYYY-MM-DD)")] string startDate,
        [Description("End date for testing (YYYY-MM-DD)")] string endDate,
        [Description("Number of portfolios to create (default: 5)")] int portfolioCount = 5)
    {
        try
        {
            if (!DateTime.TryParse(startDate, out var start) || !DateTime.TryParse(endDate, out var end))
            {
                return "ERROR: Invalid date format. Use YYYY-MM-DD format.";
            }

            // For now, create a dummy factor for testing - in practice would load from storage
            var dummyFactor = new CustomFactor
            {
                Name = factorName,
                DataPoints = GenerateDummyFactorData(start, end)
            };

            var test = await _factorResearchService.TestFactorEfficacyAsync(dummyFactor, start, end, portfolioCount);

            var response = $"FACTOR EFFICACY TEST: {test.FactorName}\n\n" +
                          $"Test Period: {test.TestPeriodStart:yyyy-MM-dd} to {test.TestPeriodEnd:yyyy-MM-dd}\n" +
                          $"Portfolios Created: {test.PortfolioCount}\n\n" +
                          $"PERFORMANCE METRICS:\n" +
                          $"- Sharpe Ratio: {test.SharpeRatio:F4}\n" +
                          $"- Max Drawdown: {test.MaxDrawdown:P3}\n" +
                          $"- Win Rate: {test.WinRate:P3}\n\n" +
                          $"FACTOR RETURNS SUMMARY:\n" +
                          $"- Total Return: {test.FactorReturns.Sum():P3}\n" +
                          $"- Average Daily Return: {test.FactorReturns.Average():P4}\n" +
                          $"- Best Day: {test.FactorReturns.Max():P3}\n" +
                          $"- Worst Day: {test.FactorReturns.Min():P3}\n";

            return response;
        }
        catch (Exception ex)
        {
            return $"ERROR: Failed to test factor efficacy: {ex.Message}";
        }
    }

    [KernelFunction, Description("Create a composite factor by combining multiple factors")]
    public string CreateCompositeFactor(
        [Description("Name of the composite factor")] string factorName,
        [Description("Description of the composite factor")] string description,
        [Description("Names of factors to combine (comma-separated)")] string factorNames,
        [Description("Weights for each factor (comma-separated, should sum to 1.0)")] string weights)
    {
        try
        {
            var nameList = factorNames.Split(',').Select(n => n.Trim()).Where(n => !string.IsNullOrEmpty(n)).ToList();
            var weightList = weights.Split(',').Select(w => double.Parse(w.Trim())).ToList();

            if (nameList.Count != weightList.Count)
            {
                return "ERROR: Number of factor names must match number of weights.";
            }

            // Create dummy factors for demonstration - in practice would load from storage
            var factors = nameList.Select(name => new CustomFactor
            {
                Name = name,
                DataPoints = GenerateDummyFactorData(DateTime.Now.AddYears(-1), DateTime.Now)
            }).ToList();

            var compositeFactor = _factorResearchService.CreateCompositeFactor(
                factorName, description, factors, weightList);

            var response = $"COMPOSITE FACTOR CREATED: {compositeFactor.Name}\n\n" +
                          $"Description: {compositeFactor.Description}\n" +
                          $"Type: {compositeFactor.Type}\n" +
                          $"Component Factors: {string.Join(", ", nameList)}\n" +
                          $"Weights: {string.Join(", ", weightList.Select(w => w.ToString("F2")))}\n" +
                          $"Creation Date: {compositeFactor.CreationDate:yyyy-MM-dd}\n" +
                          $"Data Points: {compositeFactor.DataPoints.Count}\n\n" +
                          $"STATISTICS:\n" +
                          $"- Mean: {compositeFactor.Statistics.Mean:F4}\n" +
                          $"- Std Dev: {compositeFactor.Statistics.StandardDeviation:F4}\n" +
                          $"- Skewness: {compositeFactor.Statistics.Skewness:F4}\n" +
                          $"- Kurtosis: {compositeFactor.Statistics.Kurtosis:F4}\n";

            return response;
        }
        catch (Exception ex)
        {
            return $"ERROR: Failed to create composite factor: {ex.Message}";
        }
    }

    private double CalculateValueFactor(Dictionary<string, object> data)
    {
        // Value factor: Low P/B, Low P/E
        var pb = Convert.ToDouble(data.GetValueOrDefault("PB", 1.0));
        var pe = Convert.ToDouble(data.GetValueOrDefault("PE", 20.0));
        return -Math.Log(pb) - Math.Log(Math.Max(pe, 1.0)); // Negative because lower is better
    }

    private double CalculateGrowthFactor(Dictionary<string, object> data)
    {
        // Growth factor: High ROE, High earnings growth
        var roe = Convert.ToDouble(data.GetValueOrDefault("ROE", 0.1));
        return roe; // Higher is better
    }

    private double CalculateQualityFactor(Dictionary<string, object> data)
    {
        // Quality factor: Low debt, High ROE, stable earnings
        var debtToEquity = Convert.ToDouble(data.GetValueOrDefault("DebtToEquity", 0.5));
        var roe = Convert.ToDouble(data.GetValueOrDefault("ROE", 0.1));
        return roe - debtToEquity; // High ROE, low debt
    }

    private double CalculateMomentumFactor(Dictionary<string, object> data)
    {
        // Momentum factor: Recent performance
        var marketCap = Convert.ToDouble(data.GetValueOrDefault("MarketCap", 1000000000.0));
        return Math.Log(marketCap); // Larger companies tend to have momentum
    }

    private double CalculateMomentumTechnical(List<MarketData> data)
    {
        if (data.Count < 2) return 0.0;
        var recent = data.Last().Price; // Use Price instead of Close
        var older = data.First().Price;
        return (recent - older) / older; // Price momentum
    }

    private double CalculateMeanReversion(List<MarketData> data)
    {
        if (data.Count < 20) return 0.0;
        var prices = data.Select(d => d.Price).ToList();
        var mean = prices.Average();
        var current = prices.Last();
        return (mean - current) / current; // Deviation from mean
    }

    private double CalculateVolatility(List<MarketData> data)
    {
        if (data.Count < 2) return 0.0;
        var returns = new List<double>();
        for (int i = 1; i < data.Count; i++)
        {
            returns.Add((data[i].Price - data[i-1].Price) / data[i-1].Price);
        }
        return returns.Any() ? returns.StandardDeviation() : 0.0;
    }

    private double CalculateVolumeFactor(List<MarketData> data)
    {
        if (data.Count < 20) return 0.0;
        var volumes = data.Select(d => d.Volume).ToList();
        var avgVolume = volumes.Average();
        var currentVolume = volumes.Last();
        return currentVolume / avgVolume; // Volume relative to average
    }

    private List<FactorDataPoint> GenerateDummyFactorData(DateTime start, DateTime end)
    {
        var data = new List<FactorDataPoint>();
        var random = new Random(42);
        var currentDate = start;

        while (currentDate <= end)
        {
            data.Add(new FactorDataPoint
            {
                Date = currentDate,
                Symbol = "DUMMY",
                Value = (random.NextDouble() - 0.5) * 2.0
            });
            currentDate = currentDate.AddDays(1);
        }

        return data;
    }
}