using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.Optimization;
using MathNet.Numerics.Statistics;
using QuantResearchAgent.Core;
using QuantResearchAgent.Services;

namespace QuantResearchAgent.Services
{
    public class AdvancedOptimizationService
    {
        private readonly MarketDataService _marketDataService;
        private readonly StatisticalTestingService _statisticalService;

        public AdvancedOptimizationService(
            MarketDataService marketDataService,
            StatisticalTestingService statisticalService)
        {
            _marketDataService = marketDataService;
            _statisticalService = statisticalService;
        }

        // Black-Litterman Model Implementation
        public async Task<BlackLittermanModel> RunBlackLittermanOptimizationAsync(
            List<string> assets,
            BlackLittermanViews views,
            OptimizationConstraints constraints,
            DateTime startDate,
            DateTime endDate)
        {
            var model = new BlackLittermanModel { AnalysisDate = DateTime.Now };

            // Get historical returns
            var returns = new Dictionary<string, List<double>>();
            foreach (var asset in assets)
            {
                var data = await _marketDataService.GetHistoricalDataAsync(asset, 1000);
                var filteredData = data?.Where(d => d.Timestamp >= startDate && d.Timestamp <= endDate)
                    .OrderBy(d => d.Timestamp).ToList() ?? new List<MarketData>();
                var prices = filteredData.Select(d => d.Price).ToList();
                returns[asset] = CalculateReturns(prices);
            }

            // Calculate prior returns (market equilibrium)
            var marketWeights = CalculateMarketWeights(assets);
            var riskAversion = 2.5; // Typical value
            var covarianceMatrix = CalculateCovarianceMatrix(returns);

            var priorReturns = CalculatePriorReturns(marketWeights, covarianceMatrix, riskAversion);
            model.PriorReturns = priorReturns;
            model.MarketWeights = marketWeights;
            model.RiskAversion = riskAversion;

            // Incorporate views
            var posteriorReturns = IncorporateViews(priorReturns, views, covarianceMatrix, assets);
            model.PosteriorReturns = posteriorReturns;

            // Optimize portfolio
            var optimalWeights = OptimizePortfolio(posteriorReturns, covarianceMatrix, constraints);
            model.OptimalWeights = optimalWeights;

            // Calculate portfolio metrics
            model.ExpectedReturn = optimalWeights.Values.Zip(posteriorReturns.Values, (w, r) => w * r).Sum();
            model.ExpectedVolatility = Math.Sqrt(CalculatePortfolioVariance(optimalWeights.Values.ToArray(), covarianceMatrix));
            model.SharpeRatio = model.ExpectedReturn / model.ExpectedVolatility;

            return model;
        }

        // Risk Parity Optimization
        public async Task<RiskParityPortfolio> OptimizeRiskParityAsync(
            List<string> assets,
            OptimizationConstraints constraints,
            DateTime startDate,
            DateTime endDate)
        {
            var portfolio = new RiskParityPortfolio { AnalysisDate = DateTime.Now };

            // Get historical returns and calculate covariance
            var returns = new Dictionary<string, List<double>>();
            foreach (var asset in assets)
            {
                var data = await _marketDataService.GetHistoricalDataAsync(asset, 1000);
                var filteredData = data?.Where(d => d.Timestamp >= startDate && d.Timestamp <= endDate)
                    .OrderBy(d => d.Timestamp).ToList() ?? new List<MarketData>();
                var prices = filteredData.Select(d => d.Price).ToList();
                returns[asset] = CalculateReturns(prices);
            }

            var covarianceMatrix = CalculateCovarianceMatrix(returns);
            var targetRiskContribution = 1.0 / assets.Count;

            // Risk parity optimization using iterative approach
            var weights = new double[assets.Count];
            for (int i = 0; i < weights.Length; i++) weights[i] = 1.0 / assets.Count;

            const int maxIterations = 1000;
            const double tolerance = 1e-8;
            var converged = false;

            for (int iter = 0; iter < maxIterations; iter++)
            {
                var riskContributions = CalculateRiskContributions(weights, covarianceMatrix);
                var maxDeviation = riskContributions.Max(rc => Math.Abs(rc - targetRiskContribution));

                if (maxDeviation < tolerance)
                {
                    converged = true;
                    break;
                }

                // Update weights
                for (int i = 0; i < weights.Length; i++)
                {
                    weights[i] *= targetRiskContribution / riskContributions[i];
                }

                // Normalize
                var sum = weights.Sum();
                for (int i = 0; i < weights.Length; i++)
                {
                    weights[i] /= sum;
                }
            }

            // Apply constraints
            weights = ApplyConstraints(weights, constraints, assets);

            portfolio.Converged = converged;
            portfolio.Iterations = converged ? Array.FindIndex(Enumerable.Range(0, maxIterations).ToArray(), i => true) + 1 : maxIterations;

            for (int i = 0; i < assets.Count; i++)
            {
                portfolio.AssetWeights[assets[i]] = weights[i];
                portfolio.RiskContributions[assets[i]] = CalculateRiskContributions(weights, covarianceMatrix)[i];
            }

            portfolio.TotalRisk = Math.Sqrt(CalculatePortfolioVariance(weights, covarianceMatrix));
            portfolio.ExpectedReturn = weights.Zip(returns.Values.Select(r => r.Average()).ToArray(), (w, r) => w * r).Sum();

            return portfolio;
        }

