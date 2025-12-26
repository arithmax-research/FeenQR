using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using QuantResearchAgent.Core;
using QuantResearchAgent.Services;

namespace QuantResearchAgent.Services
{
    public class CountryExposureData
    {
        public double RevenuePercentage { get; set; }
        public int SupplierCount { get; set; }
        public double AverageRiskScore { get; set; }
        public List<string> Industries { get; set; } = new();
    }

    public class SupplyChainService
    {
        private readonly HttpClient _httpClient;
        private readonly ILLMService _llmService;

        public SupplyChainService(HttpClient httpClient, ILLMService llmService)
        {
            _httpClient = httpClient;
            _llmService = llmService;
        }

        // Data sources for supply chain information
        private const string SUPPLY_CHAIN_API_BASE = "https://api.supplychaindata.com"; // Placeholder
        private const string NEWS_API_KEY = ""; // Would be configured

        public async Task<AlternativeDataModels.SupplyChainAnalysis> AnalyzeCompanySupplyChainAsync(string ticker)
        {
            var analysis = new AlternativeDataModels.SupplyChainAnalysis
            {
                AnalysisDate = DateTime.Now
            };

            try
            {
                // Get supply chain data
                var supplyChainData = await GetSupplyChainDataAsync(ticker);
                analysis.SupplyChainData = supplyChainData;

                // Perform comprehensive analysis
                analysis.RiskAssessment = await AssessSupplyChainRisksAsync(supplyChainData);
                analysis.DiversificationMetrics = await CalculateDiversificationMetricsAsync(supplyChainData);
                analysis.GeographicExposure = await AnalyzeGeographicExposureAsync(supplyChainData);
                analysis.ConcentrationRisks = await IdentifyConcentrationRisksAsync(supplyChainData);
                analysis.ResilienceScore = await CalculateResilienceScoreAsync(supplyChainData);

                return analysis;
            }
            catch (Exception ex)
            {
                analysis.Insights["Error"] = $"Analysis failed: {ex.Message}";
                return analysis;
            }
        }

        public async Task<AlternativeDataModels.SupplyChainData> GetSupplyChainDataAsync(string ticker)
        {
            var data = new AlternativeDataModels.SupplyChainData
            {
                CompanyTicker = ticker,
                DataDate = DateTime.Now
            };

            try
            {
                // In a real implementation, this would integrate with supply chain databases
                // For now, create mock data based on typical supply chain structures

                // Mock suppliers
                data.Suppliers = new List<AlternativeDataModels.Supplier>
                {
                    new AlternativeDataModels.Supplier
                    {
                        Name = "Taiwan Semiconductor Manufacturing Company",
                        Country = "Taiwan",
                        Industry = "Semiconductors",
                        RelationshipType = "Critical",
                        RevenuePercentage = 0.25,
                        RiskScore = 7.5 // High geopolitical risk
                    },
                    new AlternativeDataModels.Supplier
                    {
                        Name = "Samsung Electronics",
                        Country = "South Korea",
                        Industry = "Semiconductors",
                        RelationshipType = "Strategic",
                        RevenuePercentage = 0.15,
                        RiskScore = 6.0
                    },
                    new AlternativeDataModels.Supplier
                    {
                        Name = "SK Hynix",
                        Country = "South Korea",
                        Industry = "Memory Chips",
                        RelationshipType = "Important",
                        RevenuePercentage = 0.10,
                        RiskScore = 5.5
                    },
                    new AlternativeDataModels.Supplier
                    {
                        Name = "United Microelectronics Corporation",
                        Country = "Taiwan",
                        Industry = "Semiconductors",
                        RelationshipType = "Secondary",
                        RevenuePercentage = 0.08,
                        RiskScore = 7.0
                    },
                    new AlternativeDataModels.Supplier
                    {
                        Name = "GlobalFoundries",
                        Country = "United States",
                        Industry = "Semiconductors",
                        RelationshipType = "Backup",
                        RevenuePercentage = 0.05,
                        RiskScore = 4.0
                    }
                };

                // Mock inventory metrics
                data.InventoryMetrics = new AlternativeDataModels.InventoryMetrics
                {
                    InventoryTurnoverRatio = 8.5,
                    DaysInventoryOutstanding = 42.9,
                    InventoryToSalesRatio = 0.12,
                    SupplyChainEfficiency = 0.85
                };

                // Mock logistics data
                data.Logistics = new AlternativeDataModels.LogisticsData
                {
                    ShippingCosts = 1250000.00, // $1.25M annual shipping costs
                    DeliveryTime = 45.5, // Average 45.5 days
                    ShippingByRegion = new Dictionary<string, double>
                    {
                        ["Asia-Pacific"] = 0.65,
                        ["Trans-Pacific"] = 0.25,
                        ["Domestic US"] = 0.10
                    }
                };

                // Get real-time risk indicators from news and data feeds
                data.RiskIndicators = await GetCurrentRiskIndicatorsAsync(ticker);

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting supply chain data: {ex.Message}");
                // Return basic structure even on error
            }

            return data;
        }

