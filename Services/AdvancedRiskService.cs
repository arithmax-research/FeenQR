using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MathNet.Numerics.Distributions;
using MathNet.Numerics.Statistics;
using QuantResearchAgent.Core;
using QuantResearchAgent.Services;

namespace QuantResearchAgent.Services
{
    public class AdvancedRiskService
    {
        private readonly MarketDataService _marketDataService;
        private readonly StatisticalTestingService _statisticalService;

        public AdvancedRiskService(
            MarketDataService marketDataService,
            StatisticalTestingService statisticalService)
        {
            _marketDataService = marketDataService;
            _statisticalService = statisticalService;
        }

        // Value-at-Risk (VaR) Calculations
        public async Task<ValueAtRisk> CalculateVaRAsync(
            Dictionary<string, double> portfolioWeights,
            double confidenceLevel,
            string method,
            DateTime startDate,
            DateTime endDate,
            int? monteCarloSimulations = null)
        {
            var var = new ValueAtRisk
            {
                AnalysisDate = DateTime.Now,
                Method = method,
                ConfidenceLevel = confidenceLevel
            };

            // Get historical returns for all assets
            var assetReturns = new Dictionary<string, List<double>>();
            foreach (var asset in portfolioWeights.Keys)
            {
                var data = await _marketDataService.GetHistoricalDataAsync(asset, 1000);
                var filteredData = data?.Where(d => d.Timestamp >= startDate && d.Timestamp <= endDate)
                    .OrderBy(d => d.Timestamp).ToList() ?? new List<MarketData>();
                var prices = filteredData.Select(d => d.Price).ToList();
                assetReturns[asset] = CalculateReturns(prices);
            }

            // Calculate portfolio returns
            var portfolioReturns = CalculatePortfolioReturns(portfolioWeights, assetReturns);

            switch (method.ToLower())
            {
                case "historical":
                    var.VaR = CalculateHistoricalVaR(portfolioReturns, confidenceLevel);
                    break;
                case "parametric":
                    var.VaR = CalculateParametricVaR(portfolioReturns, confidenceLevel);
                    break;
                case "montecarlo":
                    var.VaR = await CalculateMonteCarloVaRAsync(portfolioWeights, assetReturns, confidenceLevel, monteCarloSimulations ?? 10000);
                    break;
                default:
                    throw new ArgumentException($"Unknown VaR method: {method}");
            }

            // Calculate Expected Shortfall (CVaR)
            var.ExpectedShortfall = CalculateExpectedShortfall(portfolioReturns, var.VaR);

            // Calculate Component VaR
            var.ComponentVaR = CalculateComponentVaR(portfolioWeights, assetReturns, confidenceLevel);

            // Calculate diversification ratio
            var.DiversificationRatio = CalculateDiversificationRatio(portfolioWeights, assetReturns);

            return var;
        }

        // Expected Shortfall (Conditional VaR)
        public double CalculateExpectedShortfall(List<double> returns, double var)
        {
            var losses = returns.Where(r => r <= -var).ToList();
            return losses.Any() ? -losses.Average() : 0;
        }

        // Stress Testing Framework
        public async Task<List<StressTestResult>> RunStressTestsAsync(
            Dictionary<string, double> portfolioWeights,
            List<Dictionary<string, double>> stressScenarios,
            List<string> scenarioNames,
            double threshold,
            DateTime startDate,
            DateTime endDate)
        {
            var results = new List<StressTestResult>();

            // Get current market data for baseline
            var assetReturns = new Dictionary<string, List<double>>();
            foreach (var asset in portfolioWeights.Keys)
            {
                var data = await _marketDataService.GetHistoricalDataAsync(asset, 1000);
                var filteredData = data?.Where(d => d.Timestamp >= startDate && d.Timestamp <= endDate)
                    .OrderBy(d => d.Timestamp).ToList() ?? new List<MarketData>();
                var prices = filteredData.Select(d => d.Price).ToList();
                assetReturns[asset] = CalculateReturns(prices);
            }

            for (int i = 0; i < stressScenarios.Count; i++)
            {
                var scenario = stressScenarios[i];
                var scenarioName = scenarioNames[i];

                var result = new StressTestResult
                {
                    AnalysisDate = DateTime.Now,
                    ScenarioName = scenarioName,
                    ShockReturns = scenario,
                    Threshold = threshold
                };

                // Calculate portfolio return under stress scenario
                var portfolioReturn = 0.0;
                var assetContributions = new Dictionary<string, double>();

                foreach (var asset in portfolioWeights.Keys)
                {
                    var shockReturn = scenario.GetValueOrDefault(asset, 0);
                    var contribution = portfolioWeights[asset] * shockReturn;
                    portfolioReturn += contribution;
                    assetContributions[asset] = contribution;
                }

                result.PortfolioReturn = portfolioReturn;
                result.PortfolioLoss = -portfolioReturn; // Loss is negative return
                result.AssetContributions = assetContributions;
                result.BreachThreshold = result.PortfolioLoss > threshold;

                results.Add(result);
            }

            return results;
        }

