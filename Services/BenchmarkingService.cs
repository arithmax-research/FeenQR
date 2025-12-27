using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using QuantResearchAgent.Core;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.Optimization;

namespace QuantResearchAgent.Services
{
    /// <summary>
    /// Benchmarking service for custom benchmark creation and performance comparison
    /// </summary>
    public class BenchmarkingService
    {
        private readonly ILogger<BenchmarkingService> _logger;
        private readonly MarketDataService _marketDataService;
        private readonly StatisticalTestingService _statisticalTestingService;

        public BenchmarkingService(
            ILogger<BenchmarkingService> logger,
            MarketDataService marketDataService,
            StatisticalTestingService statisticalTestingService)
        {
            _logger = logger;
            _marketDataService = marketDataService;
            _statisticalTestingService = statisticalTestingService;
        }

        /// <summary>
        /// Create a custom benchmark portfolio
        /// </summary>
        public async Task<CustomBenchmark> CreateCustomBenchmarkAsync(
            BenchmarkSpecification specification,
            List<SecurityData> universe)
        {
            _logger.LogInformation($"Creating custom benchmark with {specification.Criteria.Count} criteria and {universe.Count} securities");

            try
            {
                // Filter universe based on criteria
                var filteredUniverse = await ApplyBenchmarkCriteriaAsync(specification.Criteria, universe);

                // Calculate weights based on methodology
                var weights = await CalculateBenchmarkWeightsAsync(filteredUniverse, specification.WeightingMethodology);

                // Create benchmark composition
                var composition = filteredUniverse.Select((security, index) => new BenchmarkHolding
                {
                    SecurityId = security.Symbol,
                    SecurityName = security.Name,
                    Weight = weights[index],
                    MarketCap = security.MarketCap ?? 0,
                    Sector = security.Sector,
                    Country = security.Country
                }).ToList();

                // Calculate benchmark statistics
                var statistics = await CalculateBenchmarkStatisticsAsync(composition, specification);

                return new CustomBenchmark
                {
                    Name = specification.Name,
                    Description = specification.Description,
                    Specification = specification,
                    Composition = composition,
                    Statistics = statistics,
                    CreatedDate = DateTime.UtcNow,
                    RebalancingFrequency = specification.RebalancingFrequency
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating custom benchmark");
                throw;
            }
        }

        /// <summary>
        /// Compare portfolio performance against multiple benchmarks
        /// </summary>
        public async Task<BenchmarkComparisonResult> CompareAgainstBenchmarksAsync(
            PortfolioReturns portfolioReturns,
            List<CustomBenchmark> benchmarks,
            ComparisonMetrics metrics)
        {
            _logger.LogInformation($"Comparing portfolio against {benchmarks.Count} benchmarks");

            try
            {
                var benchmarkComparisons = new List<BenchmarkComparison>();

                foreach (var benchmark in benchmarks)
                {
                    var comparison = await CompareAgainstBenchmarkAsync(portfolioReturns, benchmark, metrics);
                    benchmarkComparisons.Add(comparison);
                }

                // Find best fit benchmark
                var bestFit = benchmarkComparisons
                    .OrderBy(c => Math.Abs(c.TrackingError))
                    .FirstOrDefault();

                // Calculate relative performance rankings
                var rankings = CalculatePerformanceRankings(benchmarkComparisons);

                return new BenchmarkComparisonResult
                {
                    BenchmarkComparisons = benchmarkComparisons,
                    BestFitBenchmark = bestFit,
                    PerformanceRankings = rankings,
                    ComparisonMetrics = metrics,
                    Timestamp = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error comparing against benchmarks");
                throw;
            }
        }

        /// <summary>
        /// Perform benchmark replication analysis
        /// </summary>
        public async Task<BenchmarkReplicationResult> AnalyzeBenchmarkReplicationAsync(
            CustomBenchmark benchmark,
            List<SecurityData> availableSecurities,
            ReplicationConstraints constraints)
        {
            _logger.LogInformation($"Analyzing benchmark replication for {benchmark.Name}");

            try
            {
                // Optimize portfolio to replicate benchmark
                var replicationPortfolio = await OptimizeBenchmarkReplicationAsync(
                    benchmark,
                    availableSecurities,
                    constraints);

                // Calculate replication quality metrics
                var replicationQuality = await CalculateReplicationQualityAsync(
                    benchmark,
                    replicationPortfolio);

                // Analyze tracking error decomposition
                var trackingErrorDecomposition = await DecomposeTrackingErrorAsync(
                    benchmark,
                    replicationPortfolio);

                return new BenchmarkReplicationResult
                {
                    TargetBenchmark = benchmark,
                    ReplicationPortfolio = replicationPortfolio,
                    ReplicationQuality = replicationQuality,
                    TrackingErrorDecomposition = trackingErrorDecomposition,
                    Constraints = constraints,
                    Timestamp = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing benchmark replication");
                throw;
            }
        }

        /// <summary>
        /// Generate benchmark performance report
        /// </summary>
        public async Task<BenchmarkPerformanceReport> GeneratePerformanceReportAsync(
            CustomBenchmark benchmark,
            DateTime startDate,
            DateTime endDate,
            List<string> comparisonBenchmarks = null)
        {
            _logger.LogInformation($"Generating performance report for {benchmark.Name}");

            try
            {
                // Calculate benchmark returns
                var benchmarkReturns = await CalculateBenchmarkReturnsAsync(benchmark, startDate, endDate);

                // Calculate risk metrics
                var riskMetrics = await CalculateBenchmarkRiskMetricsAsync(benchmarkReturns);

                // Calculate factor exposures
                var factorExposures = await CalculateBenchmarkFactorExposuresAsync(benchmark);

                // Generate comparison data
                var comparisons = new List<BenchmarkComparisonData>();
                if (comparisonBenchmarks != null)
                {
                    foreach (var compBenchmark in comparisonBenchmarks)
                    {
                        var compReturns = await _marketDataService.GetHistoricalReturnsAsync(compBenchmark, startDate, endDate);
                        var comparison = new BenchmarkComparisonData
                        {
                            BenchmarkName = compBenchmark,
                            Returns = compReturns,
                            Correlation = CalculateCorrelation(benchmarkReturns, compReturns),
                            TrackingError = CalculateTrackingError(benchmarkReturns, compReturns)
                        };
                        comparisons.Add(comparison);
                    }
                }

                return new BenchmarkPerformanceReport
                {
                    Benchmark = benchmark,
                    ReportPeriod = new DateRange { StartDate = startDate, EndDate = endDate },
                    Returns = benchmarkReturns,
                    RiskMetrics = riskMetrics,
                    FactorExposures = factorExposures,
                    Comparisons = comparisons,
                    GeneratedDate = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating performance report");
                throw;
            }
        }

        /// <summary>
        /// Perform benchmark stress testing
        /// </summary>
        public async Task<BenchmarkStressTestResult> PerformStressTestAsync(
            CustomBenchmark benchmark,
            List<StressTestScenario> scenarios)
        {
            _logger.LogInformation($"Performing stress tests on benchmark {benchmark.Name}");

            try
            {
                var stressTestResults = new List<BenchmarkStressTest>();

                foreach (var scenario in scenarios)
                {
                    var stressedBenchmark = await ApplyStressScenarioToBenchmarkAsync(benchmark, scenario);
                    var impact = await CalculateStressImpactAsync(benchmark, stressedBenchmark, scenario);

                    stressTestResults.Add(new BenchmarkStressTest
                    {
                        Scenario = scenario,
                        StressedBenchmark = stressedBenchmark,
                        Impact = impact
                    });
                }

                // Calculate worst-case scenario
                var worstCase = stressTestResults
                    .OrderByDescending(r => Math.Abs(r.Impact.ReturnImpact))
                    .FirstOrDefault();

                return new BenchmarkStressTestResult
                {
                    Benchmark = benchmark,
                    StressTestResults = stressTestResults,
                    WorstCaseScenario = worstCase,
                    Timestamp = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing benchmark stress test");
                throw;
            }
        }

        /// <summary>
        /// Update benchmark composition based on rebalancing rules
        /// </summary>
        public async Task<RebalancingResult> RebalanceBenchmarkAsync(
            CustomBenchmark benchmark,
            List<SecurityData> currentUniverse,
            RebalancingRules rules)
        {
            _logger.LogInformation($"Rebalancing benchmark {benchmark.Name}");

            try
            {
                // Check if rebalancing is needed
                var needsRebalancing = await CheckRebalancingTriggerAsync(benchmark, rules);

                if (!needsRebalancing)
                {
                    return new RebalancingResult
                    {
                        Benchmark = benchmark,
                        RebalancingNeeded = false,
                        Reason = "Rebalancing not triggered",
                        Timestamp = DateTime.UtcNow
                    };
                }

                // Calculate new composition
                var newComposition = await CalculateRebalancedCompositionAsync(
                    benchmark,
                    currentUniverse,
                    rules);

                // Calculate turnover
                var turnover = CalculatePortfolioTurnover(benchmark.Composition, newComposition);

                // Create updated benchmark
                var updatedBenchmark = new CustomBenchmark
                {
                    Name = benchmark.Name,
                    Description = benchmark.Description,
                    Specification = benchmark.Specification,
                    Composition = newComposition,
                    Statistics = await CalculateBenchmarkStatisticsAsync(newComposition, benchmark.Specification),
                    CreatedDate = DateTime.UtcNow,
                    RebalancingFrequency = benchmark.RebalancingFrequency
                };

                return new RebalancingResult
                {
                    Benchmark = updatedBenchmark,
                    RebalancingNeeded = true,
                    Reason = "Scheduled rebalancing",
                    Turnover = turnover,
                    OldComposition = benchmark.Composition,
                    NewComposition = newComposition,
                    Timestamp = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rebalancing benchmark");
                throw;
            }
        }

        #region Helper Methods

        private async Task<List<SecurityData>> ApplyBenchmarkCriteriaAsync(
            List<BenchmarkCriterion> criteria,
            List<SecurityData> universe)
        {
            var filtered = universe.AsEnumerable();

            foreach (var criterion in criteria)
            {
                filtered = await ApplyCriterionAsync(filtered, criterion);
            }

            return filtered.ToList();
        }

        private async Task<IEnumerable<SecurityData>> ApplyCriterionAsync(
            IEnumerable<SecurityData> securities,
            BenchmarkCriterion criterion)
        {
            switch (criterion.Type)
            {
                case CriterionType.MarketCapRange:
                    return securities.Where(s =>
                        s.MarketCap >= criterion.MinValue && s.MarketCap <= criterion.MaxValue);

                case CriterionType.SectorWeight:
                    // Group by sector and apply weight constraints
                    var sectorGroups = securities.GroupBy(s => s.Sector);
                    var filtered = new List<SecurityData>();

                    foreach (var group in sectorGroups)
                    {
                        var sectorSecurities = group.ToList();
                        var targetCount = (int)(sectorSecurities.Count * criterion.TargetWeight);
                        filtered.AddRange(sectorSecurities.Take(targetCount));
                    }

                    return filtered;

                case CriterionType.CountryWeight:
                    var countryGroups = securities.GroupBy(s => s.Country);
                    var countryFiltered = new List<SecurityData>();

                    foreach (var group in countryGroups)
                    {
                        var countrySecurities = group.ToList();
                        var targetCount = (int)(countrySecurities.Count * criterion.TargetWeight);
                        countryFiltered.AddRange(countrySecurities.Take(targetCount));
                    }

                    return countryFiltered;

                case CriterionType.Liquidity:
                    return securities.Where(s => s.AverageVolume >= criterion.MinValue);

                case CriterionType.CustomFilter:
                    // Apply custom filtering logic
                    return await ApplyCustomFilterAsync(securities, criterion);

                default:
                    return securities;
            }
        }

        private async Task<IEnumerable<SecurityData>> ApplyCustomFilterAsync(
            IEnumerable<SecurityData> securities,
            BenchmarkCriterion criterion)
        {
            // Implement custom filtering logic based on criterion parameters
            // This could include technical indicators, fundamental ratios, etc.
            return securities; // Placeholder
        }

        private async Task<List<double>> CalculateBenchmarkWeightsAsync(
            List<SecurityData> securities,
            WeightingMethodology methodology)
        {
            switch (methodology.Type)
            {
                case WeightingType.EqualWeight:
                    var equalWeight = 1.0 / securities.Count;
                    return Enumerable.Repeat(equalWeight, securities.Count).ToList();

                case WeightingType.MarketCapWeight:
                    var totalMarketCap = securities.Sum(s => s.MarketCap ?? 0);
                    return securities.Select(s => (s.MarketCap ?? 0) / totalMarketCap).ToList();

                case WeightingType.FundamentalWeight:
                    return await CalculateFundamentalWeightsAsync(securities, methodology);

                case WeightingType.OptimizedWeight:
                    return await CalculateOptimizedWeightsAsync(securities, methodology);

                default:
                    var defaultWeight = 1.0 / securities.Count;
                    return Enumerable.Repeat(defaultWeight, securities.Count).ToList();
            }
        }

        private async Task<List<double>> CalculateFundamentalWeightsAsync(
            List<SecurityData> securities,
            WeightingMethodology methodology)
        {
            var weights = new List<double>();
            var totalScore = 0.0;

            foreach (var security in securities)
            {
                var score = CalculateFundamentalScore(security, methodology.FundamentalFactors);
                weights.Add(score);
                totalScore += score;
            }

            // Normalize weights
            for (int i = 0; i < weights.Count; i++)
            {
                weights[i] /= totalScore;
            }

            return weights;
        }

        private double CalculateFundamentalScore(SecurityData security, List<FundamentalFactor> factors)
        {
            var score = 0.0;

            foreach (var factor in factors)
            {
                var value = GetFundamentalValue(security, factor);
                if (value.HasValue)
                {
                    var normalizedValue = NormalizeValue(value.Value, factor);
                    score += normalizedValue * factor.Weight;
                }
            }

            return score;
        }

        private double? GetFundamentalValue(SecurityData security, FundamentalFactor factor)
        {
            // Extract fundamental value based on factor type
            switch (factor.Type)
            {
                case FundamentalType.Revenue:
                    return security.Revenue;
                case FundamentalType.Earnings:
                    return security.Earnings;
                case FundamentalType.BookValue:
                    return security.BookValue;
                case FundamentalType.DividendYield:
                    return security.DividendYield;
                default:
                    return null;
            }
        }

        private double NormalizeValue(double value, FundamentalFactor factor)
        {
            // Normalize value to 0-1 scale
            if (factor.TargetValue.HasValue)
            {
                return 1.0 / (1.0 + Math.Abs(value - factor.TargetValue.Value));
            }

            // Default normalization
            return Math.Max(0, Math.Min(1, value / 1000000000)); // Scale by billion
        }

        private async Task<List<double>> CalculateOptimizedWeightsAsync(
            List<SecurityData> securities,
            WeightingMethodology methodology)
        {
            // Implement portfolio optimization for benchmark weights
            // This would use mean-variance optimization or other techniques
            var equalWeight = 1.0 / securities.Count;
            return Enumerable.Repeat(equalWeight, securities.Count).ToList(); // Placeholder
        }

        private async Task<BenchmarkStatistics> CalculateBenchmarkStatisticsAsync(
            List<BenchmarkHolding> composition,
            BenchmarkSpecification specification)
        {
            var totalMarketCap = composition.Sum(h => h.MarketCap);
            var sectorWeights = composition.GroupBy(h => h.Sector)
                .ToDictionary(g => g.Key, g => g.Sum(h => h.Weight));
            var countryWeights = composition.GroupBy(h => h.Country)
                .ToDictionary(g => g.Key, g => g.Sum(h => h.Weight));

            return new BenchmarkStatistics
            {
                NumberOfHoldings = composition.Count,
                TotalMarketCap = totalMarketCap,
                AverageMarketCap = totalMarketCap / composition.Count,
                SectorDiversification = sectorWeights.Count,
                CountryDiversification = countryWeights.Count,
                SectorWeights = sectorWeights,
                CountryWeights = countryWeights,
                LargestHoldingWeight = composition.Max(h => h.Weight),
                SmallestHoldingWeight = composition.Min(h => h.Weight)
            };
        }

        private async Task<BenchmarkComparison> CompareAgainstBenchmarkAsync(
            PortfolioReturns portfolioReturns,
            CustomBenchmark benchmark,
            ComparisonMetrics metrics)
        {
            // Calculate benchmark returns for the same periods
            var benchmarkReturns = await CalculateBenchmarkReturnsAsync(
                benchmark,
                DateTime.MinValue,
                DateTime.MaxValue); // Use portfolio date range

            // Calculate comparison metrics
            var correlation = CalculateCorrelation(portfolioReturns.ReturnsByPeriod.Values.Select(r => r.PortfolioReturn).ToList(),
                benchmarkReturns);

            var trackingError = CalculateTrackingError(portfolioReturns.ReturnsByPeriod.Values.Select(r => r.PortfolioReturn).ToList(),
                benchmarkReturns);

            var informationRatio = trackingError > 0 ? (portfolioReturns.ReturnsByPeriod.Values.Average(r => r.PortfolioReturn - r.BenchmarkReturn)) / trackingError : 0;

            var beta = await CalculateBenchmarkBetaAsync(portfolioReturns, benchmark);

            return new BenchmarkComparison
            {
                Benchmark = benchmark,
                Correlation = correlation,
                TrackingError = trackingError,
                InformationRatio = informationRatio,
                Beta = beta,
                ActiveReturn = portfolioReturns.ReturnsByPeriod.Values.Average(r => r.PortfolioReturn - r.BenchmarkReturn),
                ActiveRisk = trackingError
            };
        }

        private double CalculateCorrelation(List<double> portfolioReturns, List<double> benchmarkReturns)
        {
            if (portfolioReturns.Count != benchmarkReturns.Count || portfolioReturns.Count < 2)
                return 0.0;

            return MathNet.Numerics.Statistics.Correlation.Pearson(portfolioReturns, benchmarkReturns);
        }

        private double CalculateTrackingError(List<double> portfolioReturns, List<double> benchmarkReturns)
        {
            if (portfolioReturns.Count != benchmarkReturns.Count)
                return double.NaN;

            var differences = portfolioReturns.Zip(benchmarkReturns, (p, b) => p - b).ToList();
            return differences.StandardDeviation();
        }

        private async Task<double> CalculateBenchmarkBetaAsync(PortfolioReturns portfolioReturns, CustomBenchmark benchmark)
        {
            // Calculate beta relative to benchmark
            var benchmarkReturns = await CalculateBenchmarkReturnsAsync(benchmark, DateTime.MinValue, DateTime.MaxValue);
            var portfolioReturnList = portfolioReturns.ReturnsByPeriod.Values.Select(r => r.PortfolioReturn).ToList();

            if (portfolioReturnList.Count != benchmarkReturns.Count || portfolioReturnList.Count < 2)
                return 1.0;

            return MathNet.Numerics.Statistics.Correlation.Pearson(portfolioReturnList, benchmarkReturns);
        }

        private Dictionary<string, int> CalculatePerformanceRankings(List<BenchmarkComparison> comparisons)
        {
            var rankings = new Dictionary<string, int>();

            // Rank by different metrics
            var byTrackingError = comparisons.OrderBy(c => c.TrackingError).ToList();
            var byInformationRatio = comparisons.OrderByDescending(c => c.InformationRatio).ToList();

            for (int i = 0; i < comparisons.Count; i++)
            {
                var benchmarkName = comparisons[i].Benchmark.Name;
                rankings[$"{benchmarkName}_TrackingError"] = byTrackingError.FindIndex(c => c.Benchmark.Name == benchmarkName) + 1;
                rankings[$"{benchmarkName}_InformationRatio"] = byInformationRatio.FindIndex(c => c.Benchmark.Name == benchmarkName) + 1;
            }

            return rankings;
        }

        private async Task<List<double>> CalculateBenchmarkReturnsAsync(CustomBenchmark benchmark, DateTime startDate, DateTime endDate)
        {
            // Calculate weighted returns for benchmark
            var returns = new List<double>();

            // This would aggregate returns from individual holdings
            // For now, return placeholder
            return returns;
        }

        private async Task<BenchmarkRiskMetrics> CalculateBenchmarkRiskMetricsAsync(List<double> returns)
        {
            if (returns.Count < 2)
            {
                return new BenchmarkRiskMetrics
                {
                    Volatility = 0,
                    SharpeRatio = 0,
                    MaximumDrawdown = 0,
                    ValueAtRisk = 0
                };
            }

            var volatility = returns.StandardDeviation();
            var meanReturn = returns.Average();
            var sharpeRatio = volatility > 0 ? meanReturn / volatility : 0;

            // Calculate maximum drawdown
            var maxDrawdown = CalculateMaximumDrawdown(returns);

            // Calculate VaR (simplified)
            var valueAtRisk = meanReturn - 1.645 * volatility; // 5% VaR

            return new BenchmarkRiskMetrics
            {
                Volatility = volatility,
                SharpeRatio = sharpeRatio,
                MaximumDrawdown = maxDrawdown,
                ValueAtRisk = valueAtRisk
            };
        }

        private double CalculateMaximumDrawdown(List<double> returns)
        {
            var cumulative = new List<double> { 1.0 };
            for (int i = 0; i < returns.Count; i++)
            {
                cumulative.Add(cumulative.Last() * (1 + returns[i]));
            }

            var peak = cumulative[0];
            var maxDrawdown = 0.0;

            foreach (var value in cumulative)
            {
                if (value > peak)
                    peak = value;

                var drawdown = (peak - value) / peak;
                if (drawdown > maxDrawdown)
                    maxDrawdown = drawdown;
            }

            return maxDrawdown;
        }

        private async Task<Dictionary<string, double>> CalculateBenchmarkFactorExposuresAsync(CustomBenchmark benchmark)
        {
            // Calculate factor exposures for benchmark
            // This would use factor model regression
            return new Dictionary<string, double>
            {
                ["Market"] = 1.0,
                ["Size"] = 0.0,
                ["Value"] = 0.0,
                ["Momentum"] = 0.0
            }; // Placeholder
        }

        private async Task<ReplicationPortfolio> OptimizeBenchmarkReplicationAsync(
            CustomBenchmark benchmark,
            List<SecurityData> availableSecurities,
            ReplicationConstraints constraints)
        {
            // Implement optimization to replicate benchmark
            // This would use quadratic programming or other optimization techniques
            return new ReplicationPortfolio(); // Placeholder
        }

        private async Task<ReplicationQuality> CalculateReplicationQualityAsync(
            CustomBenchmark benchmark,
            ReplicationPortfolio replicationPortfolio)
        {
            return new ReplicationQuality(); // Placeholder
        }

        private async Task<TrackingErrorDecomposition> DecomposeTrackingErrorAsync(
            CustomBenchmark benchmark,
            ReplicationPortfolio replicationPortfolio)
        {
            return new TrackingErrorDecomposition(); // Placeholder
        }

        private async Task<CustomBenchmark> ApplyStressScenarioToBenchmarkAsync(
            CustomBenchmark benchmark,
            StressTestScenario scenario)
        {
            // Apply stress scenario to benchmark holdings
            return benchmark; // Placeholder
        }

        private async Task<StressImpact> CalculateStressImpactAsync(
            CustomBenchmark original,
            CustomBenchmark stressed,
            StressTestScenario scenario)
        {
            return new StressImpact(); // Placeholder
        }

        private async Task<bool> CheckRebalancingTriggerAsync(CustomBenchmark benchmark, RebalancingRules rules)
        {
            // Check if rebalancing is needed based on rules
            return true; // Placeholder - implement actual logic
        }

        private async Task<List<BenchmarkHolding>> CalculateRebalancedCompositionAsync(
            CustomBenchmark benchmark,
            List<SecurityData> currentUniverse,
            RebalancingRules rules)
        {
            // Calculate new benchmark composition
            return benchmark.Composition; // Placeholder
        }

        private double CalculatePortfolioTurnover(List<BenchmarkHolding> oldComposition, List<BenchmarkHolding> newComposition)
        {
            // Calculate turnover as sum of absolute weight changes
            var turnover = 0.0;

            var oldWeights = oldComposition.ToDictionary(h => h.SecurityId, h => h.Weight);
            var newWeights = newComposition.ToDictionary(h => h.SecurityId, h => h.Weight);

            var allSecurities = oldWeights.Keys.Union(newWeights.Keys);

            foreach (var security in allSecurities)
            {
                var oldWeight = oldWeights.GetValueOrDefault(security, 0.0);
                var newWeight = newWeights.GetValueOrDefault(security, 0.0);
                turnover += Math.Abs(newWeight - oldWeight);
            }

            return turnover / 2.0; // Divide by 2 because each trade affects two sides
        }

        #endregion
    }

    #region Data Classes

    public class BenchmarkSpecification
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<BenchmarkCriterion> Criteria { get; set; } = new();
        public WeightingMethodology WeightingMethodology { get; set; } = new();
        public RebalancingFrequency RebalancingFrequency { get; set; } = RebalancingFrequency.Quarterly;
    }

    public enum CriterionType
    {
        MarketCapRange,
        SectorWeight,
        CountryWeight,
        Liquidity,
        CustomFilter
    }

    public class BenchmarkCriterion
    {
        public CriterionType Type { get; set; }
        public double? MinValue { get; set; }
        public double? MaxValue { get; set; }
        public double? TargetWeight { get; set; }
        public Dictionary<string, object> Parameters { get; set; } = new();
    }

    public enum WeightingType
    {
        EqualWeight,
        MarketCapWeight,
        FundamentalWeight,
        OptimizedWeight
    }

    public class WeightingMethodology
    {
        public WeightingType Type { get; set; } = WeightingType.EqualWeight;
        public List<FundamentalFactor> FundamentalFactors { get; set; } = new();
        public OptimizationConstraints OptimizationConstraints { get; set; } = new();
    }

    public enum FundamentalType
    {
        Revenue,
        Earnings,
        BookValue,
        DividendYield
    }

    public class FundamentalFactor
    {
        public FundamentalType Type { get; set; }
        public double Weight { get; set; }
        public double? TargetValue { get; set; }
    }

    public class OptimizationConstraints
    {
        public double MinWeight { get; set; } = 0.0;
        public double MaxWeight { get; set; } = 1.0;
        public int MaxHoldings { get; set; } = 100;
    }

    public enum RebalancingFrequency
    {
        Daily,
        Weekly,
        Monthly,
        Quarterly,
        SemiAnnual,
        Annual
    }

    public class SecurityData
    {
        public string Symbol { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal? MarketCap { get; set; }
        public string Sector { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public decimal? AverageVolume { get; set; }
        public decimal? Revenue { get; set; }
        public decimal? Earnings { get; set; }
        public decimal? BookValue { get; set; }
        public decimal? DividendYield { get; set; }
    }

    public class CustomBenchmark
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public BenchmarkSpecification Specification { get; set; } = new();
        public List<BenchmarkHolding> Composition { get; set; } = new();
        public BenchmarkStatistics Statistics { get; set; } = new();
        public DateTime CreatedDate { get; set; }
        public RebalancingFrequency RebalancingFrequency { get; set; }
    }

    public class BenchmarkHolding
    {
        public string SecurityId { get; set; } = string.Empty;
        public string SecurityName { get; set; } = string.Empty;
        public double Weight { get; set; }
        public decimal MarketCap { get; set; }
        public string Sector { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
    }

    public class BenchmarkStatistics
    {
        public int NumberOfHoldings { get; set; }
        public decimal TotalMarketCap { get; set; }
        public decimal AverageMarketCap { get; set; }
        public int SectorDiversification { get; set; }
        public int CountryDiversification { get; set; }
        public Dictionary<string, double> SectorWeights { get; set; } = new();
        public Dictionary<string, double> CountryWeights { get; set; } = new();
        public double LargestHoldingWeight { get; set; }
        public double SmallestHoldingWeight { get; set; }
    }

    public class ComparisonMetrics
    {
        public bool IncludeCorrelation { get; set; } = true;
        public bool IncludeTrackingError { get; set; } = true;
        public bool IncludeInformationRatio { get; set; } = true;
        public bool IncludeBeta { get; set; } = true;
        public int MinimumPeriods { get; set; } = 12;
    }

    public class BenchmarkComparisonResult
    {
        public List<BenchmarkComparison> BenchmarkComparisons { get; set; } = new();
        public BenchmarkComparison BestFitBenchmark { get; set; } = new();
        public Dictionary<string, int> PerformanceRankings { get; set; } = new();
        public ComparisonMetrics ComparisonMetrics { get; set; } = new();
        public DateTime Timestamp { get; set; }
    }

    public class BenchmarkComparison
    {
        public CustomBenchmark Benchmark { get; set; } = new();
        public double Correlation { get; set; }
        public double TrackingError { get; set; }
        public double InformationRatio { get; set; }
        public double Beta { get; set; }
        public double ActiveReturn { get; set; }
        public double ActiveRisk { get; set; }
    }

    public class ReplicationConstraints
    {
        public int MaxHoldings { get; set; } = 50;
        public double MinWeight { get; set; } = 0.001;
        public double MaxWeight { get; set; } = 0.05;
        public List<string> AllowedSectors { get; set; } = new();
        public List<string> ExcludedSecurities { get; set; } = new();
    }

    public class BenchmarkReplicationResult
    {
        public CustomBenchmark TargetBenchmark { get; set; } = new();
        public ReplicationPortfolio ReplicationPortfolio { get; set; } = new();
        public ReplicationQuality ReplicationQuality { get; set; } = new();
        public TrackingErrorDecomposition TrackingErrorDecomposition { get; set; } = new();
        public ReplicationConstraints Constraints { get; set; } = new();
        public DateTime Timestamp { get; set; }
    }

    public class ReplicationPortfolio
    {
        public List<ReplicationHolding> Holdings { get; set; } = new();
        public double TotalWeight { get; set; }
        public double TransactionCost { get; set; }
    }

    public class ReplicationHolding
    {
        public string SecurityId { get; set; } = string.Empty;
        public double Weight { get; set; }
        public double TargetWeight { get; set; }
    }

    public class ReplicationQuality
    {
        public double RSquared { get; set; }
        public double TrackingError { get; set; }
        public double Turnover { get; set; }
        public int NumberOfHoldings { get; set; }
    }

    public class TrackingErrorDecomposition
    {
        public double SecuritySelectionError { get; set; }
        public double FactorExposureError { get; set; }
        public double InteractionError { get; set; }
        public double TotalTrackingError { get; set; }
    }

    public class BenchmarkPerformanceReport
    {
        public CustomBenchmark Benchmark { get; set; } = new();
        public DateRange ReportPeriod { get; set; } = new();
        public List<double> Returns { get; set; } = new();
        public BenchmarkRiskMetrics RiskMetrics { get; set; } = new();
        public Dictionary<string, double> FactorExposures { get; set; } = new();
        public List<BenchmarkComparisonData> Comparisons { get; set; } = new();
        public DateTime GeneratedDate { get; set; }
    }

    public class DateRange
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
    }

    public class BenchmarkRiskMetrics
    {
        public double Volatility { get; set; }
        public double SharpeRatio { get; set; }
        public double MaximumDrawdown { get; set; }
        public double ValueAtRisk { get; set; }
    }

    public class BenchmarkComparisonData
    {
        public string BenchmarkName { get; set; } = string.Empty;
        public List<double> Returns { get; set; } = new();
        public double Correlation { get; set; }
        public double TrackingError { get; set; }
    }

    public class StressTestScenario
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public Dictionary<string, double> StressMultipliers { get; set; } = new();
    }

    public class BenchmarkStressTestResult
    {
        public CustomBenchmark Benchmark { get; set; } = new();
        public List<BenchmarkStressTest> StressTestResults { get; set; } = new();
        public BenchmarkStressTest WorstCaseScenario { get; set; } = new();
        public DateTime Timestamp { get; set; }
    }

    public class BenchmarkStressTest
    {
        public StressTestScenario Scenario { get; set; } = new();
        public CustomBenchmark StressedBenchmark { get; set; } = new();
        public StressImpact Impact { get; set; } = new();
    }

    public class StressImpact
    {
        public double ReturnImpact { get; set; }
        public double VolatilityImpact { get; set; }
        public double DrawdownImpact { get; set; }
    }

    public class RebalancingRules
    {
        public RebalancingFrequency Frequency { get; set; } = RebalancingFrequency.Quarterly;
        public double DriftThreshold { get; set; } = 0.05; // 5% drift triggers rebalancing
        public List<string> RebalancingDates { get; set; } = new();
    }

    public class RebalancingResult
    {
        public CustomBenchmark Benchmark { get; set; } = new();
        public bool RebalancingNeeded { get; set; }
        public string Reason { get; set; } = string.Empty;
        public double Turnover { get; set; }
        public List<BenchmarkHolding> OldComposition { get; set; } = new();
        public List<BenchmarkHolding> NewComposition { get; set; } = new();
        public DateTime Timestamp { get; set; }
    }

    #endregion

        #region Plugin Support Methods

        /// <summary>
        /// Generate comprehensive benchmark analysis report
        /// </summary>
        public async Task<BenchmarkPerformanceReport> GenerateBenchmarkReportAsync(
            BenchmarkData benchmarkData,
            PortfolioData portfolioData,
            string reportType)
        {
            _logger.LogInformation($"Generating {reportType} benchmark report for {benchmarkData.Name}");

            try
            {
                // Create benchmark returns from holdings
                var benchmarkReturns = new BenchmarkReturns
                {
                    Name = benchmarkData.Name,
                    Returns = new List<double> { 0.0 }, // Placeholder
                    Dates = new List<DateTime> { DateTime.Now }
                };

                // Create portfolio returns from holdings
                var portfolioReturns = new PortfolioReturns
                {
                    Returns = new List<double> { 0.0 }, // Placeholder
                    Dates = new List<DateTime> { DateTime.Now }
                };

                // Generate comparison
                var comparison = await CompareToBenchmarksAsync(
                    portfolioReturns,
                    new List<BenchmarkReturns> { benchmarkReturns },
                    0.02);

                return new BenchmarkPerformanceReport
                {
                    BenchmarkName = benchmarkData.Name,
                    ReportType = reportType,
                    GeneratedAt = DateTime.Now,
                    Summary = $"Report generated for {benchmarkData.Name} vs portfolio"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating benchmark report");
                throw;
            }
        }

        /// <summary>
        /// Monitor benchmark composition drift over time
        /// </summary>
        public async Task<string> MonitorBenchmarkDriftAsync(
            List<BenchmarkComposition> historicalCompositions,
            double driftThreshold)
        {
            _logger.LogInformation($"Monitoring benchmark drift with threshold {driftThreshold:P2}");

            try
            {
                if (historicalCompositions.Count < 2)
                {
                    return "Insufficient historical data for drift analysis";
                }

                // Calculate drift between consecutive compositions
                var driftAnalysis = new List<string>();
                for (int i = 1; i < historicalCompositions.Count; i++)
                {
                    var current = historicalCompositions[i];
                    var previous = historicalCompositions[i - 1];

                    var drift = CalculateCompositionDrift(previous, current);
                    if (drift > driftThreshold)
                    {
                        driftAnalysis.Add($"Drift detected on {current.Date:yyyy-MM-dd}: {drift:P2} (threshold: {driftThreshold:P2})");
                    }
                }

                return driftAnalysis.Any()
                    ? $"Benchmark drift analysis:\n{string.Join("\n", driftAnalysis)}"
                    : $"No significant drift detected (threshold: {driftThreshold:P2})";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error monitoring benchmark drift");
                throw;
            }
        }

        /// <summary>
        /// Create sector-specific benchmark
        /// </summary>
        public async Task<CustomBenchmark> CreateSectorBenchmarkAsync(
            string sectorName,
            SectorCriteria criteria,
            WeightingMethodology weightingMethodology)
        {
            _logger.LogInformation($"Creating {sectorName} sector benchmark");

            try
            {
                // Filter universe based on sector criteria
                var universe = await GetSectorUniverseAsync(criteria);

                // Create benchmark specification
                var specification = new BenchmarkSpecification
                {
                    Name = $"{sectorName} Sector Benchmark",
                    Description = $"Benchmark for {sectorName} sector",
                    Criteria = new List<BenchmarkCriterion>
                    {
                        new BenchmarkCriterion
                        {
                            Type = "Sector",
                            Value = sectorName,
                            Operator = "Equals"
                        }
                    },
                    WeightingMethodology = weightingMethodology
                };

                return await CreateCustomBenchmarkAsync(specification, universe);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating sector benchmark");
                throw;
            }
        }

        #endregion

        #region Helper Methods

        private double CalculateCompositionDrift(BenchmarkComposition previous, BenchmarkComposition current)
        {
            // Calculate weighted difference between compositions
            var previousWeights = previous.Holdings.ToDictionary(h => h.Symbol, h => h.Weight);
            var currentWeights = current.Holdings.ToDictionary(h => h.Symbol, h => h.Weight);

            var allSymbols = previousWeights.Keys.Union(currentWeights.Keys).ToList();
            var totalDrift = 0.0;

            foreach (var symbol in allSymbols)
            {
                var prevWeight = previousWeights.GetValueOrDefault(symbol, 0.0);
                var currWeight = currentWeights.GetValueOrDefault(symbol, 0.0);
                totalDrift += Math.Abs(prevWeight - currWeight);
            }

            return totalDrift / 2.0; // Normalize to 0-1 range
        }

        private async Task<List<SecurityData>> GetSectorUniverseAsync(SectorCriteria criteria)
        {
            // Placeholder implementation - in real scenario would query market data service
            return new List<SecurityData>
            {
                new SecurityData
                {
                    Symbol = "AAPL",
                    Name = "Apple Inc.",
                    Sector = criteria.Sector,
                    MarketCap = 3000000000000,
                    Price = 150.0,
                    Volume = 50000000
                }
            };
        }

        #endregion

    /// </summary>
    public class BenchmarkData
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<BenchmarkHolding> Holdings { get; set; } = new();
        public BenchmarkStatistics Statistics { get; set; } = new();
        public DateTime CreatedAt { get; set; }
        public string Source { get; set; } = string.Empty;
    }

    /// <summary>
    /// Represents portfolio data for comparison with benchmarks
    /// </summary>
    public class PortfolioData
    {
        public string Name { get; set; } = string.Empty;
        public Dictionary<string, double> Holdings { get; set; } = new();
        public double TotalValue { get; set; }
        public DateTime AsOfDate { get; set; }
        public string Currency { get; set; } = "USD";
    }

    /// <summary>
    /// Represents historical benchmark composition for drift analysis
    /// </summary>
    public class BenchmarkComposition
    {
        public DateTime Date { get; set; }
        public List<BenchmarkHolding> Holdings { get; set; } = new();
        public double TotalWeight { get; set; }
        public string Version { get; set; } = string.Empty;
    }

    /// <summary>
    /// Represents criteria for sector benchmark creation
    /// </summary>
    public class SectorCriteria
    {
        public string Sector { get; set; } = string.Empty;
        public List<string> Industries { get; set; } = new();
        public double MinMarketCap { get; set; }
        public double MaxMarketCap { get; set; }
        public int MinVolume { get; set; }
        public List<string> Exclusions { get; set; } = new();
        public Dictionary<string, double> CustomFilters { get; set; } = new();
    }

    /// <summary>
    /// Represents universe filter criteria
    /// </summary>
    public class UniverseFilter
    {
        public List<string> Sectors { get; set; } = new();
        public List<string> Industries { get; set; } = new();
        public double MinMarketCap { get; set; }
        public double MaxMarketCap { get; set; }
        public int MinVolume { get; set; }
        public List<string> Exclusions { get; set; } = new();
        public Dictionary<string, object> CustomFilters { get; set; } = new();
    }

    /// <summary>
    /// Represents stress testing scenario
    /// </summary>
    public class StressScenario
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public Dictionary<string, double> MarketShocks { get; set; } = new();
        public double Probability { get; set; }
        public string Severity { get; set; } = string.Empty;
    }

    #endregion
}