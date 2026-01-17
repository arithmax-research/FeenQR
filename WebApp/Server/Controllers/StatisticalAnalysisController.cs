using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using MathNet.Numerics.Statistics;
using MathNet.Numerics.LinearAlgebra;

namespace QuantResearchAgent.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StatisticalAnalysisController : ControllerBase
    {
        private readonly ILogger<StatisticalAnalysisController> _logger;
        private readonly IHttpClientFactory _httpClientFactory;

        public StatisticalAnalysisController(
            ILogger<StatisticalAnalysisController> logger,
            IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
        }

        // 1. Statistical Hypothesis Testing
        [HttpGet("statistical-test")]
        public async Task<IActionResult> StatisticalTest(
            [FromQuery] string symbol,
            [FromQuery] string testType = "t-test", // t-test, z-test, chi-square, anova
            [FromQuery] int days = 252)
        {
            try
            {
                var data = await FetchHistoricalData(symbol, days);
                if (data == null || data.Length < 2)
                    return BadRequest("Insufficient data for statistical testing");

                var returns = CalculateReturns(data);
                
                var result = testType.ToLower() switch
                {
                    "t-test" => PerformTTest(returns),
                    "z-test" => PerformZTest(returns),
                    "jarque-bera" => PerformJarqueBeraTest(returns),
                    "shapiro-wilk" => PerformShapiroWilkTest(returns),
                    _ => PerformTTest(returns)
                };

                return Ok(new
                {
                    symbol,
                    testType,
                    dataPoints = returns.Length,
                    result
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing statistical test for {Symbol}", symbol);
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // 2. Hypothesis Testing
        [HttpGet("hypothesis-test")]
        public async Task<IActionResult> HypothesisTest(
            [FromQuery] string symbol,
            [FromQuery] double nullHypothesis = 0.0,
            [FromQuery] string alternative = "two-sided", // two-sided, greater, less
            [FromQuery] double alpha = 0.05,
            [FromQuery] int days = 252)
        {
            try
            {
                var data = await FetchHistoricalData(symbol, days);
                if (data == null || data.Length < 2)
                    return BadRequest("Insufficient data for hypothesis testing");

                var returns = CalculateReturns(data);
                var mean = returns.Mean();
                var stdDev = returns.StandardDeviation();
                var n = returns.Length;
                
                // Perform t-test
                var tStatistic = (mean - nullHypothesis) / (stdDev / Math.Sqrt(n));
                var degreesOfFreedom = n - 1;
                
                // Calculate p-value based on alternative hypothesis
                double pValue = CalculatePValue(tStatistic, degreesOfFreedom, alternative);
                bool rejectNull = pValue < alpha;

                return Ok(new
                {
                    symbol,
                    hypothesis = new
                    {
                        nullValue = nullHypothesis,
                        alternative,
                        alpha
                    },
                    statistics = new
                    {
                        sampleMean = mean,
                        sampleStdDev = stdDev,
                        sampleSize = n,
                        tStatistic,
                        degreesOfFreedom,
                        pValue,
                        criticalValue = GetCriticalValue(alpha, degreesOfFreedom, alternative)
                    },
                    conclusion = new
                    {
                        rejectNull,
                        interpretation = rejectNull 
                            ? $"Reject null hypothesis at {alpha * 100}% significance level"
                            : $"Fail to reject null hypothesis at {alpha * 100}% significance level"
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing hypothesis test for {Symbol}", symbol);
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // 3. Power Analysis
        [HttpGet("power-analysis")]
        public async Task<IActionResult> PowerAnalysis(
            [FromQuery] string symbol,
            [FromQuery] double effectSize = 0.5,
            [FromQuery] double alpha = 0.05,
            [FromQuery] double power = 0.8,
            [FromQuery] int days = 252)
        {
            try
            {
                var data = await FetchHistoricalData(symbol, days);
                if (data == null || data.Length < 2)
                    return BadRequest("Insufficient data for power analysis");

                var returns = CalculateReturns(data);
                var stdDev = returns.StandardDeviation();
                
                // Calculate required sample size for given power
                var zAlpha = GetZScore(alpha / 2); // two-tailed
                var zBeta = GetZScore(1 - power);
                var requiredN = Math.Pow((zAlpha + zBeta) * stdDev / effectSize, 2);

                // Calculate achieved power with current sample size
                var currentN = returns.Length;
                var ncp = effectSize * Math.Sqrt(currentN) / stdDev; // non-centrality parameter
                var achievedPower = 1 - CalculateBeta(ncp, alpha);

                return Ok(new
                {
                    symbol,
                    parameters = new
                    {
                        effectSize,
                        alpha,
                        targetPower = power,
                        currentSampleSize = currentN
                    },
                    analysis = new
                    {
                        requiredSampleSize = Math.Ceiling(requiredN),
                        achievedPower,
                        standardDeviation = stdDev,
                        recommendation = currentN >= requiredN 
                            ? "Current sample size is sufficient"
                            : $"Collect {Math.Ceiling(requiredN - currentN)} more data points"
                    },
                    interpretation = new
                    {
                        message = $"With {currentN} observations, the test has {achievedPower:P2} power to detect an effect size of {effectSize}",
                        confidence = $"At {alpha * 100}% significance level"
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing power analysis for {Symbol}", symbol);
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // 4. Time Series Analysis
        [HttpGet("time-series-analysis")]
        public async Task<IActionResult> TimeSeriesAnalysis(
            [FromQuery] string symbol,
            [FromQuery] int days = 252)
        {
            try
            {
                var data = await FetchHistoricalData(symbol, days);
                if (data == null || data.Length < 2)
                    return BadRequest("Insufficient data for time series analysis");

                var returns = CalculateReturns(data);
                
                // Descriptive statistics
                var mean = returns.Mean();
                var variance = returns.Variance();
                var stdDev = returns.StandardDeviation();
                var skewness = returns.Skewness();
                var kurtosis = returns.Kurtosis();
                
                // Autocorrelation
                var acf = CalculateACF(returns, 20);
                var pacf = CalculatePACF(returns, 20);
                
                // Trend analysis
                var trend = FitLinearTrend(data);
                
                return Ok(new
                {
                    symbol,
                    period = $"{days} days",
                    descriptiveStats = new
                    {
                        mean,
                        variance,
                        standardDeviation = stdDev,
                        skewness,
                        kurtosis,
                        excessKurtosis = kurtosis - 3
                    },
                    autocorrelation = new
                    {
                        acf = acf.Take(10).ToArray(),
                        pacf = pacf.Take(10).ToArray(),
                        significantLags = FindSignificantLags(acf)
                    },
                    trend = new
                    {
                        slope = trend.slope,
                        intercept = trend.intercept,
                        rSquared = trend.rSquared,
                        direction = trend.slope > 0 ? "Upward" : trend.slope < 0 ? "Downward" : "Flat"
                    },
                    properties = new
                    {
                        volatility = stdDev * Math.Sqrt(252), // Annualized
                        meanReversion = Math.Abs(acf[1]) < 0.5 ? "Possible" : "Unlikely",
                        distribution = Math.Abs(kurtosis - 3) > 1 ? "Non-normal (heavy tails)" : "Approximately normal"
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing time series analysis for {Symbol}", symbol);
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // 5. Stationarity Test
        [HttpGet("stationarity-test")]
        public async Task<IActionResult> StationarityTest(
            [FromQuery] string symbol,
            [FromQuery] string testType = "adf", // adf, kpss, pp
            [FromQuery] int days = 252)
        {
            try
            {
                var data = await FetchHistoricalData(symbol, days);
                if (data == null || data.Length < 10)
                    return BadRequest("Insufficient data for stationarity testing");

                var returns = CalculateReturns(data);
                
                // Augmented Dickey-Fuller test
                var adfResult = PerformADFTest(returns);
                
                // KPSS test
                var kpssResult = PerformKPSSTest(returns);
                
                // Rolling statistics
                var rollingStats = CalculateRollingStatistics(returns, 30);

                return Ok(new
                {
                    symbol,
                    tests = new
                    {
                        augmentedDickeyFuller = new
                        {
                            testStatistic = adfResult.statistic,
                            pValue = adfResult.pValue,
                            criticalValues = adfResult.criticalValues,
                            isStationary = adfResult.pValue < 0.05,
                            interpretation = adfResult.pValue < 0.05 
                                ? "Series is stationary (reject unit root)" 
                                : "Series is non-stationary (unit root present)"
                        },
                        kpss = new
                        {
                            testStatistic = kpssResult.statistic,
                            pValue = kpssResult.pValue,
                            criticalValues = kpssResult.criticalValues,
                            isStationary = kpssResult.pValue > 0.05,
                            interpretation = kpssResult.pValue > 0.05 
                                ? "Series is stationary" 
                                : "Series is non-stationary"
                        }
                    },
                    rollingStatistics = new
                    {
                        meanStability = rollingStats.meanStability,
                        varianceStability = rollingStats.varianceStability,
                        coefficientOfVariation = rollingStats.cv
                    },
                    conclusion = new
                    {
                        overallAssessment = DetermineStationarity(adfResult, kpssResult),
                        recommendation = GetStationarityRecommendation(adfResult, kpssResult)
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing stationarity test for {Symbol}", symbol);
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // 6. Autocorrelation Analysis
        [HttpGet("autocorrelation-analysis")]
        public async Task<IActionResult> AutocorrelationAnalysis(
            [FromQuery] string symbol,
            [FromQuery] int maxLags = 40,
            [FromQuery] int days = 252)
        {
            try
            {
                var data = await FetchHistoricalData(symbol, days);
                if (data == null || data.Length < maxLags)
                    return BadRequest("Insufficient data for autocorrelation analysis");

                var returns = CalculateReturns(data);
                
                // Calculate ACF and PACF
                var acf = CalculateACF(returns, maxLags);
                var pacf = CalculatePACF(returns, maxLags);
                
                // Ljung-Box test
                var ljungBox = PerformLjungBoxTest(returns, Math.Min(20, maxLags));
                
                // Identify significant lags
                var confidenceInterval = 1.96 / Math.Sqrt(returns.Length);
                var significantACF = FindSignificantLags(acf, confidenceInterval);
                var significantPACF = FindSignificantLags(pacf, confidenceInterval);

                return Ok(new
                {
                    symbol,
                    analysis = new
                    {
                        acf,
                        pacf,
                        confidenceInterval,
                        significantLags = new
                        {
                            acf = significantACF,
                            pacf = significantPACF
                        }
                    },
                    ljungBoxTest = new
                    {
                        statistic = ljungBox.statistic,
                        pValue = ljungBox.pValue,
                        degreesOfFreedom = ljungBox.df,
                        conclusion = ljungBox.pValue < 0.05 
                            ? "Significant autocorrelation detected" 
                            : "No significant autocorrelation"
                    },
                    interpretation = new
                    {
                        arOrder = EstimateAROrder(pacf, confidenceInterval),
                        maOrder = EstimateMAOrder(acf, confidenceInterval),
                        suggestedModel = SuggestARIMAModel(acf, pacf, confidenceInterval),
                        meanReversion = Math.Abs(acf[1]) < 0.5 ? "Evidence of mean reversion" : "Weak mean reversion"
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing autocorrelation analysis for {Symbol}", symbol);
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // 7. Seasonal Decomposition
        [HttpGet("seasonal-decomposition")]
        public async Task<IActionResult> SeasonalDecomposition(
            [FromQuery] string symbol,
            [FromQuery] int period = 21, // Trading days in a month
            [FromQuery] string model = "additive", // additive or multiplicative
            [FromQuery] int days = 504) // 2 years
        {
            try
            {
                var data = await FetchHistoricalData(symbol, days);
                if (data == null || data.Length < period * 2)
                    return BadRequest($"Insufficient data for seasonal decomposition. Need at least {period * 2} days");

                var decomposition = PerformSeasonalDecomposition(data, period, model);
                
                // Calculate strength of seasonality
                var seasonalStrength = CalculateSeasonalStrength(decomposition);
                var trendStrength = CalculateTrendStrength(decomposition);

                return Ok(new
                {
                    symbol,
                    parameters = new
                    {
                        period,
                        model,
                        dataPoints = data.Length
                    },
                    components = new
                    {
                        trend = decomposition.trend,
                        seasonal = decomposition.seasonal,
                        residual = decomposition.residual
                    },
                    strength = new
                    {
                        seasonal = seasonalStrength,
                        trend = trendStrength,
                        seasonalityPresent = seasonalStrength > 0.3,
                        trendPresent = trendStrength > 0.3
                    },
                    analysis = new
                    {
                        seasonalPattern = AnalyzeSeasonalPattern(decomposition.seasonal, period),
                        trendDirection = DetermineTrendDirection(decomposition.trend),
                        residualProperties = AnalyzeResiduals(decomposition.residual)
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing seasonal decomposition for {Symbol}", symbol);
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // 8. Stock Time Series Analysis
        [HttpGet("ts-analysis")]
        public async Task<IActionResult> StockTimeSeriesAnalysis(
            [FromQuery] string symbol,
            [FromQuery] int days = 252)
        {
            try
            {
                var data = await FetchHistoricalData(symbol, days);
                if (data == null || data.Length < 30)
                    return BadRequest("Insufficient data for comprehensive time series analysis");

                var returns = CalculateReturns(data);
                
                // Perform multiple analyses
                var descriptive = GetDescriptiveStats(returns);
                var acf = CalculateACF(returns, 20);
                var adfTest = PerformADFTest(returns);
                var volatilityMetrics = CalculateVolatilityMetrics(returns);
                var distributionMetrics = AnalyzeDistribution(returns);
                
                // Calculate individual values for trading characteristics
                var dailyVol = returns.StandardDeviation();
                var annualizedVol = dailyVol * Math.Sqrt(252);
                var kurtosis = returns.Kurtosis();
                
                return Ok(new
                {
                    symbol,
                    period = $"{days} days ({data.Length} observations)",
                    priceStats = new
                    {
                        current = data[^1],
                        min = data.Min(),
                        max = data.Max(),
                        range = data.Max() - data.Min(),
                        percentChange = ((data[^1] - data[0]) / data[0]) * 100
                    },
                    returnStats = descriptive,
                    stationarity = new
                    {
                        adfStatistic = adfTest.statistic,
                        pValue = adfTest.pValue,
                        isStationary = adfTest.pValue < 0.05
                    },
                    autocorrelation = new
                    {
                        lag1 = acf[1],
                        lag5 = acf.Length > 5 ? acf[5] : 0,
                        significantLags = FindSignificantLags(acf).Length
                    },
                    volatility = volatilityMetrics,
                    distribution = distributionMetrics,
                    tradingCharacteristics = new
                    {
                        meanReversion = Math.Abs(acf[1]) < 0.5,
                        trending = adfTest.pValue > 0.05,
                        highVolatility = annualizedVol > 0.3,
                        normalReturns = Math.Abs(kurtosis - 3) < 1
                    },
                    recommendation = GenerateTradingRecommendation(returns, acf, adfTest, volatilityMetrics)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing stock time series analysis for {Symbol}", symbol);
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // Helper Methods
        private async Task<double[]> FetchHistoricalData(string symbol, int days)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                var endDate = DateTime.Now;
                var startDate = endDate.AddDays(-days - 100); // Extra buffer
                
                var url = $"https://query1.finance.yahoo.com/v8/finance/chart/{symbol}?period1={((DateTimeOffset)startDate).ToUnixTimeSeconds()}&period2={((DateTimeOffset)endDate).ToUnixTimeSeconds()}&interval=1d";
                
                var response = await client.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                    return Array.Empty<double>();

                var content = await response.Content.ReadAsStringAsync();
                var json = JsonDocument.Parse(content);
                
                var quotes = json.RootElement
                    .GetProperty("chart")
                    .GetProperty("result")[0]
                    .GetProperty("indicators")
                    .GetProperty("quote")[0]
                    .GetProperty("close");

                var prices = new List<double>();
                foreach (var price in quotes.EnumerateArray())
                {
                    if (price.ValueKind != JsonValueKind.Null)
                        prices.Add(price.GetDouble());
                }

                return prices.TakeLast(days).ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching historical data for {Symbol}", symbol);
                return Array.Empty<double>();
            }
        }

        private double[] CalculateReturns(double[] prices)
        {
            var returns = new double[prices.Length - 1];
            for (int i = 0; i < returns.Length; i++)
            {
                returns[i] = (prices[i + 1] - prices[i]) / prices[i];
            }
            return returns;
        }

        private object PerformTTest(double[] data)
        {
            var mean = data.Mean();
            var stdDev = data.StandardDeviation();
            var n = data.Length;
            var tStatistic = mean / (stdDev / Math.Sqrt(n));
            var pValue = CalculatePValue(tStatistic, n - 1, "two-sided");

            return new
            {
                testName = "One-Sample T-Test",
                nullHypothesis = "Mean = 0",
                tStatistic,
                degreesOfFreedom = n - 1,
                pValue,
                mean,
                standardError = stdDev / Math.Sqrt(n),
                confidenceInterval95 = new
                {
                    lower = mean - 1.96 * (stdDev / Math.Sqrt(n)),
                    upper = mean + 1.96 * (stdDev / Math.Sqrt(n))
                }
            };
        }

        private object PerformZTest(double[] data)
        {
            var mean = data.Mean();
            var stdDev = data.StandardDeviation();
            var n = data.Length;
            var zStatistic = mean / (stdDev / Math.Sqrt(n));
            var pValue = 2 * (1 - NormalCDF(Math.Abs(zStatistic)));

            return new
            {
                testName = "Z-Test",
                nullHypothesis = "Mean = 0",
                zStatistic,
                pValue,
                mean,
                standardError = stdDev / Math.Sqrt(n)
            };
        }

        private object PerformJarqueBeraTest(double[] data)
        {
            var n = data.Length;
            var skewness = data.Skewness();
            var kurtosis = data.Kurtosis();
            var jbStatistic = (n / 6.0) * (Math.Pow(skewness, 2) + Math.Pow(kurtosis - 3, 2) / 4.0);
            var pValue = 1 - ChiSquareCDF(jbStatistic, 2);

            return new
            {
                testName = "Jarque-Bera Normality Test",
                nullHypothesis = "Data is normally distributed",
                jbStatistic,
                pValue,
                skewness,
                kurtosis,
                isNormal = pValue > 0.05
            };
        }

        private object PerformShapiroWilkTest(double[] data)
        {
            // Simplified Shapiro-Wilk approximation
            var sorted = data.OrderBy(x => x).ToArray();
            var n = sorted.Length;
            var mean = data.Mean();
            
            return new
            {
                testName = "Shapiro-Wilk Normality Test",
                note = "Simplified implementation",
                sampleSize = n,
                interpretation = "Use Jarque-Bera for more reliable results with large samples"
            };
        }

        private double[] CalculateACF(double[] data, int maxLag)
        {
            var mean = data.Mean();
            var variance = data.Variance();
            var acf = new double[maxLag + 1];
            acf[0] = 1.0;

            for (int lag = 1; lag <= maxLag && lag < data.Length; lag++)
            {
                double sum = 0;
                for (int i = 0; i < data.Length - lag; i++)
                {
                    sum += (data[i] - mean) * (data[i + lag] - mean);
                }
                acf[lag] = sum / (data.Length * variance);
            }

            return acf;
        }

        private double[] CalculatePACF(double[] data, int maxLag)
        {
            var acf = CalculateACF(data, maxLag);
            var pacf = new double[maxLag + 1];
            pacf[0] = 1.0;
            
            if (maxLag >= 1)
                pacf[1] = acf[1];

            for (int lag = 2; lag <= maxLag; lag++)
            {
                // Durbin-Levinson algorithm
                var phi = new double[lag + 1];
                var phiOld = new double[lag];
                
                double num = acf[lag];
                double den = 1.0;
                
                for (int j = 1; j < lag; j++)
                {
                    num -= pacf[j] * acf[lag - j];
                    den -= pacf[j] * acf[j];
                }
                
                pacf[lag] = num / den;
            }

            return pacf;
        }

        private (double slope, double intercept, double rSquared) FitLinearTrend(double[] data)
        {
            var n = data.Length;
            var x = Enumerable.Range(0, n).Select(i => (double)i).ToArray();
            var y = data;

            var xMean = x.Mean();
            var yMean = y.Mean();

            double sumXY = 0, sumX2 = 0, sumY2 = 0;
            for (int i = 0; i < n; i++)
            {
                sumXY += (x[i] - xMean) * (y[i] - yMean);
                sumX2 += Math.Pow(x[i] - xMean, 2);
                sumY2 += Math.Pow(y[i] - yMean, 2);
            }

            var slope = sumXY / sumX2;
            var intercept = yMean - slope * xMean;
            var rSquared = Math.Pow(sumXY, 2) / (sumX2 * sumY2);

            return (slope, intercept, rSquared);
        }

        private int[] FindSignificantLags(double[] acf, double? threshold = null)
        {
            var ci = threshold ?? 1.96 / Math.Sqrt(acf.Length);
            var significantLags = new List<int>();

            for (int i = 1; i < acf.Length; i++)
            {
                if (Math.Abs(acf[i]) > ci)
                    significantLags.Add(i);
            }

            return significantLags.ToArray();
        }

        private (double statistic, double pValue, Dictionary<string, double> criticalValues) PerformADFTest(double[] data)
        {
            // Simplified ADF test implementation
            var n = data.Length;
            var laggedData = data.Take(n - 1).ToArray();
            var diff = data.Skip(1).Select((val, i) => val - data[i]).ToArray();
            
            var (slope, _, _) = FitLinearTrend(diff);
            var statistic = slope * Math.Sqrt(n);
            
            // Approximate critical values
            var criticalValues = new Dictionary<string, double>
            {
                { "1%", -3.43 },
                { "5%", -2.86 },
                { "10%", -2.57 }
            };

            var pValue = statistic < criticalValues["1%"] ? 0.01 : 
                        statistic < criticalValues["5%"] ? 0.05 :
                        statistic < criticalValues["10%"] ? 0.10 : 0.15;

            return (statistic, pValue, criticalValues);
        }

        private (double statistic, double pValue, Dictionary<string, double> criticalValues) PerformKPSSTest(double[] data)
        {
            // Simplified KPSS test
            var mean = data.Mean();
            var cumSum = new double[data.Length];
            cumSum[0] = data[0] - mean;
            for (int i = 1; i < data.Length; i++)
            {
                cumSum[i] = cumSum[i - 1] + (data[i] - mean);
            }

            var s2 = data.Variance();
            var statistic = cumSum.Sum(x => x * x) / (data.Length * data.Length * s2);

            var criticalValues = new Dictionary<string, double>
            {
                { "10%", 0.347 },
                { "5%", 0.463 },
                { "1%", 0.739 }
            };

            var pValue = statistic > criticalValues["1%"] ? 0.01 :
                        statistic > criticalValues["5%"] ? 0.05 :
                        statistic > criticalValues["10%"] ? 0.10 : 0.15;

            return (statistic, pValue, criticalValues);
        }

        private (double meanStability, double varianceStability, double cv) CalculateRollingStatistics(double[] data, int window)
        {
            var rollingMeans = new List<double>();
            var rollingVars = new List<double>();

            for (int i = window; i <= data.Length; i++)
            {
                var segment = data.Skip(i - window).Take(window).ToArray();
                rollingMeans.Add(segment.Mean());
                rollingVars.Add(segment.Variance());
            }

            var meanStability = 1.0 / (1.0 + rollingMeans.StandardDeviation());
            var varianceStability = 1.0 / (1.0 + rollingVars.StandardDeviation());
            var cv = rollingMeans.StandardDeviation() / Math.Abs(rollingMeans.Mean());

            return (meanStability, varianceStability, cv);
        }

        private string DetermineStationarity(
            (double statistic, double pValue, Dictionary<string, double> criticalValues) adf,
            (double statistic, double pValue, Dictionary<string, double> criticalValues) kpss)
        {
            bool adfStationary = adf.pValue < 0.05;
            bool kpssStationary = kpss.pValue > 0.05;

            if (adfStationary && kpssStationary)
                return "Stationary";
            else if (!adfStationary && !kpssStationary)
                return "Non-stationary";
            else
                return "Inconclusive (mixed results)";
        }

        private string GetStationarityRecommendation(
            (double statistic, double pValue, Dictionary<string, double> criticalValues) adf,
            (double statistic, double pValue, Dictionary<string, double> criticalValues) kpss)
        {
            bool adfStationary = adf.pValue < 0.05;
            bool kpssStationary = kpss.pValue > 0.05;

            if (adfStationary && kpssStationary)
                return "Data is suitable for time series modeling as-is";
            else if (!adfStationary && !kpssStationary)
                return "Apply differencing to achieve stationarity";
            else
                return "Consider additional testing or transformations";
        }

        private (double statistic, double pValue, int df) PerformLjungBoxTest(double[] data, int lags)
        {
            var acf = CalculateACF(data, lags);
            var n = data.Length;
            
            double statistic = 0;
            for (int k = 1; k <= lags; k++)
            {
                statistic += Math.Pow(acf[k], 2) / (n - k);
            }
            statistic *= n * (n + 2);

            var pValue = 1 - ChiSquareCDF(statistic, lags);

            return (statistic, pValue, lags);
        }

        private int EstimateAROrder(double[] pacf, double threshold)
        {
            for (int i = 1; i < pacf.Length; i++)
            {
                if (Math.Abs(pacf[i]) < threshold)
                    return i - 1;
            }
            return 0;
        }

        private int EstimateMAOrder(double[] acf, double threshold)
        {
            for (int i = 1; i < acf.Length; i++)
            {
                if (Math.Abs(acf[i]) < threshold)
                    return i - 1;
            }
            return 0;
        }

        private string SuggestARIMAModel(double[] acf, double[] pacf, double threshold)
        {
            var arOrder = EstimateAROrder(pacf, threshold);
            var maOrder = EstimateMAOrder(acf, threshold);
            
            return $"ARIMA({arOrder},0,{maOrder}) or ARIMA({arOrder},1,{maOrder}) if non-stationary";
        }

        private (double[] trend, double[] seasonal, double[] residual) PerformSeasonalDecomposition(
            double[] data, int period, string model)
        {
            var n = data.Length;
            var trend = new double[n];
            var seasonal = new double[n];
            var residual = new double[n];

            // Calculate trend using moving average
            var halfPeriod = period / 2;
            for (int i = 0; i < n; i++)
            {
                var start = Math.Max(0, i - halfPeriod);
                var end = Math.Min(n, i + halfPeriod + 1);
                trend[i] = data.Skip(start).Take(end - start).Average();
            }

            // Calculate seasonal component
            var detrended = model.ToLower() == "additive"
                ? data.Select((val, i) => val - trend[i]).ToArray()
                : data.Select((val, i) => trend[i] != 0 ? val / trend[i] : 0).ToArray();

            var seasonalAverages = new double[period];
            for (int i = 0; i < period; i++)
            {
                var values = new List<double>();
                for (int j = i; j < n; j += period)
                {
                    values.Add(detrended[j]);
                }
                seasonalAverages[i] = values.Average();
            }

            // Normalize seasonal component
            var seasonalMean = seasonalAverages.Average();
            seasonalAverages = seasonalAverages.Select(x => x - seasonalMean).ToArray();

            for (int i = 0; i < n; i++)
            {
                seasonal[i] = seasonalAverages[i % period];
            }

            // Calculate residual
            residual = model.ToLower() == "additive"
                ? data.Select((val, i) => val - trend[i] - seasonal[i]).ToArray()
                : data.Select((val, i) => (trend[i] + seasonal[i]) != 0 ? val / (trend[i] + seasonal[i]) : 0).ToArray();

            return (trend, seasonal, residual);
        }

        private double CalculateSeasonalStrength((double[] trend, double[] seasonal, double[] residual) decomp)
        {
            var detrendedVar = decomp.seasonal.Zip(decomp.residual, (s, r) => s + r).ToArray().Variance();
            var residualVar = decomp.residual.Variance();
            return Math.Max(0, 1 - residualVar / detrendedVar);
        }

        private double CalculateTrendStrength((double[] trend, double[] seasonal, double[] residual) decomp)
        {
            var detrendedVar = decomp.trend.Zip(decomp.residual, (t, r) => t + r).ToArray().Variance();
            var residualVar = decomp.residual.Variance();
            return Math.Max(0, 1 - residualVar / detrendedVar);
        }

        private object AnalyzeSeasonalPattern(double[] seasonal, int period)
        {
            var seasonalAverages = new double[period];
            for (int i = 0; i < period; i++)
            {
                var values = new List<double>();
                for (int j = i; j < seasonal.Length; j += period)
                {
                    values.Add(seasonal[j]);
                }
                seasonalAverages[i] = values.Average();
            }

            var maxIdx = Array.IndexOf(seasonalAverages, seasonalAverages.Max());
            var minIdx = Array.IndexOf(seasonalAverages, seasonalAverages.Min());

            return new
            {
                peakPeriod = maxIdx + 1,
                troughPeriod = minIdx + 1,
                amplitude = seasonalAverages.Max() - seasonalAverages.Min(),
                pattern = seasonalAverages
            };
        }

        private string DetermineTrendDirection(double[] trend)
        {
            var (slope, _, _) = FitLinearTrend(trend);
            if (slope > 0.001) return "Upward";
            if (slope < -0.001) return "Downward";
            return "Flat";
        }

        private object AnalyzeResiduals(double[] residual)
        {
            return new
            {
                mean = residual.Mean(),
                standardDeviation = residual.StandardDeviation(),
                isWhiteNoise = Math.Abs(residual.Mean()) < 0.01,
                autocorrelation = CalculateACF(residual, 1)[1]
            };
        }

        private object GetDescriptiveStats(double[] data)
        {
            return new
            {
                mean = data.Mean(),
                median = data.Median(),
                standardDeviation = data.StandardDeviation(),
                variance = data.Variance(),
                skewness = data.Skewness(),
                kurtosis = data.Kurtosis(),
                min = data.Min(),
                max = data.Max(),
                range = data.Max() - data.Min()
            };
        }

        private object CalculateVolatilityMetrics(double[] returns)
        {
            var daily = returns.StandardDeviation();
            return new
            {
                daily,
                annualized = daily * Math.Sqrt(252),
                monthly = daily * Math.Sqrt(21)
            };
        }

        private object AnalyzeDistribution(double[] data)
        {
            var skewness = data.Skewness();
            var kurtosis = data.Kurtosis();
            
            return new
            {
                skewness,
                kurtosis,
                excessKurtosis = kurtosis - 3,
                isNormal = Math.Abs(kurtosis - 3) < 1 && Math.Abs(skewness) < 0.5,
                tailRisk = kurtosis > 3 ? "Heavy tails (higher tail risk)" : "Normal tails"
            };
        }

        private string GenerateTradingRecommendation(
            double[] returns,
            double[] acf,
            (double statistic, double pValue, Dictionary<string, double> criticalValues) adf,
            dynamic volatility)
        {
            var meanReverting = Math.Abs(acf[1]) < 0.5;
            var trending = adf.pValue > 0.05;
            var highVol = volatility.annualized > 0.3;

            if (meanReverting && !highVol)
                return "Mean-reverting behavior detected. Consider mean-reversion strategies.";
            else if (trending)
                return "Trending behavior detected. Consider momentum or trend-following strategies.";
            else if (highVol)
                return "High volatility detected. Consider volatility-based strategies or options.";
            else
                return "Mixed signals. Further analysis recommended before selecting strategy.";
        }

        // Statistical helper functions
        private double CalculatePValue(double tStat, int df, string alternative)
        {
            var absTStat = Math.Abs(tStat);
            // Simplified p-value calculation
            var p = 2 * (1 - NormalCDF(absTStat));
            
            return alternative.ToLower() switch
            {
                "two-sided" => p,
                "greater" => tStat > 0 ? p / 2 : 1 - p / 2,
                "less" => tStat < 0 ? p / 2 : 1 - p / 2,
                _ => p
            };
        }

        private double GetCriticalValue(double alpha, int df, string alternative)
        {
            // Simplified critical value (using normal approximation for large df)
            return alternative.ToLower() == "two-sided" 
                ? GetZScore(1 - alpha / 2)
                : GetZScore(1 - alpha);
        }

        private double GetZScore(double p)
        {
            // Approximate inverse normal CDF
            if (p <= 0) return double.NegativeInfinity;
            if (p >= 1) return double.PositiveInfinity;
            
            // Simple approximation
            if (p == 0.5) return 0;
            if (p > 0.5) return -GetZScore(1 - p);
            
            // Beasley-Springer-Moro algorithm approximation
            var t = Math.Sqrt(-2 * Math.Log(p));
            return -(t - (2.515517 + 0.802853 * t + 0.010328 * t * t) /
                    (1 + 1.432788 * t + 0.189269 * t * t + 0.001308 * t * t * t));
        }

        private double NormalCDF(double x)
        {
            // Approximation of cumulative normal distribution
            return 0.5 * (1 + Erf(x / Math.Sqrt(2)));
        }

        private double Erf(double x)
        {
            // Abramowitz and Stegun approximation
            var a1 = 0.254829592;
            var a2 = -0.284496736;
            var a3 = 1.421413741;
            var a4 = -1.453152027;
            var a5 = 1.061405429;
            var p = 0.3275911;

            var sign = x < 0 ? -1 : 1;
            x = Math.Abs(x);

            var t = 1.0 / (1.0 + p * x);
            var y = 1.0 - (((((a5 * t + a4) * t) + a3) * t + a2) * t + a1) * t * Math.Exp(-x * x);

            return sign * y;
        }

        private double ChiSquareCDF(double x, int df)
        {
            // Simplified chi-square CDF approximation
            if (x <= 0) return 0;
            if (df == 1) return 2 * (NormalCDF(Math.Sqrt(x)) - 0.5);
            
            // Wilson-Hilferty approximation
            var z = Math.Pow(x / df, 1.0 / 3.0) - (1 - 2.0 / (9 * df));
            z /= Math.Sqrt(2.0 / (9 * df));
            return NormalCDF(z);
        }

        private double CalculateBeta(double ncp, double alpha)
        {
            // Simplified beta calculation for power analysis
            return NormalCDF(GetZScore(1 - alpha) - ncp);
        }
    }
}