        // Risk Factor Attribution
        public async Task<RiskFactorAttribution> PerformRiskFactorAttributionAsync(
            Dictionary<string, double> portfolioWeights,
            List<string> factors,
            DateTime startDate,
            DateTime endDate)
        {
            var attribution = new RiskFactorAttribution { AnalysisDate = DateTime.Now };

            // Get asset returns
            var assetReturns = new Dictionary<string, List<double>>();
            foreach (var asset in portfolioWeights.Keys)
            {
                var data = await _marketDataService.GetHistoricalDataAsync(asset, 1000);
                var filteredData = data?.Where(d => d.Timestamp >= startDate && d.Timestamp <= endDate)
                    .OrderBy(d => d.Timestamp).ToList() ?? new List<MarketData>();
                var prices = filteredData.Select(d => d.Price).ToList();
                assetReturns[asset] = CalculateReturns(prices);
            }

            // Calculate portfolio returns
            var portfolioReturns = CalculatePortfolioReturns(portfolioWeights, assetReturns);

            // For demo: use simplified factor model (can be extended to use actual factor data)
            var factorReturns = GenerateFactorReturns(factors, portfolioReturns.Count);

            // Perform regression: Portfolio Return = α + Σ(β_i * Factor_i) + ε
            var factorExposures = new Dictionary<string, double>();
            var factorContributions = new Dictionary<string, double>();

            // Simplified: assume equal factor exposures for demo
            var equalExposure = 1.0 / factors.Count;
            foreach (var factor in factors)
            {
                factorExposures[factor] = equalExposure;
            }

            // Calculate factor contributions
            var totalAttribution = 0.0;
            foreach (var factor in factors)
            {
                var contribution = factorExposures[factor] * factorReturns[factor].Average();
                factorContributions[factor] = contribution;
                totalAttribution += contribution;
            }

            attribution.FactorExposures = factorExposures;
            attribution.FactorContributions = factorContributions;
            attribution.TotalAttribution = totalAttribution;
            attribution.ResidualRisk = portfolioReturns.Variance() - totalAttribution;
            attribution.R2 = totalAttribution / portfolioReturns.Variance();

            return attribution;
        }

        // Comprehensive Risk Report
        public async Task<RiskReport> GenerateRiskReportAsync(
            Dictionary<string, double> portfolioWeights,
            List<Dictionary<string, double>> stressScenarios,
            List<string> scenarioNames,
            List<string> factors,
            DateTime startDate,
            DateTime endDate)
        {
            var report = new RiskReport { ReportDate = DateTime.Now };

            // Calculate VaR (using historical method at 95% confidence)
            report.VaR = await CalculateVaRAsync(portfolioWeights, 0.95, "historical", startDate, endDate);

            // Run stress tests
            report.StressTests = await RunStressTestsAsync(portfolioWeights, stressScenarios, scenarioNames, 0.05, startDate, endDate);

            // Perform factor attribution
            report.FactorAttribution = await PerformRiskFactorAttributionAsync(portfolioWeights, factors, startDate, endDate);

            // Calculate additional risk metrics
            var assetReturns = new Dictionary<string, List<double>>();
            foreach (var asset in portfolioWeights.Keys)
            {
                var data = await _marketDataService.GetHistoricalDataAsync(asset, 1000);
                var filteredData = data?.Where(d => d.Timestamp >= startDate && d.Timestamp <= endDate)
                    .OrderBy(d => d.Timestamp).ToList() ?? new List<MarketData>();
                var prices = filteredData.Select(d => d.Price).ToList();
                assetReturns[asset] = CalculateReturns(prices);
            }

            var portfolioReturns = CalculatePortfolioReturns(portfolioWeights, assetReturns);

            report.RiskMetrics["Volatility"] = portfolioReturns.StandardDeviation();
            report.RiskMetrics["SharpeRatio"] = portfolioReturns.Average() / portfolioReturns.StandardDeviation();
            report.RiskMetrics["MaxDrawdown"] = CalculateMaxDrawdown(portfolioReturns);
            report.RiskMetrics["Skewness"] = portfolioReturns.Skewness();
            report.RiskMetrics["Kurtosis"] = portfolioReturns.Kurtosis();
            report.RiskMetrics["VaR_99"] = await CalculateVaRAsync(portfolioWeights, 0.99, "historical", startDate, endDate).ContinueWith(t => t.Result.VaR);

            // Determine risk rating
            var volatility = report.RiskMetrics["Volatility"];
            var var95 = report.VaR.VaR;
            var maxDrawdown = report.RiskMetrics["MaxDrawdown"];

            if (volatility < 0.15 && var95 < 0.03 && maxDrawdown < 0.10)
                report.RiskRating = "Low";
            else if (volatility < 0.25 && var95 < 0.05 && maxDrawdown < 0.20)
                report.RiskRating = "Medium";
            else if (volatility < 0.35 && var95 < 0.08 && maxDrawdown < 0.30)
                report.RiskRating = "High";
            else
                report.RiskRating = "Extreme";

            return report;
        }