        public async Task<AlternativeDataModels.SupplyChainRisk> AssessSupplyChainRisksAsync(
            AlternativeDataModels.SupplyChainData data)
        {
            var riskAssessment = new AlternativeDataModels.SupplyChainRisk();

            try
            {
                // Geographic concentration risk
                var countryGroups = data.Suppliers.GroupBy(s => s.Country);
                var maxCountryConcentration = countryGroups.Max(g => g.Sum(s => s.RevenuePercentage));
                riskAssessment.RiskByCategory["GeographicConcentration"] = maxCountryConcentration;

                // Supplier concentration risk
                var maxSupplierConcentration = data.Suppliers.Max(s => s.RevenuePercentage);
                riskAssessment.RiskByCategory["SupplierConcentration"] = maxSupplierConcentration;

                // Industry concentration risk
                var industryGroups = data.Suppliers.GroupBy(s => s.Industry);
                var maxIndustryConcentration = industryGroups.Max(g => g.Sum(s => s.RevenuePercentage));
                riskAssessment.RiskByCategory["IndustryConcentration"] = maxIndustryConcentration;

                // Overall risk score
                var avgRiskScore = data.Suppliers.Average(s => s.RiskScore);
                riskAssessment.OverallRiskScore = avgRiskScore;

                // Critical supplier count
                var criticalSuppliers = data.Suppliers.Count(s => s.RelationshipType == "Critical");
                riskAssessment.RiskByCategory["CriticalSupplierCount"] = criticalSuppliers;

                // Risk assessment summary
                var riskSummary = await GenerateRiskSummaryAsync(data);
                riskAssessment.MitigationStrategies.Add(riskSummary);

            }
            catch (Exception ex)
            {
                riskAssessment.MitigationStrategies.Add($"Risk assessment failed: {ex.Message}");
            }

            return riskAssessment;
        }

        public async Task<Dictionary<string, object>> CalculateDiversificationMetricsAsync(
            AlternativeDataModels.SupplyChainData data)
        {
            var metrics = new Dictionary<string, object>();

            try
            {
                // Geographic diversification
                var countryCount = data.Suppliers.Select(s => s.Country).Distinct().Count();
                var herfindahlCountry = CalculateHerfindahlIndex(
                    data.Suppliers.GroupBy(s => s.Country)
                    .Select(g => g.Sum(s => s.RevenuePercentage)));

                metrics["GeographicDiversification"] = countryCount;
                metrics["GeographicHerfindahlIndex"] = herfindahlCountry;

                // Supplier diversification
                var supplierCount = data.Suppliers.Count;
                var herfindahlSupplier = CalculateHerfindahlIndex(
                    data.Suppliers.Select(s => s.RevenuePercentage));

                metrics["SupplierDiversification"] = supplierCount;
                metrics["SupplierHerfindahlIndex"] = herfindahlSupplier;

                // Industry diversification
                var industryCount = data.Suppliers.Select(s => s.Industry).Distinct().Count();
                var herfindahlIndustry = CalculateHerfindahlIndex(
                    data.Suppliers.GroupBy(s => s.Industry)
                    .Select(g => g.Sum(s => s.RevenuePercentage)));

                metrics["IndustryDiversification"] = industryCount;
                metrics["IndustryHerfindahlIndex"] = herfindahlIndustry;

                // Overall diversification score (0-1, higher is better diversified)
                var diversificationScore = 1.0 - (herfindahlCountry + herfindahlSupplier + herfindahlIndustry) / 3.0;
                metrics["OverallDiversificationScore"] = Math.Max(0, Math.Min(1, diversificationScore));

            }
            catch (Exception ex)
            {
                metrics["Error"] = $"Diversification calculation failed: {ex.Message}";
            }

            return metrics;
        }

