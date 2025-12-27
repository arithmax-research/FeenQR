using System.ComponentModel;
using Microsoft.SemanticKernel;
using QuantResearchAgent.Core;
using QuantResearchAgent.Services;

namespace QuantResearchAgent.Plugins
{
    public class AdvancedRiskPlugin
    {
        private readonly AdvancedRiskService _riskService;

        public AdvancedRiskPlugin(AdvancedRiskService riskService)
        {
            _riskService = riskService;
        }

        [KernelFunction("calculate_var")]
        [Description("Calculates Value-at-Risk (VaR) for a portfolio")]
        public async Task<string> CalculateVaRAsync(
            [Description("Portfolio weights as JSON: {\"AAPL\": 0.3, \"MSFT\": 0.4, \"GOOGL\": 0.3}")] string weightsJson,
            [Description("Confidence level (0.95 for 95%, 0.99 for 99%)")] double confidenceLevel = 0.95,
            [Description("VaR calculation method: 'historical', 'parametric', or 'montecarlo'")] string method = "historical",
            [Description("Start date for historical data (YYYY-MM-DD)")] string startDate = "2023-01-01",
            [Description("End date for historical data (YYYY-MM-DD)")] string endDate = "2024-01-01",
            [Description("Number of Monte Carlo simulations (only for montecarlo method)")] int? simulations = null)
        {
            try
            {
                var weights = ParseWeights(weightsJson);
                var result = await _riskService.CalculateVaRAsync(
                    weights, confidenceLevel, method, DateTime.Parse(startDate), DateTime.Parse(endDate), simulations);

                return FormatVaRResult(result);
            }
            catch (Exception ex)
            {
                return $"Error calculating VaR: {ex.Message}";
            }
        }

        [KernelFunction("run_stress_test")]
        [Description("Runs stress tests on a portfolio with various market scenarios")]
        public async Task<string> RunStressTestsAsync(
            [Description("Portfolio weights as JSON: {\"AAPL\": 0.3, \"MSFT\": 0.4, \"GOOGL\": 0.3}")] string weightsJson,
            [Description("Stress scenarios as JSON array")] string scenariosJson = "[]",
            [Description("Scenario names as comma-separated string")] string scenarioNames = "",
            [Description("Loss threshold for breach detection")] double threshold = 0.05,
            [Description("Start date for historical data (YYYY-MM-DD)")] string startDate = "2023-01-01",
            [Description("End date for historical data (YYYY-MM-DD)")] string endDate = "2024-01-01")
        {
            try
            {
                var weights = ParseWeights(weightsJson);
                var scenarios = ParseStressScenarios(scenariosJson);
                var names = string.IsNullOrEmpty(scenarioNames) ?
                    scenarios.Select((_, i) => $"Scenario {i + 1}").ToList() :
                    scenarioNames.Split(',').Select(n => n.Trim()).ToList();

                var results = await _riskService.RunStressTestsAsync(
                    weights, scenarios, names, threshold, DateTime.Parse(startDate), DateTime.Parse(endDate));

                return FormatStressTestResults(results);
            }
            catch (Exception ex)
            {
                return $"Error running stress tests: {ex.Message}";
            }
        }

        [KernelFunction("factor_risk_attribution")]
        [Description("Performs risk factor attribution analysis")]
        public async Task<string> PerformRiskFactorAttributionAsync(
            [Description("Portfolio weights as JSON: {\"AAPL\": 0.3, \"MSFT\": 0.4, \"GOOGL\": 0.3}")] string weightsJson,
            [Description("Comma-separated list of risk factors")] string factors = "Market,Size,Value,Momentum",
            [Description("Start date for historical data (YYYY-MM-DD)")] string startDate = "2023-01-01",
            [Description("End date for historical data (YYYY-MM-DD)")] string endDate = "2024-01-01")
        {
            try
            {
                var weights = ParseWeights(weightsJson);
                var factorList = factors.Split(',').Select(f => f.Trim()).ToList();

                var result = await _riskService.PerformRiskFactorAttributionAsync(
                    weights, factorList, DateTime.Parse(startDate), DateTime.Parse(endDate));

                return FormatFactorAttributionResult(result);
            }
            catch (Exception ex)
            {
                return $"Error performing factor attribution: {ex.Message}";
            }
        }