        // Helper methods
        private List<double> CalculatePortfolioReturns(
            Dictionary<string, double> weights,
            Dictionary<string, List<double>> assetReturns)
        {
            var portfolioReturns = new List<double>();
            var minLength = assetReturns.Values.Min(r => r.Count);

            for (int i = 0; i < minLength; i++)
            {
                var portfolioReturn = 0.0;
                foreach (var asset in weights.Keys)
                {
                    portfolioReturn += weights[asset] * assetReturns[asset][i];
                }
                portfolioReturns.Add(portfolioReturn);
            }

            return portfolioReturns;
        }

        private double CalculateHistoricalVaR(List<double> returns, double confidenceLevel)
        {
            returns.Sort();
            var index = (int)Math.Ceiling((1 - confidenceLevel) * returns.Count);
            return -returns[index - 1]; // VaR is positive for loss
        }

        private double CalculateParametricVaR(List<double> returns, double confidenceLevel)
        {
            var mean = returns.Average();
            var std = returns.StandardDeviation();
            var zScore = Normal.InvCDF(0, 1, confidenceLevel);
            return -(mean + zScore * std);
        }

        private async Task<double> CalculateMonteCarloVaRAsync(
            Dictionary<string, double> weights,
            Dictionary<string, List<double>> assetReturns,
            double confidenceLevel,
            int simulations)
        {
            var random = new Random();
            var simulatedReturns = new List<double>();

            // Fit distributions to each asset
            var distributions = new Dictionary<string, Normal>();
            foreach (var asset in assetReturns.Keys)
            {
                var returns = assetReturns[asset];
                distributions[asset] = new Normal(returns.Average(), returns.StandardDeviation());
            }

            // Run Monte Carlo simulations
            for (int i = 0; i < simulations; i++)
            {
                var portfolioReturn = 0.0;
                foreach (var asset in weights.Keys)
                {
                    var simulatedReturn = distributions[asset].Sample();
                    portfolioReturn += weights[asset] * simulatedReturn;
                }
                simulatedReturns.Add(portfolioReturn);
            }

            simulatedReturns.Sort();
            var index = (int)Math.Ceiling((1 - confidenceLevel) * simulations);
            return -simulatedReturns[index - 1];
        }

        private Dictionary<string, double> CalculateComponentVaR(
            Dictionary<string, double> weights,
            Dictionary<string, List<double>> assetReturns,
            double confidenceLevel)
        {
            var componentVaR = new Dictionary<string, double>();
            var portfolioReturns = CalculatePortfolioReturns(weights, assetReturns);
            var portfolioVaR = CalculateHistoricalVaR(portfolioReturns, confidenceLevel);

            foreach (var asset in weights.Keys)
            {
                // Simplified component VaR calculation
                var assetWeight = weights[asset];
                var assetVolatility = assetReturns[asset].StandardDeviation();
                var beta = CalculateBeta(assetReturns[asset], portfolioReturns);
                componentVaR[asset] = assetWeight * beta * portfolioVaR;
            }

            return componentVaR;
        }

        private double CalculateDiversificationRatio(
            Dictionary<string, double> weights,
            Dictionary<string, List<double>> assetReturns)
        {
            var portfolioVolatility = CalculatePortfolioReturns(weights, assetReturns).StandardDeviation();
            var weightedVolatilitySum = 0.0;

            foreach (var asset in weights.Keys)
            {
                weightedVolatilitySum += weights[asset] * assetReturns[asset].StandardDeviation();
            }

            return weightedVolatilitySum / portfolioVolatility;
        }

        private double CalculateBeta(List<double> assetReturns, List<double> marketReturns)
        {
            var covariance = assetReturns.Zip(marketReturns, (a, m) => (a - assetReturns.Average()) * (m - marketReturns.Average())).Average();
            var marketVariance = marketReturns.Variance();
            return covariance / marketVariance;
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

        private Dictionary<string, List<double>> GenerateFactorReturns(List<string> factors, int count)
        {
            var random = new Random();
            var factorReturns = new Dictionary<string, List<double>>();

            foreach (var factor in factors)
            {
                var returns = new List<double>();
                for (int i = 0; i < count; i++)
                {
                    // Generate random factor returns (can be replaced with actual factor data)
                    returns.Add(Normal.Sample(0, 0.02)); // Mean 0, std 2%
                }
                factorReturns[factor] = returns;
            }

            return factorReturns;
        }

        private List<double> CalculateReturns(List<double> prices)
        {
            var returns = new List<double>();
            for (int i = 1; i < prices.Count; i++)
            {
                if (prices[i - 1] != 0)
                {
                    var ret = (prices[i] - prices[i - 1]) / prices[i - 1];
                    returns.Add(ret);
                }
            }
            return returns;
        }
    }
}