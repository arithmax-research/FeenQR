using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using QuantResearchAgent.Core;
using MathNet.Numerics.Statistics;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.Distributions;
using MathNet.Numerics;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace QuantResearchAgent.Services.ResearchAgents;

/// <summary>
/// Statistical Pattern Detection Agent - Identifies exploitable patterns in market data
/// </summary>
public class StatisticalPatternAgentService
{
    private readonly ILogger<StatisticalPatternAgentService> _logger;
    private readonly IConfiguration _configuration;
    private readonly Kernel _kernel;
    private readonly MarketDataService _marketDataService;
    private readonly List<PatternDetection> _patternHistory = new();

    public StatisticalPatternAgentService(
        ILogger<StatisticalPatternAgentService> logger,
        IConfiguration configuration,
        Kernel kernel,
        MarketDataService marketDataService)
    {
        _logger = logger;
        _configuration = configuration;
        _kernel = kernel;
        _marketDataService = marketDataService;
    }

    public async Task<PatternAnalysisReport> DetectStatisticalPatternsAsync(
        string symbol,
        TimeSpan analysisWindow,
        PatternType[] patternTypes = null)
    {
        _logger.LogInformation("Detecting statistical patterns for {Symbol} over {Window}", symbol, analysisWindow);

        try
        {
            patternTypes ??= Enum.GetValues<PatternType>();
            
            var report = new PatternAnalysisReport
            {
                Symbol = symbol,
                AnalysisWindow = analysisWindow,
                AnalysisDate = DateTime.UtcNow
            };

            // Get market data
            var marketData = await GetMarketDataAsync(symbol, analysisWindow);
            if (marketData == null || marketData.Count < 50)
            {
                throw new InvalidOperationException("Insufficient market data for pattern analysis");
            }

            // Run pattern detection algorithms
            var detectedPatterns = new List<DetectedPattern>();

            foreach (var patternType in patternTypes)
            {
                var patterns = patternType switch
                {
                    PatternType.MeanReversion => await DetectMeanReversionPatternsAsync(marketData),
                    PatternType.Momentum => await DetectMomentumPatternsAsync(marketData),
                    PatternType.Seasonality => await DetectSeasonalityPatternsAsync(marketData),
                    PatternType.Volatility => await DetectVolatilityPatternsAsync(marketData),
                    PatternType.Correlation => await DetectCorrelationPatternsAsync(marketData),
                    PatternType.Anomaly => await DetectAnomalyPatternsAsync(marketData),
                    PatternType.Fractal => await DetectFractalPatternsAsync(marketData),
                    PatternType.Arbitrage => await DetectArbitragePatternsAsync(marketData),
                    PatternType.Regime => await DetectRegimeChangePatternsAsync(marketData),
                    PatternType.Microstructure => await DetectMicrostructurePatternsAsync(marketData),
                    _ => new List<DetectedPattern>()
                };

                detectedPatterns.AddRange(patterns);
            }

            // Rank patterns by exploitability
            report.DetectedPatterns = await RankPatternsByExploitabilityAsync(detectedPatterns);
            
            // Generate trading strategies
            report.TradingStrategies = await GenerateTradingStrategiesAsync(report.DetectedPatterns);
            
            // Calculate risk metrics
            report.RiskMetrics = CalculateRiskMetrics(marketData, report.DetectedPatterns);
            
            // Generate implementation roadmap
            report.ImplementationRoadmap = await GenerateImplementationRoadmapAsync(report);

            // Store pattern detection results
            _patternHistory.Add(new PatternDetection
            {
                Symbol = symbol,
                PatternsFound = report.DetectedPatterns.Count,
                HighConfidencePatterns = report.DetectedPatterns.Count(p => p.Confidence > 0.7),
                Timestamp = DateTime.UtcNow
            });

            _logger.LogInformation("Detected {Count} patterns for {Symbol}, {HighConf} high confidence", 
                report.DetectedPatterns.Count, symbol, report.DetectedPatterns.Count(p => p.Confidence > 0.7));

            return report;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to detect statistical patterns for {Symbol}", symbol);
            throw;
        }
    }

    private async Task<List<MarketDataPoint>> GetMarketDataAsync(string symbol, TimeSpan window)
    {
        try
        {
            // Mock data for demonstration - in production, use real market data
            var random = new Random(symbol.GetHashCode());
            var data = new List<MarketDataPoint>();
            var basePrice = 100.0;
            var currentTime = DateTime.UtcNow.Subtract(window);
            
            for (int i = 0; i < (int)(window.TotalMinutes / 5); i++) // 5-minute intervals
            {
                var volatility = 0.02; // 2% daily volatility
                var priceChange = random.NextGaussian(0, volatility / Math.Sqrt(288)); // Intraday scaling
                basePrice *= (1 + priceChange);
                
                var volume = random.Next(1000, 10000);
                
                data.Add(new MarketDataPoint
                {
                    Symbol = symbol,
                    Timestamp = currentTime.AddMinutes(i * 5),
                    Open = basePrice * (1 + random.NextGaussian(0, 0.001)),
                    High = basePrice * (1 + Math.Abs(random.NextGaussian(0, 0.002))),
                    Low = basePrice * (1 - Math.Abs(random.NextGaussian(0, 0.002))),
                    Close = basePrice,
                    Volume = volume
                });
            }
            
            return data;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get market data for {Symbol}", symbol);
            return new List<MarketDataPoint>();
        }
    }