        // Hierarchical Risk Parity
        public async Task<HierarchicalRiskParity> OptimizeHierarchicalRiskParityAsync(
            List<string> assets,
            OptimizationConstraints constraints,
            DateTime startDate,
            DateTime endDate)
        {
            var hrp = new HierarchicalRiskParity { AnalysisDate = DateTime.Now, Assets = assets };

            // Get returns and calculate correlation matrix
            var returns = new Dictionary<string, List<double>>();
            foreach (var asset in assets)
            {
                var data = await _marketDataService.GetHistoricalDataAsync(asset, 1000);
                var filteredData = data?.Where(d => d.Timestamp >= startDate && d.Timestamp <= endDate)
                    .OrderBy(d => d.Timestamp).ToList() ?? new List<MarketData>();
                var prices = filteredData.Select(d => d.Price).ToList();
                returns[asset] = CalculateReturns(prices);
            }

            var correlationMatrix = CalculateCorrelationMatrix(returns);

            // Perform hierarchical clustering
            var clusters = PerformHierarchicalClustering(assets, correlationMatrix);
            hrp.Clusters = clusters;

            // Allocate weights using inverse variance
            var weights = AllocateWeightsHierarchically(clusters, returns);
            hrp.Weights = weights;

            // Calculate portfolio metrics
            var covarianceMatrix = CalculateCovarianceMatrix(returns);
            hrp.TotalRisk = Math.Sqrt(CalculatePortfolioVariance(weights.Values.ToArray(), covarianceMatrix));
            hrp.ExpectedReturn = weights.Zip(returns.Values.Select(r => r.Average()).ToArray(), (w, r) => w.Value * r).Sum();

            return hrp;
        }

        // Minimum Variance Portfolio
        public async Task<MinimumVariancePortfolio> OptimizeMinimumVarianceAsync(
            List<string> assets,
            OptimizationConstraints constraints,
            DateTime startDate,
            DateTime endDate)
        {
            var portfolio = new MinimumVariancePortfolio { AnalysisDate = DateTime.Now };

            try
            {
                // Get historical returns
                var returns = new Dictionary<string, List<double>>();
                foreach (var asset in assets)
                {
                    var data = await _marketDataService.GetHistoricalDataAsync(asset, 1000);
                    var filteredData = data?.Where(d => d.Timestamp >= startDate && d.Timestamp <= endDate)
                        .OrderBy(d => d.Timestamp).ToList() ?? new List<MarketData>();
                    var prices = filteredData.Select(d => d.Price).ToList();
                    returns[asset] = CalculateReturns(prices);
                }

                var covarianceMatrix = CalculateCovarianceMatrix(returns);

                // Minimize portfolio variance subject to constraints
                var weights = MinimizeVariance(covarianceMatrix, constraints, assets);

                for (int i = 0; i < assets.Count; i++)
                {
                    portfolio.Weights[assets[i]] = weights[i];
                }

                portfolio.PortfolioVariance = CalculatePortfolioVariance(weights, covarianceMatrix);
                portfolio.PortfolioVolatility = Math.Sqrt(portfolio.PortfolioVariance);
                portfolio.ExpectedReturn = weights.Zip(returns.Values.Select(r => r.Average()).ToArray(), (w, r) => w * r).Sum();
                portfolio.Success = true;
            }
            catch (Exception)
            {
                portfolio.Success = false;
            }

            return portfolio;
        }