        [KernelFunction("generate_risk_report")]
        [Description("Generates a comprehensive risk report for a portfolio")]
        public async Task<string> GenerateRiskReportAsync(
            [Description("Portfolio weights as JSON: {\"AAPL\": 0.3, \"MSFT\": 0.4, \"GOOGL\": 0.3}")] string weightsJson,
            [Description("Stress scenarios as JSON array")] string scenariosJson = "[]",
            [Description("Scenario names as comma-separated string")] string scenarioNames = "",
            [Description("Comma-separated list of risk factors")] string factors = "Market,Size,Value,Momentum",
            [Description("Start date for historical data (YYYY-MM-DD)")] string startDate = "2023-01-01",
            [Description("End date for historical data (YYYY-MM-DD)")] string endDate = "2024-01-01")
        {
            try
            {
                var weights = ParseWeights(weightsJson);
                var scenarios = ParseStressScenarios(scenariosJson);
                var names = string.IsNullOrEmpty(scenarioNames) ?
                    scenarios.Select((_, i) => $"Scenario {i + 1}").ToList() :
                    scenarioNames.Split(',').Select(n => n.Trim()).ToList();
                var factorList = factors.Split(',').Select(f => f.Trim()).ToList();

                var report = await _riskService.GenerateRiskReportAsync(
                    weights, scenarios, names, factorList, DateTime.Parse(startDate), DateTime.Parse(endDate));

                return FormatRiskReport(report);
            }
            catch (Exception ex)
            {
                return $"Error generating risk report: {ex.Message}";
            }
        }

        [KernelFunction("compare_risk_measures")]
        [Description("Compares different risk measures for a portfolio")]
        public async Task<string> CompareRiskMeasuresAsync(
            [Description("Portfolio weights as JSON: {\"AAPL\": 0.3, \"MSFT\": 0.4, \"GOOGL\": 0.3}")] string weightsJson,
            [Description("Start date for historical data (YYYY-MM-DD)")] string startDate = "2023-01-01",
            [Description("End date for historical data (YYYY-MM-DD)")] string endDate = "2024-01-01")
        {
            try
            {
                var weights = ParseWeights(weightsJson);

                // Calculate different VaR measures
                var historicalVaR = await _riskService.CalculateVaRAsync(weights, 0.95, "historical", DateTime.Parse(startDate), DateTime.Parse(endDate));
                var parametricVaR = await _riskService.CalculateVaRAsync(weights, 0.95, "parametric", DateTime.Parse(startDate), DateTime.Parse(endDate));
                var monteCarloVaR = await _riskService.CalculateVaRAsync(weights, 0.95, "montecarlo", DateTime.Parse(startDate), DateTime.Parse(endDate), 10000);

                return FormatRiskMeasuresComparison(historicalVaR, parametricVaR, monteCarloVaR);
            }
            catch (Exception ex)
            {
                return $"Error comparing risk measures: {ex.Message}";
            }
        }

        // Helper methods
        private Dictionary<string, double> ParseWeights(string weightsJson)
        {
            var weights = new Dictionary<string, double>();
            if (string.IsNullOrEmpty(weightsJson) || weightsJson == "{}")
            {
                // Default equal weights for demo
                weights["AAPL"] = 0.33;
                weights["MSFT"] = 0.33;
                weights["GOOGL"] = 0.34;
                return weights;
            }

            try
            {
                // Simple parsing - can be enhanced with proper JSON parsing
                var cleaned = weightsJson.Replace("{", "").Replace("}", "").Replace("\"", "");
                var pairs = cleaned.Split(',');

                foreach (var pair in pairs)
                {
                    var parts = pair.Split(':');
                    if (parts.Length == 2)
                    {
                        var asset = parts[0].Trim();
                        var weight = double.Parse(parts[1].Trim());
                        weights[asset] = weight;
                    }
                }
            }
            catch (Exception ex)
            {
                throw new ArgumentException($"Invalid weights format: {ex.Message}");
            }

            return weights;
        }

