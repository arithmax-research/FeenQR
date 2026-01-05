using Microsoft.AspNetCore.Mvc;
using QuantResearchAgent.Services;
using QuantResearchAgent.Core;
using System.Text.Json;

namespace Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RiskAnalyticsController : ControllerBase
    {
        private readonly ILogger<RiskAnalyticsController> _logger;
        private readonly AdvancedRiskService _advancedRiskService;
        private readonly AdvancedRiskAnalyticsService _advancedRiskAnalyticsService;
        private readonly CounterpartyRiskService _counterpartyRiskService;
        private readonly RiskManagementService _riskManagementService;
        private readonly PortfolioService _portfolioService;

        public RiskAnalyticsController(
            ILogger<RiskAnalyticsController> logger,
            AdvancedRiskService advancedRiskService,
            AdvancedRiskAnalyticsService advancedRiskAnalyticsService,
            CounterpartyRiskService counterpartyRiskService,
            RiskManagementService riskManagementService,
            PortfolioService portfolioService)
        {
            _logger = logger;
            _advancedRiskService = advancedRiskService;
            _advancedRiskAnalyticsService = advancedRiskAnalyticsService;
            _counterpartyRiskService = counterpartyRiskService;
            _riskManagementService = riskManagementService;
            _portfolioService = portfolioService;
        }

        /// <summary>
        /// Calculate risk metrics for portfolio
        /// Endpoint: GET /api/riskanalytics/risk-metrics
        /// </summary>
        [HttpGet("risk-metrics")]
        public async Task<IActionResult> GetRiskMetrics()
        {
            try
            {
                _logger.LogInformation("Calculating portfolio risk metrics");

                var positions = await _portfolioService.GetPositionsAsync();
                var metrics = await _portfolioService.CalculateMetricsAsync();
                var riskAssessment = await _riskManagementService.AssessPortfolioRiskAsync();

                var response = new
                {
                    type = "risk-metrics",
                    timestamp = DateTime.UtcNow,
                    metrics = new
                    {
                        volatility = metrics.Volatility,
                        sharpeRatio = metrics.SharpeRatio,
                        maxDrawdown = metrics.MaxDrawdown,
                        beta = 1.0, // Placeholder
                        alpha = 0.0, // Placeholder
                    },
                    riskLevel = DetermineRiskLevel(metrics.Volatility, metrics.MaxDrawdown),
                    assessment = riskAssessment,
                    positionCount = positions.Count
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating risk metrics");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Calculate Value at Risk (VaR) for portfolio
        /// Endpoint: POST /api/riskanalytics/calculate-var
        /// </summary>
        [HttpPost("calculate-var")]
        public async Task<IActionResult> CalculateVaR([FromBody] VaRRequest request)
        {
            try
            {
                _logger.LogInformation($"Calculating VaR with method: {request.Method}");

                var weights = request.Weights ?? GetDefaultWeights();
                var confidenceLevel = request.ConfidenceLevel ?? 0.95;
                var method = request.Method ?? "historical";
                var startDate = request.StartDate ?? DateTime.Now.AddYears(-1);
                var endDate = request.EndDate ?? DateTime.Now;

                var result = await _advancedRiskService.CalculateVaRAsync(
                    weights, 
                    confidenceLevel, 
                    method, 
                    startDate, 
                    endDate, 
                    request.MonteCarloSimulations);

                var response = new
                {
                    type = "value-at-risk",
                    timestamp = DateTime.UtcNow,
                    analysisDate = result.AnalysisDate,
                    method = result.Method,
                    confidenceLevel = result.ConfidenceLevel,
                    metrics = new
                    {
                        var = result.VaR,
                        expectedShortfall = result.ExpectedShortfall,
                        diversificationRatio = result.DiversificationRatio
                    },
                    componentVaR = result.ComponentVaR.OrderByDescending(c => Math.Abs(c.Value))
                        .Select(c => new { asset = c.Key, var = c.Value }),
                    riskLevel = DetermineVaRRiskLevel(result.VaR),
                    interpretation = GetVaRInterpretation(result.VaR, result.ExpectedShortfall, confidenceLevel)
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating VaR");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Run stress tests on portfolio
        /// Endpoint: POST /api/riskanalytics/stress-test
        /// </summary>
        [HttpPost("stress-test")]
        public async Task<IActionResult> StressTest([FromBody] StressTestRequest request)
        {
            try
            {
                _logger.LogInformation("Running portfolio stress tests");

                var weights = request.Weights ?? GetDefaultWeights();
                var scenarios = request.Scenarios ?? GetDefaultStressScenarios(weights.Keys.ToList());
                var scenarioNames = request.ScenarioNames ?? GetDefaultScenarioNames();
                var threshold = request.Threshold ?? 0.05;
                var startDate = request.StartDate ?? DateTime.Now.AddYears(-1);
                var endDate = request.EndDate ?? DateTime.Now;

                var results = await _advancedRiskService.RunStressTestsAsync(
                    weights, 
                    scenarios, 
                    scenarioNames, 
                    threshold, 
                    startDate, 
                    endDate);

                var response = new
                {
                    type = "stress-test",
                    timestamp = DateTime.UtcNow,
                    threshold = threshold,
                    scenarios = results.Select(r => new
                    {
                        scenarioName = r.ScenarioName,
                        portfolioReturn = r.PortfolioReturn,
                        portfolioLoss = r.PortfolioLoss,
                        breachThreshold = r.BreachThreshold,
                        assetContributions = r.AssetContributions.OrderBy(c => c.Value)
                            .Select(c => new { asset = c.Key, contribution = c.Value }),
                        severity = r.PortfolioLoss > threshold * 2 ? "Critical" : 
                                  r.PortfolioLoss > threshold ? "High" : "Moderate"
                    }),
                    summary = new
                    {
                        totalScenarios = results.Count,
                        breaches = results.Count(r => r.BreachThreshold),
                        worstCase = results.OrderBy(r => r.PortfolioReturn).FirstOrDefault()?.ScenarioName,
                        worstLoss = results.Min(r => r.PortfolioReturn)
                    }
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error running stress tests");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Perform risk attribution analysis
        /// Endpoint: POST /api/riskanalytics/risk-attribution
        /// </summary>
        [HttpPost("risk-attribution")]
        public async Task<IActionResult> RiskAttribution([FromBody] RiskAttributionRequest request)
        {
            try
            {
                _logger.LogInformation("Performing risk factor attribution");

                var weights = request.Weights ?? GetDefaultWeights();
                var factors = request.Factors ?? new List<string> { "Market", "Size", "Value", "Momentum" };
                var startDate = request.StartDate ?? DateTime.Now.AddYears(-1);
                var endDate = request.EndDate ?? DateTime.Now;

                var result = await _advancedRiskService.PerformRiskFactorAttributionAsync(
                    weights, 
                    factors, 
                    startDate, 
                    endDate);

                var response = new
                {
                    type = "risk-attribution",
                    timestamp = DateTime.UtcNow,
                    analysisDate = result.AnalysisDate,
                    totalAttribution = result.TotalAttribution,
                    factorContributions = result.FactorContributions
                        .OrderByDescending(f => Math.Abs(f.Value))
                        .Select(f => new
                        {
                            factor = f.Key,
                            contribution = f.Value,
                            percentage = result.TotalAttribution > 0 ? (f.Value / result.TotalAttribution) * 100 : 0
                        }),
                    residualRisk = result.ResidualRisk,
                    rSquared = result.R2,
                    interpretation = "Factor contributions show the sources of portfolio risk"
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing risk attribution");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Generate comprehensive risk report
        /// Endpoint: POST /api/riskanalytics/risk-report
        /// </summary>
        [HttpPost("risk-report")]
        public async Task<IActionResult> GenerateRiskReport([FromBody] RiskReportRequest request)
        {
            try
            {
                _logger.LogInformation("Generating comprehensive risk report");

                var weights = request.Weights ?? GetDefaultWeights();
                var scenarios = request.Scenarios ?? GetDefaultStressScenarios(weights.Keys.ToList());
                var scenarioNames = request.ScenarioNames ?? GetDefaultScenarioNames();
                var factors = request.Factors ?? new List<string> { "Market", "Size", "Value", "Momentum" };
                var startDate = request.StartDate ?? DateTime.Now.AddYears(-1);
                var endDate = request.EndDate ?? DateTime.Now;

                var report = await _advancedRiskService.GenerateRiskReportAsync(
                    weights, 
                    scenarios, 
                    scenarioNames, 
                    factors, 
                    startDate, 
                    endDate);

                var response = new
                {
                    type = "comprehensive-risk-report",
                    timestamp = DateTime.UtcNow,
                    generatedDate = report.ReportDate,
                    portfolio = new
                    {
                        weights = weights,
                        assets = weights.Keys
                    },
                    valueAtRisk = new
                    {
                        var = report.VaR.VaR,
                        expectedShortfall = report.VaR.ExpectedShortfall,
                        diversificationRatio = report.VaR.DiversificationRatio,
                        method = report.VaR.Method,
                        confidenceLevel = report.VaR.ConfidenceLevel
                    },
                    stressTests = report.StressTests.Select(st => new
                    {
                        scenarioName = st.ScenarioName,
                        portfolioLoss = st.PortfolioLoss,
                        breachThreshold = st.BreachThreshold
                    }),
                    factorAttribution = new
                    {
                        totalAttribution = report.FactorAttribution.TotalAttribution,
                        factorContributions = report.FactorAttribution.FactorContributions,
                        residualRisk = report.FactorAttribution.ResidualRisk
                    },
                    summary = new
                    {
                        overallRiskLevel = DetermineOverallRiskLevel(report),
                        keyFindings = GenerateKeyFindings(report),
                        recommendations = GenerateRecommendations(report)
                    }
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating risk report");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Compare different risk measures
        /// Endpoint: POST /api/riskanalytics/compare-risk-measures
        /// </summary>
        [HttpPost("compare-risk-measures")]
        public async Task<IActionResult> CompareRiskMeasures([FromBody] CompareRiskMeasuresRequest request)
        {
            try
            {
                _logger.LogInformation("Comparing risk measures");

                var weights = request.Weights ?? GetDefaultWeights();
                var confidenceLevel = request.ConfidenceLevel ?? 0.95;
                var startDate = request.StartDate ?? DateTime.Now.AddYears(-1);
                var endDate = request.EndDate ?? DateTime.Now;

                // Calculate VaR using different methods
                var historicalVaR = await _advancedRiskService.CalculateVaRAsync(
                    weights, confidenceLevel, "historical", startDate, endDate);
                
                var parametricVaR = await _advancedRiskService.CalculateVaRAsync(
                    weights, confidenceLevel, "parametric", startDate, endDate);
                
                var monteCarloVaR = await _advancedRiskService.CalculateVaRAsync(
                    weights, confidenceLevel, "montecarlo", startDate, endDate, 10000);

                var response = new
                {
                    type = "risk-measures-comparison",
                    timestamp = DateTime.UtcNow,
                    confidenceLevel = confidenceLevel,
                    comparison = new
                    {
                        historical = new
                        {
                            method = "Historical Simulation",
                            var = historicalVaR.VaR,
                            expectedShortfall = historicalVaR.ExpectedShortfall,
                            diversificationRatio = historicalVaR.DiversificationRatio
                        },
                        parametric = new
                        {
                            method = "Parametric (Variance-Covariance)",
                            var = parametricVaR.VaR,
                            expectedShortfall = parametricVaR.ExpectedShortfall,
                            diversificationRatio = parametricVaR.DiversificationRatio
                        },
                        monteCarlo = new
                        {
                            method = "Monte Carlo Simulation",
                            var = monteCarloVaR.VaR,
                            expectedShortfall = monteCarloVaR.ExpectedShortfall,
                            diversificationRatio = monteCarloVaR.DiversificationRatio,
                            simulations = 10000
                        }
                    },
                    analysis = new
                    {
                        avgVaR = new[] { historicalVaR.VaR, parametricVaR.VaR, monteCarloVaR.VaR }.Average(),
                        maxVaR = new[] { historicalVaR.VaR, parametricVaR.VaR, monteCarloVaR.VaR }.Max(),
                        minVaR = new[] { historicalVaR.VaR, parametricVaR.VaR, monteCarloVaR.VaR }.Min(),
                        variance = CalculateVariance(new[] { historicalVaR.VaR, parametricVaR.VaR, monteCarloVaR.VaR }),
                        recommendation = "Monte Carlo provides most comprehensive estimate under normal conditions"
                    }
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error comparing risk measures");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Advanced risk analytics (CVaR, Expected Shortfall, etc.)
        /// Endpoint: POST /api/riskanalytics/advanced-risk
        /// </summary>
        [HttpPost("advanced-risk")]
        public async Task<IActionResult> AdvancedRisk([FromBody] AdvancedRiskRequest request)
        {
            try
            {
                _logger.LogInformation("Performing advanced risk analytics");

                var symbols = request.Symbols ?? GetDefaultSymbols();
                var confidenceLevel = request.ConfidenceLevel ?? 0.95m;
                var lookbackDays = request.LookbackDays ?? 252;

                // Calculate CVaR
                var cvarResult = await _advancedRiskAnalyticsService.CalculateCVaRAsync(
                    symbols, confidenceLevel, lookbackDays);

                // Calculate Expected Shortfall
                var esResult = await _advancedRiskAnalyticsService.CalculateExpectedShortfallAsync(
                    symbols, confidenceLevel, request.Simulations ?? 10000);

                var response = new
                {
                    type = "advanced-risk-analytics",
                    timestamp = DateTime.UtcNow,
                    symbols = symbols,
                    confidenceLevel = confidenceLevel,
                    cvar = new
                    {
                        value = cvarResult.CVaR,
                        var = cvarResult.VaR,
                        expectedShortfall = cvarResult.ExpectedShortfall,
                        calculationMethod = cvarResult.CalculationMethod,
                        sampleSize = cvarResult.SampleSize
                    },
                    expectedShortfall = new
                    {
                        value = esResult.ExpectedShortfall,
                        simulations = esResult.Simulations,
                        method = esResult.Method
                    },
                    analysis = new
                    {
                        riskLevel = DetermineCVaRRiskLevel((double)cvarResult.CVaR),
                        tailRiskSignificance = Math.Abs((double)cvarResult.CVaR - (double)cvarResult.VaR),
                        interpretation = GetCVaRInterpretation((double)cvarResult.CVaR, (double)confidenceLevel)
                    }
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing advanced risk analytics");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Counterparty risk analysis
        /// Endpoint: POST /api/riskanalytics/counterparty-risk
        /// </summary>
        [HttpPost("counterparty-risk")]
        public async Task<IActionResult> CounterpartyRisk([FromBody] CounterpartyRiskRequest request)
        {
            try
            {
                _logger.LogInformation("Analyzing counterparty risk");

                var positions = request.Positions ?? GetDefaultCounterpartyPositions();
                var counterparties = request.Counterparties ?? GetDefaultCounterparties();

                var analysis = await _counterpartyRiskService.AnalyzeCounterpartyExposureAsync(
                    positions, counterparties);

                var response = new
                {
                    type = "counterparty-risk-analysis",
                    timestamp = DateTime.UtcNow,
                    portfolioMetrics = new
                    {
                        totalExposure = analysis.PortfolioMetrics.TotalExposure,
                        herfindahlIndex = analysis.PortfolioMetrics.HerfindahlIndex,
                        top10Concentration = analysis.PortfolioMetrics.Top10Concentration,
                        maxSingleConcentration = analysis.PortfolioMetrics.MaxSingleConcentration,
                        numberOfCounterparties = analysis.PortfolioMetrics.NumberOfCounterparties,
                        effectiveNumberOfCounterparties = analysis.PortfolioMetrics.EffectiveNumberOfCounterparties
                    },
                    concentrationMetrics = analysis.ConcentrationMetrics
                        .OrderByDescending(m => m.Concentration)
                        .Select(m => new
                        {
                            counterpartyId = m.CounterpartyId,
                            counterpartyName = m.CounterpartyName,
                            exposure = m.Exposure,
                            concentration = m.Concentration,
                            herfindahlIndex = m.HerfindahlIndex,
                            riskLevel = m.Concentration > 0.2m ? "High" : 
                                       m.Concentration > 0.1m ? "Moderate" : "Low"
                        }),
                    riskMetrics = analysis.RiskMetrics.Select(r => new
                    {
                        counterpartyId = r.CounterpartyId,
                        counterpartyName = r.CounterpartyName,
                        exposure = r.Exposure,
                        volatility = r.Volatility,
                        correlation = r.Correlation,
                        riskScore = r.RiskScore
                    }),
                    alerts = GenerateConcentrationAlerts(analysis)
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing counterparty risk");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Tail risk analysis
        /// Endpoint: POST /api/riskanalytics/tail-risk
        /// </summary>
        [HttpPost("tail-risk")]
        public async Task<IActionResult> TailRisk([FromBody] TailRiskRequest request)
        {
            try
            {
                _logger.LogInformation("Analyzing tail risk");

                var symbols = request.Symbols ?? GetDefaultSymbols();
                var confidenceLevel = request.ConfidenceLevel ?? 0.99m; // Higher confidence for tail events
                var lookbackDays = request.LookbackDays ?? 252;

                // Calculate CVaR at high confidence level (tail risk)
                var cvarResult = await _advancedRiskAnalyticsService.CalculateCVaRAsync(
                    symbols, confidenceLevel, lookbackDays);

                // Get historical data for tail analysis
                var tailEvents = new List<object>();
                foreach (var symbol in symbols)
                {
                    var historicalData = await GetHistoricalReturns(symbol, lookbackDays * 2);
                    var extremeEvents = historicalData
                        .Where(r => r < -0.05) // Events with >5% loss
                        .OrderBy(r => r)
                        .Take(10)
                        .ToList();

                    if (extremeEvents.Any())
                    {
                        tailEvents.Add(new
                        {
                            symbol = symbol,
                            extremeEvents = extremeEvents,
                            frequency = (double)extremeEvents.Count / historicalData.Count,
                            avgTailLoss = extremeEvents.Average()
                        });
                    }
                }

                var response = new
                {
                    type = "tail-risk-analysis",
                    timestamp = DateTime.UtcNow,
                    symbols = symbols,
                    confidenceLevel = confidenceLevel,
                    tailRiskMetrics = new
                    {
                        cvar99 = cvarResult.CVaR,
                        var99 = cvarResult.VaR,
                        expectedShortfall = cvarResult.ExpectedShortfall,
                        tailIndex = CalculateTailIndex(symbols, lookbackDays)
                    },
                    extremeEvents = tailEvents,
                    analysis = new
                    {
                        tailRiskLevel = DetermineTailRiskLevel((double)cvarResult.CVaR),
                        blackSwanProbability = CalculateBlackSwanProbability((double)cvarResult.CVaR),
                        stressResilience = (double)cvarResult.CVaR < 0.15 ? "Strong" : 
                                          (double)cvarResult.CVaR < 0.25 ? "Moderate" : "Weak",
                        recommendations = GenerateTailRiskRecommendations((double)cvarResult.CVaR)
                    }
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing tail risk");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // Helper Methods

        private Dictionary<string, double> GetDefaultWeights()
        {
            return new Dictionary<string, double>
            {
                { "AAPL", 0.25 },
                { "MSFT", 0.25 },
                { "GOOGL", 0.25 },
                { "NVDA", 0.25 }
            };
        }

        private List<string> GetDefaultSymbols()
        {
            return new List<string> { "AAPL", "MSFT", "GOOGL", "NVDA" };
        }

        private List<Dictionary<string, double>> GetDefaultStressScenarios(List<string> assets)
        {
            var scenarios = new List<Dictionary<string, double>>();
            
            // Market crash scenario
            var marketCrash = new Dictionary<string, double>();
            foreach (var asset in assets)
            {
                marketCrash[asset] = -0.10; // 10% drop
            }
            scenarios.Add(marketCrash);

            // Tech sector shock
            var techShock = new Dictionary<string, double>();
            foreach (var asset in assets)
            {
                techShock[asset] = -0.15; // 15% drop for tech
            }
            scenarios.Add(techShock);

            // Volatility spike
            var volSpike = new Dictionary<string, double>();
            foreach (var asset in assets)
            {
                volSpike[asset] = -0.08; // 8% drop
            }
            scenarios.Add(volSpike);

            return scenarios;
        }

        private List<string> GetDefaultScenarioNames()
        {
            return new List<string> { "Market Crash", "Tech Sector Shock", "Volatility Spike" };
        }

        private List<CounterpartyPosition> GetDefaultCounterpartyPositions()
        {
            return new List<CounterpartyPosition>
            {
                new CounterpartyPosition 
                { 
                    CounterpartyId = "BROKER_A", 
                    Quantity = 100, 
                    AveragePrice = 150.00m 
                },
                new CounterpartyPosition 
                { 
                    CounterpartyId = "BROKER_B", 
                    Quantity = 200, 
                    AveragePrice = 200.00m 
                }
            };
        }

        private Dictionary<string, CounterpartyInfo> GetDefaultCounterparties()
        {
            return new Dictionary<string, CounterpartyInfo>
            {
                { "BROKER_A", new CounterpartyInfo { Id = "BROKER_A", Name = "Broker A", CreditRating = "AA" } },
                { "BROKER_B", new CounterpartyInfo { Id = "BROKER_B", Name = "Broker B", CreditRating = "A" } }
            };
        }

        private async Task<List<double>> GetHistoricalReturns(string symbol, int days)
        {
            var data = await _portfolioService.GetPositionsAsync();
            // Simplified - would normally fetch actual historical data
            var random = new Random(symbol.GetHashCode());
            return Enumerable.Range(0, days)
                .Select(_ => (random.NextDouble() - 0.5) * 0.1) // -5% to +5% returns
                .ToList();
        }

        private string DetermineRiskLevel(double volatility, double maxDrawdown)
        {
            if (volatility > 30 || maxDrawdown > 30) return "High";
            if (volatility > 20 || maxDrawdown > 20) return "Moderate";
            return "Low";
        }

        private string DetermineVaRRiskLevel(double var)
        {
            if (var > 0.05) return "High";
            if (var > 0.02) return "Moderate";
            return "Low";
        }

        private string DetermineCVaRRiskLevel(double cvar)
        {
            if (Math.Abs(cvar) > 0.15) return "High";
            if (Math.Abs(cvar) > 0.08) return "Moderate";
            return "Low";
        }

        private string DetermineTailRiskLevel(double cvar99)
        {
            if (Math.Abs(cvar99) > 0.25) return "Extreme";
            if (Math.Abs(cvar99) > 0.15) return "High";
            if (Math.Abs(cvar99) > 0.08) return "Moderate";
            return "Low";
        }

        private string GetVaRInterpretation(double var, double es, double confidence)
        {
            return $"At {confidence:P0} confidence, the portfolio could lose {var:P2} or more. " +
                   $"If this threshold is breached, the expected loss is {es:P2} (Expected Shortfall).";
        }

        private string GetCVaRInterpretation(double cvar, double confidence)
        {
            return $"The Conditional VaR (CVaR) at {confidence:P0} confidence is {Math.Abs(cvar):P2}. " +
                   $"This represents the average loss in the worst {(1 - confidence):P1} of scenarios.";
        }

        private string DetermineOverallRiskLevel(RiskReport report)
        {
            var varRisk = report.VaR.VaR;
            var stressBreaches = report.StressTests.Count(st => st.BreachThreshold);
            
            if (varRisk > 0.05 || stressBreaches > report.StressTests.Count / 2) return "High";
            if (varRisk > 0.02 || stressBreaches > 0) return "Moderate";
            return "Low";
        }

        private List<string> GenerateKeyFindings(RiskReport report)
        {
            var findings = new List<string>();
            
            findings.Add($"Portfolio VaR: {report.VaR.VaR:P2} at {report.VaR.ConfidenceLevel:P0} confidence");
            findings.Add($"Expected Shortfall: {report.VaR.ExpectedShortfall:P2}");
            findings.Add($"{report.StressTests.Count(st => st.BreachThreshold)} of {report.StressTests.Count} stress scenarios breach threshold");
            
            if (report.FactorAttribution.TotalAttribution > 0)
            {
                var topFactor = report.FactorAttribution.FactorContributions.OrderByDescending(f => Math.Abs(f.Value)).First();
                findings.Add($"Top risk factor: {topFactor.Key} contributing {(topFactor.Value / report.FactorAttribution.TotalAttribution * 100):F1}%");
            }

            return findings;
        }

        private List<string> GenerateRecommendations(RiskReport report)
        {
            var recommendations = new List<string>();
            
            if (report.VaR.VaR > 0.05)
                recommendations.Add("Consider reducing position sizes to lower overall portfolio risk");
            
            if (report.VaR.DiversificationRatio < 1.5)
                recommendations.Add("Increase diversification to reduce concentration risk");
            
            var breaches = report.StressTests.Count(st => st.BreachThreshold);
            if (breaches > report.StressTests.Count / 2)
                recommendations.Add("Portfolio shows vulnerability to stress scenarios - consider hedging strategies");
            
            if (report.FactorAttribution.ResidualRisk > report.FactorAttribution.TotalAttribution * 0.3)
                recommendations.Add("High residual risk suggests idiosyncratic factors - review individual holdings");

            return recommendations;
        }

        private List<string> GenerateTailRiskRecommendations(double cvar99)
        {
            var recommendations = new List<string>();
            
            if (Math.Abs(cvar99) > 0.20)
            {
                recommendations.Add("Consider tail risk hedging with out-of-the-money options");
                recommendations.Add("Reduce leverage and increase cash allocation");
                recommendations.Add("Implement stop-loss orders on concentrated positions");
            }
            else if (Math.Abs(cvar99) > 0.10)
            {
                recommendations.Add("Monitor for early warning signals of tail events");
                recommendations.Add("Maintain adequate liquidity for stress scenarios");
            }
            else
            {
                recommendations.Add("Current tail risk exposure is manageable");
                recommendations.Add("Continue monitoring extreme value distributions");
            }

            return recommendations;
        }

        private List<string> GenerateConcentrationAlerts(CounterpartyExposureAnalysis analysis)
        {
            var alerts = new List<string>();
            
            foreach (var metric in analysis.ConcentrationMetrics)
            {
                if (metric.Concentration > 0.25m)
                    alerts.Add($"Critical: {metric.CounterpartyName} exceeds 25% concentration ({metric.Concentration:P1})");
                else if (metric.Concentration > 0.15m)
                    alerts.Add($"Warning: {metric.CounterpartyName} concentration is elevated ({metric.Concentration:P1})");
            }

            if (analysis.PortfolioMetrics.HerfindahlIndex > 0.25m)
                alerts.Add("Portfolio concentration (HHI) is above recommended threshold");

            return alerts;
        }

        private double CalculateVariance(double[] values)
        {
            var mean = values.Average();
            return values.Sum(v => Math.Pow(v - mean, 2)) / values.Length;
        }

        private double CalculateTailIndex(List<string> symbols, int lookbackDays)
        {
            // Simplified tail index calculation
            // In production, would use Hill estimator or similar
            return 3.0; // Placeholder value
        }

        private double CalculateBlackSwanProbability(double cvar99)
        {
            // Simplified probability estimation
            return Math.Abs(cvar99) > 0.20 ? 0.05 : 
                   Math.Abs(cvar99) > 0.15 ? 0.02 : 0.01;
        }
    }

    // Request/Response Models

    public class VaRRequest
    {
        public Dictionary<string, double>? Weights { get; set; }
        public double? ConfidenceLevel { get; set; }
        public string? Method { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int? MonteCarloSimulations { get; set; }
    }

    public class StressTestRequest
    {
        public Dictionary<string, double>? Weights { get; set; }
        public List<Dictionary<string, double>>? Scenarios { get; set; }
        public List<string>? ScenarioNames { get; set; }
        public double? Threshold { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }

    public class RiskAttributionRequest
    {
        public Dictionary<string, double>? Weights { get; set; }
        public List<string>? Factors { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }

    public class RiskReportRequest
    {
        public Dictionary<string, double>? Weights { get; set; }
        public List<Dictionary<string, double>>? Scenarios { get; set; }
        public List<string>? ScenarioNames { get; set; }
        public List<string>? Factors { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }

    public class CompareRiskMeasuresRequest
    {
        public Dictionary<string, double>? Weights { get; set; }
        public double? ConfidenceLevel { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }

    public class AdvancedRiskRequest
    {
        public List<string>? Symbols { get; set; }
        public decimal? ConfidenceLevel { get; set; }
        public int? LookbackDays { get; set; }
        public int? Simulations { get; set; }
    }

    public class CounterpartyRiskRequest
    {
        public List<CounterpartyPosition>? Positions { get; set; }
        public Dictionary<string, CounterpartyInfo>? Counterparties { get; set; }
    }

    public class TailRiskRequest
    {
        public List<string>? Symbols { get; set; }
        public decimal? ConfidenceLevel { get; set; }
        public int? LookbackDays { get; set; }
    }
}
