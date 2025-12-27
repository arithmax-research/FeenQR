using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using QuantResearchAgent.Core;
using MathNet.Numerics.Statistics;

namespace QuantResearchAgent.Services
{
    /// <summary>
    /// Counterparty risk analysis and concentration monitoring service
    /// </summary>
    public class CounterpartyRiskService
    {
        private readonly ILogger<CounterpartyRiskService> _logger;
        private readonly MarketDataService _marketDataService;

        public CounterpartyRiskService(
            ILogger<CounterpartyRiskService> logger,
            MarketDataService marketDataService)
        {
            _logger = logger;
            _marketDataService = marketDataService;
        }

        /// <summary>
        /// Analyze counterparty exposure across portfolio
        /// </summary>
        public async Task<CounterpartyExposureAnalysis> AnalyzeCounterpartyExposureAsync(
            List<CounterpartyPosition> positions,
            Dictionary<string, CounterpartyInfo> counterparties)
        {
            _logger.LogInformation($"Analyzing counterparty exposure for {positions.Count} positions and {counterparties.Count} counterparties");

            try
            {
                var exposureByCounterparty = new Dictionary<string, decimal>();
                var concentrationMetrics = new List<ConcentrationMetric>();
                var riskMetrics = new List<CounterpartyRiskMetric>();

                // Calculate exposure by counterparty
                foreach (var position in positions)
                {
                    if (counterparties.ContainsKey(position.CounterpartyId))
                    {
                        var exposure = position.Quantity * position.AveragePrice;
                        if (!exposureByCounterparty.ContainsKey(position.CounterpartyId))
                            exposureByCounterparty[position.CounterpartyId] = 0;
                        exposureByCounterparty[position.CounterpartyId] += exposure;
                    }
                }

                var totalExposure = exposureByCounterparty.Values.Sum();

                // Calculate concentration metrics
                foreach (var kvp in exposureByCounterparty)
                {
                    var counterparty = counterparties[kvp.Key];
                    var exposure = kvp.Value;
                    var concentration = totalExposure > 0 ? exposure / totalExposure : 0;

                    concentrationMetrics.Add(new ConcentrationMetric
                    {
                        CounterpartyId = kvp.Key,
                        CounterpartyName = counterparty.Name,
                        Exposure = exposure,
                        Concentration = concentration,
                        HerfindahlIndex = concentration * concentration
                    });

                    // Calculate risk metrics
                    var riskMetric = await CalculateCounterpartyRiskMetricAsync(counterparty, exposure);
                    riskMetrics.Add(riskMetric);
                }

                // Calculate portfolio-level metrics
                var herfindahlIndex = concentrationMetrics.Sum(m => m.HerfindahlIndex);
                var top10Concentration = concentrationMetrics
                    .OrderByDescending(m => m.Concentration)
                    .Take(10)
                    .Sum(m => m.Concentration);

                var maxConcentration = concentrationMetrics.Any() ?
                    concentrationMetrics.Max(m => m.Concentration) : 0;

                return new CounterpartyExposureAnalysis
                {
                    ExposureByCounterparty = exposureByCounterparty,
                    ConcentrationMetrics = concentrationMetrics,
                    RiskMetrics = riskMetrics,
                    PortfolioMetrics = new PortfolioConcentrationMetrics
                    {
                        TotalExposure = totalExposure,
                        HerfindahlIndex = herfindahlIndex,
                        Top10Concentration = top10Concentration,
                        MaxSingleConcentration = maxConcentration,
                        NumberOfCounterparties = exposureByCounterparty.Count,
                        EffectiveNumberOfCounterparties = herfindahlIndex > 0 ? 1.0m / herfindahlIndex : 0
                    },
                    Timestamp = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing counterparty exposure");
                throw;
            }
        }

        /// <summary>
        /// Monitor concentration limits and generate alerts
        /// </summary>
        public async Task<ConcentrationAlert> MonitorConcentrationLimitsAsync(
            CounterpartyExposureAnalysis analysis,
            ConcentrationLimits limits)
        {
            _logger.LogInformation("Monitoring concentration limits");

            try
            {
                var alerts = new List<ConcentrationViolation>();

                // Check individual counterparty limits
                foreach (var metric in analysis.ConcentrationMetrics)
                {
                    if (metric.Concentration > limits.MaxSingleCounterparty)
                    {
                        alerts.Add(new ConcentrationViolation
                        {
                            CounterpartyId = metric.CounterpartyId,
                            CounterpartyName = metric.CounterpartyName,
                            ViolationType = "Single Counterparty Limit",
                            ActualValue = metric.Concentration,
                            LimitValue = limits.MaxSingleCounterparty,
                            Severity = metric.Concentration > limits.MaxSingleCounterparty * 1.2m ? "Critical" : "Warning"
                        });
                    }
                }

                // Check top 10 concentration
                if (analysis.PortfolioMetrics.Top10Concentration > limits.MaxTop10Concentration)
                {
                    alerts.Add(new ConcentrationViolation
                    {
                        ViolationType = "Top 10 Concentration Limit",
                        ActualValue = analysis.PortfolioMetrics.Top10Concentration,
                        LimitValue = limits.MaxTop10Concentration,
                        Severity = analysis.PortfolioMetrics.Top10Concentration > limits.MaxTop10Concentration * 1.1m ? "Critical" : "Warning"
                    });
                }

                // Check Herfindahl-Hirschman Index
                if (analysis.PortfolioMetrics.HerfindahlIndex > limits.MaxHerfindahlIndex)
                {
                    alerts.Add(new ConcentrationViolation
                    {
                        ViolationType = "Herfindahl-Hirschman Index Limit",
                        ActualValue = analysis.PortfolioMetrics.HerfindahlIndex,
                        LimitValue = limits.MaxHerfindahlIndex,
                        Severity = analysis.PortfolioMetrics.HerfindahlIndex > limits.MaxHerfindahlIndex * 1.15m ? "Critical" : "Warning"
                    });
                }

                // Check effective number of counterparties
                if (analysis.PortfolioMetrics.EffectiveNumberOfCounterparties < limits.MinEffectiveCounterparties)
                {
                    alerts.Add(new ConcentrationViolation
                    {
                        ViolationType = "Minimum Effective Counterparties",
                        ActualValue = analysis.PortfolioMetrics.EffectiveNumberOfCounterparties,
                        LimitValue = limits.MinEffectiveCounterparties,
                        Severity = "Warning"
                    });
                }

                return new ConcentrationAlert
                {
                    Analysis = analysis,
                    Violations = alerts,
                    Limits = limits,
                    HasViolations = alerts.Any(),
                    CriticalViolations = alerts.Count(a => a.Severity == "Critical"),
                    WarningViolations = alerts.Count(a => a.Severity == "Warning"),
                    Timestamp = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error monitoring concentration limits");
                throw;
            }
        }

        /// <summary>
        /// Calculate counterparty default probability and credit risk
        /// </summary>
        public async Task<CounterpartyCreditRisk> CalculateCreditRiskAsync(
            string counterpartyId,
            CounterpartyInfo counterparty,
            decimal exposure,
            int lookbackDays = 252)
        {
            _logger.LogInformation($"Calculating credit risk for counterparty {counterpartyId}");

            try
            {
                // Get market data for credit risk indicators
                var creditMetrics = await CalculateCreditMetricsAsync(counterparty, lookbackDays);

                // Calculate distance to default using Merton's model
                var distanceToDefault = CalculateDistanceToDefault(counterparty, creditMetrics);

                // Estimate default probability
                var defaultProbability = CalculateDefaultProbability(distanceToDefault);

                // Calculate credit VaR
                var creditVaR = CalculateCreditVaR(exposure, defaultProbability, creditMetrics);

                // Calculate expected loss
                var expectedLoss = exposure * defaultProbability * creditMetrics.LossGivenDefault;

                return new CounterpartyCreditRisk
                {
                    CounterpartyId = counterpartyId,
                    CounterpartyName = counterparty.Name,
                    Exposure = exposure,
                    DistanceToDefault = distanceToDefault,
                    DefaultProbability = defaultProbability,
                    CreditVaR = creditVaR,
                    ExpectedLoss = expectedLoss,
                    LossGivenDefault = creditMetrics.LossGivenDefault,
                    CreditRating = counterparty.CreditRating,
                    CreditMetrics = creditMetrics,
                    Timestamp = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error calculating credit risk for {counterpartyId}");
                throw;
            }
        }

        /// <summary>
        /// Analyze counterparty correlation and contagion risk
        /// </summary>
        public async Task<ContagionRiskAnalysis> AnalyzeContagionRiskAsync(
            List<CounterpartyCreditRisk> creditRisks,
            Dictionary<string, List<double>> correlationMatrix)
        {
            _logger.LogInformation($"Analyzing contagion risk for {creditRisks.Count} counterparties");

            try
            {
                var contagionScenarios = new List<ContagionScenario>();
                var systemicRiskMetrics = new SystemicRiskMetrics();

                // Generate contagion scenarios
                for (int i = 0; i < creditRisks.Count; i++)
                {
                    var triggeringCounterparty = creditRisks[i];

                    // Calculate cascading defaults
                    var cascadingDefaults = CalculateCascadingDefaults(
                        triggeringCounterparty,
                        creditRisks.Where((c, idx) => idx != i).ToList(),
                        correlationMatrix);

                    contagionScenarios.Add(new ContagionScenario
                    {
                        TriggeringCounterparty = triggeringCounterparty.CounterpartyId,
                        CascadingDefaults = cascadingDefaults,
                        TotalLoss = triggeringCounterparty.ExpectedLoss + cascadingDefaults.Sum(c => c.ExpectedLoss),
                        Probability = triggeringCounterparty.DefaultProbability
                    });
                }

                // Calculate systemic risk metrics
                systemicRiskMetrics.TotalContagionLoss = contagionScenarios.Sum(s => s.TotalLoss * s.Probability);
                systemicRiskMetrics.MaxContagionLoss = contagionScenarios.Max(s => s.TotalLoss);
                systemicRiskMetrics.AverageContagionProbability = contagionScenarios.Average(s => s.Probability);
                systemicRiskMetrics.SystemicRiskIndex = CalculateSystemicRiskIndex(contagionScenarios);

                return new ContagionRiskAnalysis
                {
                    ContagionScenarios = contagionScenarios,
                    SystemicRiskMetrics = systemicRiskMetrics,
                    Timestamp = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing contagion risk");
                throw;
            }
        }

        /// <summary>
        /// Generate counterparty risk stress test scenarios
        /// </summary>
        public async Task<StressTestResults> RunStressTestAsync(
            CounterpartyExposureAnalysis baselineAnalysis,
            List<StressTestScenario> scenarios)
        {
            _logger.LogInformation($"Running stress tests with {scenarios.Count} scenarios");

            try
            {
                var scenarioResults = new List<StressTestResult>();

                foreach (var scenario in scenarios)
                {
                    // Apply stress scenario to exposures
                    var stressedExposures = ApplyStressScenario(baselineAnalysis, scenario);

                    // Recalculate concentration metrics
                    var stressedMetrics = RecalculateConcentrationMetrics(stressedExposures);

                    // Calculate losses under stress
                    var losses = CalculateStressLosses(stressedExposures, scenario);

                    scenarioResults.Add(new StressTestResult
                    {
                        ScenarioName = scenario.Name,
                        ScenarioType = scenario.Type,
                        StressedExposures = stressedExposures,
                        StressedMetrics = stressedMetrics,
                        TotalLoss = losses.TotalLoss,
                        MaxLoss = losses.MaxLoss,
                        LossDistribution = losses.LossDistribution
                    });
                }

                return new StressTestResults
                {
                    BaselineAnalysis = baselineAnalysis,
                    ScenarioResults = scenarioResults,
                    WorstCaseScenario = scenarioResults.OrderByDescending(r => r.TotalLoss).FirstOrDefault(),
                    AverageLoss = scenarioResults.Average(r => r.TotalLoss),
                    Timestamp = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error running stress tests");
                throw;
            }
        }

        #region Helper Methods

        private async Task<CounterpartyRiskMetric> CalculateCounterpartyRiskMetricAsync(CounterpartyInfo counterparty, decimal exposure)
        {
            // Calculate various risk metrics for the counterparty
            var volatility = await CalculateCounterpartyVolatilityAsync(counterparty);
            var correlation = await CalculateCounterpartyCorrelationAsync(counterparty);

            return new CounterpartyRiskMetric
            {
                CounterpartyId = counterparty.Id,
                CounterpartyName = counterparty.Name,
                Exposure = exposure,
                Volatility = volatility,
                Correlation = correlation,
                RiskScore = CalculateRiskScore(volatility, correlation, counterparty.CreditRating),
                LastUpdated = DateTime.UtcNow
            };
        }

        private async Task<CreditMetrics> CalculateCreditMetricsAsync(CounterpartyInfo counterparty, int lookbackDays)
        {
            // Get market data for credit analysis
            var marketData = await _marketDataService.GetHistoricalDataAsync(counterparty.MarketSymbol ?? "SPY", lookbackDays);

            if (marketData == null || marketData.Count < 30)
            {
                return new CreditMetrics
                {
                    Volatility = 0.02m,
                    Beta = 1.0m,
                    LossGivenDefault = 0.4m,
                    RecoveryRate = 0.6m
                };
            }

            var prices = marketData.Select(d => (double)d.Close).ToList();
            var returns = CalculateReturns(prices);

            return new CreditMetrics
            {
                Volatility = (decimal)returns.StandardDeviation(),
                Beta = await CalculateBetaAsync(counterparty.MarketSymbol ?? "SPY", "SPY"),
                LossGivenDefault = counterparty.CreditRating switch
                {
                    "AAA" => 0.3m,
                    "AA" => 0.35m,
                    "A" => 0.4m,
                    "BBB" => 0.5m,
                    "BB" => 0.65m,
                    "B" => 0.75m,
                    "CCC" => 0.9m,
                    _ => 0.6m
                },
                RecoveryRate = 1.0m - counterparty.CreditRating switch
                {
                    "AAA" => 0.3m,
                    "AA" => 0.35m,
                    "A" => 0.4m,
                    "BBB" => 0.5m,
                    "BB" => 0.65m,
                    "B" => 0.75m,
                    "CCC" => 0.9m,
                    _ => 0.6m
                }
            };
        }

        private decimal CalculateDistanceToDefault(CounterpartyInfo counterparty, CreditMetrics metrics)
        {
            // Simplified Merton's distance to default
            // Distance to Default = (ln(V/E) + (μ - 0.5σ²)T) / (σ√T)
            // Where V = asset value, E = debt, μ = drift, σ = volatility, T = time

            // Using market cap as proxy for asset value
            var assetValue = counterparty.MarketCap ?? 1000000000m; // Default 1B
            var debt = counterparty.TotalDebt ?? assetValue * 0.3m; // Assume 30% debt ratio
            var timeHorizon = 1.0; // 1 year

            if (debt <= 0 || assetValue <= 0)
                return 10.0m; // Very low risk

            var logRatio = Math.Log((double)(assetValue / debt));
            var drift = 0.05; // Assume 5% drift
            var volatility = (double)metrics.Volatility;
            var timeValue = drift - 0.5 * volatility * volatility;
            var denominator = volatility * Math.Sqrt(timeHorizon);

            return (decimal)((logRatio + timeValue * timeHorizon) / denominator);
        }

        private decimal CalculateDefaultProbability(decimal distanceToDefault)
        {
            // Convert distance to default to probability using normal CDF approximation
            // P(default) = 1 - N(d), where d is distance to default
            var d = (double)distanceToDefault;

            // Abramowitz & Stegun approximation of normal CDF
            var a1 = 0.254829592;
            var a2 = -0.284496736;
            var a3 = 1.421413741;
            var a4 = -1.453152027;
            var a5 = 1.061405429;
            var p = 0.3275911;

            var sign = d < 0 ? -1 : 1;
            var x = Math.Abs(d) / Math.Sqrt(2.0);

            var t = 1.0 / (1.0 + p * x);
            var erf = 1.0 - (((((a5 * t + a4) * t) + a3) * t + a2) * t + a1) * t * Math.Exp(-x * x);

            var cdf = 0.5 * (1.0 + sign * erf);
            return (decimal)(1.0 - cdf);
        }

        private decimal CalculateCreditVaR(decimal exposure, decimal defaultProbability, CreditMetrics metrics)
        {
            // Credit VaR using Vasicek model approximation
            // VaR = Exposure * LGD * N^(-1)(PD) * √(ρ)
            // Where ρ is asset correlation (typically 0.12 for corporates)

            var assetCorrelation = 0.12;
            var lossGivenDefault = (double)metrics.LossGivenDefault;

            if (defaultProbability <= 0)
                return 0;

            // Inverse normal CDF approximation for PD
            var pdZScore = MathNet.Numerics.Distributions.Normal.InvCDF(0, 1, (double)defaultProbability);

            // Vasicek single-factor model
            var portfolioZScore = pdZScore / Math.Sqrt(1 - assetCorrelation);

            // Convert back to probability
            var portfolioPD = MathNet.Numerics.Distributions.Normal.CDF(0, 1, portfolioZScore);

            return exposure * (decimal)portfolioPD * metrics.LossGivenDefault;
        }

        private List<CascadingDefault> CalculateCascadingDefaults(
            CounterpartyCreditRisk triggeringParty,
            List<CounterpartyCreditRisk> otherParties,
            Dictionary<string, List<double>> correlationMatrix)
        {
            var cascadingDefaults = new List<CascadingDefault>();

            foreach (var party in otherParties)
            {
                // Calculate conditional default probability given triggering default
                var correlation = GetCorrelation(triggeringParty.CounterpartyId, party.CounterpartyId, correlationMatrix);
                var conditionalPD = CalculateConditionalDefaultProbability(
                    triggeringParty.DefaultProbability,
                    party.DefaultProbability,
                    correlation);

                if (conditionalPD > party.DefaultProbability * 1.5m) // Significant increase
                {
                    cascadingDefaults.Add(new CascadingDefault
                    {
                        CounterpartyId = party.CounterpartyId,
                        CounterpartyName = party.CounterpartyName,
                        BaseDefaultProbability = party.DefaultProbability,
                        ConditionalDefaultProbability = conditionalPD,
                        ExpectedLoss = party.ExpectedLoss * (conditionalPD / party.DefaultProbability),
                        Correlation = correlation
                    });
                }
            }

            return cascadingDefaults;
        }

        private decimal CalculateConditionalDefaultProbability(decimal pd1, decimal pd2, decimal correlation)
        {
            // Using Gaussian copula for conditional probability
            // P(D2|D1) = P(D2,D1) / P(D1)

            if (pd1 <= 0 || pd2 <= 0)
                return pd2;

            // Simplified calculation using normal distribution
            var z1 = (decimal)MathNet.Numerics.Distributions.Normal.InvCDF(0, 1, (double)pd1);
            var z2 = (decimal)MathNet.Numerics.Distributions.Normal.InvCDF(0, 1, (double)pd2);

            // Conditional normal CDF
            var conditionalZ = (z2 - correlation * z1) / (decimal)Math.Sqrt(1 - (double)(correlation * correlation));
            var conditionalPD = (decimal)MathNet.Numerics.Distributions.Normal.CDF(0, 1, (double)conditionalZ);

            return Math.Max(0, Math.Min(1, conditionalPD));
        }

        private decimal CalculateSystemicRiskIndex(List<ContagionScenario> scenarios)
        {
            if (!scenarios.Any())
                return 0;

            // Systemic risk index based on expected contagion losses
            var totalExpectedLoss = scenarios.Sum(s => s.TotalLoss * s.Probability);
            var maxPossibleLoss = scenarios.Max(s => s.TotalLoss);

            return maxPossibleLoss > 0 ? totalExpectedLoss / maxPossibleLoss : 0;
        }

        private List<double> CalculateReturns(List<double> prices)
        {
            var returns = new List<double>();
            for (int i = 1; i < prices.Count; i++)
            {
                returns.Add(Math.Log(prices[i] / prices[i - 1]));
            }
            return returns;
        }

        private async Task<decimal> CalculateBetaAsync(string symbol, string benchmark)
        {
            try
            {
                var symbolData = await _marketDataService.GetHistoricalDataAsync(symbol, 252);
                var benchmarkData = await _marketDataService.GetHistoricalDataAsync(benchmark, 252);

                if (symbolData == null || benchmarkData == null || symbolData.Count != benchmarkData.Count)
                    return 1.0m;

                var symbolReturns = CalculateReturns(symbolData.Select(d => (double)d.Close).ToList());
                var benchmarkReturns = CalculateReturns(benchmarkData.Select(d => (double)d.Close).ToList());

                return (decimal)symbolReturns.Covariance(benchmarkReturns) / (decimal)benchmarkReturns.Variance();
            }
            catch
            {
                return 1.0m;
            }
        }

        private async Task<decimal> CalculateCounterpartyVolatilityAsync(CounterpartyInfo counterparty)
        {
            if (string.IsNullOrEmpty(counterparty.MarketSymbol))
                return 0.02m; // Default 2%

            try
            {
                var data = await _marketDataService.GetHistoricalDataAsync(counterparty.MarketSymbol, 252);
                if (data == null || data.Count < 30)
                    return 0.02m;

                var returns = CalculateReturns(data.Select(d => (double)d.Close).ToList());
                return (decimal)returns.StandardDeviation();
            }
            catch
            {
                return 0.02m;
            }
        }

        private async Task<decimal> CalculateCounterpartyCorrelationAsync(CounterpartyInfo counterparty)
        {
            if (string.IsNullOrEmpty(counterparty.MarketSymbol))
                return 0.3m; // Default correlation

            try
            {
                return await CalculateBetaAsync(counterparty.MarketSymbol, "SPY");
            }
            catch
            {
                return 0.3m;
            }
        }

        private decimal CalculateRiskScore(decimal volatility, decimal correlation, string creditRating)
        {
            // Risk score from 0-100, higher = riskier
            var ratingScore = creditRating switch
            {
                "AAA" => 10,
                "AA" => 20,
                "A" => 30,
                "BBB" => 40,
                "BB" => 60,
                "B" => 70,
                "CCC" => 90,
                _ => 50
            };

            var volatilityScore = Math.Min(100, (double)volatility * 1000); // Scale volatility
            var correlationScore = Math.Min(100, Math.Abs((double)correlation) * 50); // Scale correlation

            return (decimal)(ratingScore * 0.5 + volatilityScore * 0.3 + correlationScore * 0.2);
        }

        private decimal GetCorrelation(string id1, string id2, Dictionary<string, List<double>> correlationMatrix)
        {
            var key = $"{id1}_{id2}";
            if (correlationMatrix.ContainsKey(key) && correlationMatrix[key].Any())
                return (decimal)correlationMatrix[key].Average();

            key = $"{id2}_{id1}";
            if (correlationMatrix.ContainsKey(key) && correlationMatrix[key].Any())
                return (decimal)correlationMatrix[key].Average();

            return 0.3m; // Default correlation
        }

        private Dictionary<string, decimal> ApplyStressScenario(CounterpartyExposureAnalysis analysis, StressTestScenario scenario)
        {
            var stressedExposures = new Dictionary<string, decimal>();

            foreach (var kvp in analysis.ExposureByCounterparty)
            {
                var stressMultiplier = scenario.StressMultipliers.GetValueOrDefault(kvp.Key, 1.0m);
                stressedExposures[kvp.Key] = kvp.Value * stressMultiplier;
            }

            return stressedExposures;
        }

        private PortfolioConcentrationMetrics RecalculateConcentrationMetrics(Dictionary<string, decimal> exposures)
        {
            var totalExposure = exposures.Values.Sum();
            var herfindahlIndex = exposures.Values.Sum(e => (e / totalExposure) * (e / totalExposure));

            var top10Concentration = exposures.Values
                .OrderByDescending(e => e)
                .Take(10)
                .Sum(e => e / totalExposure);

            var maxConcentration = exposures.Values.Any() ?
                exposures.Values.Max() / totalExposure : 0;

            return new PortfolioConcentrationMetrics
            {
                TotalExposure = totalExposure,
                HerfindahlIndex = herfindahlIndex,
                Top10Concentration = top10Concentration,
                MaxSingleConcentration = maxConcentration,
                NumberOfCounterparties = exposures.Count,
                EffectiveNumberOfCounterparties = herfindahlIndex > 0 ? 1.0m / herfindahlIndex : 0
            };
        }

        private StressLosses CalculateStressLosses(Dictionary<string, decimal> exposures, StressTestScenario scenario)
        {
            var losses = new Dictionary<string, decimal>();

            foreach (var kvp in exposures)
            {
                // Assume loss rate based on scenario severity
                var lossRate = scenario.Severity switch
                {
                    "Mild" => 0.05m,
                    "Moderate" => 0.15m,
                    "Severe" => 0.30m,
                    "Extreme" => 0.50m,
                    _ => 0.10m
                };

                losses[kvp.Key] = kvp.Value * lossRate;
            }

            return new StressLosses
            {
                TotalLoss = losses.Values.Sum(),
                MaxLoss = losses.Values.Max(),
                LossDistribution = losses
            };
        }

        #endregion
    }

    #region Data Classes

    public class CounterpartyPosition
    {
        public string CounterpartyId { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal AveragePrice { get; set; }
    }

    public class CounterpartyInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string CreditRating { get; set; } = string.Empty;
        public decimal? MarketCap { get; set; }
        public decimal? TotalDebt { get; set; }
        public string? MarketSymbol { get; set; }
    }

    public class CounterpartyExposureAnalysis
    {
        public Dictionary<string, decimal> ExposureByCounterparty { get; set; } = new();
        public List<ConcentrationMetric> ConcentrationMetrics { get; set; } = new();
        public List<CounterpartyRiskMetric> RiskMetrics { get; set; } = new();
        public PortfolioConcentrationMetrics PortfolioMetrics { get; set; } = new();
        public DateTime Timestamp { get; set; }
    }

    public class ConcentrationMetric
    {
        public string CounterpartyId { get; set; } = string.Empty;
        public string CounterpartyName { get; set; } = string.Empty;
        public decimal Exposure { get; set; }
        public decimal Concentration { get; set; }
        public decimal HerfindahlIndex { get; set; }
    }

    public class CounterpartyRiskMetric
    {
        public string CounterpartyId { get; set; } = string.Empty;
        public string CounterpartyName { get; set; } = string.Empty;
        public decimal Exposure { get; set; }
        public decimal Volatility { get; set; }
        public decimal Correlation { get; set; }
        public decimal RiskScore { get; set; }
        public DateTime LastUpdated { get; set; }
    }

    public class PortfolioConcentrationMetrics
    {
        public decimal TotalExposure { get; set; }
        public decimal HerfindahlIndex { get; set; }
        public decimal Top10Concentration { get; set; }
        public decimal MaxSingleConcentration { get; set; }
        public int NumberOfCounterparties { get; set; }
        public decimal EffectiveNumberOfCounterparties { get; set; }
    }

    public class ConcentrationLimits
    {
        public decimal MaxSingleCounterparty { get; set; } = 0.10m; // 10%
        public decimal MaxTop10Concentration { get; set; } = 0.50m; // 50%
        public decimal MaxHerfindahlIndex { get; set; } = 0.15m; // HHI limit
        public decimal MinEffectiveCounterparties { get; set; } = 5.0m;
    }

    public class ConcentrationAlert
    {
        public CounterpartyExposureAnalysis Analysis { get; set; } = new();
        public List<ConcentrationViolation> Violations { get; set; } = new();
        public ConcentrationLimits Limits { get; set; } = new();
        public bool HasViolations { get; set; }
        public int CriticalViolations { get; set; }
        public int WarningViolations { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class ConcentrationViolation
    {
        public string CounterpartyId { get; set; } = string.Empty;
        public string CounterpartyName { get; set; } = string.Empty;
        public string ViolationType { get; set; } = string.Empty;
        public decimal ActualValue { get; set; }
        public decimal LimitValue { get; set; }
        public string Severity { get; set; } = string.Empty;
    }

    public class CreditMetrics
    {
        public decimal Volatility { get; set; }
        public decimal Beta { get; set; }
        public decimal LossGivenDefault { get; set; }
        public decimal RecoveryRate { get; set; }
    }

    public class CounterpartyCreditRisk
    {
        public string CounterpartyId { get; set; } = string.Empty;
        public string CounterpartyName { get; set; } = string.Empty;
        public decimal Exposure { get; set; }
        public decimal DistanceToDefault { get; set; }
        public decimal DefaultProbability { get; set; }
        public decimal CreditVaR { get; set; }
        public decimal ExpectedLoss { get; set; }
        public decimal LossGivenDefault { get; set; }
        public string CreditRating { get; set; } = string.Empty;
        public CreditMetrics CreditMetrics { get; set; } = new();
        public DateTime Timestamp { get; set; }
    }

    public class ContagionRiskAnalysis
    {
        public List<ContagionScenario> ContagionScenarios { get; set; } = new();
        public SystemicRiskMetrics SystemicRiskMetrics { get; set; } = new();
        public DateTime Timestamp { get; set; }
    }

    public class ContagionScenario
    {
        public string TriggeringCounterparty { get; set; } = string.Empty;
        public List<CascadingDefault> CascadingDefaults { get; set; } = new();
        public decimal TotalLoss { get; set; }
        public decimal Probability { get; set; }
    }

    public class CascadingDefault
    {
        public string CounterpartyId { get; set; } = string.Empty;
        public string CounterpartyName { get; set; } = string.Empty;
        public decimal BaseDefaultProbability { get; set; }
        public decimal ConditionalDefaultProbability { get; set; }
        public decimal ExpectedLoss { get; set; }
        public decimal Correlation { get; set; }
    }

    public class SystemicRiskMetrics
    {
        public decimal TotalContagionLoss { get; set; }
        public decimal MaxContagionLoss { get; set; }
        public decimal AverageContagionProbability { get; set; }
        public decimal SystemicRiskIndex { get; set; }
    }

    public class StressTestResults
    {
        public CounterpartyExposureAnalysis BaselineAnalysis { get; set; } = new();
        public List<StressTestResult> ScenarioResults { get; set; } = new();
        public StressTestResult WorstCaseScenario { get; set; } = new();
        public decimal AverageLoss { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class StressTestResult
    {
        public string ScenarioName { get; set; } = string.Empty;
        public string ScenarioType { get; set; } = string.Empty;
        public Dictionary<string, decimal> StressedExposures { get; set; } = new();
        public PortfolioConcentrationMetrics StressedMetrics { get; set; } = new();
        public decimal TotalLoss { get; set; }
        public decimal MaxLoss { get; set; }
        public Dictionary<string, decimal> LossDistribution { get; set; } = new();
    }

    public class StressLosses
    {
        public decimal TotalLoss { get; set; }
        public decimal MaxLoss { get; set; }
        public Dictionary<string, decimal> LossDistribution { get; set; } = new();
    }

    #endregion
}