        public async Task<Dictionary<string, object>> AnalyzeGeographicExposureAsync(
            AlternativeDataModels.SupplyChainData data)
        {
            var exposure = new Dictionary<string, object>();

            try
            {
                var countryExposure = data.Suppliers
                    .GroupBy(s => s.Country)
                    .ToDictionary(
                        g => g.Key,
                        g => new CountryExposureData
                        {
                            RevenuePercentage = g.Sum(s => s.RevenuePercentage),
                            SupplierCount = g.Count(),
                            AverageRiskScore = g.Average(s => s.RiskScore),
                            Industries = g.Select(s => s.Industry).Distinct().ToList()
                        });

                exposure["CountryBreakdown"] = countryExposure;

                // High-risk countries
                var highRiskCountries = countryExposure
                    .Where(c => c.Value.AverageRiskScore > 6.0)
                    .OrderByDescending(c => c.Value.RevenuePercentage)
                    .ToDictionary(c => c.Key, c => c.Value);

                exposure["HighRiskCountries"] = highRiskCountries;

                // Regional analysis
                var regionalExposure = CalculateRegionalExposure(countryExposure);
                exposure["RegionalExposure"] = regionalExposure;

            }
            catch (Exception ex)
            {
                exposure["Error"] = $"Geographic analysis failed: {ex.Message}";
            }

            return exposure;
        }

        public async Task<Dictionary<string, object>> IdentifyConcentrationRisksAsync(
            AlternativeDataModels.SupplyChainData data)
        {
            var risks = new Dictionary<string, object>();

            try
            {
                // Check for single supplier dominance
                var maxSupplierShare = data.Suppliers.Max(s => s.RevenuePercentage);
                if (maxSupplierShare > 0.3)
                {
                    risks["SupplierConcentration"] = $"{data.Suppliers.First(s => s.RevenuePercentage == maxSupplierShare).Name} represents {maxSupplierShare:P1} of revenue";
                }

                // Check for geographic concentration
                var countryShares = data.Suppliers.GroupBy(s => s.Country)
                    .Select(g => g.Sum(s => s.RevenuePercentage));
                var maxCountryShare = countryShares.Max();
                if (maxCountryShare > 0.4)
                {
                    var country = data.Suppliers.First(s => s.Country == data.Suppliers
                        .GroupBy(x => x.Country)
                        .OrderByDescending(g => g.Sum(x => x.RevenuePercentage))
                        .First().Key).Country;
                    risks["GeographicConcentration"] = $"{country} represents {maxCountryShare:P1} of supply chain";
                }

                // Check for industry concentration
                var industryShares = data.Suppliers.GroupBy(s => s.Industry)
                    .Select(g => g.Sum(s => s.RevenuePercentage));
                var maxIndustryShare = industryShares.Max();
                if (maxIndustryShare > 0.5)
                {
                    var industry = data.Suppliers.First(s => s.Industry == data.Suppliers
                        .GroupBy(x => x.Industry)
                        .OrderByDescending(g => g.Sum(x => x.RevenuePercentage))
                        .First().Key).Industry;
                    risks["IndustryConcentration"] = $"{industry} represents {maxIndustryShare:P1} of supply chain";
                }

                // Check for critical supplier risks
                var criticalSuppliers = data.Suppliers.Where(s => s.RelationshipType == "Critical").ToList();
                if (criticalSuppliers.Any(s => s.RiskScore > 7.0))
                {
                    risks["CriticalSupplierRisk"] = "Critical suppliers have high risk scores - consider backup suppliers";
                }

            }
            catch (Exception ex)
            {
                risks["Error"] = $"Could not identify concentration risks: {ex.Message}";
            }

            return risks;
        }

