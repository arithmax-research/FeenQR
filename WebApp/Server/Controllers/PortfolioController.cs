using Microsoft.AspNetCore.Mvc;
using QuantResearchAgent.Services;
using QuantResearchAgent.Core;
using System.Text.Json;

namespace Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PortfolioController : ControllerBase
    {
        private readonly ILogger<PortfolioController> _logger;
        private readonly PortfolioService _portfolioService;
        private readonly PortfolioOptimizationService _portfolioOptimizationService;
        private readonly RiskManagementService _riskManagementService;
        private readonly MonteCarloService _monteCarloService;

        public PortfolioController(
            ILogger<PortfolioController> logger,
            PortfolioService portfolioService,
            PortfolioOptimizationService portfolioOptimizationService,
            RiskManagementService riskManagementService,
            MonteCarloService monteCarloService)
        {
            _logger = logger;
            _portfolioService = portfolioService;
            _portfolioOptimizationService = portfolioOptimizationService;
            _riskManagementService = riskManagementService;
            _monteCarloService = monteCarloService;
        }

        /// <summary>
        /// Get portfolio summary and allocation visualization
        /// </summary>
        [HttpGet("summary")]
        public async Task<IActionResult> GetPortfolioSummary()
        {
            try
            {
                _logger.LogInformation("Fetching portfolio summary");

                var positions = await _portfolioService.GetPositionsAsync();
                var metrics = await _portfolioService.CalculateMetricsAsync();
                var cashBalance = _portfolioService.GetCashBalance();

                // Calculate allocation percentages
                var allocations = new Dictionary<string, object>();
                var totalValue = metrics.TotalValue;

                foreach (var position in positions)
                {
                    var positionValue = position.Quantity * position.CurrentPrice;
                    var allocationPercent = totalValue > 0 ? (positionValue / totalValue) * 100 : 0;
                    
                    allocations[position.Symbol] = new
                    {
                        symbol = position.Symbol,
                        quantity = position.Quantity,
                        currentPrice = position.CurrentPrice,
                        averagePrice = position.AveragePrice,
                        value = positionValue,
                        allocationPercent = allocationPercent,
                        unrealizedPnL = position.UnrealizedPnL,
                        unrealizedPnLPercent = position.UnrealizedPnLPercent
                    };
                }

                var response = new
                {
                    type = "portfolio-summary",
                    timestamp = DateTime.UtcNow,
                    summary = new
                    {
                        totalValue = metrics.TotalValue,
                        cashBalance = cashBalance,
                        investedValue = totalValue - cashBalance,
                        totalPnL = metrics.TotalPnL,
                        totalPnLPercent = metrics.TotalPnLPercent,
                        dailyReturn = metrics.DailyReturn,
                        volatility = metrics.Volatility,
                        sharpeRatio = metrics.SharpeRatio,
                        maxDrawdown = metrics.MaxDrawdown,
                        positionCount = positions.Count
                    },
                    positions = allocations.Values,
                    performance = new
                    {
                        winningTrades = metrics.WinningTrades,
                        losingTrades = metrics.LosingTrades,
                        winRate = metrics.WinningTrades + metrics.LosingTrades > 0 
                            ? (double)metrics.WinningTrades / (metrics.WinningTrades + metrics.LosingTrades) * 100 
                            : 0
                    }
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching portfolio summary");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Perform portfolio risk assessment
        /// </summary>
        [HttpPost("risk-assessment")]
        public async Task<IActionResult> AssessRisk([FromBody] RiskAssessmentRequest? request)
        {
            try
            {
                _logger.LogInformation("Performing portfolio risk assessment");

                var riskAssessment = await _riskManagementService.AssessPortfolioRiskAsync();
                var metrics = await _portfolioService.CalculateMetricsAsync();
                var positions = await _portfolioService.GetPositionsAsync();

                // Calculate concentration risk
                var concentrationRisks = new List<object>();
                var totalValue = metrics.TotalValue;
                foreach (var position in positions)
                {
                    var positionValue = position.Quantity * position.CurrentPrice;
                    var concentration = totalValue > 0 ? (positionValue / totalValue) * 100 : 0;
                    
                    if (concentration > 20) // Flag positions over 20%
                    {
                        concentrationRisks.Add(new
                        {
                            symbol = position.Symbol,
                            concentration = concentration,
                            risk = "High",
                            recommendation = "Consider reducing position size"
                        });
                    }
                    else if (concentration > 10)
                    {
                        concentrationRisks.Add(new
                        {
                            symbol = position.Symbol,
                            concentration = concentration,
                            risk = "Moderate",
                            recommendation = "Monitor position size"
                        });
                    }
                }

                // Determine overall risk level
                var riskLevel = "Low";
                if (metrics.Volatility > 0.25 || metrics.MaxDrawdown > 0.20)
                    riskLevel = "High";
                else if (metrics.Volatility > 0.15 || metrics.MaxDrawdown > 0.10)
                    riskLevel = "Moderate";

                var response = new
                {
                    type = "risk-assessment",
                    timestamp = DateTime.UtcNow,
                    overallRisk = new
                    {
                        level = riskLevel,
                        assessment = riskAssessment,
                        volatility = metrics.Volatility,
                        maxDrawdown = metrics.MaxDrawdown,
                        sharpeRatio = metrics.SharpeRatio
                    },
                    concentrationRisk = new
                    {
                        numberOfHighConcentrations = concentrationRisks.Count,
                        details = concentrationRisks
                    },
                    recommendations = GenerateRiskRecommendations(metrics, positions, riskLevel)
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in risk assessment");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Get advanced portfolio analytics
        /// </summary>
        [HttpPost("analytics")]
        public async Task<IActionResult> GetPortfolioAnalytics([FromBody] AnalyticsRequest request)
        {
            try
            {
                _logger.LogInformation("Performing advanced portfolio analytics");

                var metrics = await _portfolioService.CalculateMetricsAsync();
                var positions = await _portfolioService.GetPositionsAsync();
                var metricsHistory = _portfolioService.GetMetricsHistory();

                // Calculate rolling statistics
                dynamic rollingReturns = CalculateRollingStatistics(metricsHistory, request.RollingWindow ?? 30);
                
                // Calculate sector exposure (simplified - would need sector data)
                var sectorExposure = new Dictionary<string, double>();
                foreach (var position in positions)
                {
                    var sector = DetermineSector(position.Symbol); // Simplified
                    if (!sectorExposure.ContainsKey(sector))
                        sectorExposure[sector] = 0;
                    
                    var positionValue = position.Quantity * position.CurrentPrice;
                    sectorExposure[sector] += positionValue;
                }

                // Normalize sector exposure
                var totalInvested = sectorExposure.Values.Sum();
                var normalizedSectors = sectorExposure.ToDictionary(
                    kvp => kvp.Key,
                    kvp => totalInvested > 0 ? (kvp.Value / totalInvested) * 100 : 0
                );

                var response = new
                {
                    type = "portfolio-analytics",
                    timestamp = DateTime.UtcNow,
                    performanceMetrics = new
                    {
                        totalReturn = metrics.TotalPnLPercent,
                        annualizedReturn = CalculateAnnualizedReturn(metricsHistory),
                        volatility = metrics.Volatility,
                        sharpeRatio = metrics.SharpeRatio,
                        maxDrawdown = metrics.MaxDrawdown,
                        calmarRatio = CalculateCalmarRatio(metricsHistory),
                        sortinoRatio = CalculateSortinoRatio(metricsHistory)
                    },
                    rollingMetrics = new
                    {
                        window = request.RollingWindow ?? 30,
                        returns = rollingReturns.Returns,
                        volatility = rollingReturns.Volatility,
                        sharpeRatio = rollingReturns.SharpeRatio
                    },
                    allocation = new
                    {
                        bySector = normalizedSectors,
                        byPosition = positions.Select(p => new
                        {
                            symbol = p.Symbol,
                            weight = metrics.TotalValue > 0 
                                ? (p.Quantity * p.CurrentPrice / metrics.TotalValue) * 100 
                                : 0
                        }).ToList(),
                        cashWeight = metrics.TotalValue > 0 
                            ? (_portfolioService.GetCashBalance() / metrics.TotalValue) * 100 
                            : 0
                    },
                    diversification = new
                    {
                        numberOfPositions = positions.Count,
                        effectiveNumberOfPositions = CalculateEffectivePositions(positions, metrics.TotalValue),
                        concentrationIndex = CalculateHerfindahlIndex(positions, metrics.TotalValue)
                    }
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in portfolio analytics");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Build portfolio strategy recommendations
        /// </summary>
        [HttpPost("strategy")]
        public async Task<IActionResult> BuildPortfolioStrategy([FromBody] StrategyRequest request)
        {
            try
            {
                _logger.LogInformation("Building portfolio strategy with risk tolerance: {RiskTolerance}, goal: {Goal}", 
                    request.RiskTolerance, request.InvestmentGoal);

                var currentMetrics = await _portfolioService.CalculateMetricsAsync();
                var positions = await _portfolioService.GetPositionsAsync();

                // Determine target allocation based on risk tolerance
                var targetAllocation = DetermineTargetAllocation(request.RiskTolerance);
                
                // Generate rebalancing recommendations
                var rebalancingSteps = GenerateRebalancingRecommendations(positions, currentMetrics, targetAllocation);

                // Calculate target metrics
                var targetMetrics = CalculateTargetMetrics(request.RiskTolerance, request.InvestmentGoal);

                var response = new
                {
                    type = "portfolio-strategy",
                    timestamp = DateTime.UtcNow,
                    currentState = new
                    {
                        totalValue = currentMetrics.TotalValue,
                        volatility = currentMetrics.Volatility,
                        sharpeRatio = currentMetrics.SharpeRatio,
                        numberOfPositions = positions.Count
                    },
                    strategy = new
                    {
                        riskTolerance = request.RiskTolerance,
                        investmentGoal = request.InvestmentGoal,
                        timeHorizon = request.TimeHorizon ?? "medium-term",
                        targetAllocation = targetAllocation,
                        targetMetrics = targetMetrics
                    },
                    recommendations = new
                    {
                        rebalancing = rebalancingSteps,
                        assetAllocation = GenerateAssetAllocationGuidance(request.RiskTolerance),
                        riskManagement = GenerateRiskManagementGuidance(currentMetrics, request.RiskTolerance)
                    },
                    actionPlan = new
                    {
                        immediate = GenerateImmediateActions(positions, currentMetrics, request.RiskTolerance),
                        shortTerm = GenerateShortTermActions(request),
                        longTerm = GenerateLongTermActions(request)
                    }
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error building portfolio strategy");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Run Monte Carlo portfolio simulation
        /// </summary>
        [HttpPost("monte-carlo")]
        public async Task<IActionResult> RunMonteCarloSimulation([FromBody] MonteCarloRequest request)
        {
            try
            {
                _logger.LogInformation("Running Monte Carlo simulation: {NumSimulations} simulations over {TimeHorizon} periods",
                    request.NumSimulations, request.TimeHorizon);

                var positions = await _portfolioService.GetPositionsAsync();
                var currentMetrics = await _portfolioService.CalculateMetricsAsync();

                // Prepare weights
                var weights = new Dictionary<string, double>();
                var totalValue = currentMetrics.TotalValue;
                foreach (var position in positions)
                {
                    var positionValue = position.Quantity * position.CurrentPrice;
                    weights[position.Symbol] = totalValue > 0 ? positionValue / totalValue : 0;
                }

                // Simplified expected returns and volatilities (in practice, would calculate from historical data)
                var expectedReturns = new Dictionary<string, double>();
                var volatilities = new Dictionary<string, double>();
                foreach (var position in positions)
                {
                    expectedReturns[position.Symbol] = 0.08 / 252; // 8% annual return, daily
                    volatilities[position.Symbol] = 0.20 / Math.Sqrt(252); // 20% annual vol, daily
                }

                // Simplified correlation matrix (assume 0.5 correlation)
                var correlationMatrix = new Dictionary<string, double[,]>();
                var symbols = positions.Select(p => p.Symbol).ToList();
                var size = symbols.Count;
                var corrMatrix = new double[size, size];
                for (int i = 0; i < size; i++)
                {
                    for (int j = 0; j < size; j++)
                    {
                        corrMatrix[i, j] = i == j ? 1.0 : 0.5;
                    }
                }
                correlationMatrix["portfolio"] = corrMatrix;

                // Run simulation
                var result = _monteCarloService.RunPortfolioSimulation(
                    weights,
                    expectedReturns,
                    volatilities,
                    correlationMatrix,
                    totalValue,
                    request.NumSimulations ?? 10000,
                    request.TimeHorizon ?? 252 // Default 1 year
                );

                var response = new
                {
                    type = "monte-carlo-simulation",
                    timestamp = DateTime.UtcNow,
                    parameters = new
                    {
                        initialValue = totalValue,
                        numSimulations = request.NumSimulations ?? 10000,
                        timeHorizon = request.TimeHorizon ?? 252,
                        confidenceLevel = request.ConfidenceLevel ?? 95
                    },
                    results = new
                    {
                        expectedValue = result.ExpectedValue,
                        medianValue = result.MedianValue,
                        standardDeviation = result.StandardDeviation,
                        minValue = result.MinValue,
                        maxValue = result.MaxValue,
                        valueAtRisk = result.VaR95,
                        conditionalVaR = result.CVaR95,
                        percentile5 = result.Percentile5,
                        percentile95 = result.Percentile95
                    },
                    probabilityDistribution = new
                    {
                        bins = CreateHistogramBins(result.AllFinalValues, 50),
                        paths = result.SimulationPaths.Take(10).ToList() // Return 10 sample paths
                    },
                    insights = new
                    {
                        expectedReturn = ((result.ExpectedValue / totalValue) - 1) * 100,
                        probabilityOfProfit = CalculateProbabilityOfProfit(result.AllFinalValues, totalValue),
                        bestCase = $"Top 5% outcome: ${result.Percentile95:F2}",
                        worstCase = $"Bottom 5% outcome: ${result.Percentile5:F2}",
                        riskReward = result.ExpectedValue / Math.Max(result.StandardDeviation, 0.001)
                    }
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Monte Carlo simulation");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Optimize portfolio allocation
        /// </summary>
        [HttpPost("optimize")]
        public async Task<IActionResult> OptimizePortfolio([FromBody] OptimizationRequest request)
        {
            try
            {
                _logger.LogInformation("Optimizing portfolio for symbols: {Symbols}", 
                    string.Join(", ", request.Symbols ?? Array.Empty<string>()));

                var symbols = request.Symbols;
                if (symbols == null || symbols.Length == 0)
                {
                    // Use current portfolio positions
                    var positions = await _portfolioService.GetPositionsAsync();
                    symbols = positions.Select(p => p.Symbol).ToArray();
                }

                if (symbols.Length < 2)
                {
                    return BadRequest(new { error = "Need at least 2 symbols for optimization" });
                }

                // Run optimization
                var optimizationResult = await _portfolioOptimizationService.OptimizePortfolioAsync(
                    symbols,
                    request.InitialWeights,
                    request.LookbackDays ?? 252
                );

                // Compare with current allocation
                var currentPositions = await _portfolioService.GetPositionsAsync();
                var currentMetrics = await _portfolioService.CalculateMetricsAsync();
                var currentWeights = CalculateCurrentWeights(currentPositions, currentMetrics.TotalValue);

                var rebalancingSteps = new List<object>();
                foreach (var symbol in symbols)
                {
                    var currentWeight = currentWeights.GetValueOrDefault(symbol, 0);
                    var optimalWeight = optimizationResult.OptimizedWeights.GetValueOrDefault(symbol, 0) * 100;
                    var difference = optimalWeight - (currentWeight * 100);

                    if (Math.Abs(difference) > 1) // Only show if difference > 1%
                    {
                        rebalancingSteps.Add(new
                        {
                            symbol = symbol,
                            currentWeight = currentWeight * 100,
                            optimalWeight = optimalWeight,
                            difference = difference,
                            action = difference > 0 ? "BUY" : "SELL",
                            amount = Math.Abs(difference)
                        });
                    }
                }

                var response = new
                {
                    type = "portfolio-optimization",
                    timestamp = DateTime.UtcNow,
                    optimization = new
                    {
                        method = "Mean-Variance (Equal-Weight)",
                        symbols = optimizationResult.Tickers,
                        optimalWeights = optimizationResult.OptimizedWeights.ToDictionary(
                            kvp => kvp.Key, 
                            kvp => kvp.Value * 100
                        ),
                        expectedReturn = optimizationResult.ExpectedReturn * 100,
                        expectedRisk = optimizationResult.Risk * 100,
                        sharpeRatio = optimizationResult.SharpeRatio
                    },
                    currentAllocation = currentWeights.ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value * 100
                    ),
                    rebalancing = new
                    {
                        required = rebalancingSteps.Count > 0,
                        steps = rebalancingSteps
                    },
                    expectedImprovement = new
                    {
                        returnImprovement = (optimizationResult.ExpectedReturn - currentMetrics.DailyReturn) * 100,
                        sharpeImprovement = optimizationResult.SharpeRatio - currentMetrics.SharpeRatio
                    }
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in portfolio optimization");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // Helper methods
        private List<string> GenerateRiskRecommendations(PortfolioMetrics metrics, List<Position> positions, string riskLevel)
        {
            var recommendations = new List<string>();

            if (riskLevel == "High")
            {
                recommendations.Add("Consider reducing position sizes to lower overall portfolio volatility");
                recommendations.Add("Implement stop-loss orders to protect against further downside");
            }

            if (metrics.MaxDrawdown > 0.15)
            {
                recommendations.Add($"Maximum drawdown of {metrics.MaxDrawdown:P} is elevated - review risk management strategy");
            }

            if (positions.Count < 5)
            {
                recommendations.Add("Portfolio is under-diversified - consider adding more positions");
            }

            if (metrics.SharpeRatio < 1.0)
            {
                recommendations.Add("Risk-adjusted returns can be improved - review position selection");
            }

            return recommendations;
        }

        private object CalculateRollingStatistics(List<PortfolioMetrics> history, int window)
        {
            if (history.Count < window)
            {
                return new { Returns = 0, Volatility = 0, SharpeRatio = 0 };
            }

            var recentMetrics = history.TakeLast(window).ToList();
            var returns = recentMetrics.Select(m => m.DailyReturn).ToList();
            var avgReturn = returns.Average();
            var volatility = CalculateStandardDeviation(returns);
            var sharpe = volatility > 0 ? (avgReturn - 0.02/252) / volatility : 0;

            return new
            {
                Returns = avgReturn * 252 * 100, // Annualized
                Volatility = volatility * Math.Sqrt(252) * 100,
                SharpeRatio = sharpe * Math.Sqrt(252)
            };
        }

        private double CalculateStandardDeviation(List<double> values)
        {
            if (values.Count < 2) return 0;
            var avg = values.Average();
            var sumSquares = values.Sum(v => Math.Pow(v - avg, 2));
            return Math.Sqrt(sumSquares / values.Count);
        }

        private double CalculateAnnualizedReturn(List<PortfolioMetrics> history)
        {
            if (history.Count < 2) return 0;
            var firstValue = history.First().TotalValue;
            var lastValue = history.Last().TotalValue;
            return ((lastValue / firstValue) - 1) * 100;
        }

        private double CalculateCalmarRatio(List<PortfolioMetrics> history)
        {
            if (history.Count < 2) return 0;
            var annualizedReturn = CalculateAnnualizedReturn(history);
            var maxDrawdown = history.Max(m => m.MaxDrawdown);
            return maxDrawdown > 0 ? annualizedReturn / (maxDrawdown * 100) : 0;
        }

        private double CalculateSortinoRatio(List<PortfolioMetrics> history)
        {
            if (history.Count < 2) return 0;
            var returns = history.Select(m => m.DailyReturn).ToList();
            var avgReturn = returns.Average();
            var downside = returns.Where(r => r < 0).ToList();
            if (downside.Count == 0) return 0;
            
            var downsideDeviation = Math.Sqrt(downside.Sum(r => r * r) / downside.Count);
            return downsideDeviation > 0 ? (avgReturn - 0.02/252) / downsideDeviation * Math.Sqrt(252) : 0;
        }

        private string DetermineSector(string symbol)
        {
            // Simplified sector mapping - in production, use actual sector data
            var techStocks = new[] { "AAPL", "MSFT", "GOOGL", "NVDA", "META", "TSLA" };
            var financials = new[] { "JPM", "BAC", "GS", "MS", "WFC" };
            
            if (techStocks.Contains(symbol)) return "Technology";
            if (financials.Contains(symbol)) return "Financials";
            return "Other";
        }

        private double CalculateEffectivePositions(List<Position> positions, double totalValue)
        {
            if (totalValue == 0) return 0;
            var weights = positions.Select(p => (p.Quantity * p.CurrentPrice) / totalValue).ToList();
            var sumSquares = weights.Sum(w => w * w);
            return sumSquares > 0 ? 1.0 / sumSquares : 0;
        }

        private double CalculateHerfindahlIndex(List<Position> positions, double totalValue)
        {
            if (totalValue == 0) return 0;
            var weights = positions.Select(p => (p.Quantity * p.CurrentPrice) / totalValue).ToList();
            return weights.Sum(w => w * w);
        }

        private object DetermineTargetAllocation(string riskTolerance)
        {
            return riskTolerance.ToLower() switch
            {
                "conservative" => new { Stocks = 40, Bonds = 50, Cash = 10 },
                "moderate" => new { Stocks = 60, Bonds = 30, Cash = 10 },
                "aggressive" => new { Stocks = 80, Bonds = 15, Cash = 5 },
                _ => new { Stocks = 60, Bonds = 30, Cash = 10 }
            };
        }

        private List<object> GenerateRebalancingRecommendations(List<Position> positions, PortfolioMetrics metrics, object targetAllocation)
        {
            return new List<object>
            {
                new { Step = 1, Action = "Review current allocation against target", Status = "Pending" },
                new { Step = 2, Action = "Identify overweight and underweight positions", Status = "Pending" },
                new { Step = 3, Action = "Execute rebalancing trades", Status = "Pending" }
            };
        }

        private object CalculateTargetMetrics(string riskTolerance, string investmentGoal)
        {
            return riskTolerance.ToLower() switch
            {
                "conservative" => new { TargetReturn = 6.0, TargetVolatility = 8.0, MinSharpe = 0.75 },
                "moderate" => new { TargetReturn = 10.0, TargetVolatility = 12.0, MinSharpe = 0.83 },
                "aggressive" => new { TargetReturn = 15.0, TargetVolatility = 18.0, MinSharpe = 0.83 },
                _ => new { TargetReturn = 10.0, TargetVolatility = 12.0, MinSharpe = 0.83 }
            };
        }

        private List<string> GenerateAssetAllocationGuidance(string riskTolerance)
        {
            return riskTolerance.ToLower() switch
            {
                "conservative" => new List<string>
                {
                    "Focus on blue-chip dividend stocks",
                    "Include high-quality bonds",
                    "Maintain adequate cash reserves"
                },
                "aggressive" => new List<string>
                {
                    "Emphasize growth stocks",
                    "Consider sector-specific ETFs",
                    "Include emerging market exposure"
                },
                _ => new List<string>
                {
                    "Balance between growth and value",
                    "Diversify across sectors",
                    "Include both domestic and international exposure"
                }
            };
        }

        private List<string> GenerateRiskManagementGuidance(PortfolioMetrics metrics, string riskTolerance)
        {
            var guidance = new List<string>();
            
            if (metrics.Volatility > 0.20)
                guidance.Add("Consider hedging strategies to reduce volatility");
            
            guidance.Add("Regularly review and rebalance portfolio");
            guidance.Add("Use stop-loss orders for individual positions");
            guidance.Add("Monitor correlation between positions");
            
            return guidance;
        }

        private List<string> GenerateImmediateActions(List<Position> positions, PortfolioMetrics metrics, string riskTolerance)
        {
            var actions = new List<string>();
            
            if (positions.Count < 5)
                actions.Add("Increase diversification by adding 2-3 more positions");
            
            if (metrics.MaxDrawdown > 0.15)
                actions.Add("Review and adjust position sizes to reduce risk");
            
            return actions;
        }

        private List<string> GenerateShortTermActions(StrategyRequest request)
        {
            return new List<string>
            {
                "Rebalance portfolio quarterly",
                "Review performance against benchmarks",
                "Adjust positions based on market conditions"
            };
        }

        private List<string> GenerateLongTermActions(StrategyRequest request)
        {
            return new List<string>
            {
                "Maintain consistent investment strategy",
                "Increase contributions during market downturns",
                "Review and adjust risk tolerance annually"
            };
        }

        private List<object> CreateHistogramBins(List<double> values, int numBins)
        {
            if (values.Count == 0) return new List<object>();

            var min = values.Min();
            var max = values.Max();
            var binWidth = (max - min) / numBins;
            var bins = new List<object>();

            for (int i = 0; i < numBins; i++)
            {
                var binStart = min + (i * binWidth);
                var binEnd = binStart + binWidth;
                var count = values.Count(v => v >= binStart && v < binEnd);
                
                bins.Add(new
                {
                    Range = $"{binStart:F0}-{binEnd:F0}",
                    Count = count,
                    Frequency = (double)count / values.Count
                });
            }

            return bins;
        }

        private double CalculateProbabilityOfProfit(List<double> values, double initialValue)
        {
            var profitableOutcomes = values.Count(v => v > initialValue);
            return (double)profitableOutcomes / values.Count * 100;
        }

        private Dictionary<string, double> CalculateCurrentWeights(List<Position> positions, double totalValue)
        {
            var weights = new Dictionary<string, double>();
            foreach (var position in positions)
            {
                var positionValue = position.Quantity * position.CurrentPrice;
                weights[position.Symbol] = totalValue > 0 ? positionValue / totalValue : 0;
            }
            return weights;
        }
    }

    // Request/Response models
    public class RiskAssessmentRequest
    {
        public bool IncludeStressTesting { get; set; } = false;
    }

    public class AnalyticsRequest
    {
        public int? RollingWindow { get; set; } = 30;
        public bool IncludeSectorAnalysis { get; set; } = true;
    }

    public class StrategyRequest
    {
        public string RiskTolerance { get; set; } = "moderate"; // conservative, moderate, aggressive
        public string InvestmentGoal { get; set; } = "growth"; // growth, income, balanced
        public string? TimeHorizon { get; set; } = "medium-term"; // short-term, medium-term, long-term
    }

    public class MonteCarloRequest
    {
        public int? NumSimulations { get; set; } = 10000;
        public int? TimeHorizon { get; set; } = 252; // Trading days (1 year)
        public double? ConfidenceLevel { get; set; } = 95;
    }

    public class OptimizationRequest
    {
        public string[]? Symbols { get; set; }
        public double[]? InitialWeights { get; set; }
        public int? LookbackDays { get; set; } = 252;
        public string OptimizationMethod { get; set; } = "mean-variance"; // mean-variance, risk-parity, black-litterman
    }
}
