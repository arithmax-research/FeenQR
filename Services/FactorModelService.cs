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
/// Service for implementing multi-factor risk models
/// </summary>
public class FactorModelService
{
    private readonly ILogger<FactorModelService> _logger;
    private readonly MarketDataService _marketDataService;
    private readonly YahooFinanceService _yahooFinanceService;

    public FactorModelService(
        ILogger<FactorModelService> logger,
        MarketDataService marketDataService,
        YahooFinanceService yahooFinanceService)
    {
        _logger = logger;
        _marketDataService = marketDataService;
        _yahooFinanceService = yahooFinanceService;
    }

    /// <summary>
    /// Run Fama-French 3-Factor model analysis
    /// </summary>
    public async Task<FamaFrench3FactorModel> AnalyzeFamaFrench3FactorAsync(
        string assetSymbol,
        DateTime startDate,
        DateTime endDate)
    {
        try
        {
            _logger.LogInformation("Running Fama-French 3-Factor analysis for {Asset}", assetSymbol);

            // Get asset returns
            var assetReturns = await GetAssetReturnsAsync(assetSymbol, startDate, endDate);
            if (!assetReturns.Any())
            {
                throw new InvalidOperationException($"No return data available for {assetSymbol}");
            }

            // Get Fama-French factors (simplified - in practice would load from data provider)
            var factors = await GetFamaFrenchFactorsAsync(startDate, endDate);
            if (!factors.Any())
            {
                throw new InvalidOperationException("No Fama-French factor data available");
            }

            // Align data by dates
            var alignedData = AlignFactorData(assetReturns, factors);
            if (!alignedData.Any())
            {
                throw new InvalidOperationException("No aligned data for regression");
            }

            // Run regression
            var regressionResult = RunFactorRegression(alignedData, new[] { "Market", "SMB", "HML" });

            var model = new FamaFrench3FactorModel
            {
                MarketBeta = regressionResult.Coefficients.GetValueOrDefault("Market", 0),
                SizeBeta = regressionResult.Coefficients.GetValueOrDefault("SMB", 0),
                ValueBeta = regressionResult.Coefficients.GetValueOrDefault("HML", 0),
                Alpha = regressionResult.Intercept,
                R2 = regressionResult.RSquared,
                AnalysisDate = DateTime.UtcNow
            };

            // Calculate factor returns
            model.FactorReturns = CalculateFactorReturns(alignedData, new[] { "Market", "SMB", "HML" });

            _logger.LogInformation("Fama-French 3-Factor analysis completed for {Asset}", assetSymbol);
            return model;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze Fama-French 3-Factor for {Asset}", assetSymbol);
            throw;
        }
    }

    /// <summary>
    /// Run Carhart 4-Factor model analysis
    /// </summary>
    public async Task<Carhart4FactorModel> AnalyzeCarhart4FactorAsync(
        string assetSymbol,
        DateTime startDate,
        DateTime endDate)
    {
        try
        {
            _logger.LogInformation("Running Carhart 4-Factor analysis for {Asset}", assetSymbol);

            // Get asset returns
            var assetReturns = await GetAssetReturnsAsync(assetSymbol, startDate, endDate);
            if (!assetReturns.Any())
            {
                throw new InvalidOperationException($"No return data available for {assetSymbol}");
            }

            // Get Carhart factors
            var factors = await GetCarhartFactorsAsync(startDate, endDate);
            if (!factors.Any())
            {
                throw new InvalidOperationException("No Carhart factor data available");
            }

            // Align data
            var alignedData = AlignFactorData(assetReturns, factors);
            if (!alignedData.Any())
            {
                throw new InvalidOperationException("No aligned data for regression");
            }

            // Run regression
            var regressionResult = RunFactorRegression(alignedData, new[] { "Market", "SMB", "HML", "MOM" });

            var model = new Carhart4FactorModel
            {
                MarketBeta = regressionResult.Coefficients.GetValueOrDefault("Market", 0),
                SizeBeta = regressionResult.Coefficients.GetValueOrDefault("SMB", 0),
                ValueBeta = regressionResult.Coefficients.GetValueOrDefault("HML", 0),
                MomentumBeta = regressionResult.Coefficients.GetValueOrDefault("MOM", 0),
                Alpha = regressionResult.Intercept,
                R2 = regressionResult.RSquared,
                AnalysisDate = DateTime.UtcNow
            };

            // Calculate factor returns
            model.FactorReturns = CalculateFactorReturns(alignedData, new[] { "Market", "SMB", "HML", "MOM" });

            _logger.LogInformation("Carhart 4-Factor analysis completed for {Asset}", assetSymbol);
            return model;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze Carhart 4-Factor for {Asset}", assetSymbol);
            throw;
        }
    }

