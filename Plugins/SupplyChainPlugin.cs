using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using QuantResearchAgent.Core;
using QuantResearchAgent.Services;

namespace QuantResearchAgent.Plugins
{
    public class SupplyChainPlugin
    {
        private readonly SupplyChainService _supplyChainService;

        public SupplyChainPlugin(SupplyChainService supplyChainService)
        {
            _supplyChainService = supplyChainService;
        }

        [KernelFunction, Description("Analyzes the supply chain for a given company ticker")]
        public async Task<string> AnalyzeSupplyChain(
            [Description("Stock ticker symbol (e.g., AAPL, NVDA)")] string ticker)
        {
            try
            {
                var analysis = await _supplyChainService.AnalyzeCompanySupplyChainAsync(ticker);

                var result = $"Supply Chain Analysis for {ticker}\n\n";
                result += $"Analysis Date: {analysis.AnalysisDate:yyyy-MM-dd}\n";
                result += $"Data Date: {analysis.SupplyChainData.DataDate:yyyy-MM-dd}\n\n";

                result += "Key Metrics:\n";
                result += $"- Suppliers: {analysis.SupplyChainData.Suppliers.Count}\n";
                result += $"- Inventory Turnover: {analysis.SupplyChainData.InventoryMetrics.InventoryTurnoverRatio:F1}\n";
                result += $"- Days Inventory Outstanding: {analysis.SupplyChainData.InventoryMetrics.DaysInventoryOutstanding:F1}\n";
                result += $"- Supply Chain Efficiency: {analysis.SupplyChainData.InventoryMetrics.SupplyChainEfficiency:P1}\n\n";

                result += $"Resilience Score: {analysis.ResilienceScore:P1}\n\n";

                result += "Risk Assessment:\n";
                result += $"Overall Risk Score: {analysis.RiskAssessment.OverallRiskScore:F1}/10\n";
                foreach (var category in analysis.RiskAssessment.RiskByCategory)
                {
                    result += $"{category.Key}: {category.Value:F1}\n";
                }
                result += "\n";

                result += "Concentration Risks:\n";
                foreach (var risk in analysis.ConcentrationRisks)
                {
                    result += $"- {risk}\n";
                }

                return result;
            }
            catch (Exception ex)
            {
                return $"Error analyzing supply chain: {ex.Message}";
            }
        }

        [KernelFunction, Description("Assesses supply chain risks for a company")]
        public async Task<string> AssessSupplyChainRisks(
            [Description("Stock ticker symbol")] string ticker)
        {
            try
            {
                var analysis = await _supplyChainService.AnalyzeCompanySupplyChainAsync(ticker);

                var result = $"Supply Chain Risk Assessment for {ticker}\n\n";

                result += "Risk Metrics:\n";
                result += $"Overall Risk Score: {analysis.RiskAssessment.OverallRiskScore:F1}/10\n";
                foreach (var risk in analysis.RiskAssessment.RiskByCategory)
                {
                    if (risk.Key.Contains("Risk") || risk.Key.Contains("Concentration"))
                    {
                        result += $"{risk.Key}: {risk.Value:F1}\n";
                    }
                }
                result += "\n";

                result += "Concentration Risks:\n";
                foreach (var risk in analysis.ConcentrationRisks)
                {
                    result += $"- {risk}\n";
                }
                result += "\n";

                result += $"Overall Resilience Score: {analysis.ResilienceScore:P1}\n";
                result += GetResilienceInterpretation(analysis.ResilienceScore);

                return result;
            }
            catch (Exception ex)
            {
                return $"Error assessing supply chain risks: {ex.Message}";
            }
        }