        private List<Dictionary<string, double>> ParseStressScenarios(string scenariosJson)
        {
            var scenarios = new List<Dictionary<string, double>>();
            if (string.IsNullOrEmpty(scenariosJson) || scenariosJson == "[]")
            {
                // Default stress scenarios
                scenarios.Add(new Dictionary<string, double> { { "AAPL", -0.1 }, { "MSFT", -0.05 }, { "GOOGL", -0.08 } }); // Market crash
                scenarios.Add(new Dictionary<string, double> { { "AAPL", -0.15 }, { "MSFT", 0.02 }, { "GOOGL", -0.12 } }); // Tech sector shock
                return scenarios;
            }

            // Simplified parsing - can be enhanced
            return scenarios;
        }

        private string FormatVaRResult(ValueAtRisk var)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("VALUE-AT-RISK (VaR) ANALYSIS");
            sb.AppendLine("===========================");
            sb.AppendLine($"Analysis Date: {var.AnalysisDate:yyyy-MM-dd}");
            sb.AppendLine($"Method: {var.Method}");
            sb.AppendLine($"Confidence Level: {var.ConfidenceLevel:P1}");
            sb.AppendLine();

            sb.AppendLine("RISK MEASURES:");
            sb.AppendLine($"VaR (Value-at-Risk): {var.VaR:P3}");
            sb.AppendLine($"Expected Shortfall (CVaR): {var.ExpectedShortfall:P3}");
            sb.AppendLine($"Diversification Ratio: {var.DiversificationRatio:F2}");
            sb.AppendLine();

            sb.AppendLine("COMPONENT VaR (by asset):");
            foreach (var component in var.ComponentVaR.OrderByDescending(c => Math.Abs(c.Value)))
            {
                sb.AppendLine($"{component.Key}: {component.Value:P3}");
            }

            return sb.ToString();
        }

        private string FormatStressTestResults(List<QuantResearchAgent.Core.StressTestResult> results)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("STRESS TEST RESULTS");
            sb.AppendLine("===================");
            sb.AppendLine();

            foreach (var result in results)
            {
                sb.AppendLine($"SCENARIO: {result.ScenarioName}");
                sb.AppendLine($"Portfolio Return: {result.PortfolioReturn:P3}");
                sb.AppendLine($"Portfolio Loss: {result.PortfolioLoss:P3}");
                sb.AppendLine($"Breach Threshold ({result.Threshold:P1}): {result.BreachThreshold}");
                sb.AppendLine();

                sb.AppendLine("Asset Contributions:");
                foreach (var contribution in result.AssetContributions.OrderBy(c => c.Value))
                {
                    sb.AppendLine($"  {contribution.Key}: {contribution.Value:P3}");
                }
                sb.AppendLine("----------------------------------------");
            }