    private async Task<List<DetectedPattern>> DetectMeanReversionPatternsAsync(List<MarketDataPoint> data)
    {
        try
        {
            var patterns = new List<DetectedPattern>();
            var prices = data.Select(d => d.Close).ToArray();
            
            // Calculate statistical measures
            var mean = prices.Mean();
            var stdDev = prices.StandardDeviation();
            var adfTest = PerformAugmentedDickeyFullerTest(prices);
            var hurstExponent = CalculateHurstExponent(prices);
            
            // Half-life of mean reversion
            var halfLife = CalculateMeanReversionHalfLife(prices);
            
            var prompt = $@"
Analyze this mean reversion pattern detection:

Statistical Measures:
- Price Mean: ${mean:F2}
- Standard Deviation: ${stdDev:F4}
- ADF Test Statistic: {adfTest:F4} (< -2.86 suggests stationarity)
- Hurst Exponent: {hurstExponent:F4} (< 0.5 suggests mean reversion)
- Half-life: {halfLife:F1} periods

Data Points: {data.Count}
Current Price: ${data.Last().Close:F2}
Distance from Mean: {((data.Last().Close - mean) / stdDev):F2} standard deviations

Evaluate mean reversion opportunity:
1. PATTERN_STRENGTH: 0.0 to 1.0 (how strong is the mean reversion signal)
2. CONFIDENCE: 0.0 to 1.0 (statistical confidence in the pattern)
3. EXPLOITABILITY: 0.0 to 1.0 (how tradeable is this pattern)
4. ENTRY_THRESHOLD: How many std devs from mean to enter
5. EXIT_STRATEGY: When to exit the trade
6. RISK_FACTORS: Key risks to this strategy

Focus on statistical significance and practical trading viability.
";

            var function = _kernel.CreateFunctionFromPrompt(prompt);
            var result = await _kernel.InvokeAsync(function);
            var analysis = result.ToString();
            
            // Extract pattern strength from AI analysis
            var strengthMatch = Regex.Match(analysis, @"PATTERN_STRENGTH[:\s]+(\d+\.?\d*)", RegexOptions.IgnoreCase);
            var confidenceMatch = Regex.Match(analysis, @"CONFIDENCE[:\s]+(\d+\.?\d*)", RegexOptions.IgnoreCase);
            var exploitabilityMatch = Regex.Match(analysis, @"EXPLOITABILITY[:\s]+(\d+\.?\d*)", RegexOptions.IgnoreCase);
            
            var strength = strengthMatch.Success ? double.Parse(strengthMatch.Groups[1].Value) : 0.5;
            var confidence = confidenceMatch.Success ? double.Parse(confidenceMatch.Groups[1].Value) : 0.5;
            var exploitability = exploitabilityMatch.Success ? double.Parse(exploitabilityMatch.Groups[1].Value) : 0.5;
            
            patterns.Add(new DetectedPattern
            {
                Type = PatternType.MeanReversion,
                Name = "Mean Reversion Pattern",
                Description = $"Statistical mean reversion with {halfLife:F1} period half-life",
                Confidence = confidence,
                Exploitability = exploitability,
                Strength = strength,
                Parameters = new Dictionary<string, object>
                {
                    ["Mean"] = mean,
                    ["StdDev"] = stdDev,
                    ["HurstExponent"] = hurstExponent,
                    ["HalfLife"] = halfLife,
                    ["ADFStatistic"] = adfTest
                },
                Analysis = analysis,
                DetectedAt = DateTime.UtcNow
            });
            
            return patterns;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to detect mean reversion patterns");
            return new List<DetectedPattern>();
        }
    }

    private async Task<List<DetectedPattern>> DetectMomentumPatternsAsync(List<MarketDataPoint> data)
    {
        try
        {
            var patterns = new List<DetectedPattern>();
            var prices = data.Select(d => d.Close).ToArray();
            var returns = CalculateReturns(prices);
            
            // Momentum indicators
            var autocorrelations = CalculateAutocorrelations(returns, 10);
            var momentumScore = CalculateMomentumScore(returns);
            var trendStrength = CalculateTrendStrength(prices);
            var volatilityAdjustedMomentum = momentumScore / returns.StandardDeviation();
            
            var prompt = $@"
Analyze this momentum pattern detection:

Momentum Analysis:
- Momentum Score: {momentumScore:F4} (positive = upward momentum)
- Trend Strength: {trendStrength:F4} (0-1 scale)
- Volatility-Adjusted Momentum: {volatilityAdjustedMomentum:F4}
- 1-lag Autocorrelation: {autocorrelations[0]:F4}
- 5-lag Autocorrelation: {autocorrelations[4]:F4}

Recent Performance:
- 10-period return: {returns.TakeLast(10).Sum():F4}
- 20-period return: {returns.TakeLast(20).Sum():F4}

Evaluate momentum opportunity:
1. PATTERN_STRENGTH: 0.0 to 1.0
2. CONFIDENCE: 0.0 to 1.0
3. EXPLOITABILITY: 0.0 to 1.0
4. MOMENTUM_PERSISTENCE: Expected duration
5. MOMENTUM_DECAY: How quickly momentum fades
6. ENTRY_SIGNALS: When to enter momentum trades

Consider momentum sustainability and reversal risks.
";

            var function = _kernel.CreateFunctionFromPrompt(prompt);
            var result = await _kernel.InvokeAsync(function);
            var analysis = result.ToString();
            
            var confidence = ExtractValueFromAnalysis(analysis, "CONFIDENCE", 0.5);
            var exploitability = ExtractValueFromAnalysis(analysis, "EXPLOITABILITY", 0.5);
            var strength = ExtractValueFromAnalysis(analysis, "PATTERN_STRENGTH", 0.5);
            
            patterns.Add(new DetectedPattern
            {
                Type = PatternType.Momentum,
                Name = "Momentum Pattern",
                Description = $"Momentum with {trendStrength:F2} trend strength",
                Confidence = confidence,
                Exploitability = exploitability,
                Strength = strength,
                Parameters = new Dictionary<string, object>
                {
                    ["MomentumScore"] = momentumScore,
                    ["TrendStrength"] = trendStrength,
                    ["VolAdjMomentum"] = volatilityAdjustedMomentum,
                    ["Autocorrelations"] = autocorrelations
                },
                Analysis = analysis,
                DetectedAt = DateTime.UtcNow
            });
            
            return patterns;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to detect momentum patterns");
            return new List<DetectedPattern>();
        }
    }