    /// <summary>
    /// Create custom factor model
    /// </summary>
    public async Task<CustomFactorModel> CreateCustomFactorModelAsync(
        string name,
        string description,
        List<string> factorNames,
        string assetSymbol,
        DateTime startDate,
        DateTime endDate)
    {
        try
        {
            _logger.LogInformation("Creating custom factor model {Name} for {Asset}", name, assetSymbol);

            // Get asset returns
            var assetReturns = await GetAssetReturnsAsync(assetSymbol, startDate, endDate);
            if (!assetReturns.Any())
            {
                throw new InvalidOperationException($"No return data available for {assetSymbol}");
            }

            // Get custom factors (simplified - would need factor data source)
            var factors = await GetCustomFactorsAsync(factorNames, startDate, endDate);
            if (!factors.Any())
            {
                throw new InvalidOperationException("No custom factor data available");
            }

            // Align data
            var alignedData = AlignFactorData(assetReturns, factors);
            if (!alignedData.Any())
            {
                throw new InvalidOperationException("No aligned data for regression");
            }

            // Run regression
            var regressionResult = RunFactorRegression(alignedData, factorNames.ToArray());

            var model = new CustomFactorModel(name, description, factorNames)
            {
                Alpha = regressionResult.Intercept,
                R2 = regressionResult.RSquared,
                AnalysisDate = DateTime.UtcNow
            };

            // Set factor exposures
            foreach (var factor in factorNames)
            {
                model.FactorExposures[factor] = regressionResult.Coefficients.GetValueOrDefault(factor, 0);
            }

            // Calculate factor returns
            model.FactorReturns = CalculateFactorReturns(alignedData, factorNames.ToArray());

            _logger.LogInformation("Custom factor model {Name} created for {Asset}", name, assetSymbol);
            return model;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create custom factor model {Name} for {Asset}", name, assetSymbol);
            throw;
        }
    }

    /// <summary>
    /// Perform factor attribution analysis
    /// </summary>
    public async Task<List<FactorAttribution>> PerformFactorAttributionAsync(
        List<string> assets,
        List<string> factors,
        DateTime startDate,
        DateTime endDate)
    {
        try
        {
            _logger.LogInformation("Performing factor attribution analysis for {Count} assets", assets.Count);

            var attributions = new List<FactorAttribution>();

            foreach (var asset in assets)
            {
                var attribution = await PerformSingleAssetAttributionAsync(asset, factors, startDate, endDate);
                if (attribution != null)
                {
                    attributions.Add(attribution);
                }
            }

            _logger.LogInformation("Factor attribution analysis completed for {Count} assets", attributions.Count);
            return attributions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to perform factor attribution analysis");
            throw;
        }
    }

    private async Task<FactorAttribution?> PerformSingleAssetAttributionAsync(
        string asset,
        List<string> factors,
        DateTime startDate,
        DateTime endDate)
    {
        try
        {
            // Get asset returns
            var assetReturns = await GetAssetReturnsAsync(asset, startDate, endDate);
            if (!assetReturns.Any()) return null;

            // Get factor data
            var factorData = await GetCustomFactorsAsync(factors, startDate, endDate);
            if (!factorData.Any()) return null;

            // Align data
            var alignedData = AlignFactorData(assetReturns, factorData);
            if (!alignedData.Any()) return null;

            // Run regression
            var regressionResult = RunFactorRegression(alignedData, factors.ToArray());

            var attribution = new FactorAttribution
            {
                Asset = asset,
                PeriodStart = startDate,
                PeriodEnd = endDate,
                ResidualReturn = regressionResult.Intercept
            };

            // Calculate factor contributions
            foreach (var factor in factors)
            {
                var exposure = regressionResult.Coefficients.GetValueOrDefault(factor, 0);
                var factorReturn = alignedData.Average(d => d.FactorValues.GetValueOrDefault(factor, 0));
                attribution.FactorContributions[factor] = exposure * factorReturn;
            }

            attribution.TotalAttribution = attribution.FactorContributions.Values.Sum() + attribution.ResidualReturn;

            return attribution;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to perform attribution for asset {Asset}", asset);
            return null;
        }
    }

    private async Task<Dictionary<DateTime, double>> GetAssetReturnsAsync(string symbol, DateTime startDate, DateTime endDate)
    {
        // Simplified - in practice would get historical price data and calculate returns
        var returns = new Dictionary<DateTime, double>();

        // Mock data for demonstration - replace with actual data retrieval
        var currentDate = startDate;
        var random = new Random();
        while (currentDate <= endDate)
        {
            returns[currentDate] = (random.NextDouble() - 0.5) * 0.04; // Random daily return ~ ±2%
            currentDate = currentDate.AddDays(1);
        }

        return returns;
    }

