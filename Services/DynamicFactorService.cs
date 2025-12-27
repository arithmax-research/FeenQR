using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.Statistics;
using Microsoft.SemanticKernel;
using QuantResearchAgent.Core;
using QuantResearchAgent.Services;

namespace QuantResearchAgent.Services
{
    public class DynamicFactorService
    {
        private readonly MarketDataService _marketDataService;
        private readonly Kernel _kernel;

        public DynamicFactorService(MarketDataService marketDataService, Kernel kernel)
        {
            _marketDataService = marketDataService;
            _kernel = kernel;
        }

        // Dynamic factor models for market environments
        public async Task<string> ComputeDynamicFactorsAsync(object marketData)
        {
            try
            {
                var data = marketData as Dictionary<string, object> ?? new Dictionary<string, object>();
                var symbols = (data.GetValueOrDefault("symbols", "SPY,AAPL,MSFT") as string)?.Split(',').Select(s => s.Trim()).ToList();
                var lookbackDays = Convert.ToInt32(data.GetValueOrDefault("lookbackDays", 252));

                if (symbols == null || !symbols.Any())
                    return "No symbols provided for factor analysis";

                var factorModel = await BuildDynamicFactorModelAsync(symbols, lookbackDays);
                return FormatFactorModelReport(factorModel);
            }
            catch (Exception ex)
            {
                return $"Error computing dynamic factors: {ex.Message}";
            }
        }

        public async Task<DynamicFactorModel> BuildDynamicFactorModelAsync(List<string> symbols, int lookbackDays)
        {
            // Get data for all symbols
            var assetData = new Dictionary<string, List<MarketData>>();
            var returns = new Dictionary<string, List<double>>();

            foreach (var symbol in symbols)
            {
                var data = await _marketDataService.GetHistoricalDataAsync(symbol, lookbackDays);
                if (data != null && data.Any())
                {
                    assetData[symbol] = data;
                    returns[symbol] = CalculateReturns(data.Select(d => d.Price).ToList());
                }
            }

            if (!returns.Any())
                throw new InvalidOperationException("No valid return data available");

            // Extract dynamic factors
            var marketFactor = await ExtractMarketFactorAsync(returns);
            var sizeFactor = await ExtractSizeFactorAsync(returns);
            var valueFactor = await ExtractValueFactorAsync(returns);
            var momentumFactor = await ExtractMomentumFactorAsync(returns);
            var volatilityFactor = await ExtractVolatilityFactorAsync(returns);

            // Build factor covariance matrix
            var factorReturns = new List<List<double>>
            {
                marketFactor.Returns,
                sizeFactor.Returns,
                valueFactor.Returns,
                momentumFactor.Returns,
                volatilityFactor.Returns
            };

            var factorCovariance = CalculateCovarianceMatrix(factorReturns);

            return new DynamicFactorModel
            {
                Symbols = symbols,
                Factors = new List<Factor>
                {
                    marketFactor,
                    sizeFactor,
                    valueFactor,
                    momentumFactor,
                    volatilityFactor
                },
                FactorCovariance = factorCovariance,
                AssetFactorLoadings = await CalculateFactorLoadingsAsync(returns, factorReturns),
                RegimeAdjustedFactors = await CalculateRegimeAdjustmentsAsync(returns)
            };
        }

        private async Task<Factor> ExtractMarketFactorAsync(Dictionary<string, List<double>> returns)
        {
            // Market factor is the equal-weighted average of all asset returns
            var marketReturns = new List<double>();
            var dates = returns.First().Value.Count;

            for (int i = 0; i < dates; i++)
            {
                var dailyReturns = returns.Values
                    .Where(r => i < r.Count)
                    .Select(r => r[i])
                    .ToList();

                if (dailyReturns.Any())
                    marketReturns.Add(dailyReturns.Average());
            }

            return new Factor
            {
                Name = "Market",
                Type = FactorType.Market,
                Returns = marketReturns,
                Volatility = marketReturns.StandardDeviation(),
                SharpeRatio = CalculateSharpeRatio(marketReturns)
            };
        }