    private async Task<List<DetectedPattern>> DetectSeasonalityPatternsAsync(List<MarketDataPoint> data)
    {
        try
        {
            var patterns = new List<DetectedPattern>();
            
            // Analyze different seasonal components
            var hourlyReturns = AnalyzeIntradayPatterns(data);
            var dayOfWeekEffects = AnalyzeDayOfWeekEffects(data);
            var monthlyEffects = AnalyzeMonthlyEffects(data);
            
            var prompt = $@"
Analyze seasonal pattern detection:

Intraday Patterns:
{string.Join("\n", hourlyReturns.Select(h => $"- Hour {h.Key}: {h.Value:F4} avg return"))}

Day-of-Week Effects:
{string.Join("\n", dayOfWeekEffects.Select(d => $"- {d.Key}: {d.Value:F4} avg return"))}

Monthly Seasonality:
{string.Join("\n", monthlyEffects.Select(m => $"- {m.Key}: {m.Value:F4} avg return"))}

Evaluate seasonal opportunities:
1. PATTERN_STRENGTH: 0.0 to 1.0
2. CONFIDENCE: 0.0 to 1.0  
3. EXPLOITABILITY: 0.0 to 1.0
4. STRONGEST_EFFECT: Which seasonal effect is most pronounced
5. TRADING_WINDOW: Best times to trade based on seasonality
6. PERSISTENCE: How consistent are these seasonal effects

Focus on statistical significance of seasonal effects.
";

            var function = _kernel.CreateFunctionFromPrompt(prompt);
            var result = await _kernel.InvokeAsync(function);
            var analysis = result.ToString();
            
            var confidence = ExtractValueFromAnalysis(analysis, "CONFIDENCE", 0.5);
            var exploitability = ExtractValueFromAnalysis(analysis, "EXPLOITABILITY", 0.5);
            var strength = ExtractValueFromAnalysis(analysis, "PATTERN_STRENGTH", 0.5);
            
            patterns.Add(new DetectedPattern
            {
                Type = PatternType.Seasonality,
                Name = "Seasonal Patterns",
                Description = "Calendar-based seasonal effects in returns",
                Confidence = confidence,
                Exploitability = exploitability,
                Strength = strength,
                Parameters = new Dictionary<string, object>
                {
                    ["HourlyEffects"] = hourlyReturns,
                    ["DayOfWeekEffects"] = dayOfWeekEffects,
                    ["MonthlyEffects"] = monthlyEffects
                },
                Analysis = analysis,
                DetectedAt = DateTime.UtcNow
            });
            
            return patterns;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to detect seasonality patterns");
            return new List<DetectedPattern>();
        }
    }

    private async Task<List<DetectedPattern>> DetectVolatilityPatternsAsync(List<MarketDataPoint> data)
    {
        try
        {
            var patterns = new List<DetectedPattern>();
            var prices = data.Select(d => d.Close).ToArray();
            var returns = CalculateReturns(prices);
            
            // Volatility analysis
            var volatility = returns.StandardDeviation();
            var garchParameters = EstimateGARCH(returns);
            var volatilityClustering = DetectVolatilityClustering(returns);
            var volOfVol = CalculateVolatilityOfVolatility(returns);
            
            var prompt = $@"
Analyze volatility pattern detection:

Volatility Metrics:
- Current Volatility: {volatility:F4}
- Volatility of Volatility: {volOfVol:F4}
- GARCH Alpha: {garchParameters.Alpha:F4}
- GARCH Beta: {garchParameters.Beta:F4}
- Vol Clustering Score: {volatilityClustering:F4}

Volatility Patterns:
- Mean reverting volatility: {(garchParameters.Alpha + garchParameters.Beta < 1):ToString().ToUpper()}
- Volatility persistence: {garchParameters.Beta:F4}

Evaluate volatility trading opportunities:
1. PATTERN_STRENGTH: 0.0 to 1.0
2. CONFIDENCE: 0.0 to 1.0
3. EXPLOITABILITY: 0.0 to 1.0
4. VOL_REGIME: Current volatility regime (Low/Medium/High)
5. VOL_FORECAST: Expected volatility direction
6. TRADING_APPROACH: Best approach for current vol environment

Consider volatility forecasting accuracy and trading costs.
";

            var function = _kernel.CreateFunctionFromPrompt(prompt);
            var result = await _kernel.InvokeAsync(function);
            var analysis = result.ToString();
            
            var confidence = ExtractValueFromAnalysis(analysis, "CONFIDENCE", 0.5);
            var exploitability = ExtractValueFromAnalysis(analysis, "EXPLOITABILITY", 0.5);
            var strength = ExtractValueFromAnalysis(analysis, "PATTERN_STRENGTH", 0.5);
            
            patterns.Add(new DetectedPattern
            {
                Type = PatternType.Volatility,
                Name = "Volatility Patterns",
                Description = $"GARCH effects with {garchParameters.Beta:F2} persistence",
                Confidence = confidence,
                Exploitability = exploitability,
                Strength = strength,
                Parameters = new Dictionary<string, object>
                {
                    ["Volatility"] = volatility,
                    ["GARCHAlpha"] = garchParameters.Alpha,
                    ["GARCHBeta"] = garchParameters.Beta,
                    ["VolClustering"] = volatilityClustering,
                    ["VolOfVol"] = volOfVol
                },
                Analysis = analysis,
                DetectedAt = DateTime.UtcNow
            });
            
            return patterns;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to detect volatility patterns");
            return new List<DetectedPattern>();
        }
    }