        [KernelFunction, Description("Analyzes geographic exposure in supply chain")]
        public async Task<string> AnalyzeGeographicExposure(
            [Description("Stock ticker symbol")] string ticker)
        {
            try
            {
                var analysis = await _supplyChainService.AnalyzeCompanySupplyChainAsync(ticker);

                var result = $"Geographic Exposure Analysis for {ticker}\n\n";

                var regionalExposure = analysis.GeographicExposure.GetValueOrDefault("RegionalExposure", new Dictionary<string, double>()) as Dictionary<string, double>;

                if (regionalExposure != null)
                {
                    result += "Regional Breakdown:\n";
                    foreach (var region in regionalExposure.OrderByDescending(r => r.Value))
                    {
                        result += $"{region.Key}: {region.Value:P1}\n";
                    }
                    result += "\n";
                }

                var countryBreakdown = analysis.GeographicExposure.GetValueOrDefault("CountryBreakdown", new Dictionary<string, dynamic>()) as Dictionary<string, dynamic>;

                if (countryBreakdown != null)
                {
                    result += "Country Breakdown:\n";
                    foreach (var country in countryBreakdown.OrderByDescending(c => c.Value.RevenuePercentage))
                    {
                        result += $"{country.Key}:\n";
                        result += $"  - Revenue %: {country.Value.RevenuePercentage:P1}\n";
                        result += $"  - Suppliers: {country.Value.SupplierCount}\n";
                        result += $"  - Risk Score: {country.Value.AverageRiskScore:F1}/10\n";
                        result += $"  - Industries: {string.Join(", ", country.Value.Industries)}\n\n";
                    }
                }

                var highRiskCountries = analysis.GeographicExposure.GetValueOrDefault("HighRiskCountries", new Dictionary<string, dynamic>()) as Dictionary<string, dynamic>;

                if (highRiskCountries != null && highRiskCountries.Any())
                {
                    result += "High-Risk Countries (>6.0 risk score):\n";
                    foreach (var country in highRiskCountries)
                    {
                        result += $"- {country.Key}: {country.Value.RevenuePercentage:P1} exposure\n";
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                return $"Error analyzing geographic exposure: {ex.Message}";
            }
        }

        [KernelFunction, Description("Calculates supply chain diversification metrics")]
        public async Task<string> CalculateDiversificationMetrics(
            [Description("Stock ticker symbol")] string ticker)
        {
            try
            {
                var analysis = await _supplyChainService.AnalyzeCompanySupplyChainAsync(ticker);

                var result = $"Supply Chain Diversification Metrics for {ticker}\n\n";

                result += "Diversification Scores:\n";
                foreach (var metric in analysis.DiversificationMetrics)
                {
                    if (metric.Key.Contains("Diversification") || metric.Key.Contains("Herfindahl"))
                    {
                        result += $"{metric.Key}: {metric.Value}\n";
                    }
                }
                result += "\n";

                var overallScore = analysis.DiversificationMetrics.GetValueOrDefault("OverallDiversificationScore", 0.0);
                result += $"Overall Diversification Score: {overallScore:P1}\n";
                result += GetDiversificationInterpretation((double)overallScore);

                return result;
            }
            catch (Exception ex)
            {
                return $"Error calculating diversification metrics: {ex.Message}";
            }
        }

        [KernelFunction, Description("Gets detailed supplier information")]
        public async Task<string> GetSupplierDetails(
            [Description("Stock ticker symbol")] string ticker)
        {
            try
            {
                var data = await _supplyChainService.GetSupplyChainDataAsync(ticker);

                var result = $"Supplier Details for {ticker}\n\n";

                result += $"Total Suppliers: {data.Suppliers.Count}\n\n";

                result += "Supplier Breakdown:\n";
                foreach (var supplier in data.Suppliers.OrderByDescending(s => s.RevenuePercentage))
                {
                    result += $"{supplier.Name}:\n";
                    result += $"  - Country: {supplier.Country}\n";
                    result += $"  - Industry: {supplier.Industry}\n";
                    result += $"  - Revenue %: {supplier.RevenuePercentage:P1}\n";
                    result += $"  - Relationship: {supplier.RelationshipType}\n";
                    result += $"  - Risk Score: {supplier.RiskScore:F1}/10\n\n";
                }

                return result;
            }
            catch (Exception ex)
            {
                return $"Error getting supplier details: {ex.Message}";
            }
        }

        [KernelFunction, Description("Analyzes supply chain resilience")]
        public async Task<string> AnalyzeSupplyChainResilience(
            [Description("Stock ticker symbol")] string ticker)
        {
            try
            {
                var analysis = await _supplyChainService.AnalyzeCompanySupplyChainAsync(ticker);

                var result = $"Supply Chain Resilience Analysis for {ticker}\n\n";

                result += $"Resilience Score: {analysis.ResilienceScore:P1}\n";
                result += GetResilienceInterpretation(analysis.ResilienceScore);
                result += "\n";

                result += "Contributing Factors:\n";

                // Diversification factor
                var diversificationScore = analysis.DiversificationMetrics.GetValueOrDefault("OverallDiversificationScore", 0.0);
                result += $"- Diversification: {diversificationScore:P1} (40% weight)\n";

                // Risk factor
                var avgRiskScore = analysis.RiskAssessment.OverallRiskScore;
                var riskNormalized = 1.0 - (Math.Min(avgRiskScore, 10.0) / 10.0);
                result += $"- Risk Profile: {riskNormalized:P1} (40% weight)\n";

                // Inventory efficiency factor
                var inventoryEfficiency = analysis.SupplyChainData.InventoryMetrics.InventoryTurnoverRatio / 12.0;
                inventoryEfficiency = Math.Min(inventoryEfficiency, 1.0);
                result += $"- Inventory Efficiency: {inventoryEfficiency:P1} (20% weight)\n\n";

                result += "Recommendations:\n";
                var recommendations = GenerateResilienceRecommendations(analysis);
                foreach (var rec in recommendations)
                {
                    result += $"- {rec}\n";
                }

                return result;
            }
            catch (Exception ex)
            {
                return $"Error analyzing supply chain resilience: {ex.Message}";
            }
        }

        [KernelFunction, Description("Performs comprehensive supply chain analysis")]
        public async Task<string> ComprehensiveSupplyChainAnalysis(
            [Description("Stock ticker symbol")] string ticker)
        {
            try
            {
                var analysis = await _supplyChainService.AnalyzeCompanySupplyChainAsync(ticker);

                var result = $"COMPREHENSIVE SUPPLY CHAIN ANALYSIS: {ticker}\n";
                result += $"Analysis Date: {analysis.AnalysisDate:yyyy-MM-dd HH:mm}\n\n";

                result += "=== SUPPLY CHAIN OVERVIEW ===\n";
                result += $"Data Date: {analysis.SupplyChainData.DataDate:yyyy-MM-dd}\n";
                result += $"Total Suppliers: {analysis.SupplyChainData.Suppliers.Count}\n";
                result += $"Critical Suppliers: {analysis.SupplyChainData.Suppliers.Count(s => s.RelationshipType == "Critical")}\n\n";

                result += "=== INVENTORY METRICS ===\n";
                result += $"Inventory Turnover Ratio: {analysis.SupplyChainData.InventoryMetrics.InventoryTurnoverRatio:F1}\n";
                result += $"Days Inventory Outstanding: {analysis.SupplyChainData.InventoryMetrics.DaysInventoryOutstanding:F1} days\n";
                result += $"Inventory to Sales Ratio: {analysis.SupplyChainData.InventoryMetrics.InventoryToSalesRatio:P1}\n";
                result += $"Supply Chain Efficiency: {analysis.SupplyChainData.InventoryMetrics.SupplyChainEfficiency:P1}\n\n";

                result += "=== RESILIENCE ASSESSMENT ===\n";
                result += $"Overall Resilience Score: {analysis.ResilienceScore:P1}\n";
                result += GetResilienceInterpretation(analysis.ResilienceScore);
                result += "\n";

                result += "=== RISK ANALYSIS ===\n";
                var riskSummary = analysis.RiskAssessment.MitigationStrategies.Any() 
                    ? string.Join(", ", analysis.RiskAssessment.MitigationStrategies) 
                    : "Not available";
                result += $"Summary: {riskSummary}\n\n";

                result += "Key Risk Metrics:\n";
                foreach (var risk in analysis.RiskAssessment.RiskByCategory.Where(r => r.Key.Contains("Risk") || r.Key.Contains("Concentration")))
                {
                    result += $"{risk.Key}: {risk.Value:F1}\n";
                }
                result += "\n";

                result += "=== DIVERSIFICATION METRICS ===\n";
                foreach (var metric in analysis.DiversificationMetrics)
                {
                    result += $"{metric.Key}: {metric.Value}\n";
                }
                result += "\n";

                result += "=== GEOGRAPHIC EXPOSURE ===\n";
                var regionalExposure = analysis.GeographicExposure.GetValueOrDefault("RegionalExposure", new Dictionary<string, double>()) as Dictionary<string, double>;
                if (regionalExposure != null)
                {
                    foreach (var region in regionalExposure.OrderByDescending(r => r.Value))
                    {
                        result += $"{region.Key}: {region.Value:P1}\n";
                    }
                }
                result += "\n";

                result += "=== CONCENTRATION RISKS ===\n";
                if (analysis.ConcentrationRisks.Any())
                {
                    foreach (var risk in analysis.ConcentrationRisks)
                    {
                        result += $"- {risk}\n";
                    }
                }
                else
                {
                    result += "No significant concentration risks identified\n";
                }
                result += "\n";

                result += "=== TOP SUPPLIERS ===\n";
                foreach (var supplier in analysis.SupplyChainData.Suppliers.OrderByDescending(s => s.RevenuePercentage).Take(5))
                {
                    result += $"{supplier.Name} ({supplier.Country}): {supplier.RevenuePercentage:P1} - Risk: {supplier.RiskScore:F1}\n";
                }

                return result;
            }
            catch (Exception ex)
            {
                return $"Error performing comprehensive supply chain analysis: {ex.Message}";
            }
        }

        private string GetResilienceInterpretation(double score)
        {
            if (score > 0.8)
                return "Highly resilient supply chain with strong diversification and low risk exposure.";
            else if (score > 0.6)
                return "Moderately resilient with good diversification but some risk concentrations.";
            else if (score > 0.4)
                return "Average resilience with balanced diversification and risk management.";
            else if (score > 0.2)
                return "Below average resilience with significant risk concentrations.";
            else
                return "Low resilience with high vulnerability to supply chain disruptions.";
        }

        private string GetDiversificationInterpretation(double score)
        {
            if (score > 0.8)
                return "Excellent diversification across suppliers, geographies, and industries.";
            else if (score > 0.6)
                return "Good diversification with some concentration in specific areas.";
            else if (score > 0.4)
                return "Moderate diversification with notable concentrations to monitor.";
            else if (score > 0.2)
                return "Poor diversification with significant concentration risks.";
            else
                return "Very poor diversification with extreme concentration vulnerabilities.";
        }

        private List<string> GenerateResilienceRecommendations(AlternativeDataModels.SupplyChainAnalysis analysis)
        {
            var recommendations = new List<string>();

            // Check diversification
            var diversificationScore = (double)analysis.DiversificationMetrics.GetValueOrDefault("OverallDiversificationScore", 0.0);
            if (diversificationScore < 0.5)
            {
                recommendations.Add("Increase supplier diversification to reduce concentration risk");
            }

            // Check geographic exposure
            var regionalExposure = analysis.GeographicExposure.GetValueOrDefault("RegionalExposure", new Dictionary<string, double>()) as Dictionary<string, double>;
            if (regionalExposure != null)
            {
                var asiaExposure = regionalExposure.GetValueOrDefault("Asia-Pacific", 0.0);
                if (asiaExposure > 0.6)
                {
                    recommendations.Add("Reduce Asia-Pacific exposure through alternative sourcing regions");
                }
            }

            // Check critical suppliers
            var criticalSuppliers = analysis.SupplyChainData.Suppliers.Count(s => s.RelationshipType == "Critical");
            if (criticalSuppliers > 3)
            {
                recommendations.Add("Develop backup suppliers for critical components");
            }

            // Check inventory efficiency
            var inventoryTurnover = analysis.SupplyChainData.InventoryMetrics.InventoryTurnoverRatio;
            if (inventoryTurnover < 6)
            {
                recommendations.Add("Improve inventory turnover through better demand forecasting");
            }

            if (!recommendations.Any())
            {
                recommendations.Add("Supply chain resilience is well-managed");
            }

            return recommendations;
        }
    }
}