        private async Task<Factor> ExtractSizeFactorAsync(Dictionary<string, List<double>> returns)
        {
            // Size factor based on relative performance of small vs large cap assets
            // Simplified: use volatility as proxy for size (smaller companies tend to be more volatile)
            var sizeReturns = new List<double>();

            foreach (var kvp in returns)
            {
                var assetReturns = kvp.Value;
                var volatility = assetReturns.StandardDeviation();
                // Higher volatility = smaller size factor loading
                sizeReturns.Add(assetReturns.Last() * (1 + volatility));
            }

            return new Factor
            {
                Name = "Size",
                Type = FactorType.Size,
                Returns = sizeReturns,
                Volatility = sizeReturns.StandardDeviation(),
                SharpeRatio = CalculateSharpeRatio(sizeReturns)
            };
        }

        private async Task<Factor> ExtractValueFactorAsync(Dictionary<string, List<double>> returns)
        {
            // Value factor based on mean-reversion characteristics
            var valueReturns = new List<double>();

            foreach (var kvp in returns)
            {
                var assetReturns = kvp.Value;
                // Value factor: assets that have underperformed recently tend to rebound
                var recentPerformance = assetReturns.TakeLast(20).Average();
                var longerPerformance = assetReturns.TakeLast(60).Average();

                var valueSignal = longerPerformance - recentPerformance; // Mean reversion signal
                valueReturns.Add(valueSignal);
            }

            return new Factor
            {
                Name = "Value",
                Type = FactorType.Value,
                Returns = valueReturns,
                Volatility = valueReturns.StandardDeviation(),
                SharpeRatio = CalculateSharpeRatio(valueReturns)
            };
        }

        private async Task<Factor> ExtractMomentumFactorAsync(Dictionary<string, List<double>> returns)
        {
            // Momentum factor based on recent performance continuation
            var momentumReturns = new List<double>();

            foreach (var kvp in returns)
            {
                var assetReturns = kvp.Value;
                if (assetReturns.Count >= 20)
                {
                    // Short-term momentum (last 20 days)
                    var shortTerm = assetReturns.TakeLast(20).Average();
                    // Medium-term momentum (last 60 days)
                    var mediumTerm = assetReturns.TakeLast(60).Average();

                    var momentumSignal = shortTerm - mediumTerm;
                    momentumReturns.Add(momentumSignal);
                }
            }

            return new Factor
            {
                Name = "Momentum",
                Type = FactorType.Momentum,
                Returns = momentumReturns,
                Volatility = momentumReturns.StandardDeviation(),
                SharpeRatio = CalculateSharpeRatio(momentumReturns)
            };
        }

        private async Task<Factor> ExtractVolatilityFactorAsync(Dictionary<string, List<double>> returns)
        {
            // Volatility factor based on changes in volatility regimes
            var volatilityReturns = new List<double>();

            foreach (var kvp in returns)
            {
                var assetReturns = kvp.Value;
                if (assetReturns.Count >= 20)
                {
                    var recentVol = assetReturns.TakeLast(20).StandardDeviation();
                    var longerVol = assetReturns.TakeLast(60).StandardDeviation();

                    var volChange = recentVol - longerVol;
                    volatilityReturns.Add(volChange);
                }
            }

            return new Factor
            {
                Name = "Volatility",
                Type = FactorType.Volatility,
                Returns = volatilityReturns,
                Volatility = volatilityReturns.StandardDeviation(),
                SharpeRatio = CalculateSharpeRatio(volatilityReturns)
            };
        }

        private Matrix<double> CalculateCovarianceMatrix(List<List<double>> factorReturns)
        {
            var factorCount = factorReturns.Count;
            var dataPoints = factorReturns[0].Count;

            var matrix = Matrix<double>.Build.Dense(factorCount, factorCount);

            for (int i = 0; i < factorCount; i++)
            {
                for (int j = 0; j < factorCount; j++)
                {
                    matrix[i, j] = Correlation.Pearson(factorReturns[i], factorReturns[j]) *
                                 factorReturns[i].StandardDeviation() *
                                 factorReturns[j].StandardDeviation();
                }
            }

            return matrix;
        }