    private async Task<List<DetectedPattern>> DetectCorrelationPatternsAsync(List<MarketDataPoint> data)
    {
        // Simplified correlation analysis - in production, you'd analyze multiple assets
        var patterns = new List<DetectedPattern>();
        
        try
        {
            var prices = data.Select(d => d.Close).ToArray();
            var volumes = data.Select(d => (double)d.Volume).ToArray();
            
            // Price-volume correlation
            var priceVolCorrelation = Correlation.Pearson(prices, volumes);
            var laggedCorrelations = CalculateLaggedCorrelations(prices, 5);
            
            var analysis = $"Price-Volume Correlation: {priceVolCorrelation:F4}\n" +
                          $"Lagged autocorrelations suggest persistence patterns.";
            
            patterns.Add(new DetectedPattern
            {
                Type = PatternType.Correlation,
                Name = "Correlation Patterns",
                Description = "Price-volume and autocorrelation patterns",
                Confidence = Math.Abs(priceVolCorrelation) > 0.3 ? 0.7 : 0.4,
                Exploitability = Math.Abs(priceVolCorrelation) > 0.5 ? 0.8 : 0.3,
                Strength = Math.Abs(priceVolCorrelation),
                Parameters = new Dictionary<string, object>
                {
                    ["PriceVolumeCorr"] = priceVolCorrelation,
                    ["LaggedCorrelations"] = laggedCorrelations
                },
                Analysis = analysis,
                DetectedAt = DateTime.UtcNow
            });
            
            return patterns;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to detect correlation patterns");
            return new List<DetectedPattern>();
        }
    }

    private async Task<List<DetectedPattern>> DetectAnomalyPatternsAsync(List<MarketDataPoint> data)
    {
        try
        {
            var patterns = new List<DetectedPattern>();
            var prices = data.Select(d => d.Close).ToArray();
            var returns = CalculateReturns(prices);
            
            // Outlier detection
            var outliers = DetectOutliers(returns, 3.0); // 3-sigma rule
            var jumpDetection = DetectJumps(returns);
            var anomalyScore = (double)outliers.Count / returns.Length;
            
            var analysis = $"Detected {outliers.Count} outliers ({anomalyScore:P2} of observations)\n" +
                          $"Jump detection identified {jumpDetection.Count} significant jumps.";
            
            patterns.Add(new DetectedPattern
            {
                Type = PatternType.Anomaly,
                Name = "Anomaly Patterns",
                Description = $"{outliers.Count} outliers detected in {data.Count} observations",
                Confidence = anomalyScore > 0.05 ? 0.8 : 0.4,
                Exploitability = anomalyScore > 0.1 ? 0.6 : 0.2,
                Strength = anomalyScore,
                Parameters = new Dictionary<string, object>
                {
                    ["OutlierCount"] = outliers.Count,
                    ["AnomalyScore"] = anomalyScore,
                    ["JumpCount"] = jumpDetection.Count
                },
                Analysis = analysis,
                DetectedAt = DateTime.UtcNow
            });
            
            return patterns;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to detect anomaly patterns");
            return new List<DetectedPattern>();
        }
    }

    private async Task<List<DetectedPattern>> DetectFractalPatternsAsync(List<MarketDataPoint> data)
    {
        try
        {
            var patterns = new List<DetectedPattern>();
            var prices = data.Select(d => d.Close).ToArray();
            
            // Fractal analysis
            var hurstExponent = CalculateHurstExponent(prices);
            var fractalDimension = 2 - hurstExponent;
            var selfSimilarity = AnalyzeSelfSimilarity(prices);
            
            var analysis = $"Hurst Exponent: {hurstExponent:F4}\n" +
                          $"Fractal Dimension: {fractalDimension:F4}\n" +
                          $"Self-similarity Score: {selfSimilarity:F4}";
            
            patterns.Add(new DetectedPattern
            {
                Type = PatternType.Fractal,
                Name = "Fractal Patterns",
                Description = $"Fractal dimension {fractalDimension:F2}",
                Confidence = Math.Abs(hurstExponent - 0.5) > 0.1 ? 0.7 : 0.4,
                Exploitability = Math.Abs(hurstExponent - 0.5) > 0.2 ? 0.6 : 0.3,
                Strength = Math.Abs(hurstExponent - 0.5),
                Parameters = new Dictionary<string, object>
                {
                    ["HurstExponent"] = hurstExponent,
                    ["FractalDimension"] = fractalDimension,
                    ["SelfSimilarity"] = selfSimilarity
                },
                Analysis = analysis,
                DetectedAt = DateTime.UtcNow
            });
            
            return patterns;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to detect fractal patterns");
            return new List<DetectedPattern>();
        }
    }

    private async Task<List<DetectedPattern>> DetectArbitragePatternsAsync(List<MarketDataPoint> data)
    {
        // Simplified arbitrage detection - in production, you'd compare multiple venues/instruments
        var patterns = new List<DetectedPattern>();
        
        try
        {
            var bidAskSpreads = data.Select(d => (d.High - d.Low) / d.Close).ToArray();
            var avgSpread = bidAskSpreads.Mean();
            var spreadVolatility = bidAskSpreads.StandardDeviation();
            
            var analysis = $"Average Bid-Ask Spread: {avgSpread:P4}\n" +
                          $"Spread Volatility: {spreadVolatility:F6}\n" +
                          $"Potential microstructure arbitrage opportunities.";
            
            patterns.Add(new DetectedPattern
            {
                Type = PatternType.Arbitrage,
                Name = "Arbitrage Patterns",
                Description = "Microstructure arbitrage opportunities",
                Confidence = avgSpread > 0.001 ? 0.6 : 0.3,
                Exploitability = avgSpread > 0.002 ? 0.7 : 0.2,
                Strength = avgSpread,
                Parameters = new Dictionary<string, object>
                {
                    ["AvgSpread"] = avgSpread,
                    ["SpreadVolatility"] = spreadVolatility
                },
                Analysis = analysis,
                DetectedAt = DateTime.UtcNow
            });
            
            return patterns;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to detect arbitrage patterns");
            return new List<DetectedPattern>();
        }
    }

