using System.ComponentModel;
using Microsoft.SemanticKernel;
using QuantResearchAgent.Core;
using QuantResearchAgent.Services;

namespace QuantResearchAgent.Plugins
{
    public class AdvancedOptimizationPlugin
    {
        private readonly AdvancedOptimizationService _optimizationService;

        public AdvancedOptimizationPlugin(AdvancedOptimizationService optimizationService)
        {
            _optimizationService = optimizationService;
        }

        [KernelFunction("run_black_litterman")]
        [Description("Runs Black-Litterman portfolio optimization with investor views")]
        public async Task<string> RunBlackLittermanOptimizationAsync(
            [Description("Comma-separated list of asset symbols")] string assets,
            [Description("Absolute return views as JSON: {\"AAPL\": 0.15, \"MSFT\": 0.12}")] string absoluteViews = "{}",
            [Description("Start date for historical data (YYYY-MM-DD)")] string startDate = "2023-01-01",
            [Description("End date for historical data (YYYY-MM-DD)")] string endDate = "2024-01-01")
        {
            try
            {
                var assetList = assets.Split(',').Select(a => a.Trim()).ToList();
                var views = ParseViews(absoluteViews);

                var constraints = new OptimizationConstraints();
                var result = await _optimizationService.RunBlackLittermanOptimizationAsync(
                    assetList, views, constraints, DateTime.Parse(startDate), DateTime.Parse(endDate));

                return FormatBlackLittermanResult(result);
            }
            catch (Exception ex)
            {
                return $"Error in Black-Litterman optimization: {ex.Message}";
            }
        }

        [KernelFunction("optimize_risk_parity")]
        [Description("Optimizes portfolio using risk parity approach")]
        public async Task<string> OptimizeRiskParityAsync(
            [Description("Comma-separated list of asset symbols")] string assets,
            [Description("Start date for historical data (YYYY-MM-DD)")] string startDate = "2023-01-01",
            [Description("End date for historical data (YYYY-MM-DD)")] string endDate = "2024-01-01")
        {
            try
            {
                var assetList = assets.Split(',').Select(a => a.Trim()).ToList();
                var constraints = new OptimizationConstraints();

                var result = await _optimizationService.OptimizeRiskParityAsync(
                    assetList, constraints, DateTime.Parse(startDate), DateTime.Parse(endDate));

                return FormatRiskParityResult(result);
            }
            catch (Exception ex)
            {
                return $"Error in risk parity optimization: {ex.Message}";
            }
        }

        [KernelFunction("optimize_hierarchical_risk_parity")]
        [Description("Optimizes portfolio using hierarchical risk parity")]
        public async Task<string> OptimizeHierarchicalRiskParityAsync(
            [Description("Comma-separated list of asset symbols")] string assets,
            [Description("Start date for historical data (YYYY-MM-DD)")] string startDate = "2023-01-01",
            [Description("End date for historical data (YYYY-MM-DD)")] string endDate = "2024-01-01")
        {
            try
            {
                var assetList = assets.Split(',').Select(a => a.Trim()).ToList();
                var constraints = new OptimizationConstraints();

                var result = await _optimizationService.OptimizeHierarchicalRiskParityAsync(
                    assetList, constraints, DateTime.Parse(startDate), DateTime.Parse(endDate));

                return FormatHierarchicalRiskParityResult(result);
            }
            catch (Exception ex)
            {
                return $"Error in hierarchical risk parity optimization: {ex.Message}";
            }
        }

        [KernelFunction("optimize_minimum_variance")]
        [Description("Optimizes portfolio for minimum variance")]
        public async Task<string> OptimizeMinimumVarianceAsync(
            [Description("Comma-separated list of asset symbols")] string assets,
            [Description("Start date for historical data (YYYY-MM-DD)")] string startDate = "2023-01-01",
            [Description("End date for historical data (YYYY-MM-DD)")] string endDate = "2024-01-01")
        {
            try
            {
                var assetList = assets.Split(',').Select(a => a.Trim()).ToList();
                var constraints = new OptimizationConstraints();

                var result = await _optimizationService.OptimizeMinimumVarianceAsync(
                    assetList, constraints, DateTime.Parse(startDate), DateTime.Parse(endDate));

                return FormatMinimumVarianceResult(result);
            }
            catch (Exception ex)
            {
                return $"Error in minimum variance optimization: {ex.Message}";
            }
        }