        // Helper methods
        private Dictionary<string, double> CalculateMarketWeights(List<string> assets)
        {
            // Simplified: equal weights for demo
            var weights = new Dictionary<string, double>();
            var equalWeight = 1.0 / assets.Count;
            foreach (var asset in assets)
            {
                weights[asset] = equalWeight;
            }
            return weights;
        }

        private Dictionary<string, double> CalculatePriorReturns(
            Dictionary<string, double> marketWeights,
            double[,] covarianceMatrix,
            double riskAversion)
        {
            // Black-Litterman prior returns: λ * Σ * w
            var assets = marketWeights.Keys.ToList();
            var weights = marketWeights.Values.ToArray();
            var priorReturns = new Dictionary<string, double>();

            for (int i = 0; i < assets.Count; i++)
            {
                double expectedReturn = 0;
                for (int j = 0; j < assets.Count; j++)
                {
                    expectedReturn += covarianceMatrix[i, j] * weights[j];
                }
                expectedReturn *= riskAversion;
                priorReturns[assets[i]] = expectedReturn;
            }

            return priorReturns;
        }

        private Dictionary<string, double> IncorporateViews(
            Dictionary<string, double> priorReturns,
            BlackLittermanViews views,
            double[,] covarianceMatrix,
            List<string> assets)
        {
            // Simplified Black-Litterman view incorporation
            var posteriorReturns = new Dictionary<string, double>(priorReturns);

            // Incorporate absolute views
            foreach (var view in views.AbsoluteViews)
            {
                if (posteriorReturns.ContainsKey(view.Key))
                {
                    var confidence = views.ViewConfidences.GetValueOrDefault(view.Key, 0.5);
                    posteriorReturns[view.Key] = confidence * view.Value + (1 - confidence) * priorReturns[view.Key];
                }
            }

            return posteriorReturns;
        }

        private Dictionary<string, double> OptimizePortfolio(
            Dictionary<string, double> expectedReturns,
            double[,] covarianceMatrix,
            OptimizationConstraints constraints)
        {
            // Simplified mean-variance optimization
            var assets = expectedReturns.Keys.ToList();
            var returns = expectedReturns.Values.ToArray();

            // Maximize Sharpe ratio (simplified)
            var weights = new double[assets.Count];
            var total = 0.0;

            for (int i = 0; i < assets.Count; i++)
            {
                weights[i] = Math.Max(0, returns[i] / covarianceMatrix[i, i]); // Risk-adjusted return
                total += weights[i];
            }

            // Normalize
            for (int i = 0; i < weights.Length; i++)
            {
                weights[i] /= total;
            }

            // Apply constraints
            weights = ApplyConstraints(weights, constraints, assets);

            var result = new Dictionary<string, double>();
            for (int i = 0; i < assets.Count; i++)
            {
                result[assets[i]] = weights[i];
            }

            return result;
        }

        private double[] CalculateRiskContributions(double[] weights, double[,] covarianceMatrix)
        {
            var portfolioVariance = CalculatePortfolioVariance(weights, covarianceMatrix);
            var portfolioVolatility = Math.Sqrt(portfolioVariance);
            var contributions = new double[weights.Length];

            for (int i = 0; i < weights.Length; i++)
            {
                double marginalContribution = 0;
                for (int j = 0; j < weights.Length; j++)
                {
                    marginalContribution += covarianceMatrix[i, j] * weights[j];
                }
                contributions[i] = weights[i] * marginalContribution / portfolioVariance;
            }

            return contributions;
        }

        private List<Cluster> PerformHierarchicalClustering(List<string> assets, double[,] correlationMatrix)
        {
            // Simplified clustering - group highly correlated assets
            var clusters = new List<Cluster>();

            // For demo: create clusters based on correlation threshold
            var usedAssets = new HashSet<string>();
            const double correlationThreshold = 0.7;

            foreach (var asset in assets)
            {
                if (usedAssets.Contains(asset)) continue;

                var cluster = new Cluster { Name = $"Cluster_{clusters.Count + 1}", Assets = new List<string> { asset } };
                usedAssets.Add(asset);

                // Find correlated assets
                for (int i = 0; i < assets.Count; i++)
                {
                    if (assets[i] == asset || usedAssets.Contains(assets[i])) continue;

                    var correlation = correlationMatrix[assets.IndexOf(asset), i];
                    if (Math.Abs(correlation) > correlationThreshold)
                    {
                        cluster.Assets.Add(assets[i]);
                        usedAssets.Add(assets[i]);
                    }
                }

                clusters.Add(cluster);
            }

            return clusters;
        }