    private async Task<List<DetectedPattern>> DetectRegimeChangePatternsAsync(List<MarketDataPoint> data)
    {
        try
        {
            var patterns = new List<DetectedPattern>();
            var returns = CalculateReturns(data.Select(d => d.Close).ToArray());
            
            // Simple regime detection using rolling statistics
            var regimes = DetectRegimeChanges(returns);
            var currentRegime = regimes.LastOrDefault();
            
            var analysis = $"Detected {regimes.Count} regime changes\n" +
                          $"Current regime: {currentRegime?.RegimeType ?? "Unknown"}\n" +
                          $"Regime stability: {currentRegime?.Stability ?? 0:F2}";
            
            patterns.Add(new DetectedPattern
            {
                Type = PatternType.Regime,
                Name = "Regime Change Patterns",
                Description = $"{regimes.Count} regime changes detected",
                Confidence = regimes.Count > 0 ? 0.7 : 0.3,
                Exploitability = regimes.Count > 2 ? 0.6 : 0.3,
                Strength = regimes.Count > 0 ? currentRegime?.Stability ?? 0 : 0,
                Parameters = new Dictionary<string, object>
                {
                    ["RegimeCount"] = regimes.Count,
                    ["CurrentRegime"] = currentRegime?.RegimeType ?? "Unknown"
                },
                Analysis = analysis,
                DetectedAt = DateTime.UtcNow
            });
            
            return patterns;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to detect regime change patterns");
            return new List<DetectedPattern>();
        }
    }

    private async Task<List<DetectedPattern>> DetectMicrostructurePatternsAsync(List<MarketDataPoint> data)
    {
        try
        {
            var patterns = new List<DetectedPattern>();
            
            // Volume-weighted analysis
            var vwap = CalculateVWAP(data);
            var volumeProfile = AnalyzeVolumeProfile(data);
            var orderFlowImbalance = EstimateOrderFlowImbalance(data);
            
            var analysis = $"VWAP Analysis shows {(data.Last().Close > vwap ? "above" : "below")} average\n" +
                          $"Volume profile indicates {volumeProfile}\n" +
                          $"Order flow imbalance: {orderFlowImbalance:F4}";
            
            patterns.Add(new DetectedPattern
            {
                Type = PatternType.Microstructure,
                Name = "Microstructure Patterns",
                Description = "Order flow and volume-based patterns",
                Confidence = Math.Abs(orderFlowImbalance) > 0.1 ? 0.6 : 0.4,
                Exploitability = Math.Abs(orderFlowImbalance) > 0.2 ? 0.7 : 0.3,
                Strength = Math.Abs(orderFlowImbalance),
                Parameters = new Dictionary<string, object>
                {
                    ["VWAP"] = vwap,
                    ["VolumeProfile"] = volumeProfile,
                    ["OrderFlowImbalance"] = orderFlowImbalance
                },
                Analysis = analysis,
                DetectedAt = DateTime.UtcNow
            });
            
            return patterns;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to detect microstructure patterns");
            return new List<DetectedPattern>();
        }
    }

    // Statistical calculation methods
    private double[] CalculateReturns(double[] prices)
    {
        var returns = new double[prices.Length - 1];
        for (int i = 1; i < prices.Length; i++)
        {
            returns[i - 1] = Math.Log(prices[i] / prices[i - 1]);
        }
        return returns;
    }

    private double CalculateHurstExponent(double[] prices)
    {
        // Simplified Hurst exponent calculation using R/S analysis
        var returns = CalculateReturns(prices);
        var n = returns.Length;
        
        if (n < 20) return 0.5; // Not enough data
        
        var lags = new[] { 10, 20, 50, 100 }.Where(l => l < n / 2).ToArray();
        if (lags.Length < 2) return 0.5;
        
        var rsValues = new List<double>();
        var lagValues = new List<double>();
        
        foreach (var lag in lags)
        {
            var chunks = n / lag;
            var rsSum = 0.0;
            
            for (int i = 0; i < chunks; i++)
            {
                var chunk = returns.Skip(i * lag).Take(lag).ToArray();
                var mean = chunk.Mean();
                var cumulative = chunk.Select(r => r - mean).Scan((a, b) => a + b).ToArray();
                var range = cumulative.Max() - cumulative.Min();
                var stdDev = chunk.StandardDeviation();
                
                if (stdDev > 0)
                    rsSum += range / stdDev;
            }
            
            if (chunks > 0)
            {
                rsValues.Add(Math.Log(rsSum / chunks));
                lagValues.Add(Math.Log(lag));
            }
        }
        
        if (rsValues.Count < 2) return 0.5;
        
        // Linear regression to find Hurst exponent
        var correlation = Correlation.Pearson(lagValues, rsValues);
        var slope = correlation * rsValues.StandardDeviation() / lagValues.StandardDeviation();
        
        return Math.Max(0.0, Math.Min(1.0, slope));
    }

    private double PerformAugmentedDickeyFullerTest(double[] prices)
    {
        // Simplified ADF test - returns test statistic
        var returns = CalculateReturns(prices);
        if (returns.Length < 10) return 0;
        
        var laggedPrices = prices.Take(prices.Length - 1).ToArray();
        var deltaLogPrices = returns;
        
        // Simple regression: ΔY_t = α + βY_{t-1} + error
        var correlation = Correlation.Pearson(laggedPrices, deltaLogPrices);
        var slope = correlation * deltaLogPrices.StandardDeviation() / laggedPrices.StandardDeviation();
        
        // Approximate t-statistic
        var residuals = deltaLogPrices.Zip(laggedPrices, (dy, y) => dy - slope * y).ToArray();
        var residualStdDev = residuals.StandardDeviation();
        var tStatistic = slope / (residualStdDev / Math.Sqrt(laggedPrices.Length));
        
        return tStatistic;
    }

