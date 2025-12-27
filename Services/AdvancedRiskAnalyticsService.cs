using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using QuantResearchAgent.Core;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.Statistics;

namespace QuantResearchAgent.Services
{
    /// <summary>
    /// Advanced risk analytics service providing CVaR, Expected Shortfall, Black-Litterman model, and risk parity optimization
    /// </summary>
    public class AdvancedRiskAnalyticsService
    {
        private readonly ILogger<AdvancedRiskAnalyticsService> _logger;
        private readonly MarketDataService _marketDataService;
        private readonly StatisticalTestingService _statisticalTestingService;

        public AdvancedRiskAnalyticsService(
            ILogger<AdvancedRiskAnalyticsService> logger,
            MarketDataService marketDataService,
            StatisticalTestingService statisticalTestingService)
        {
            _logger = logger;
            _marketDataService = marketDataService;
            _statisticalTestingService = statisticalTestingService;
        }

        /// <summary>
        /// Calculate Conditional Value at Risk (CVaR) using historical simulation
        /// </summary>
        public async Task<CVaRResult> CalculateCVaRAsync(List<string> symbols, decimal confidenceLevel = 0.95m, int lookbackDays = 252)
        {
            _logger.LogInformation($"Calculating CVaR for {symbols.Count} assets at {confidenceLevel:P2} confidence level");

            try
            {
                // Get historical returns for all symbols
                var returnsData = new Dictionary<string, List<double>>();
                var currentPrices = new Dictionary<string, decimal>();

                foreach (var symbol in symbols)
                {
                    var historicalData = await _marketDataService.GetHistoricalDataAsync(symbol, lookbackDays);
                    if (historicalData != null && historicalData.Count > 1)
                    {
                        var prices = historicalData.Select(d => (double)d.Close).ToList();
                        var returns = CalculateReturns(prices);
                        returnsData[symbol] = returns;
                        currentPrices[symbol] = historicalData.Last().Close;
                    }
                }

                if (!returnsData.Any())
                    throw new InvalidOperationException("No valid historical data found for CVaR calculation");

                // Calculate portfolio returns for different scenarios
                var portfolioReturns = GeneratePortfolioReturns(returnsData, 10000);

                // Calculate VaR first
                var sortedReturns = portfolioReturns.OrderBy(r => r).ToList();
                var varIndex = (int)Math.Floor((1 - (double)confidenceLevel) * sortedReturns.Count);
                var varValue = sortedReturns[varIndex];

                // Calculate CVaR (Expected Shortfall) - average of returns beyond VaR
                var tailLosses = sortedReturns.Take(varIndex + 1).ToList();
                var cvar = tailLosses.Average();

                return new CVaRResult
                {
                    ConfidenceLevel = confidenceLevel,
                    VaR = (decimal)varValue,
                    CVaR = (decimal)cvar,
                    ExpectedShortfall = (decimal)cvar,
                    SampleSize = portfolioReturns.Count,
                    CalculationMethod = "Historical Simulation",
                    Timestamp = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating CVaR");
                throw;
            }
        }

        /// <summary>
        /// Calculate Expected Shortfall (ES) using Monte Carlo simulation
        /// </summary>
        public async Task<ExpectedShortfallResult> CalculateExpectedShortfallAsync(List<string> symbols, decimal confidenceLevel = 0.95m, int simulations = 10000)
        {
            _logger.LogInformation($"Calculating Expected Shortfall for {symbols.Count} assets using {simulations} simulations");

            try
            {
                // Get historical data for covariance matrix estimation
                var returnsData = new Dictionary<string, List<double>>();
                var meanReturns = new Dictionary<string, double>();
                var currentPrices = new Dictionary<string, decimal>();

                foreach (var symbol in symbols)
                {
                    var historicalData = await _marketDataService.GetHistoricalDataAsync(symbol, 252);
                    if (historicalData != null && historicalData.Count > 30)
                    {
                        var prices = historicalData.Select(d => (double)d.Close).ToList();
                        var returns = CalculateReturns(prices);
                        returnsData[symbol] = returns;
                        meanReturns[symbol] = returns.Average();
                        currentPrices[symbol] = historicalData.Last().Close;
                    }
                }

                if (returnsData.Count < 2)
                    throw new InvalidOperationException("Need at least 2 assets with sufficient historical data");

                // Calculate covariance matrix
                var covarianceMatrix = CalculateCovarianceMatrix(returnsData);

                // Generate Monte Carlo scenarios
                var random = new Random(42); // Fixed seed for reproducibility
                var portfolioReturns = new List<double>();

                for (int i = 0; i < simulations; i++)
                {
                    // Generate random returns using multivariate normal distribution
                    var scenarioReturns = GenerateMultivariateNormalReturns(meanReturns, covarianceMatrix, random);

                    // Assume equal weight portfolio for simplicity
                    var portfolioReturn = scenarioReturns.Values.Average();
                    portfolioReturns.Add(portfolioReturn);
                }

                // Calculate Expected Shortfall
                var sortedReturns = portfolioReturns.OrderBy(r => r).ToList();
                var varIndex = (int)Math.Floor((1 - (double)confidenceLevel) * sortedReturns.Count);
                var tailLosses = sortedReturns.Take(varIndex + 1).ToList();
                var expectedShortfall = tailLosses.Average();

                return new ExpectedShortfallResult
                {
                    ConfidenceLevel = confidenceLevel,
                    ExpectedShortfall = (decimal)expectedShortfall,
                    Simulations = simulations,
                    Method = "Monte Carlo Simulation",
                    Timestamp = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating Expected Shortfall");
                throw;
            }
        }

        /// <summary>
        /// Implement Black-Litterman model for portfolio optimization with views
        /// </summary>
        public async Task<BlackLittermanResult> CalculateBlackLittermanAsync(
            List<string> symbols,
            Dictionary<string, double> marketWeights,
            Dictionary<string, (double expectedReturn, double confidence)> views,
            double riskAversion = 2.5,
            double tau = 0.05)
        {
            _logger.LogInformation($"Calculating Black-Litterman optimization for {symbols.Count} assets with {views.Count} views");

            try
            {
                // Get historical data for covariance estimation
                var returnsData = new Dictionary<string, List<double>>();
                var marketReturns = new List<double>();

                foreach (var symbol in symbols)
                {
                    var historicalData = await _marketDataService.GetHistoricalDataAsync(symbol, 252);
                    if (historicalData != null && historicalData.Count > 30)
                    {
                        var prices = historicalData.Select(d => (double)d.Close).ToList();
                        var returns = CalculateReturns(prices);
                        returnsData[symbol] = returns;
                    }
                }

                // Calculate covariance matrix
                var covarianceMatrix = CalculateCovarianceMatrix(returnsData);
                var covArray = covarianceMatrix.ToArray();

                // Calculate market equilibrium returns
                var marketWeightsVector = Vector<double>.Build.Dense(symbols.Select(s => marketWeights.GetValueOrDefault(s, 0)).ToArray());
                var equilibriumReturns = tau * covArray * marketWeightsVector * riskAversion;

                // Process views and create P matrix and Q vector
                var P = Matrix<double>.Build.Dense(views.Count, symbols.Count);
                var Q = Vector<double>.Build.Dense(views.Count);
                var omega = Matrix<double>.Build.Dense(views.Count, views.Count);

                int viewIndex = 0;
                foreach (var view in views)
                {
                    var symbolIndex = symbols.IndexOf(view.Key);
                    if (symbolIndex >= 0)
                    {
                        P[viewIndex, symbolIndex] = 1;
                        Q[viewIndex] = view.Value.expectedReturn;
                        omega[viewIndex, viewIndex] = (1 - view.Value.confidence) / view.Value.confidence;
                    }
                    viewIndex++;
                }

                // Black-Litterman formula
                var tauSigma = tau * covArray;
                var omegaInv = omega.Inverse();
                var tempMatrix = P.Transpose() * omegaInv * P + tauSigma.Inverse();
                var posteriorCovariance = tempMatrix.Inverse();

                var tempVector = P.Transpose() * omegaInv * Q + tauSigma.Inverse() * equilibriumReturns;
                var posteriorReturns = posteriorCovariance * tempVector;

                // Calculate optimal weights
                var optimalWeights = posteriorCovariance * posteriorReturns / riskAversion;

                return new BlackLittermanResult
                {
                    OptimalWeights = symbols.Zip(optimalWeights.ToArray(), (s, w) => new AssetWeight { Symbol = s, Weight = (decimal)w }).ToList(),
                    PosteriorReturns = symbols.Zip(posteriorReturns.ToArray(), (s, r) => new AssetReturn { Symbol = s, ExpectedReturn = (decimal)r }).ToList(),
                    PosteriorCovariance = posteriorCovariance.ToArray(),
                    EquilibriumReturns = equilibriumReturns.ToArray().Select(r => (decimal)r).ToList(),
                    Views = views.Select(v => new View { Symbol = v.Key, ExpectedReturn = (decimal)v.Value.expectedReturn, Confidence = (decimal)v.Value.confidence }).ToList(),
                    Timestamp = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating Black-Litterman optimization");
                throw;
            }
        }

        /// <summary>
        /// Calculate Risk Parity portfolio weights
        /// </summary>
        public async Task<RiskParityResult> CalculateRiskParityAsync(List<string> symbols, int maxIterations = 100, double tolerance = 1e-6)
        {
            _logger.LogInformation($"Calculating Risk Parity weights for {symbols.Count} assets");

            try
            {
                // Get historical data
                var returnsData = new Dictionary<string, List<double>>();

                foreach (var symbol in symbols)
                {
                    var historicalData = await _marketDataService.GetHistoricalDataAsync(symbol, 252);
                    if (historicalData != null && historicalData.Count > 30)
                    {
                        var prices = historicalData.Select(d => (double)d.Close).ToList();
                        var returns = CalculateReturns(prices);
                        returnsData[symbol] = returns;
                    }
                }

                // Calculate covariance matrix
                var covarianceMatrix = CalculateCovarianceMatrix(returnsData);
                var covArray = covarianceMatrix.ToArray();

                // Initialize equal weights
                var weights = Vector<double>.Build.Dense(symbols.Count, 1.0 / symbols.Count);

                // Risk parity optimization using iterative algorithm
                for (int iteration = 0; iteration < maxIterations; iteration++)
                {
                    // Calculate portfolio volatility contribution of each asset
                    var portfolioVolatility = Math.Sqrt(weights.DotProduct(covArray * weights));
                    var marginalRiskContributions = covArray * weights / portfolioVolatility;
                    var totalRiskContributions = weights.PointwiseMultiply(marginalRiskContributions);

                    // Calculate target risk contribution (equal for all assets)
                    var targetRiskContribution = totalRiskContributions.Sum() / symbols.Count;

                    // Update weights
                    var newWeights = new double[symbols.Count];
                    for (int i = 0; i < symbols.Count; i++)
                    {
                        newWeights[i] = weights[i] * (targetRiskContribution / totalRiskContributions[i]);
                    }

                    // Normalize weights
                    var sumWeights = newWeights.Sum();
                    for (int i = 0; i < symbols.Count; i++)
                    {
                        newWeights[i] /= sumWeights;
                    }

                    weights = Vector<double>.Build.Dense(newWeights);

                    // Check convergence
                    var maxChange = Math.Abs(weights.Maximum() - weights.Minimum());
                    if (maxChange < tolerance)
                        break;
                }

                // Calculate risk contributions
                var portfolioVol = Math.Sqrt(weights.DotProduct(covArray * weights));
                var marginalRisks = covArray * weights / portfolioVol;
                var riskContributions = weights.PointwiseMultiply(marginalRisks);

                return new RiskParityResult
                {
                    Weights = symbols.Zip(weights.ToArray(), (s, w) => new AssetWeight { Symbol = s, Weight = (decimal)w }).ToList(),
                    RiskContributions = symbols.Zip(riskContributions.ToArray(), (s, rc) => new RiskContribution { Symbol = s, Contribution = (decimal)rc }).ToList(),
                    PortfolioVolatility = (decimal)portfolioVol,
                    Iterations = maxIterations,
                    Converged = true,
                    Timestamp = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating Risk Parity");
                throw;
            }
        }

        /// <summary>
        /// Calculate Hierarchical Risk Parity portfolio
        /// </summary>
        public async Task<HierarchicalRiskParityResult> CalculateHierarchicalRiskParityAsync(List<string> symbols)
        {
            _logger.LogInformation($"Calculating Hierarchical Risk Parity for {symbols.Count} assets");

            try
            {
                // Get historical data
                var returnsData = new Dictionary<string, List<double>>();

                foreach (var symbol in symbols)
                {
                    var historicalData = await _marketDataService.GetHistoricalDataAsync(symbol, 252);
                    if (historicalData != null && historicalData.Count > 30)
                    {
                        var prices = historicalData.Select(d => (double)d.Close).ToList();
                        var returns = CalculateReturns(prices);
                        returnsData[symbol] = returns;
                    }
                }

                // Calculate correlation matrix for hierarchical clustering
                var correlationMatrix = CalculateCorrelationMatrix(returnsData);

                // Perform hierarchical clustering (simplified implementation)
                var clusters = PerformHierarchicalClustering(symbols, correlationMatrix);

                // Allocate weights using risk parity within clusters
                var weights = new Dictionary<string, double>();
                var clusterWeights = 1.0 / clusters.Count;

                foreach (var cluster in clusters)
                {
                    var clusterSymbols = cluster.ToList();
                    if (clusterSymbols.Count == 1)
                    {
                        weights[clusterSymbols[0]] = clusterWeights;
                    }
                    else
                    {
                        // Risk parity within cluster
                        var clusterCovariance = CalculateCovarianceMatrix(clusterSymbols.ToDictionary(s => s, s => returnsData[s]));
                        var clusterWeightsResult = await CalculateRiskParityAsync(clusterSymbols, 50, 1e-4);

                        foreach (var assetWeight in clusterWeightsResult.Weights)
                        {
                            weights[assetWeight.Symbol] = (double)assetWeight.Weight * clusterWeights;
                        }
                    }
                }

                return new HierarchicalRiskParityResult
                {
                    Weights = weights.Select(w => new AssetWeight { Symbol = w.Key, Weight = (decimal)w.Value }).ToList(),
                    Clusters = clusters.Select(c => new AssetCluster { Assets = c.ToList() }).ToList(),
                    Method = "Hierarchical Clustering + Risk Parity",
                    Timestamp = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating Hierarchical Risk Parity");
                throw;
            }
        }

        #region Helper Methods

        private List<double> CalculateReturns(List<double> prices)
        {
            var returns = new List<double>();
            for (int i = 1; i < prices.Count; i++)
            {
                returns.Add(Math.Log(prices[i] / prices[i - 1]));
            }
            return returns;
        }

        private List<double> GeneratePortfolioReturns(Dictionary<string, List<double>> returnsData, int scenarios)
        {
            var portfolioReturns = new List<double>();
            var random = new Random(42);
            var symbols = returnsData.Keys.ToList();

            for (int i = 0; i < scenarios; i++)
            {
                // Randomly select a historical period for each asset
                var scenarioReturn = 0.0;
                foreach (var symbol in symbols)
                {
                    var returns = returnsData[symbol];
                    var randomIndex = random.Next(returns.Count);
                    scenarioReturn += returns[randomIndex];
                }
                scenarioReturn /= symbols.Count; // Equal weight
                portfolioReturns.Add(scenarioReturn);
            }

            return portfolioReturns;
        }

        private Matrix<double> CalculateCovarianceMatrix(Dictionary<string, List<double>> returnsData)
        {
            var symbols = returnsData.Keys.ToList();
            var matrix = Matrix<double>.Build.Dense(symbols.Count, symbols.Count);

            for (int i = 0; i < symbols.Count; i++)
            {
                for (int j = 0; j < symbols.Count; j++)
                {
                    if (i == j)
                    {
                        matrix[i, j] = returnsData[symbols[i]].Variance();
                    }
                    else
                    {
                        matrix[i, j] = returnsData[symbols[i]].Covariance(returnsData[symbols[j]]);
                    }
                }
            }

            return matrix;
        }

        private Matrix<double> CalculateCorrelationMatrix(Dictionary<string, List<double>> returnsData)
        {
            var symbols = returnsData.Keys.ToList();
            var matrix = Matrix<double>.Build.Dense(symbols.Count, symbols.Count);

            for (int i = 0; i < symbols.Count; i++)
            {
                for (int j = 0; j < symbols.Count; j++)
                {
                    matrix[i, j] = Statistics.Correlation(returnsData[symbols[i]], returnsData[symbols[j]]);
                }
            }

            return matrix;
        }

        private Dictionary<string, double> GenerateMultivariateNormalReturns(
            Dictionary<string, double> meanReturns,
            Matrix<double> covarianceMatrix,
            Random random)
        {
            // Simplified multivariate normal generation using Cholesky decomposition
            var cholesky = covarianceMatrix.Cholesky();
            var symbols = meanReturns.Keys.ToList();
            var result = new Dictionary<string, double>();

            // Generate standard normal random variables
            var z = Vector<double>.Build.Dense(symbols.Count);
            for (int i = 0; i < symbols.Count; i++)
            {
                z[i] = MathNet.Numerics.Distributions.Normal.Sample(random, 0, 1);
            }

            // Transform to multivariate normal
            var returnsVector = Vector<double>.Build.Dense(symbols.Select(s => meanReturns[s]).ToArray()) + cholesky * z;

            for (int i = 0; i < symbols.Count; i++)
            {
                result[symbols[i]] = returnsVector[i];
            }

            return result;
        }

        private List<List<string>> PerformHierarchicalClustering(List<string> symbols, Matrix<double> correlationMatrix)
        {
            // Simplified hierarchical clustering based on correlation distance
            var clusters = symbols.Select(s => new List<string> { s }).ToList();

            while (clusters.Count > Math.Max(2, symbols.Count / 3))
            {
                // Find closest clusters
                double minDistance = double.MaxValue;
                int cluster1 = -1, cluster2 = -1;

                for (int i = 0; i < clusters.Count; i++)
                {
                    for (int j = i + 1; j < clusters.Count; j++)
                    {
                        var distance = CalculateClusterDistance(clusters[i], clusters[j], correlationMatrix, symbols);
                        if (distance < minDistance)
                        {
                            minDistance = distance;
                            cluster1 = i;
                            cluster2 = j;
                        }
                    }
                }

                if (cluster1 >= 0 && cluster2 >= 0)
                {
                    // Merge clusters
                    clusters[cluster1].AddRange(clusters[cluster2]);
                    clusters.RemoveAt(cluster2);
                }
                else
                {
                    break;
                }
            }

            return clusters;
        }

        private double CalculateClusterDistance(List<string> cluster1, List<string> cluster2, Matrix<double> correlationMatrix, List<string> symbols)
        {
            // Average correlation distance between clusters
            double totalDistance = 0;
            int count = 0;

            foreach (var s1 in cluster1)
            {
                foreach (var s2 in cluster2)
                {
                    var i1 = symbols.IndexOf(s1);
                    var i2 = symbols.IndexOf(s2);
                    if (i1 >= 0 && i2 >= 0)
                    {
                        totalDistance += 1 - correlationMatrix[i1, i2]; // Convert correlation to distance
                        count++;
                    }
                }
            }

            return count > 0 ? totalDistance / count : double.MaxValue;
        }

        #endregion
    }

    #region Result Classes

    public class CVaRResult
    {
        public decimal ConfidenceLevel { get; set; }
        public decimal VaR { get; set; }
        public decimal CVaR { get; set; }
        public decimal ExpectedShortfall { get; set; }
        public int SampleSize { get; set; }
        public string CalculationMethod { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }

    public class ExpectedShortfallResult
    {
        public decimal ConfidenceLevel { get; set; }
        public decimal ExpectedShortfall { get; set; }
        public int Simulations { get; set; }
        public string Method { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }

    public class BlackLittermanResult
    {
        public List<AssetWeight> OptimalWeights { get; set; } = new();
        public List<AssetReturn> PosteriorReturns { get; set; } = new();
        public double[,] PosteriorCovariance { get; set; } = new double[0, 0];
        public List<decimal> EquilibriumReturns { get; set; } = new();
        public List<View> Views { get; set; } = new();
        public DateTime Timestamp { get; set; }
    }

    public class View
    {
        public string Symbol { get; set; } = string.Empty;
        public decimal ExpectedReturn { get; set; }
        public decimal Confidence { get; set; }
    }

    public class AssetReturn
    {
        public string Symbol { get; set; } = string.Empty;
        public decimal ExpectedReturn { get; set; }
    }

    public class RiskParityResult
    {
        public List<AssetWeight> Weights { get; set; } = new();
        public List<RiskContribution> RiskContributions { get; set; } = new();
        public decimal PortfolioVolatility { get; set; }
        public int Iterations { get; set; }
        public bool Converged { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class RiskContribution
    {
        public string Symbol { get; set; } = string.Empty;
        public decimal Contribution { get; set; }
    }

    public class HierarchicalRiskParityResult
    {
        public List<AssetWeight> Weights { get; set; } = new();
        public List<AssetCluster> Clusters { get; set; } = new();
        public string Method { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }

    public class AssetCluster
    {
        public List<string> Assets { get; set; } = new();
    }

    public class AssetWeight
    {
        public string Symbol { get; set; } = string.Empty;
        public decimal Weight { get; set; }
    }

    #endregion
}