        [KernelFunction("compare_optimization_methods")]
        [Description("Compares different portfolio optimization methods")]
        public async Task<string> CompareOptimizationMethodsAsync(
            [Description("Comma-separated list of asset symbols")] string assets,
            [Description("Start date for historical data (YYYY-MM-DD)")] string startDate = "2023-01-01",
            [Description("End date for historical data (YYYY-MM-DD)")] string endDate = "2024-01-01")
        {
            try
            {
                var assetList = assets.Split(',').Select(a => a.Trim()).ToList();
                var constraints = new OptimizationConstraints();

                // Run all optimization methods
                var riskParity = await _optimizationService.OptimizeRiskParityAsync(
                    assetList, constraints, DateTime.Parse(startDate), DateTime.Parse(endDate));

                var hrp = await _optimizationService.OptimizeHierarchicalRiskParityAsync(
                    assetList, constraints, DateTime.Parse(startDate), DateTime.Parse(endDate));

                var minVar = await _optimizationService.OptimizeMinimumVarianceAsync(
                    assetList, constraints, DateTime.Parse(startDate), DateTime.Parse(endDate));

                return FormatOptimizationComparison(riskParity, hrp, minVar);
            }
            catch (Exception ex)
            {
                return $"Error comparing optimization methods: {ex.Message}";
            }
        }

        // Helper methods
        private BlackLittermanViews ParseViews(string viewsJson)
        {
            var views = new BlackLittermanViews();
            if (string.IsNullOrEmpty(viewsJson) || viewsJson == "{}") return views;

            try
            {
                // Simple parsing - can be enhanced with proper JSON parsing
                var cleaned = viewsJson.Replace("{", "").Replace("}", "").Replace("\"", "");
                var pairs = cleaned.Split(',');

                foreach (var pair in pairs)
                {
                    var parts = pair.Split(':');
                    if (parts.Length == 2)
                    {
                        var asset = parts[0].Trim();
                        var returnValue = double.Parse(parts[1].Trim());
                        views.AbsoluteViews[asset] = returnValue;
                        views.ViewConfidences[asset] = 0.7; // Default confidence
                    }
                }
            }
            catch
            {
                // If parsing fails, return empty views
            }

            return views;
        }

        private string FormatBlackLittermanResult(BlackLittermanModel result)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("BLACK-LITTERMAN PORTFOLIO OPTIMIZATION");
            sb.AppendLine("=====================================");
            sb.AppendLine($"Analysis Date: {result.AnalysisDate:yyyy-MM-dd}");
            sb.AppendLine($"Risk Aversion: {result.RiskAversion:F2}");
            sb.AppendLine();

            sb.AppendLine("PRIOR RETURNS (Market Equilibrium):");
            foreach (var prior in result.PriorReturns)
            {
                sb.AppendLine($"{prior.Key}: {prior.Value:P3}");
            }
            sb.AppendLine();

            sb.AppendLine("POSTERIOR RETURNS (After Views):");
            foreach (var posterior in result.PosteriorReturns)
            {
                sb.AppendLine($"{posterior.Key}: {posterior.Value:P3}");
            }
            sb.AppendLine();

            sb.AppendLine("OPTIMAL PORTFOLIO WEIGHTS:");
            foreach (var weight in result.OptimalWeights.OrderByDescending(w => w.Value))
            {
                sb.AppendLine($"{weight.Key}: {weight.Value:P2}");
            }
            sb.AppendLine();

            sb.AppendLine("PORTFOLIO METRICS:");
            sb.AppendLine($"Expected Return: {result.ExpectedReturn:P3}");
            sb.AppendLine($"Expected Volatility: {result.ExpectedVolatility:P3}");
            sb.AppendLine($"Sharpe Ratio: {result.SharpeRatio:F3}");

            return sb.ToString();
        }

        private string FormatRiskParityResult(RiskParityPortfolio result)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("RISK PARITY PORTFOLIO OPTIMIZATION");
            sb.AppendLine("==================================");
            sb.AppendLine($"Analysis Date: {result.AnalysisDate:yyyy-MM-dd}");
            sb.AppendLine($"Converged: {result.Converged}");
            sb.AppendLine($"Iterations: {result.Iterations}");
            sb.AppendLine();

            sb.AppendLine("PORTFOLIO WEIGHTS:");
            foreach (var weight in result.AssetWeights.OrderByDescending(w => w.Value))
            {
                sb.AppendLine($"{weight.Key}: {weight.Value:P2}");
            }
            sb.AppendLine();

            sb.AppendLine("RISK CONTRIBUTIONS:");
            foreach (var contribution in result.RiskContributions.OrderByDescending(c => c.Value))
            {
                sb.AppendLine($"{contribution.Key}: {contribution.Value:P3}");
            }
            sb.AppendLine();