    private double CalculateMeanReversionHalfLife(double[] prices)
    {
        var returns = CalculateReturns(prices);
        if (returns.Length < 10) return double.NaN;
        
        // Estimate AR(1) coefficient
        var laggedReturns = returns.Take(returns.Length - 1).ToArray();
        var currentReturns = returns.Skip(1).ToArray();
        
        var correlation = Correlation.Pearson(laggedReturns, currentReturns);
        var ar1Coefficient = correlation;
        
        if (ar1Coefficient >= 1.0 || ar1Coefficient <= 0) return double.PositiveInfinity;
        
        return Math.Log(0.5) / Math.Log(ar1Coefficient);
    }

    private double[] CalculateAutocorrelations(double[] series, int maxLag)
    {
        var autocorr = new double[maxLag];
        var mean = series.Mean();
        var variance = series.Variance();
        
        for (int lag = 1; lag <= maxLag; lag++)
        {
            if (lag >= series.Length) break;
            
            var covariance = 0.0;
            for (int i = lag; i < series.Length; i++)
            {
                covariance += (series[i] - mean) * (series[i - lag] - mean);
            }
            
            autocorr[lag - 1] = (covariance / (series.Length - lag)) / variance;
        }
        
        return autocorr;
    }

    private double CalculateMomentumScore(double[] returns)
    {
        if (returns.Length < 20) return 0;
        
        var recentReturns = returns.TakeLast(20).ToArray();
        var momentum = recentReturns.Sum();
        var consistency = recentReturns.Count(r => r > 0) / (double)recentReturns.Length;
        
        return momentum * consistency;
    }

    private double CalculateTrendStrength(double[] prices)
    {
        if (prices.Length < 20) return 0;
        
        var x = Enumerable.Range(0, prices.Length).Select(i => (double)i).ToArray();
        var correlation = Correlation.Pearson(x, prices);
        
        return Math.Abs(correlation);
    }

    private Dictionary<int, double> AnalyzeIntradayPatterns(List<MarketDataPoint> data)
    {
        return data
            .GroupBy(d => d.Timestamp.Hour)
            .Where(g => g.Count() > 1)
            .ToDictionary(
                g => g.Key,
                g => g.Skip(1).Select(d => Math.Log(d.Close / g.ElementAt(0).Close)).DefaultIfEmpty(0).Average()
            );
    }

    private Dictionary<string, double> AnalyzeDayOfWeekEffects(List<MarketDataPoint> data)
    {
        return data
            .Where(d => data.Any(prev => prev.Timestamp.Date == d.Timestamp.Date.AddDays(-1)))
            .GroupBy(d => d.Timestamp.DayOfWeek.ToString())
            .ToDictionary(
                g => g.Key,
                g => g.Select(d => 
                {
                    var prev = data.LastOrDefault(p => p.Timestamp.Date == d.Timestamp.Date.AddDays(-1));
                    return prev != null ? Math.Log(d.Close / prev.Close) : 0;
                }).Where(r => r != 0).DefaultIfEmpty(0).Average()
            );
    }

    private Dictionary<string, double> AnalyzeMonthlyEffects(List<MarketDataPoint> data)
    {
        return data
            .GroupBy(d => d.Timestamp.ToString("MMM"))
            .Where(g => g.Count() > 1)
            .ToDictionary(
                g => g.Key,
                g => CalculateReturns(g.OrderBy(d => d.Timestamp).Select(d => d.Close).ToArray()).DefaultIfEmpty(0).Average()
            );
    }

    private (double Alpha, double Beta) EstimateGARCH(double[] returns)
    {
        // Simplified GARCH(1,1) estimation
        var variance = returns.Variance();
        var alpha = 0.1; // Simplified assumption
        var beta = 0.8;  // Simplified assumption
        
        return (alpha, beta);
    }

    private double DetectVolatilityClustering(double[] returns)
    {
        var squaredReturns = returns.Select(r => r * r).ToArray();
        var autocorr = CalculateAutocorrelations(squaredReturns, 1);
        return autocorr.Length > 0 ? autocorr[0] : 0;
    }

    private double CalculateVolatilityOfVolatility(double[] returns)
    {
        if (returns.Length < 20) return 0;
        
        var windowSize = 10;
        var rollingVols = new List<double>();
        
        for (int i = windowSize; i < returns.Length; i++)
        {
            var window = returns.Skip(i - windowSize).Take(windowSize).ToArray();
            rollingVols.Add(window.StandardDeviation());
        }
        
        return rollingVols.StandardDeviation();
    }

    private double[] CalculateLaggedCorrelations(double[] series, int maxLag)
    {
        var correlations = new double[maxLag];
        
        for (int lag = 1; lag <= maxLag; lag++)
        {
            if (lag >= series.Length) break;
            
            var original = series.Take(series.Length - lag).ToArray();
            var lagged = series.Skip(lag).ToArray();
            
            correlations[lag - 1] = Correlation.Pearson(original, lagged);
        }
        
        return correlations;
    }

    private List<int> DetectOutliers(double[] series, double threshold)
    {
        var mean = series.Mean();
        var stdDev = series.StandardDeviation();
        var outliers = new List<int>();
        
        for (int i = 0; i < series.Length; i++)
        {
            if (Math.Abs(series[i] - mean) > threshold * stdDev)
            {
                outliers.Add(i);
            }
        }
        
        return outliers;
    }

    private List<int> DetectJumps(double[] returns)
    {
        var jumps = new List<int>();
        var threshold = 3.0 * returns.StandardDeviation();
        
        for (int i = 0; i < returns.Length; i++)
        {
            if (Math.Abs(returns[i]) > threshold)
            {
                jumps.Add(i);
            }
        }
        
        return jumps;
    }

    private double AnalyzeSelfSimilarity(double[] prices)
    {
        // Simplified self-similarity measure
        if (prices.Length < 100) return 0;
        
        var halfLength = prices.Length / 2;
        var firstHalf = prices.Take(halfLength).ToArray();
        var secondHalf = prices.Skip(halfLength).Take(halfLength).ToArray();
        
        var firstReturns = CalculateReturns(firstHalf);
        var secondReturns = CalculateReturns(secondHalf);
        
        return Correlation.Pearson(firstReturns, secondReturns);
    }