        private Dictionary<string, double> AllocateWeightsHierarchically(List<Cluster> clusters, Dictionary<string, List<double>> returns)
        {
            var weights = new Dictionary<string, double>();
            var totalClusters = clusters.Count;

            foreach (var cluster in clusters)
            {
                var clusterWeight = 1.0 / totalClusters;
                var clusterVariance = CalculateClusterVariance(cluster.Assets, returns);

                foreach (var asset in cluster.Assets)
                {
                    var assetVariance = returns[asset].Variance();
                    var assetWeight = clusterWeight * (clusterVariance / assetVariance) / cluster.Assets.Count;
                    weights[asset] = assetWeight;
                }
            }

            // Normalize
            var total = weights.Values.Sum();
            foreach (var key in weights.Keys.ToArray())
            {
                weights[key] /= total;
            }

            return weights;
        }

        private double CalculateClusterVariance(List<string> assets, Dictionary<string, List<double>> returns)
        {
            var combinedReturns = new List<double>();
            foreach (var asset in assets)
            {
                combinedReturns.AddRange(returns[asset]);
            }
            return combinedReturns.Variance();
        }

        private double[] MinimizeVariance(double[,] covarianceMatrix, OptimizationConstraints constraints, List<string> assets)
        {
            // Simplified minimum variance optimization
            var n = assets.Count;
            var weights = new double[n];

            // Equal weights as starting point
            for (int i = 0; i < n; i++) weights[i] = 1.0 / n;

            // Apply constraints
            return ApplyConstraints(weights, constraints, assets);
        }

        private double[] ApplyConstraints(double[] weights, OptimizationConstraints constraints, List<string> assets)
        {
            // Apply minimum and maximum weight constraints
            for (int i = 0; i < weights.Length; i++)
            {
                var asset = assets[i];
                var minWeight = constraints.MinWeights.GetValueOrDefault(asset, 0);
                var maxWeight = constraints.MaxWeights.GetValueOrDefault(asset, 1);

                weights[i] = Math.Max(minWeight, Math.Min(maxWeight, weights[i]));
            }

            // Exclude assets
            foreach (var excludedAsset in constraints.ExcludedAssets)
            {
                var index = assets.IndexOf(excludedAsset);
                if (index >= 0) weights[index] = 0;
            }

            // Normalize
            var sum = weights.Sum();
            if (sum > 0)
            {
                for (int i = 0; i < weights.Length; i++)
                {
                    weights[i] /= sum;
                }
            }

            return weights;
        }

        private double[,] CalculateCovarianceMatrix(Dictionary<string, List<double>> returns)
        {
            var assets = returns.Keys.ToList();
            var n = assets.Count;
            var matrix = new double[n, n];

            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    matrix[i, j] = CalculateCovariance(returns[assets[i]], returns[assets[j]]);
                }
            }

            return matrix;
        }

        private double[,] CalculateCorrelationMatrix(Dictionary<string, List<double>> returns)
        {
            var covarianceMatrix = CalculateCovarianceMatrix(returns);
            var n = returns.Count;
            var correlationMatrix = new double[n, n];

            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    var stdI = Math.Sqrt(covarianceMatrix[i, i]);
                    var stdJ = Math.Sqrt(covarianceMatrix[j, j]);
                    correlationMatrix[i, j] = covarianceMatrix[i, j] / (stdI * stdJ);
                }
            }

            return correlationMatrix;
        }

        private double CalculateCovariance(List<double> returns1, List<double> returns2)
        {
            var mean1 = returns1.Average();
            var mean2 = returns2.Average();
            var covariance = 0.0;

            for (int i = 0; i < Math.Min(returns1.Count, returns2.Count); i++)
            {
                covariance += (returns1[i] - mean1) * (returns2[i] - mean2);
            }

            return covariance / (Math.Min(returns1.Count, returns2.Count) - 1);
        }

        private double CalculatePortfolioVariance(double[] weights, double[,] covarianceMatrix)
        {
            var variance = 0.0;
            var n = weights.Length;

            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    variance += weights[i] * weights[j] * covarianceMatrix[i, j];
                }
            }

            return variance;
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