            sb.AppendLine("PORTFOLIO METRICS:");
            sb.AppendLine($"Total Risk: {result.TotalRisk:P3}");
            sb.AppendLine($"Expected Return: {result.ExpectedReturn:P3}");

            return sb.ToString();
        }

        private string FormatHierarchicalRiskParityResult(HierarchicalRiskParity result)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("HIERARCHICAL RISK PARITY OPTIMIZATION");
            sb.AppendLine("====================================");
            sb.AppendLine($"Analysis Date: {result.AnalysisDate:yyyy-MM-dd}");
            sb.AppendLine($"Number of Clusters: {result.Clusters.Count}");
            sb.AppendLine();

            sb.AppendLine("CLUSTERS:");
            foreach (var cluster in result.Clusters)
            {
                sb.AppendLine($"Cluster '{cluster.Name}': {string.Join(", ", cluster.Assets)}");
            }
            sb.AppendLine();

            sb.AppendLine("ASSET WEIGHTS:");
            foreach (var weight in result.Weights.OrderByDescending(w => w.Value))
            {
                sb.AppendLine($"{weight.Key}: {weight.Value:P2}");
            }
            sb.AppendLine();

            sb.AppendLine("PORTFOLIO METRICS:");
            sb.AppendLine($"Total Risk: {result.TotalRisk:P3}");
            sb.AppendLine($"Expected Return: {result.ExpectedReturn:P3}");

            return sb.ToString();
        }

        private string FormatMinimumVarianceResult(MinimumVariancePortfolio result)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("MINIMUM VARIANCE PORTFOLIO OPTIMIZATION");
            sb.AppendLine("=======================================");
            sb.AppendLine($"Analysis Date: {result.AnalysisDate:yyyy-MM-dd}");
            sb.AppendLine($"Success: {result.Success}");
            sb.AppendLine();

            if (result.Success)
            {
                sb.AppendLine("PORTFOLIO WEIGHTS:");
                foreach (var weight in result.Weights.OrderByDescending(w => w.Value))
                {
                    sb.AppendLine($"{weight.Key}: {weight.Value:P2}");
                }
                sb.AppendLine();

                sb.AppendLine("PORTFOLIO METRICS:");
                sb.AppendLine($"Portfolio Variance: {result.PortfolioVariance:P4}");
                sb.AppendLine($"Portfolio Volatility: {result.PortfolioVolatility:P3}");
                sb.AppendLine($"Expected Return: {result.ExpectedReturn:P3}");
            }

            return sb.ToString();
        }

        private string FormatOptimizationComparison(
            RiskParityPortfolio riskParity,
            HierarchicalRiskParity hrp,
            MinimumVariancePortfolio minVar)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("PORTFOLIO OPTIMIZATION METHODS COMPARISON");
            sb.AppendLine("=========================================");
            sb.AppendLine();

            sb.AppendLine("RISK PARITY:");
            sb.AppendLine($"  Risk: {riskParity.TotalRisk:P3}, Return: {riskParity.ExpectedReturn:P3}");
            sb.AppendLine($"  Converged: {riskParity.Converged}, Iterations: {riskParity.Iterations}");
            sb.AppendLine();

            sb.AppendLine("HIERARCHICAL RISK PARITY:");
            sb.AppendLine($"  Risk: {hrp.TotalRisk:P3}, Return: {hrp.ExpectedReturn:P3}");
            sb.AppendLine($"  Clusters: {hrp.Clusters.Count}");
            sb.AppendLine();

            sb.AppendLine("MINIMUM VARIANCE:");
            sb.AppendLine($"  Risk: {minVar.PortfolioVolatility:P3}, Return: {minVar.ExpectedReturn:P3}");
            sb.AppendLine($"  Success: {minVar.Success}");
            sb.AppendLine();

            // Calculate Sharpe ratios for comparison
            var rpSharpe = riskParity.ExpectedReturn / riskParity.TotalRisk;
            var hrpSharpe = hrp.ExpectedReturn / hrp.TotalRisk;
            var mvSharpe = minVar.ExpectedReturn / minVar.PortfolioVolatility;

            sb.AppendLine("SHARPE RATIOS:");
            sb.AppendLine($"  Risk Parity: {rpSharpe:F3}");
            sb.AppendLine($"  HRP: {hrpSharpe:F3}");
            sb.AppendLine($"  Min Variance: {mvSharpe:F3}");

            return sb.ToString();
        }
    }
}