    private List<RegimeChange> DetectRegimeChanges(double[] returns)
    {
        var regimes = new List<RegimeChange>();
        var windowSize = 50;
        
        if (returns.Length < windowSize * 2) return regimes;
        
        for (int i = windowSize; i < returns.Length - windowSize; i += windowSize / 2)
        {
            var before = returns.Skip(i - windowSize).Take(windowSize).ToArray();
            var after = returns.Skip(i).Take(windowSize).ToArray();
            
            var beforeMean = before.Mean();
            var afterMean = after.Mean();
            var beforeVol = before.StandardDeviation();
            var afterVol = after.StandardDeviation();
            
            // Simple regime detection based on mean and volatility changes
            if (Math.Abs(afterMean - beforeMean) > 2 * beforeVol || 
                Math.Abs(afterVol - beforeVol) > 0.5 * beforeVol)
            {
                var regimeType = afterMean > beforeMean ? "Bull" : "Bear";
                if (afterVol > beforeVol * 1.5) regimeType += " High Vol";
                
                regimes.Add(new RegimeChange
                {
                    Index = i,
                    RegimeType = regimeType,
                    Stability = 1.0 / (1.0 + Math.Abs(afterVol - beforeVol))
                });
            }
        }
        
        return regimes;
    }

    private double CalculateVWAP(List<MarketDataPoint> data)
    {
        var totalValue = data.Sum(d => d.Close * d.Volume);
        var totalVolume = data.Sum(d => d.Volume);
        
        return totalVolume > 0 ? totalValue / totalVolume : 0;
    }

    private string AnalyzeVolumeProfile(List<MarketDataPoint> data)
    {
        var avgVolume = data.Average(d => d.Volume);
        var recentVolume = data.TakeLast(10).Average(d => d.Volume);
        
        if (recentVolume > avgVolume * 1.5) return "High volume activity";
        if (recentVolume < avgVolume * 0.5) return "Low volume activity";
        return "Normal volume activity";
    }

    private double EstimateOrderFlowImbalance(List<MarketDataPoint> data)
    {
        // Simplified order flow estimation based on price-volume relationship
        var buyVolume = 0.0;
        var sellVolume = 0.0;
        
        for (int i = 1; i < data.Count; i++)
        {
            var priceChange = data[i].Close - data[i - 1].Close;
            if (priceChange > 0)
                buyVolume += data[i].Volume;
            else if (priceChange < 0)
                sellVolume += data[i].Volume;
        }
        
        var totalVolume = buyVolume + sellVolume;
        return totalVolume > 0 ? (buyVolume - sellVolume) / totalVolume : 0;
    }

    private async Task<List<DetectedPattern>> RankPatternsByExploitabilityAsync(List<DetectedPattern> patterns)
    {
        // Sort by exploitability score (combination of confidence, strength, and exploitability)
        return patterns
            .OrderByDescending(p => p.Exploitability * p.Confidence * p.Strength)
            .ToList();
    }

    private async Task<List<string>> GenerateTradingStrategiesAsync(List<DetectedPattern> patterns)
    {
        var strategies = new List<string>();
        
        foreach (var pattern in patterns.Where(p => p.Exploitability > 0.5))
        {
            var strategy = pattern.Type switch
            {
                PatternType.MeanReversion => "Mean reversion strategy: Enter contrarian positions when price deviates > 2 std dev from mean",
                PatternType.Momentum => "Momentum strategy: Follow trend with position sizing based on momentum strength",
                PatternType.Seasonality => "Calendar strategy: Exploit seasonal patterns with time-based entry/exit rules",
                PatternType.Volatility => "Volatility strategy: Trade volatility forecasts using options or volatility products",
                PatternType.Correlation => "Pairs trading: Exploit correlation breakdowns between correlated assets",
                PatternType.Anomaly => "Anomaly strategy: Trade around outlier events with rapid entry/exit",
                PatternType.Fractal => "Fractal strategy: Use self-similarity for multi-timeframe analysis",
                PatternType.Arbitrage => "Arbitrage strategy: Exploit price discrepancies across venues/instruments",
                PatternType.Regime => "Regime strategy: Adjust strategy parameters based on detected regime",
                PatternType.Microstructure => "Microstructure strategy: Use order flow for short-term directional trades",
                _ => $"Custom strategy for {pattern.Type}"
            };
            
            strategies.Add($"{pattern.Name}: {strategy} (Confidence: {pattern.Confidence:P0})");
        }
        
        return strategies;
    }

    private RiskMetrics CalculateRiskMetrics(List<MarketDataPoint> data, List<DetectedPattern> patterns)
    {
        var returns = CalculateReturns(data.Select(d => d.Close).ToArray());
        
        return new RiskMetrics
        {
            Volatility = returns.StandardDeviation(),
            MaxDrawdown = CalculateMaxDrawdown(data.Select(d => d.Close).ToArray()),
            SharpeRatio = returns.Mean() / returns.StandardDeviation(),
            VaR95 = returns.Percentile(5),
            PatternRisk = patterns.Average(p => 1.0 - p.Confidence)
        };
    }

    private double CalculateMaxDrawdown(double[] prices)
    {
        var peak = prices[0];
        var maxDrawdown = 0.0;
        
        foreach (var price in prices)
        {
            if (price > peak)
                peak = price;
            
            var drawdown = (peak - price) / peak;
            if (drawdown > maxDrawdown)
                maxDrawdown = drawdown;
        }
        
        return maxDrawdown;
    }