        private async Task<Dictionary<string, Dictionary<string, double>>> CalculateFactorLoadingsAsync(
            Dictionary<string, List<double>> assetReturns,
            List<List<double>> factorReturns)
        {
            var loadings = new Dictionary<string, Dictionary<string, double>>();
            var factorNames = new[] { "Market", "Size", "Value", "Momentum", "Volatility" };

            foreach (var asset in assetReturns)
            {
                var assetLoadings = new Dictionary<string, double>();

                for (int i = 0; i < factorNames.Length && i < factorReturns.Count; i++)
                {
                    var factorReturn = factorReturns[i];
                    var correlation = Correlation.Pearson(asset.Value, factorReturn);
                    assetLoadings[factorNames[i]] = correlation;
                }

                loadings[asset.Key] = assetLoadings;
            }

            return loadings;
        }

        private async Task<Dictionary<string, double>> CalculateRegimeAdjustmentsAsync(Dictionary<string, List<double>> returns)
        {
            // Calculate regime-based adjustments for factor exposures
            var adjustments = new Dictionary<string, double>();

            // Simple regime detection based on recent volatility
            var recentVolatility = returns.Values
                .Select(r => r.TakeLast(20).StandardDeviation())
                .Average();

            var longTermVolatility = returns.Values
                .Select(r => r.TakeLast(60).StandardDeviation())
                .Average();

            var regimeMultiplier = recentVolatility > longTermVolatility * 1.2 ? 1.5 :
                                 recentVolatility < longTermVolatility * 0.8 ? 0.7 : 1.0;

            adjustments["HighVolatilityRegime"] = regimeMultiplier;
            adjustments["LowVolatilityRegime"] = 2.0 - regimeMultiplier;

            return adjustments;
        }

        private List<double> CalculateReturns(List<double> prices)
        {
            var returns = new List<double>();
            for (int i = 1; i < prices.Count; i++)
            {
                returns.Add((prices[i] - prices[i - 1]) / prices[i - 1]);
            }
            return returns;
        }

        private double CalculateSharpeRatio(List<double> returns)
        {
            if (!returns.Any()) return 0;

            var avgReturn = returns.Average();
            var volatility = returns.StandardDeviation();
            var riskFreeRate = 0.02 / 252; // Daily risk-free rate

            return volatility > 0 ? (avgReturn - riskFreeRate) / volatility : 0;
        }

        private string FormatFactorModelReport(DynamicFactorModel model)
        {
            var result = new System.Text.StringBuilder();

            result.AppendLine("Dynamic Factor Model Analysis");
            result.AppendLine($"Assets Analyzed: {string.Join(", ", model.Symbols)}");
            result.AppendLine();

            result.AppendLine("Factor Summary:");
            foreach (var factor in model.Factors)
            {
                result.AppendLine($"- {factor.Name}:");
                result.AppendLine($"  Volatility: {factor.Volatility:P2}");
                result.AppendLine($"  Sharpe Ratio: {factor.SharpeRatio:F2}");
                result.AppendLine($"  Data Points: {factor.Returns.Count}");
            }
            result.AppendLine();

            result.AppendLine("Factor Correlations:");
            var factorNames = model.Factors.Select(f => f.Name).ToArray();
            for (int i = 0; i < factorNames.Length; i++)
            {
                for (int j = i + 1; j < factorNames.Length; j++)
                {
                    var correlation = Correlation.Pearson(model.Factors[i].Returns, model.Factors[j].Returns);
                    result.AppendLine($"{factorNames[i]} vs {factorNames[j]}: {correlation:F2}");
                }
            }
            result.AppendLine();

            result.AppendLine("Asset Factor Loadings:");
            foreach (var asset in model.AssetFactorLoadings)
            {
                result.AppendLine($"{asset.Key}:");
                foreach (var loading in asset.Value)
                {
                    result.AppendLine($"  {loading.Key}: {loading.Value:F2}");
                }
            }

            return result.ToString();
        }
    }

    public enum FactorType
    {
        Market,
        Size,
        Value,
        Momentum,
        Volatility,
        Quality,
        Growth
    }

    public class Factor
    {
        public required string Name { get; set; }
        public FactorType Type { get; set; }
        public required List<double> Returns { get; set; }
        public double Volatility { get; set; }
        public double SharpeRatio { get; set; }
    }

    public class DynamicFactorModel
    {
        public required List<string> Symbols { get; set; }
        public required List<Factor> Factors { get; set; }
        public required Matrix<double> FactorCovariance { get; set; }
        public required Dictionary<string, Dictionary<string, double>> AssetFactorLoadings { get; set; }
        public required Dictionary<string, double> RegimeAdjustedFactors { get; set; }
    }
}