        public async Task<double> CalculateResilienceScoreAsync(AlternativeDataModels.SupplyChainData data)
        {
            try
            {
                // Calculate resilience based on multiple factors
                var diversificationScore = await CalculateDiversificationMetricsAsync(data);
                var overallDiversification = (double)diversificationScore.GetValueOrDefault("OverallDiversificationScore", 0.5);

                var riskAssessment = await AssessSupplyChainRisksAsync(data);
                var avgRiskScore = riskAssessment.OverallRiskScore;

                // Normalize risk score (lower risk = higher resilience)
                var riskNormalized = 1.0 - (Math.Min(avgRiskScore, 10.0) / 10.0);

                // Inventory efficiency factor
                var inventoryEfficiency = data.InventoryMetrics.InventoryTurnoverRatio / 12.0; // Normalize to 12 as benchmark
                inventoryEfficiency = Math.Min(inventoryEfficiency, 1.0);

                // Weighted resilience score
                var resilienceScore = (overallDiversification * 0.4) +
                                    (riskNormalized * 0.4) +
                                    (inventoryEfficiency * 0.2);

                return Math.Max(0, Math.Min(1, resilienceScore));
            }
            catch (Exception)
            {
                return 0.5; // Default neutral score
            }
        }

        // Helper methods
        private double CalculateHerfindahlIndex(IEnumerable<double> shares)
        {
            return shares.Sum(share => share * share);
        }

        private async Task<string> GenerateRiskSummaryAsync(AlternativeDataModels.SupplyChainData data)
        {
            try
            {
                var prompt = $"Summarize the supply chain risks for {data.CompanyTicker} based on the following supplier data:\n\n" +
                           $"Suppliers: {string.Join(", ", data.Suppliers.Select(s => $"{s.Name} ({s.Country}, {s.RevenuePercentage:P1})"))}\n\n" +
                           "Focus on concentration risks, geographic exposure, and potential vulnerabilities.";

                return await _llmService.GetChatCompletionAsync(prompt);
            }
            catch (Exception)
            {
                return "Risk summary generation failed";
            }
        }

        private Dictionary<string, double> CalculateRegionalExposure(Dictionary<string, CountryExposureData> countryExposure)
        {
            var regions = new Dictionary<string, List<string>>
            {
                ["Asia-Pacific"] = new List<string> { "Taiwan", "South Korea", "China", "Japan" },
                ["North America"] = new List<string> { "United States", "Canada", "Mexico" },
                ["Europe"] = new List<string> { "Germany", "Netherlands", "United Kingdom", "France" },
                ["Other"] = new List<string>() // Catch-all for unassigned countries
            };

            var regionalExposure = new Dictionary<string, double>();

            foreach (var region in regions)
            {
                var regionCountries = region.Value;
                var regionExposure = countryExposure
                    .Where(c => regionCountries.Contains(c.Key))
                    .Sum(c => c.Value.RevenuePercentage);

                regionalExposure[region.Key] = regionExposure;
            }

            // Add any remaining countries to "Other"
            var accountedCountries = regions.Values.SelectMany(c => c).ToHashSet();
            var otherExposure = countryExposure
                .Where(c => !accountedCountries.Contains(c.Key))
                .Sum(c => c.Value.RevenuePercentage);

            regionalExposure["Other"] += otherExposure;

            return regionalExposure;
        }

        private async Task<Dictionary<string, object>> GetCurrentRiskIndicatorsAsync(string ticker)
        {
            var indicators = new Dictionary<string, object>();

            try
            {
                // In a real implementation, this would query news APIs, trade data, etc.
                // For now, return mock risk indicators

                indicators["GeopoliticalRisks"] = new List<string>
                {
                    "Taiwan Strait tensions affecting semiconductor supply",
                    "US-China trade relations impact on electronics components"
                };

                indicators["MarketDisruptions"] = new List<string>
                {
                    "Chip shortage continues to affect automotive sector",
                    "COVID-19 related factory shutdowns in Asia"
                };

                indicators["SupplierFinancialHealth"] = new Dictionary<string, double>
                {
                    ["TSMC"] = 8.5, // Financial health score
                    ["Samsung"] = 8.0,
                    ["SK Hynix"] = 7.5
                };

                indicators["LeadTimeChanges"] = new Dictionary<string, int>
                {
                    ["Semiconductors"] = 2, // Weeks increase
                    ["Memory Chips"] = 1
                };

            }
            catch (Exception ex)
            {
                indicators["Error"] = $"Could not retrieve risk indicators: {ex.Message}";
            }

            return indicators;
        }
    }
}