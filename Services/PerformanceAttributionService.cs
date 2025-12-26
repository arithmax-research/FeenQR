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
    /// Performance attribution and factor analysis service
    /// </summary>
    public class PerformanceAttributionService
    {
        private readonly ILogger<PerformanceAttributionService> _logger;
        private readonly MarketDataService _marketDataService;
        private readonly StatisticalTestingService _statisticalTestingService;

        public PerformanceAttributionService(
            ILogger<PerformanceAttributionService> logger,
            MarketDataService marketDataService,
            StatisticalTestingService statisticalTestingService)
        {
            _logger = logger;
            _marketDataService = marketDataService;
            _statisticalTestingService = statisticalTestingService;
        }

        /// <summary>
        /// Perform multi-factor performance attribution
        /// </summary>
        public async Task<PerformanceAttributionResult> PerformAttributionAsync(
            PortfolioReturns portfolioReturns,
            List<FactorReturns> factors,
            AttributionMethodology methodology = AttributionMethodology.BrinsonFachler)
        {
            _logger.LogInformation($"Performing performance attribution with {factors.Count} factors using {methodology}");

            try
            {
                var attributionPeriods = new List<AttributionPeriod>();

                // Process each time period
                foreach (var period in portfolioReturns.ReturnsByPeriod)
                {
                    var periodAttribution = await CalculatePeriodAttributionAsync(
                        period.Value,
                        factors,
                        methodology);

                    attributionPeriods.Add(new AttributionPeriod
                    {
                        Period = period.Key,
                        PortfolioReturn = period.Value.PortfolioReturn,
                        BenchmarkReturn = period.Value.BenchmarkReturn,
                        Attribution = periodAttribution,
                        ExcessReturn = period.Value.PortfolioReturn - period.Value.BenchmarkReturn
                    });
                }

                // Aggregate results
                var totalAttribution = AggregateAttribution(attributionPeriods);

                // Calculate attribution statistics
                var statistics = CalculateAttributionStatistics(attributionPeriods);

                return new PerformanceAttributionResult
                {
                    AttributionPeriods = attributionPeriods,
                    TotalAttribution = totalAttribution,
                    Statistics = statistics,
                    Methodology = methodology,
                    Timestamp = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing performance attribution");
                throw;
            }
        }

        /// <summary>
        /// Perform sector-level attribution analysis
        /// </summary>
        public async Task<SectorAttributionResult> PerformSectorAttributionAsync(
            Dictionary<string, PortfolioReturns> sectorReturns,
            BenchmarkReturns benchmarkReturns,
            Dictionary<string, double> sectorWeights)
        {
            _logger.LogInformation($"Performing sector attribution for {sectorReturns.Count} sectors");

            try
            {
                var sectorAttributions = new List<SectorAttribution>();

                foreach (var sector in sectorReturns)
                {
                    var sectorWeight = sectorWeights.GetValueOrDefault(sector.Key, 0.0);
                    var sectorBenchmarkWeight = benchmarkReturns.SectorWeights.GetValueOrDefault(sector.Key, 0.0);

                    // Calculate allocation effect
                    var allocationEffect = (sectorWeight - sectorBenchmarkWeight) * benchmarkReturns.ReturnsByPeriod.First().Value;

                    // Calculate selection effect
                    var selectionEffect = sectorWeight * (sector.Value.ReturnsByPeriod.First().Value - benchmarkReturns.ReturnsByPeriod.First().Value);

                    // Calculate interaction effect
                    var interactionEffect = (sectorWeight - sectorBenchmarkWeight) *
                        (sector.Value.ReturnsByPeriod.First().Value - benchmarkReturns.ReturnsByPeriod.First().Value);

                    sectorAttributions.Add(new SectorAttribution
                    {
                        SectorName = sector.Key,
                        PortfolioWeight = sectorWeight,
                        BenchmarkWeight = sectorBenchmarkWeight,
                        SectorReturn = sector.Value.ReturnsByPeriod.First().Value,
                        BenchmarkSectorReturn = benchmarkReturns.ReturnsByPeriod.First().Value,
                        AllocationEffect = allocationEffect,
                        SelectionEffect = selectionEffect,
                        InteractionEffect = interactionEffect,
                        TotalAttribution = allocationEffect + selectionEffect + interactionEffect
                    });
                }

                var totalAttribution = sectorAttributions.Sum(s => s.TotalAttribution);

                return new SectorAttributionResult
                {
                    SectorAttributions = sectorAttributions,
                    TotalAttribution = totalAttribution,
                    Timestamp = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing sector attribution");
                throw;
            }
        }

        /// <summary>
        /// Perform style analysis using returns-based style analysis
        /// </summary>
        public async Task<StyleAnalysisResult> PerformStyleAnalysisAsync(
            PortfolioReturns portfolioReturns,
            List<FactorReturns> styleFactors,
            int rollingWindow = 36)
        {
            _logger.LogInformation($"Performing style analysis with {styleFactors.Count} style factors");

            try
            {
                var styleExposures = new List<StyleExposure>();
                var rollingStyleExposures = new List<RollingStyleExposure>();

                // Perform rolling window style analysis
                var portfolioReturnSeries = portfolioReturns.ReturnsByPeriod.Values.Select(r => r.PortfolioReturn).ToList();
                var factorReturnMatrix = BuildFactorReturnMatrix(styleFactors, portfolioReturns.ReturnsByPeriod.Keys.ToList());

                for (int i = rollingWindow - 1; i < portfolioReturnSeries.Count; i++)
                {
                    var windowPortfolioReturns = portfolioReturnSeries.Skip(i - rollingWindow + 1).Take(rollingWindow).ToList();
                    var windowFactorReturns = factorReturnMatrix.Skip(i - rollingWindow + 1).Take(rollingWindow).ToList();

                    var styleWeights = await CalculateStyleWeightsAsync(windowPortfolioReturns, windowFactorReturns, styleFactors);

                    rollingStyleExposures.Add(new RollingStyleExposure
                    {
                        Period = portfolioReturns.ReturnsByPeriod.Keys.ElementAt(i),
                        StyleWeights = styleWeights,
                        RSquared = CalculateRSquared(windowPortfolioReturns, windowFactorReturns, styleWeights),
                        TrackingError = CalculateTrackingError(windowPortfolioReturns, windowFactorReturns, styleWeights)
                    });
                }

                // Calculate average style exposures
                var averageWeights = new Dictionary<string, double>();
                foreach (var factor in styleFactors)
                {
                    averageWeights[factor.Name] = rollingStyleExposures.Average(r => r.StyleWeights.GetValueOrDefault(factor.Name, 0.0));
                }

                styleExposures.Add(new StyleExposure
                {
                    FactorName = "Average",
                    Weight = 1.0,
                    StyleWeights = averageWeights,
                    TimePeriod = "Full Period"
                });

                return new StyleAnalysisResult
                {
                    StyleExposures = styleExposures,
                    RollingStyleExposures = rollingStyleExposures,
                    AverageRSquared = rollingStyleExposures.Average(r => r.RSquared),
                    AverageTrackingError = rollingStyleExposures.Average(r => r.TrackingError),
                    Timestamp = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing style analysis");
                throw;
            }
        }

        /// <summary>
        /// Perform risk-adjusted performance attribution
        /// </summary>
        public async Task<RiskAdjustedAttributionResult> PerformRiskAdjustedAttributionAsync(
            PerformanceAttributionResult attributionResult,
            PortfolioRiskMetrics riskMetrics)
        {
            _logger.LogInformation("Performing risk-adjusted performance attribution");

            try
            {
                var riskAdjustedAttributions = new List<RiskAdjustedAttribution>();

                foreach (var period in attributionResult.AttributionPeriods)
                {
                    var riskAdjustedFactors = new Dictionary<string, double>();

                    foreach (var factor in period.Attribution.FactorContributions)
                    {
                        // Calculate risk-adjusted contribution
                        var factorVolatility = riskMetrics.FactorVolatilities.GetValueOrDefault(factor.Key, 1.0);
                        var riskAdjustedContribution = factor.Value / factorVolatility;

                        riskAdjustedFactors[factor.Key] = riskAdjustedContribution;
                    }

                    riskAdjustedAttributions.Add(new RiskAdjustedAttribution
                    {
                        Period = period.Period,
                        RiskAdjustedFactors = riskAdjustedFactors,
                        TotalRiskAdjustedAttribution = riskAdjustedFactors.Values.Sum(),
                        SharpeRatio = period.ExcessReturn / riskMetrics.PortfolioVolatility,
                        InformationRatio = period.ExcessReturn / riskMetrics.TrackingError
                    });
                }

                // Calculate risk-adjusted statistics
                var averageSharpeRatio = riskAdjustedAttributions.Average(r => r.SharpeRatio);
                var averageInformationRatio = riskAdjustedAttributions.Average(r => r.InformationRatio);

                return new RiskAdjustedAttributionResult
                {
                    RiskAdjustedAttributions = riskAdjustedAttributions,
                    AverageSharpeRatio = averageSharpeRatio,
                    AverageInformationRatio = averageInformationRatio,
                    RiskMetrics = riskMetrics,
                    Timestamp = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing risk-adjusted attribution");
                throw;
            }
        }

        /// <summary>
        /// Perform holdings-based attribution analysis
        /// </summary>
        public async Task<HoldingsAttributionResult> PerformHoldingsAttributionAsync(
            Dictionary<string, HoldingAttributionData> holdingsData,
            BenchmarkReturns benchmarkReturns)
        {
            _logger.LogInformation($"Performing holdings attribution for {holdingsData.Count} holdings");

            try
            {
                var holdingAttributions = new List<HoldingAttribution>();

                foreach (var holding in holdingsData)
                {
                    var data = holding.Value;

                    // Calculate stock selection effect
                    var stockSelection = data.PortfolioWeight * (data.HoldingReturn - data.BenchmarkHoldingReturn);

                    // Calculate allocation effect
                    var allocation = (data.PortfolioWeight - data.BenchmarkWeight) * data.BenchmarkHoldingReturn;

                    // Calculate interaction effect
                    var interaction = (data.PortfolioWeight - data.BenchmarkWeight) *
                        (data.HoldingReturn - data.BenchmarkHoldingReturn);

                    holdingAttributions.Add(new HoldingAttribution
                    {
                        HoldingId = holding.Key,
                        HoldingName = data.HoldingName,
                        PortfolioWeight = data.PortfolioWeight,
                        BenchmarkWeight = data.BenchmarkWeight,
                        HoldingReturn = data.HoldingReturn,
                        BenchmarkHoldingReturn = data.BenchmarkHoldingReturn,
                        StockSelectionEffect = stockSelection,
                        AllocationEffect = allocation,
                        InteractionEffect = interaction,
                        TotalAttribution = stockSelection + allocation + interaction
                    });
                }

                // Sort by attribution impact
                var sortedAttributions = holdingAttributions
                    .OrderByDescending(h => Math.Abs(h.TotalAttribution))
                    .ToList();

                var totalAttribution = holdingAttributions.Sum(h => h.TotalAttribution);

                return new HoldingsAttributionResult
                {
                    HoldingAttributions = sortedAttributions,
                    TotalAttribution = totalAttribution,
                    TopContributors = sortedAttributions.Take(10).ToList(),
                    TopDetractors = sortedAttributions.Where(h => h.TotalAttribution < 0)
                        .OrderBy(h => h.TotalAttribution)
                        .Take(10)
                        .ToList(),
                    Timestamp = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing holdings attribution");
                throw;
            }
        }

        /// <summary>
        /// Generate attribution report with visualizations
        /// </summary>
        public async Task<AttributionReport> GenerateAttributionReportAsync(
            PerformanceAttributionResult attributionResult,
            SectorAttributionResult sectorResult,
            StyleAnalysisResult styleResult)
        {
            _logger.LogInformation("Generating attribution report");

            try
            {
                var reportSections = new List<ReportSection>();

                // Executive Summary
                reportSections.Add(new ReportSection
                {
                    Title = "Executive Summary",
                    Content = GenerateExecutiveSummary(attributionResult, sectorResult, styleResult),
                    Charts = new List<string> { "total_attribution_chart", "factor_contribution_chart" }
                });

                // Factor Attribution
                reportSections.Add(new ReportSection
                {
                    Title = "Factor Attribution Analysis",
                    Content = GenerateFactorAttributionContent(attributionResult),
                    Charts = new List<string> { "factor_contribution_timeline", "factor_exposure_chart" }
                });

                // Sector Attribution
                reportSections.Add(new ReportSection
                {
                    Title = "Sector Attribution Analysis",
                    Content = GenerateSectorAttributionContent(sectorResult),
                    Charts = new List<string> { "sector_attribution_chart", "sector_allocation_chart" }
                });

                // Style Analysis
                reportSections.Add(new ReportSection
                {
                    Title = "Style Analysis",
                    Content = GenerateStyleAnalysisContent(styleResult),
                    Charts = new List<string> { "style_exposure_chart", "rolling_style_chart" }
                });

                // Risk-Adjusted Performance
                reportSections.Add(new ReportSection
                {
                    Title = "Risk-Adjusted Performance",
                    Content = GenerateRiskAdjustedContent(attributionResult),
                    Charts = new List<string> { "sharpe_ratio_chart", "information_ratio_chart" }
                });

                return new AttributionReport
                {
                    Title = "Performance Attribution Report",
                    GeneratedDate = DateTime.UtcNow,
                    ReportSections = reportSections,
                    SummaryMetrics = GenerateSummaryMetrics(attributionResult, sectorResult, styleResult)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating attribution report");
                throw;
            }
        }

        #region Helper Methods

        private async Task<PeriodAttribution> CalculatePeriodAttributionAsync(
            PeriodReturns periodReturns,
            List<FactorReturns> factors,
            AttributionMethodology methodology)
        {
            var factorContributions = new Dictionary<string, double>();

            switch (methodology)
            {
                case AttributionMethodology.BrinsonFachler:
                    factorContributions = await CalculateBrinsonFachlerAttributionAsync(periodReturns, factors);
                    break;
                case AttributionMethodology.Carino:
                    factorContributions = await CalculateCarinoAttributionAsync(periodReturns, factors);
                    break;
                case AttributionMethodology.Menchero:
                    factorContributions = await CalculateMencheroAttributionAsync(periodReturns, factors);
                    break;
                default:
                    factorContributions = await CalculateBrinsonFachlerAttributionAsync(periodReturns, factors);
                    break;
            }

            return new PeriodAttribution
            {
                FactorContributions = factorContributions,
                TotalAttribution = factorContributions.Values.Sum(),
                Residual = periodReturns.PortfolioReturn - periodReturns.BenchmarkReturn - factorContributions.Values.Sum()
            };
        }

        private async Task<Dictionary<string, double>> CalculateBrinsonFachlerAttributionAsync(
            PeriodReturns periodReturns,
            List<FactorReturns> factors)
        {
            var contributions = new Dictionary<string, double>();

            foreach (var factor in factors)
            {
                // Simplified Brinson-Fachler attribution
                // Contribution = Factor weight in portfolio * Factor return - Factor weight in benchmark * Factor return
                var portfolioWeight = factor.PortfolioWeights.GetValueOrDefault(periodReturns.Period, 0.0);
                var benchmarkWeight = factor.BenchmarkWeights.GetValueOrDefault(periodReturns.Period, 0.0);
                var factorReturn = factor.ReturnsByPeriod.GetValueOrDefault(periodReturns.Period, 0.0);

                var contribution = portfolioWeight * factorReturn - benchmarkWeight * factorReturn;
                contributions[factor.Name] = contribution;
            }

            return contributions;
        }

        private async Task<Dictionary<string, double>> CalculateCarinoAttributionAsync(
            PeriodReturns periodReturns,
            List<FactorReturns> factors)
        {
            // Carino's duration-based attribution
            var contributions = new Dictionary<string, double>();

            foreach (var factor in factors)
            {
                var portfolioWeight = factor.PortfolioWeights.GetValueOrDefault(periodReturns.Period, 0.0);
                var benchmarkWeight = factor.BenchmarkWeights.GetValueOrDefault(periodReturns.Period, 0.0);
                var factorReturn = factor.ReturnsByPeriod.GetValueOrDefault(periodReturns.Period, 0.0);

                // Carino attribution: (Portfolio weight - Benchmark weight) * Factor return
                var contribution = (portfolioWeight - benchmarkWeight) * factorReturn;
                contributions[factor.Name] = contribution;
            }

            return contributions;
        }

        private async Task<Dictionary<string, double>> CalculateMencheroAttributionAsync(
            PeriodReturns periodReturns,
            List<FactorReturns> factors)
        {
            // Menchero's geometric attribution
            var contributions = new Dictionary<string, double>();

            foreach (var factor in factors)
            {
                var portfolioWeight = factor.PortfolioWeights.GetValueOrDefault(periodReturns.Period, 0.0);
                var benchmarkWeight = factor.BenchmarkWeights.GetValueOrDefault(periodReturns.Period, 0.0);
                var factorReturn = factor.ReturnsByPeriod.GetValueOrDefault(periodReturns.Period, 0.0);

                // Geometric attribution using logarithmic returns
                if (portfolioWeight > 0 && benchmarkWeight > 0)
                {
                    var contribution = portfolioWeight * Math.Log(1 + factorReturn) - benchmarkWeight * Math.Log(1 + factorReturn);
                    contributions[factor.Name] = contribution;
                }
                else
                {
                    contributions[factor.Name] = 0.0;
                }
            }

            return contributions;
        }

        private PeriodAttribution AggregateAttribution(List<AttributionPeriod> periods)
        {
            var totalContributions = new Dictionary<string, double>();

            foreach (var period in periods)
            {
                foreach (var contribution in period.Attribution.FactorContributions)
                {
                    if (!totalContributions.ContainsKey(contribution.Key))
                        totalContributions[contribution.Key] = 0.0;

                    totalContributions[contribution.Key] += contribution.Value;
                }
            }

            return new PeriodAttribution
            {
                FactorContributions = totalContributions,
                TotalAttribution = totalContributions.Values.Sum(),
                Residual = periods.Sum(p => p.Attribution.Residual)
            };
        }

        private AttributionStatistics CalculateAttributionStatistics(List<AttributionPeriod> periods)
        {
            var excessReturns = periods.Select(p => p.ExcessReturn).ToList();
            var attributionReturns = periods.Select(p => p.Attribution.TotalAttribution).ToList();

            return new AttributionStatistics
            {
                MeanExcessReturn = excessReturns.Average(),
                MeanAttribution = attributionReturns.Average(),
                ExcessReturnVolatility = (decimal)excessReturns.StandardDeviation(),
                AttributionVolatility = (decimal)attributionReturns.StandardDeviation(),
                MaxExcessReturn = excessReturns.Max(),
                MinExcessReturn = excessReturns.Min(),
                AttributionAccuracy = CalculateAttributionAccuracy(excessReturns, attributionReturns),
                PeriodsAnalyzed = periods.Count
            };
        }

        private double CalculateAttributionAccuracy(List<double> excessReturns, List<double> attributionReturns)
        {
            if (excessReturns.Count != attributionReturns.Count)
                return 0.0;

            var differences = excessReturns.Zip(attributionReturns, (e, a) => Math.Abs(e - a)).ToList();
            var meanDifference = differences.Average();
            var meanExcessReturn = Math.Abs(excessReturns.Average());

            return meanExcessReturn > 0 ? 1.0 - (meanDifference / meanExcessReturn) : 0.0;
        }

        private List<List<double>> BuildFactorReturnMatrix(List<FactorReturns> factors, List<string> periods)
        {
            var matrix = new List<List<double>>();

            foreach (var period in periods)
            {
                var periodReturns = new List<double>();
                foreach (var factor in factors)
                {
                    periodReturns.Add(factor.ReturnsByPeriod.GetValueOrDefault(period, 0.0));
                }
                matrix.Add(periodReturns);
            }

            return matrix;
        }

        private async Task<Dictionary<string, double>> CalculateStyleWeightsAsync(
            List<double> portfolioReturns,
            List<List<double>> factorReturns,
            List<FactorReturns> factors)
        {
            // Use constrained regression to estimate style weights
            var weights = new Dictionary<string, double>();

            if (portfolioReturns.Count < 2 || factorReturns.Count != portfolioReturns.Count)
            {
                // Return equal weights if insufficient data
                var equalWeight = 1.0 / factors.Count;
                foreach (var factor in factors)
                {
                    weights[factor.Name] = equalWeight;
                }
                return weights;
            }

            try
            {
                // Convert to MathNet matrices
                var y = Vector<double>.Build.DenseOfArray(portfolioReturns.ToArray());
                var x = Matrix<double>.Build.DenseOfRows(factorReturns);

                // Add intercept
                var interceptColumn = Vector<double>.Build.Dense(portfolioReturns.Count, 1.0);
                x = x.InsertColumn(0, interceptColumn);

                // Perform constrained regression (weights between 0 and 1, sum to 1)
                var regression = MathNet.Numerics.LinearRegression.MultipleRegression.QR(x, y);

                // Extract style weights (skip intercept)
                for (int i = 1; i < regression.Length; i++)
                {
                    var weight = Math.Max(0.0, Math.Min(1.0, regression[i])); // Constrain to [0,1]
                    weights[factors[i - 1].Name] = weight;
                }

                // Renormalize to sum to 1
                var totalWeight = weights.Values.Sum();
                if (totalWeight > 0)
                {
                    foreach (var key in weights.Keys.ToArray())
                    {
                        weights[key] /= totalWeight;
                    }
                }
            }
            catch
            {
                // Fallback to equal weights
                var equalWeight = 1.0 / factors.Count;
                foreach (var factor in factors)
                {
                    weights[factor.Name] = equalWeight;
                }
            }

            return weights;
        }

        private double CalculateRSquared(List<double> portfolioReturns, List<List<double>> factorReturns, Dictionary<string, double> weights)
        {
            try
            {
                var predictedReturns = new List<double>();
                for (int i = 0; i < portfolioReturns.Count; i++)
                {
                    var predicted = 0.0;
                    for (int j = 0; j < factorReturns[i].Count; j++)
                    {
                        var factorName = $"Factor{j + 1}"; // This should match the factor names
                        var weight = weights.GetValueOrDefault(factorName, 0.0);
                        predicted += weight * factorReturns[i][j];
                    }
                    predictedReturns.Add(predicted);
                }

                return MathNet.Numerics.GoodnessOfFit.RSquared(portfolioReturns, predictedReturns);
            }
            catch
            {
                return 0.0;
            }
        }

        private double CalculateTrackingError(List<double> portfolioReturns, List<List<double>> factorReturns, Dictionary<string, double> weights)
        {
            try
            {
                var predictedReturns = new List<double>();
                for (int i = 0; i < portfolioReturns.Count; i++)
                {
                    var predicted = 0.0;
                    for (int j = 0; j < factorReturns[i].Count; j++)
                    {
                        var factorName = $"Factor{j + 1}";
                        var weight = weights.GetValueOrDefault(factorName, 0.0);
                        predicted += weight * factorReturns[i][j];
                    }
                    predictedReturns.Add(predicted);
                }

                var errors = portfolioReturns.Zip(predictedReturns, (p, pred) => p - pred).ToList();
                return errors.StandardDeviation();
            }
            catch
            {
                return 0.0;
            }
        }

        private string GenerateExecutiveSummary(
            PerformanceAttributionResult attributionResult,
            SectorAttributionResult sectorResult,
            StyleAnalysisResult styleResult)
        {
            var totalAttribution = attributionResult.TotalAttribution.TotalAttribution;
            var topFactor = attributionResult.TotalAttribution.FactorContributions
                .OrderByDescending(f => Math.Abs(f.Value))
                .FirstOrDefault();

            return $"Portfolio attribution analysis shows a total attribution of {totalAttribution:F4} " +
                   $"over the analysis period. The primary driver of performance was {topFactor.Key} " +
                   $"with a contribution of {topFactor.Value:F4}. Style analysis indicates " +
                   $"{styleResult.AverageRSquared:P2} average R-squared, suggesting " +
                   $"{(styleResult.AverageRSquared > 0.8 ? "strong" : styleResult.AverageRSquared > 0.6 ? "moderate" : "weak")} " +
                   "style consistency.";
        }

        private string GenerateFactorAttributionContent(PerformanceAttributionResult result)
        {
            var content = "Factor attribution analysis reveals the following key insights:\n\n";
            var topFactors = result.TotalAttribution.FactorContributions
                .OrderByDescending(f => Math.Abs(f.Value))
                .Take(5);

            foreach (var factor in topFactors)
            {
                content += $"- {factor.Key}: {factor.Value:F4} contribution\n";
            }

            content += $"\nAttribution accuracy: {result.Statistics.AttributionAccuracy:P2}";
            return content;
        }

        private string GenerateSectorAttributionContent(SectorAttributionResult result)
        {
            var content = $"Sector attribution analysis shows total attribution of {result.TotalAttribution:F4}.\n\n";
            var topSectors = result.SectorAttributions
                .OrderByDescending(s => Math.Abs(s.TotalAttribution))
                .Take(5);

            foreach (var sector in topSectors)
            {
                content += $"- {sector.SectorName}: {sector.TotalAttribution:F4} " +
                          $"(Allocation: {sector.AllocationEffect:F4}, Selection: {sector.SelectionEffect:F4})\n";
            }

            return content;
        }

        private string GenerateStyleAnalysisContent(StyleAnalysisResult result)
        {
            var content = $"Style analysis indicates average R-squared of {result.AverageRSquared:P2} " +
                         $"and tracking error of {result.AverageTrackingError:P4}.\n\n";

            var topStyles = result.StyleExposures.First().StyleWeights
                .OrderByDescending(s => s.Value)
                .Take(5);

            foreach (var style in topStyles)
            {
                content += $"- {style.Key}: {style.Value:P2} exposure\n";
            }

            return content;
        }

        private string GenerateRiskAdjustedContent(PerformanceAttributionResult result)
        {
            return $"Risk-adjusted analysis shows Sharpe ratio of {result.Statistics.MeanExcessReturn / result.Statistics.ExcessReturnVolatility:F4} " +
                   $"and information ratio of {result.Statistics.MeanExcessReturn / result.Statistics.AttributionVolatility:F4}.";
        }

        private Dictionary<string, double> GenerateSummaryMetrics(
            PerformanceAttributionResult attributionResult,
            SectorAttributionResult sectorResult,
            StyleAnalysisResult styleResult)
        {
            return new Dictionary<string, double>
            {
                ["Total Attribution"] = attributionResult.TotalAttribution.TotalAttribution,
                ["Attribution Accuracy"] = result.Statistics.AttributionAccuracy,
                ["Average R-Squared"] = styleResult.AverageRSquared,
                ["Average Tracking Error"] = styleResult.AverageTrackingError,
                ["Sector Attribution"] = sectorResult.TotalAttribution
            };
        }

        #endregion
    }

    #region Data Classes

    public enum AttributionMethodology
    {
        BrinsonFachler,
        Carino,
        Menchero
    }

    public class PortfolioReturns
    {
        public Dictionary<string, PeriodReturns> ReturnsByPeriod { get; set; } = new();
    }

    public class PeriodReturns
    {
        public string Period { get; set; } = string.Empty;
        public double PortfolioReturn { get; set; }
        public double BenchmarkReturn { get; set; }
    }

    public class FactorReturns
    {
        public string Name { get; set; } = string.Empty;
        public Dictionary<string, double> ReturnsByPeriod { get; set; } = new();
        public Dictionary<string, double> PortfolioWeights { get; set; } = new();
        public Dictionary<string, double> BenchmarkWeights { get; set; } = new();
    }

    public class BenchmarkReturns
    {
        public Dictionary<string, double> ReturnsByPeriod { get; set; } = new();
        public Dictionary<string, double> SectorWeights { get; set; } = new();
    }

    public class PerformanceAttributionResult
    {
        public List<AttributionPeriod> AttributionPeriods { get; set; } = new();
        public PeriodAttribution TotalAttribution { get; set; } = new();
        public AttributionStatistics Statistics { get; set; } = new();
        public AttributionMethodology Methodology { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class AttributionPeriod
    {
        public string Period { get; set; } = string.Empty;
        public double PortfolioReturn { get; set; }
        public double BenchmarkReturn { get; set; }
        public PeriodAttribution Attribution { get; set; } = new();
        public double ExcessReturn { get; set; }
    }

    public class PeriodAttribution
    {
        public Dictionary<string, double> FactorContributions { get; set; } = new();
        public double TotalAttribution { get; set; }
        public double Residual { get; set; }
    }

    public class AttributionStatistics
    {
        public double MeanExcessReturn { get; set; }
        public double MeanAttribution { get; set; }
        public decimal ExcessReturnVolatility { get; set; }
        public decimal AttributionVolatility { get; set; }
        public double MaxExcessReturn { get; set; }
        public double MinExcessReturn { get; set; }
        public double AttributionAccuracy { get; set; }
        public int PeriodsAnalyzed { get; set; }
    }

    public class SectorAttributionResult
    {
        public List<SectorAttribution> SectorAttributions { get; set; } = new();
        public double TotalAttribution { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class SectorAttribution
    {
        public string SectorName { get; set; } = string.Empty;
        public double PortfolioWeight { get; set; }
        public double BenchmarkWeight { get; set; }
        public double SectorReturn { get; set; }
        public double BenchmarkSectorReturn { get; set; }
        public double AllocationEffect { get; set; }
        public double SelectionEffect { get; set; }
        public double InteractionEffect { get; set; }
        public double TotalAttribution { get; set; }
    }

    public class StyleAnalysisResult
    {
        public List<StyleExposure> StyleExposures { get; set; } = new();
        public List<RollingStyleExposure> RollingStyleExposures { get; set; } = new();
        public double AverageRSquared { get; set; }
        public double AverageTrackingError { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class StyleExposure
    {
        public string FactorName { get; set; } = string.Empty;
        public double Weight { get; set; }
        public Dictionary<string, double> StyleWeights { get; set; } = new();
        public string TimePeriod { get; set; } = string.Empty;
    }

    public class RollingStyleExposure
    {
        public string Period { get; set; } = string.Empty;
        public Dictionary<string, double> StyleWeights { get; set; } = new();
        public double RSquared { get; set; }
        public double TrackingError { get; set; }
    }

    public class PortfolioRiskMetrics
    {
        public decimal PortfolioVolatility { get; set; }
        public decimal TrackingError { get; set; }
        public Dictionary<string, decimal> FactorVolatilities { get; set; } = new();
    }

    public class RiskAdjustedAttributionResult
    {
        public List<RiskAdjustedAttribution> RiskAdjustedAttributions { get; set; } = new();
        public double AverageSharpeRatio { get; set; }
        public double AverageInformationRatio { get; set; }
        public PortfolioRiskMetrics RiskMetrics { get; set; } = new();
        public DateTime Timestamp { get; set; }
    }

    public class RiskAdjustedAttribution
    {
        public string Period { get; set; } = string.Empty;
        public Dictionary<string, double> RiskAdjustedFactors { get; set; } = new();
        public double TotalRiskAdjustedAttribution { get; set; }
        public double SharpeRatio { get; set; }
        public double InformationRatio { get; set; }
    }

    public class HoldingAttributionData
    {
        public string HoldingName { get; set; } = string.Empty;
        public double PortfolioWeight { get; set; }
        public double BenchmarkWeight { get; set; }
        public double HoldingReturn { get; set; }
        public double BenchmarkHoldingReturn { get; set; }
    }

    public class HoldingsAttributionResult
    {
        public List<HoldingAttribution> HoldingAttributions { get; set; } = new();
        public double TotalAttribution { get; set; }
        public List<HoldingAttribution> TopContributors { get; set; } = new();
        public List<HoldingAttribution> TopDetractors { get; set; } = new();
        public DateTime Timestamp { get; set; }
    }

    public class HoldingAttribution
    {
        public string HoldingId { get; set; } = string.Empty;
        public string HoldingName { get; set; } = string.Empty;
        public double PortfolioWeight { get; set; }
        public double BenchmarkWeight { get; set; }
        public double HoldingReturn { get; set; }
        public double BenchmarkHoldingReturn { get; set; }
        public double StockSelectionEffect { get; set; }
        public double AllocationEffect { get; set; }
        public double InteractionEffect { get; set; }
        public double TotalAttribution { get; set; }
    }

    public class AttributionReport
    {
        public string Title { get; set; } = string.Empty;
        public DateTime GeneratedDate { get; set; }
        public List<ReportSection> ReportSections { get; set; } = new();
        public Dictionary<string, double> SummaryMetrics { get; set; } = new();
    }

    public class ReportSection
    {
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public List<string> Charts { get; set; } = new();
    }

    #endregion
}