    private async Task<List<FactorData>> GetFamaFrenchFactorsAsync(DateTime startDate, DateTime endDate)
    {
        // Simplified - in practice would load from Fama-French data library or API
        var factors = new List<FactorData>();
        var currentDate = startDate;
        var random = new Random(42); // Fixed seed for reproducible results

        while (currentDate <= endDate)
        {
            factors.Add(new FactorData
            {
                Date = currentDate,
                FactorValues = new Dictionary<string, double>
                {
                    ["Market"] = (random.NextDouble() - 0.5) * 0.02,
                    ["SMB"] = (random.NextDouble() - 0.5) * 0.015,
                    ["HML"] = (random.NextDouble() - 0.5) * 0.015
                }
            });
            currentDate = currentDate.AddDays(1);
        }

        return factors;
    }

    private async Task<List<FactorData>> GetCarhartFactorsAsync(DateTime startDate, DateTime endDate)
    {
        var factors = await GetFamaFrenchFactorsAsync(startDate, endDate);

        // Add momentum factor
        var random = new Random(43);
        foreach (var factor in factors)
        {
            factor.FactorValues["MOM"] = (random.NextDouble() - 0.5) * 0.012;
        }

        return factors;
    }

    private async Task<List<FactorData>> GetCustomFactorsAsync(List<string> factorNames, DateTime startDate, DateTime endDate)
    {
        var factors = new List<FactorData>();
        var currentDate = startDate;
        var random = new Random(44);

        while (currentDate <= endDate)
        {
            var factorData = new FactorData { Date = currentDate };
            foreach (var factor in factorNames)
            {
                factorData.FactorValues[factor] = (random.NextDouble() - 0.5) * 0.02;
            }
            factors.Add(factorData);
            currentDate = currentDate.AddDays(1);
        }

        return factors;
    }

    private List<FactorData> AlignFactorData(
        Dictionary<DateTime, double> assetReturns,
        List<FactorData> factors)
    {
        var aligned = new List<FactorData>();

        foreach (var factorData in factors)
        {
            if (assetReturns.ContainsKey(factorData.Date))
            {
                var alignedFactor = new FactorData
                {
                    Date = factorData.Date,
                    FactorValues = new Dictionary<string, double>(factorData.FactorValues)
                };
                alignedFactor.FactorValues["AssetReturn"] = assetReturns[factorData.Date];
                aligned.Add(alignedFactor);
            }
        }

        return aligned;
    }

    private FactorRegressionResult RunFactorRegression(List<FactorData> data, string[] factorNames)
    {
        if (!data.Any() || factorNames.Length == 0)
        {
            throw new InvalidOperationException("Insufficient data for regression");
        }

        var n = data.Count;
        var k = factorNames.Length;

        // Prepare matrices
        var y = Vector<double>.Build.Dense(n); // Asset returns
        var X = Matrix<double>.Build.Dense(n, k + 1); // Factors + intercept

        for (int i = 0; i < n; i++)
        {
            y[i] = data[i].FactorValues.GetValueOrDefault("AssetReturn", 0);

            // Intercept
            X[i, 0] = 1;

            // Factors
            for (int j = 0; j < k; j++)
            {
                X[i, j + 1] = data[i].FactorValues.GetValueOrDefault(factorNames[j], 0);
            }
        }

        // Run OLS regression
        var qr = X.QR();
        var beta = qr.R.Solve(qr.Q.Transpose() * y);

        // Calculate R-squared
        var yPredicted = X * beta;
        var ssRes = (y - yPredicted).PointwisePower(2).Sum();
        var ssTot = (y - y.Average()).PointwisePower(2).Sum();
        var rSquared = 1 - (ssRes / ssTot);

        // Calculate residuals
        var residuals = (y - yPredicted).ToList();

        // Simplified statistics (in practice would calculate proper t-stats and p-values)
        var coefficients = new Dictionary<string, double>();
        var tStats = new Dictionary<string, double>();
        var pValues = new Dictionary<string, double>();

        for (int j = 0; j < k; j++)
        {
            coefficients[factorNames[j]] = beta[j + 1];
            tStats[factorNames[j]] = beta[j + 1] / 0.01; // Simplified
            pValues[factorNames[j]] = 0.05; // Simplified
        }

        return new FactorRegressionResult
        {
            Coefficients = coefficients,
            Intercept = beta[0],
            RSquared = rSquared,
            AdjustedRSquared = 1 - ((1 - rSquared) * (n - 1) / (n - k - 1)),
            TStatistics = tStats,
            PValues = pValues,
            Residuals = residuals,
            AnalysisDate = DateTime.UtcNow
        };
    }

    private Dictionary<string, double> CalculateFactorReturns(List<FactorData> data, string[] factorNames)
    {
        var factorReturns = new Dictionary<string, double>();

        foreach (var factor in factorNames)
        {
            var returns = data.Select(d => d.FactorValues.GetValueOrDefault(factor, 0)).ToList();
            factorReturns[factor] = returns.Any() ? returns.Average() : 0;
        }

        return factorReturns;
    }
}