            return sb.ToString();
        }

        private string FormatFactorAttributionResult(RiskFactorAttribution attribution)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("RISK FACTOR ATTRIBUTION ANALYSIS");
            sb.AppendLine("================================");
            sb.AppendLine($"Analysis Date: {attribution.AnalysisDate:yyyy-MM-dd}");
            sb.AppendLine($"R² (Explained Variance): {attribution.R2:P3}");
            sb.AppendLine();

            sb.AppendLine("FACTOR EXPOSURES:");
            foreach (var exposure in attribution.FactorExposures)
            {
                sb.AppendLine($"{exposure.Key}: {exposure.Value:F3}");
            }
            sb.AppendLine();

            sb.AppendLine("FACTOR CONTRIBUTIONS TO RISK:");
            foreach (var contribution in attribution.FactorContributions.OrderByDescending(c => Math.Abs(c.Value)))
            {
                sb.AppendLine($"{contribution.Key}: {contribution.Value:P3}");
            }
            sb.AppendLine();

            sb.AppendLine("ATTRIBUTION SUMMARY:");
            sb.AppendLine($"Total Attribution: {attribution.TotalAttribution:P3}");
            sb.AppendLine($"Residual Risk: {attribution.ResidualRisk:P3}");

            return sb.ToString();
        }

        private string FormatRiskReport(RiskReport report)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("COMPREHENSIVE RISK REPORT");
            sb.AppendLine("========================");
            sb.AppendLine($"Report Date: {report.ReportDate:yyyy-MM-dd}");
            sb.AppendLine($"Risk Rating: {report.RiskRating}");
            sb.AppendLine();

            sb.AppendLine("VALUE-AT-RISK (95% Confidence):");
            sb.AppendLine($"VaR: {report.VaR.VaR:P3}");
            sb.AppendLine($"Expected Shortfall: {report.VaR.ExpectedShortfall:P3}");
            sb.AppendLine();

            sb.AppendLine("STRESS TEST SUMMARY:");
            var breaches = report.StressTests.Count(st => st.BreachThreshold);
            sb.AppendLine($"Scenarios Tested: {report.StressTests.Count}");
            sb.AppendLine($"Threshold Breaches: {breaches}");
            sb.AppendLine($"Worst Case Loss: {report.StressTests.Max(st => st.PortfolioLoss):P3}");
            sb.AppendLine();

            sb.AppendLine("RISK METRICS:");
            foreach (var metric in report.RiskMetrics)
            {
                sb.AppendLine($"{metric.Key}: {metric.Value:F4}");
            }
            sb.AppendLine();

            sb.AppendLine("FACTOR ATTRIBUTION:");
            sb.AppendLine($"Explained Variance (R²): {report.FactorAttribution.R2:P3}");
            sb.AppendLine($"Residual Risk: {report.FactorAttribution.ResidualRisk:P3}");

            return sb.ToString();
        }

        private string FormatRiskMeasuresComparison(ValueAtRisk historical, ValueAtRisk parametric, ValueAtRisk monteCarlo)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("RISK MEASURES COMPARISON (95% Confidence)");
            sb.AppendLine("========================================");
            sb.AppendLine();

            sb.AppendLine("HISTORICAL VaR:");
            sb.AppendLine($"  VaR: {historical.VaR:P3}");
            sb.AppendLine($"  Expected Shortfall: {historical.ExpectedShortfall:P3}");
            sb.AppendLine();

            sb.AppendLine("PARAMETRIC VaR:");
            sb.AppendLine($"  VaR: {parametric.VaR:P3}");
            sb.AppendLine($"  Expected Shortfall: {parametric.ExpectedShortfall:P3}");
            sb.AppendLine();

            sb.AppendLine("MONTE CARLO VaR:");
            sb.AppendLine($"  VaR: {monteCarlo.VaR:P3}");
            sb.AppendLine($"  Expected Shortfall: {monteCarlo.ExpectedShortfall:P3}");
            sb.AppendLine();

            sb.AppendLine("COMPARISON INSIGHTS:");
            var avgVar = (historical.VaR + parametric.VaR + monteCarlo.VaR) / 3;
            sb.AppendLine($"Average VaR: {avgVar:P3}");
            sb.AppendLine($"Range: {Math.Max(historical.VaR, Math.Max(parametric.VaR, monteCarlo.VaR)) - Math.Min(historical.VaR, Math.Min(parametric.VaR, monteCarlo.VaR)):P3}");

            return sb.ToString();
        }
    }
}