    private async Task<string> GenerateImplementationRoadmapAsync(PatternAnalysisReport report)
    {
        var topPatterns = report.DetectedPatterns.Take(3).ToList();
        
        var prompt = $@"
Create an implementation roadmap for these top statistical patterns:

{string.Join("\n", topPatterns.Select(p => $"- {p.Name}: {p.Description} (Exploitability: {p.Exploitability:P0})"))}

Risk Metrics:
- Volatility: {report.RiskMetrics.Volatility:P2}
- Max Drawdown: {report.RiskMetrics.MaxDrawdown:P2}
- Sharpe Ratio: {report.RiskMetrics.SharpeRatio:F2}

Create implementation roadmap:
1. PRIORITY_RANKING: Which patterns to implement first
2. TECHNICAL_REQUIREMENTS: Infrastructure needed
3. DATA_REQUIREMENTS: Additional data sources needed
4. RISK_MANAGEMENT: Risk controls for each pattern
5. BACKTESTING_PLAN: How to validate each pattern
6. IMPLEMENTATION_TIMELINE: Suggested implementation order
7. MONITORING_METRICS: KPIs to track pattern performance

Focus on practical, actionable implementation steps.
";

        try
        {
            var function = _kernel.CreateFunctionFromPrompt(prompt);
            var result = await _kernel.InvokeAsync(function);
            return result.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate implementation roadmap");
            return "Error generating implementation roadmap.";
        }
    }

    private double ExtractValueFromAnalysis(string analysis, string key, double defaultValue)
    {
        var pattern = $@"{key}[:\s]+(\d+\.?\d*)";
        var match = Regex.Match(analysis, pattern, RegexOptions.IgnoreCase);
        
        if (match.Success && double.TryParse(match.Groups[1].Value, out var value))
        {
            return Math.Max(0.0, Math.Min(1.0, value));
        }
        
        return defaultValue;
    }

    public List<PatternDetection> GetPatternHistory() => _patternHistory.ToList();

    public async Task<string> GeneratePatternTrendAnalysisAsync(string symbol, int days = 30)
    {
        try
        {
            var recentPatterns = _patternHistory
                .Where(p => p.Symbol == symbol && p.Timestamp >= DateTime.UtcNow.AddDays(-days))
                .OrderBy(p => p.Timestamp)
                .ToList();

            if (!recentPatterns.Any())
            {
                return $"No pattern detection history available for {symbol} in the last {days} days.";
            }

            var avgPatternsFound = recentPatterns.Average(p => p.PatternsFound);
            var avgHighConfidence = recentPatterns.Average(p => p.HighConfidencePatterns);
            var trend = recentPatterns.Count > 1 ? 
                (recentPatterns.Last().PatternsFound - recentPatterns.First().PatternsFound) : 0;

            return $"Pattern Detection Trend Analysis for {symbol} ({days} days):\n" +
                   $"Average Patterns Found: {avgPatternsFound:F1}\n" +
                   $"Average High Confidence Patterns: {avgHighConfidence:F1}\n" +
                   $"Pattern Discovery Trend: {(trend > 0 ? "Increasing" : trend < 0 ? "Decreasing" : "Stable")}\n" +
                   $"Analysis Runs: {recentPatterns.Count}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate pattern trend analysis");
            return "Error generating pattern trend analysis.";
        }
    }
}

// Enums and model classes for Statistical Pattern Agent
public enum PatternType
{
    MeanReversion,
    Momentum,
    Seasonality,
    Volatility,
    Correlation,
    Anomaly,
    Fractal,
    Arbitrage,
    Regime,
    Microstructure
}

public class PatternAnalysisReport
{
    public string Symbol { get; set; } = string.Empty;
    public TimeSpan AnalysisWindow { get; set; }
    public DateTime AnalysisDate { get; set; }
    public List<DetectedPattern> DetectedPatterns { get; set; } = new();
    public List<string> TradingStrategies { get; set; } = new();
    public RiskMetrics RiskMetrics { get; set; } = new();
    public string ImplementationRoadmap { get; set; } = string.Empty;
}

public class DetectedPattern
{
    public PatternType Type { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public double Confidence { get; set; } = 0; // 0.0 to 1.0
    public double Exploitability { get; set; } = 0; // 0.0 to 1.0  
    public double Strength { get; set; } = 0; // Pattern strength metric
    public Dictionary<string, object> Parameters { get; set; } = new();
    public string Analysis { get; set; } = string.Empty;
    public DateTime DetectedAt { get; set; }
}

public class RiskMetrics
{
    public double Volatility { get; set; }
    public double MaxDrawdown { get; set; }
    public double SharpeRatio { get; set; }
    public double VaR95 { get; set; }
    public double PatternRisk { get; set; }
}

public class PatternDetection
{
    public string Symbol { get; set; } = string.Empty;
    public int PatternsFound { get; set; }
    public int HighConfidencePatterns { get; set; }
    public DateTime Timestamp { get; set; }
}

public class MarketDataPoint
{
    public string Symbol { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public double Open { get; set; }
    public double High { get; set; }
    public double Low { get; set; }
    public double Close { get; set; }
    public long Volume { get; set; }
}

public class RegimeChange
{
    public int Index { get; set; }
    public string RegimeType { get; set; } = string.Empty;
    public double Stability { get; set; }
}

// Extension methods for mathematical operations
public static class MathExtensions
{
    public static IEnumerable<T> Scan<T>(this IEnumerable<T> source, Func<T, T, T> accumulator)
    {
        using var enumerator = source.GetEnumerator();
        if (!enumerator.MoveNext()) yield break;
        
        var current = enumerator.Current;
        yield return current;
        
        while (enumerator.MoveNext())
        {
            current = accumulator(current, enumerator.Current);
            yield return current;
        }
    }
    
    public static double NextGaussian(this Random random, double mean = 0, double stdDev = 1)
    {
        // Box-Muller transform
        if (random == null) random = new Random();
        
        var u1 = 1.0 - random.NextDouble();
        var u2 = 1.0 - random.NextDouble();
        var randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
        return mean + stdDev * randStdNormal;
    }
}
