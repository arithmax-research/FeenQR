using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using MathNet.Numerics.Statistics;
using MathNet.Numerics.LinearAlgebra;

namespace QuantResearchAgent.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ForecastingController : ControllerBase
    {
        private readonly ILogger<ForecastingController> _logger;
        private readonly IHttpClientFactory _httpClientFactory;

        public ForecastingController(
            ILogger<ForecastingController> logger,
            IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
        }

        // 1. Time Series Forecasting
        [HttpGet("forecast")]
        public async Task<IActionResult> Forecast(
            [FromQuery] string symbol,
            [FromQuery] int forecastDays = 30,
            [FromQuery] string method = "arima", // arima, exponential-smoothing, linear, prophet
            [FromQuery] int trainingDays = 252)
        {
            try
            {
                var historicalData = await FetchHistoricalData(symbol, trainingDays);
                if (historicalData == null || historicalData.Length < 30)
                    return BadRequest("Insufficient historical data for forecasting");

                var forecast = method.ToLower() switch
                {
                    "arima" => ForecastARIMA(historicalData, forecastDays),
                    "exponential-smoothing" => ForecastExponentialSmoothing(historicalData, forecastDays),
                    "linear" => ForecastLinear(historicalData, forecastDays),
                    "moving-average" => ForecastMovingAverage(historicalData, forecastDays),
                    "ets" => ForecastETS(historicalData, forecastDays),
                    _ => ForecastARIMA(historicalData, forecastDays)
                };

                // Calculate confidence intervals
                var forecastError = CalculateForecastError(historicalData);
                var confidenceIntervals = CalculateConfidenceIntervals(forecast.predictions, forecastError, forecastDays);

                // Calculate forecast metrics
                var lastPrice = historicalData[^1];
                var forecastedChange = ((forecast.predictions[^1] - lastPrice) / lastPrice) * 100;

                return Ok(new
                {
                    symbol,
                    method,
                    training = new
                    {
                        dataPoints = historicalData.Length,
                        startValue = historicalData[0],
                        endValue = lastPrice,
                        period = $"{trainingDays} days"
                    },
                    forecast = new
                    {
                        horizon = forecastDays,
                        predictions = forecast.predictions,
                        confidenceIntervals,
                        trend = forecast.predictions[^1] > lastPrice ? "Upward" : "Downward",
                        expectedChange = forecastedChange,
                        expectedReturn = forecastedChange / 100
                    },
                    modelInfo = forecast.modelInfo,
                    quality = new
                    {
                        modelFit = forecast.modelFit,
                        forecastError,
                        reliability = CalculateReliability(forecastError, forecastDays)
                    },
                    interpretation = GenerateForecastInterpretation(forecast.predictions, lastPrice, forecastDays, method)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error forecasting for {Symbol}", symbol);
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // 2. Compare Forecast Models
        [HttpGet("forecast-compare")]
        public async Task<IActionResult> CompareForecastModels(
            [FromQuery] string symbol,
            [FromQuery] int forecastDays = 30,
            [FromQuery] int trainingDays = 252,
            [FromQuery] int validationDays = 30)
        {
            try
            {
                var totalDays = trainingDays + validationDays;
                var allData = await FetchHistoricalData(symbol, totalDays);
                if (allData == null || allData.Length < totalDays)
                    return BadRequest("Insufficient historical data for model comparison");

                // Split into training and validation
                var trainingData = allData.Take(trainingDays).ToArray();
                var validationData = allData.Skip(trainingDays).ToArray();

                // Test multiple forecasting methods
                var methods = new[] { "arima", "exponential-smoothing", "linear", "moving-average", "ets" };
                var modelComparisons = new List<object>();

                foreach (var method in methods)
                {
                    try
                    {
                        // Generate forecast
                        var forecast = method.ToLower() switch
                        {
                            "arima" => ForecastARIMA(trainingData, validationDays),
                            "exponential-smoothing" => ForecastExponentialSmoothing(trainingData, validationDays),
                            "linear" => ForecastLinear(trainingData, validationDays),
                            "moving-average" => ForecastMovingAverage(trainingData, validationDays),
                            "ets" => ForecastETS(trainingData, validationDays),
                            _ => ForecastARIMA(trainingData, validationDays)
                        };

                        // Calculate accuracy metrics against validation data
                        var accuracy = CalculateForecastAccuracy(forecast.predictions, validationData);

                        modelComparisons.Add(new
                        {
                            method,
                            accuracy,
                            modelFit = forecast.modelFit,
                            score = CalculateOverallScore(accuracy, forecast.modelFit)
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error testing {Method} for {Symbol}", method, symbol);
                        modelComparisons.Add(new
                        {
                            method,
                            error = ex.Message
                        });
                    }
                }

                // Rank models
                var rankedModels = modelComparisons
                    .Where(m => m.GetType().GetProperty("score") != null)
                    .OrderByDescending(m => (double)(m.GetType().GetProperty("score")?.GetValue(m) ?? 0.0))
                    .ToList();

                // Generate forecast with best model
                var bestMethod = rankedModels.FirstOrDefault();
                var bestMethodName = bestMethod?.GetType().GetProperty("method")?.GetValue(bestMethod)?.ToString() ?? "arima";
                
                var finalForecast = bestMethodName.ToLower() switch
                {
                    "arima" => ForecastARIMA(allData.Take(trainingDays + validationDays).ToArray(), forecastDays),
                    "exponential-smoothing" => ForecastExponentialSmoothing(allData.Take(trainingDays + validationDays).ToArray(), forecastDays),
                    "linear" => ForecastLinear(allData.Take(trainingDays + validationDays).ToArray(), forecastDays),
                    "moving-average" => ForecastMovingAverage(allData.Take(trainingDays + validationDays).ToArray(), forecastDays),
                    "ets" => ForecastETS(allData.Take(trainingDays + validationDays).ToArray(), forecastDays),
                    _ => ForecastARIMA(allData.Take(trainingDays + validationDays).ToArray(), forecastDays)
                };

                return Ok(new
                {
                    symbol,
                    comparison = new
                    {
                        trainingPeriod = $"{trainingDays} days",
                        validationPeriod = $"{validationDays} days",
                        forecastHorizon = $"{forecastDays} days",
                        modelsTested = methods.Length
                    },
                    models = modelComparisons,
                    ranking = rankedModels,
                    recommendation = new
                    {
                        bestModel = bestMethodName,
                        reason = "Highest overall score based on accuracy and model fit",
                        confidence = GetConfidenceLevel(bestMethod!)
                    },
                    forecast = new
                    {
                        method = bestMethodName,
                        predictions = finalForecast.predictions,
                        expectedChange = ((finalForecast.predictions[^1] - allData[^1]) / allData[^1]) * 100
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error comparing forecast models for {Symbol}", symbol);
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // 3. Forecast Accuracy Metrics
        [HttpGet("forecast-accuracy")]
        public async Task<IActionResult> ForecastAccuracyMetrics(
            [FromQuery] string symbol,
            [FromQuery] string method = "arima",
            [FromQuery] int forecastDays = 30,
            [FromQuery] int trainingDays = 252,
            [FromQuery] int testDays = 60)
        {
            try
            {
                var totalDays = trainingDays + testDays;
                var allData = await FetchHistoricalData(symbol, totalDays);
                if (allData == null || allData.Length < totalDays)
                    return BadRequest("Insufficient historical data for accuracy testing");

                // Perform rolling forecast validation
                var rollingResults = new List<object>();
                var allForecasts = new List<double>();
                var allActuals = new List<double>();

                for (int i = trainingDays; i < allData.Length - forecastDays; i += forecastDays)
                {
                    var trainData = allData.Take(i).ToArray();
                    var actualData = allData.Skip(i).Take(forecastDays).ToArray();

                    var forecast = method.ToLower() switch
                    {
                        "arima" => ForecastARIMA(trainData, forecastDays),
                        "exponential-smoothing" => ForecastExponentialSmoothing(trainData, forecastDays),
                        "linear" => ForecastLinear(trainData, forecastDays),
                        "moving-average" => ForecastMovingAverage(trainData, forecastDays),
                        "ets" => ForecastETS(trainData, forecastDays),
                        _ => ForecastARIMA(trainData, forecastDays)
                    };

                    var windowAccuracy = CalculateForecastAccuracy(forecast.predictions, actualData);
                    
                    rollingResults.Add(new
                    {
                        window = rollingResults.Count + 1,
                        trainingEnd = i,
                        accuracy = windowAccuracy
                    });

                    allForecasts.AddRange(forecast.predictions.Take(actualData.Length));
                    allActuals.AddRange(actualData);
                }

                // Calculate overall accuracy metrics
                var overallAccuracy = CalculateForecastAccuracy(allForecasts.ToArray(), allActuals.ToArray());
                
                // Calculate directional accuracy
                var directionalAccuracy = CalculateDirectionalAccuracy(allForecasts.ToArray(), allActuals.ToArray());

                // Calculate forecast bias
                var bias = CalculateForecastBias(allForecasts.ToArray(), allActuals.ToArray());

                return Ok(new
                {
                    symbol,
                    method,
                    evaluation = new
                    {
                        trainingPeriod = $"{trainingDays} days",
                        testPeriod = $"{testDays} days",
                        forecastHorizon = $"{forecastDays} days",
                        rollingWindows = rollingResults.Count
                    },
                    accuracyMetrics = overallAccuracy,
                    additionalMetrics = new
                    {
                        directionalAccuracy,
                        bias = new
                        {
                            value = bias,
                            interpretation = Math.Abs(bias) < 0.05 
                                ? "Low bias (good)" 
                                : bias > 0 
                                    ? "Positive bias (tends to overpredict)" 
                                    : "Negative bias (tends to underpredict)"
                        },
                        consistency = CalculateConsistency(rollingResults)
                    },
                    rollingValidation = rollingResults,
                    performanceGrade = GetPerformanceGrade(overallAccuracy, directionalAccuracy),
                    recommendations = GenerateAccuracyRecommendations(overallAccuracy, directionalAccuracy, bias, method)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating forecast accuracy for {Symbol}", symbol);
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
                var startDate = endDate.AddDays(-days - 100);
                
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

        private (double[] predictions, object modelInfo, object modelFit) ForecastARIMA(double[] data, int horizon)
        {
            // Simplified ARIMA(1,1,1) implementation
            var n = data.Length;
            var diff = new double[n - 1];
            for (int i = 0; i < n - 1; i++)
            {
                diff[i] = data[i + 1] - data[i];
            }

            // Simple AR(1) on differenced data
            var phi = CalculateAR1Coefficient(diff);
            var mean = diff.Mean();
            var residualStdDev = CalculateResidualStdDev(diff, phi, mean);

            // Generate forecast
            var predictions = new double[horizon];
            var lastValue = data[^1];
            var lastDiff = diff[^1];

            for (int i = 0; i < horizon; i++)
            {
                var forecastDiff = mean + phi * (i == 0 ? lastDiff - mean : predictions[i - 1] - lastValue - mean);
                predictions[i] = (i == 0 ? lastValue : predictions[i - 1]) + forecastDiff;
            }

            var modelInfo = new
            {
                order = "ARIMA(1,1,1)",
                ar1Coefficient = phi,
                mean,
                residualStdDev
            };

            var modelFit = new
            {
                aic = CalculateAIC(data, predictions, 3),
                bic = CalculateBIC(data, predictions, 3),
                rmse = residualStdDev
            };

            return (predictions, modelInfo, modelFit);
        }

        private (double[] predictions, object modelInfo, object modelFit) ForecastExponentialSmoothing(double[] data, int horizon)
        {
            // Simple Exponential Smoothing
            var alpha = 0.3; // Smoothing parameter
            var n = data.Length;
            var smoothed = new double[n];
            smoothed[0] = data[0];

            for (int i = 1; i < n; i++)
            {
                smoothed[i] = alpha * data[i] + (1 - alpha) * smoothed[i - 1];
            }

            // Forecast as constant (last smoothed value)
            var lastSmoothed = smoothed[^1];
            var predictions = Enumerable.Repeat(lastSmoothed, horizon).ToArray();

            var residuals = data.Select((val, i) => val - smoothed[i]).ToArray();
            var rmse = Math.Sqrt(residuals.Select(r => r * r).Average());

            var modelInfo = new
            {
                method = "Simple Exponential Smoothing",
                alpha,
                lastSmoothedValue = lastSmoothed
            };

            var modelFit = new
            {
                aic = CalculateAIC(data, smoothed, 1),
                bic = CalculateBIC(data, smoothed, 1),
                rmse
            };

            return (predictions, modelInfo, modelFit);
        }

        private (double[] predictions, object modelInfo, object modelFit) ForecastLinear(double[] data, int horizon)
        {
            // Linear regression forecast
            var n = data.Length;
            var x = Enumerable.Range(0, n).Select(i => (double)i).ToArray();
            var (slope, intercept, rSquared) = FitLinearRegression(x, data);

            var predictions = new double[horizon];
            for (int i = 0; i < horizon; i++)
            {
                predictions[i] = intercept + slope * (n + i);
            }

            var fitted = x.Select(xi => intercept + slope * xi).ToArray();
            var residuals = data.Select((val, i) => val - fitted[i]).ToArray();
            var rmse = Math.Sqrt(residuals.Select(r => r * r).Average());

            var modelInfo = new
            {
                method = "Linear Regression",
                slope,
                intercept,
                rSquared
            };

            var modelFit = new
            {
                aic = CalculateAIC(data, fitted, 2),
                bic = CalculateBIC(data, fitted, 2),
                rmse,
                rSquared
            };

            return (predictions, modelInfo, modelFit);
        }

        private (double[] predictions, object modelInfo, object modelFit) ForecastMovingAverage(double[] data, int horizon)
        {
            // Simple Moving Average forecast
            var window = Math.Min(20, data.Length / 2);
            var recentData = data.TakeLast(window).ToArray();
            var average = recentData.Average();

            var predictions = Enumerable.Repeat(average, horizon).ToArray();

            // Calculate historical fit
            var fitted = new double[data.Length];
            for (int i = 0; i < data.Length; i++)
            {
                var start = Math.Max(0, i - window + 1);
                fitted[i] = data.Skip(start).Take(i - start + 1).Average();
            }

            var residuals = data.Select((val, i) => val - fitted[i]).ToArray();
            var rmse = Math.Sqrt(residuals.Select(r => r * r).Average());

            var modelInfo = new
            {
                method = "Moving Average",
                window,
                average
            };

            var modelFit = new
            {
                aic = CalculateAIC(data, fitted, 1),
                bic = CalculateBIC(data, fitted, 1),
                rmse
            };

            return (predictions, modelInfo, modelFit);
        }

        private (double[] predictions, object modelInfo, object modelFit) ForecastETS(double[] data, int horizon)
        {
            // Exponential Smoothing with Trend (Holt's method)
            var alpha = 0.3; // Level smoothing
            var beta = 0.1;  // Trend smoothing

            var n = data.Length;
            var level = new double[n];
            var trend = new double[n];

            level[0] = data[0];
            trend[0] = data[1] - data[0];

            for (int i = 1; i < n; i++)
            {
                var prevLevel = level[i - 1];
                var prevTrend = trend[i - 1];
                
                level[i] = alpha * data[i] + (1 - alpha) * (prevLevel + prevTrend);
                trend[i] = beta * (level[i] - prevLevel) + (1 - beta) * prevTrend;
            }

            // Forecast
            var predictions = new double[horizon];
            var lastLevel = level[^1];
            var lastTrend = trend[^1];

            for (int i = 0; i < horizon; i++)
            {
                predictions[i] = lastLevel + (i + 1) * lastTrend;
            }

            var fitted = level.Select((l, i) => l + trend[i]).ToArray();
            var residuals = data.Select((val, i) => val - (i < fitted.Length ? fitted[i] : val)).ToArray();
            var rmse = Math.Sqrt(residuals.Select(r => r * r).Average());

            var modelInfo = new
            {
                method = "Exponential Smoothing with Trend (Holt's)",
                alpha,
                beta,
                finalLevel = lastLevel,
                finalTrend = lastTrend
            };

            var modelFit = new
            {
                aic = CalculateAIC(data, fitted, 2),
                bic = CalculateBIC(data, fitted, 2),
                rmse
            };

            return (predictions, modelInfo, modelFit);
        }

        private double CalculateAR1Coefficient(double[] data)
        {
            var mean = data.Mean();
            var n = data.Length;
            
            double numerator = 0;
            double denominator = 0;

            for (int i = 0; i < n - 1; i++)
            {
                numerator += (data[i] - mean) * (data[i + 1] - mean);
            }

            for (int i = 0; i < n; i++)
            {
                denominator += Math.Pow(data[i] - mean, 2);
            }

            return numerator / denominator;
        }

        private double CalculateResidualStdDev(double[] data, double phi, double mean)
        {
            var residuals = new double[data.Length - 1];
            for (int i = 1; i < data.Length; i++)
            {
                residuals[i - 1] = data[i] - (mean + phi * (data[i - 1] - mean));
            }
            return residuals.StandardDeviation();
        }

        private (double slope, double intercept, double rSquared) FitLinearRegression(double[] x, double[] y)
        {
            var n = x.Length;
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

        private double CalculateForecastError(double[] data)
        {
            // Calculate historical forecast error using walk-forward validation
            var errors = new List<double>();
            var testWindow = Math.Min(30, data.Length / 4);

            for (int i = data.Length - testWindow; i < data.Length - 1; i++)
            {
                var trainData = data.Take(i).ToArray();
                var (forecast, _, _) = ForecastARIMA(trainData, 1);
                errors.Add(Math.Abs(forecast[0] - data[i]));
            }

            return errors.Average();
        }

        private object[] CalculateConfidenceIntervals(double[] predictions, double forecastError, int horizon)
        {
            var intervals = new object[predictions.Length];
            
            for (int i = 0; i < predictions.Length; i++)
            {
                var errorMultiplier = Math.Sqrt(i + 1); // Error grows with horizon
                var margin = 1.96 * forecastError * errorMultiplier;
                
                intervals[i] = new
                {
                    point = predictions[i],
                    lower95 = predictions[i] - margin,
                    upper95 = predictions[i] + margin,
                    lower80 = predictions[i] - 1.28 * forecastError * errorMultiplier,
                    upper80 = predictions[i] + 1.28 * forecastError * errorMultiplier
                };
            }

            return intervals;
        }

        private string CalculateReliability(double forecastError, int horizon)
        {
            var errorRatio = forecastError / horizon;
            
            if (errorRatio < 0.01) return "Very High";
            if (errorRatio < 0.05) return "High";
            if (errorRatio < 0.10) return "Moderate";
            if (errorRatio < 0.20) return "Low";
            return "Very Low";
        }

        private string GenerateForecastInterpretation(double[] predictions, double lastPrice, int horizon, string method)
        {
            var finalPrice = predictions[^1];
            var change = ((finalPrice - lastPrice) / lastPrice) * 100;
            var direction = change > 0 ? "increase" : "decrease";
            
            return $"Based on {method} model, price is forecasted to {direction} by {Math.Abs(change):F2}% over the next {horizon} days. " +
                   $"Expected price: ${finalPrice:F2} (current: ${lastPrice:F2})";
        }

        private object CalculateForecastAccuracy(double[] forecasts, double[] actuals)
        {
            var n = Math.Min(forecasts.Length, actuals.Length);
            var errors = new double[n];
            var percentErrors = new double[n];

            for (int i = 0; i < n; i++)
            {
                errors[i] = forecasts[i] - actuals[i];
                percentErrors[i] = Math.Abs(errors[i] / actuals[i]) * 100;
            }

            var mae = errors.Select(Math.Abs).Average();
            var rmse = Math.Sqrt(errors.Select(e => e * e).Average());
            var mape = percentErrors.Average();
            var mse = errors.Select(e => e * e).Average();

            return new
            {
                mae, // Mean Absolute Error
                rmse, // Root Mean Square Error
                mape, // Mean Absolute Percentage Error
                mse, // Mean Square Error
                r2 = CalculateR2(forecasts, actuals)
            };
        }

        private double CalculateR2(double[] predicted, double[] actual)
        {
            var n = Math.Min(predicted.Length, actual.Length);
            var mean = actual.Take(n).Average();
            
            double ssRes = 0, ssTot = 0;
            for (int i = 0; i < n; i++)
            {
                ssRes += Math.Pow(actual[i] - predicted[i], 2);
                ssTot += Math.Pow(actual[i] - mean, 2);
            }

            return 1 - (ssRes / ssTot);
        }

        private double CalculateDirectionalAccuracy(double[] forecasts, double[] actuals)
        {
            if (forecasts.Length < 2 || actuals.Length < 2)
                return 0;

            var n = Math.Min(forecasts.Length - 1, actuals.Length - 1);
            var correct = 0;

            for (int i = 0; i < n; i++)
            {
                var forecastDirection = forecasts[i + 1] > forecasts[i];
                var actualDirection = actuals[i + 1] > actuals[i];
                
                if (forecastDirection == actualDirection)
                    correct++;
            }

            return (double)correct / n * 100;
        }

        private double CalculateForecastBias(double[] forecasts, double[] actuals)
        {
            var n = Math.Min(forecasts.Length, actuals.Length);
            var errors = new double[n];

            for (int i = 0; i < n; i++)
            {
                errors[i] = (forecasts[i] - actuals[i]) / actuals[i];
            }

            return errors.Average();
        }

        private double CalculateConsistency(List<object> rollingResults)
        {
            if (rollingResults.Count < 2)
                return 1.0;

            var rmseValues = rollingResults
                .Select(r => 
                {
                    var accuracy = r.GetType().GetProperty("accuracy")?.GetValue(r);
                    if (accuracy == null) return 0.0;
                    return (double)(accuracy.GetType().GetProperty("rmse")?.GetValue(accuracy) ?? 0.0);
                })
                .ToArray();

            var mean = rmseValues.Average();
            var stdDev = Math.Sqrt(rmseValues.Select(x => Math.Pow(x - mean, 2)).Average());
            var cv = stdDev / mean;

            return Math.Max(0, 1 - cv);
        }

        private string GetPerformanceGrade(object accuracy, double directionalAccuracy)
        {
            var mape = (double)(accuracy.GetType().GetProperty("mape")?.GetValue(accuracy) ?? 100.0);
            
            if (mape < 5 && directionalAccuracy > 80) return "A+ (Excellent)";
            if (mape < 10 && directionalAccuracy > 70) return "A (Very Good)";
            if (mape < 15 && directionalAccuracy > 60) return "B (Good)";
            if (mape < 20 && directionalAccuracy > 50) return "C (Fair)";
            if (mape < 30) return "D (Poor)";
            return "F (Very Poor)";
        }

        private string[] GenerateAccuracyRecommendations(object accuracy, double directionalAccuracy, double bias, string method)
        {
            var recommendations = new List<string>();
            var mape = (double)(accuracy.GetType().GetProperty("mape")?.GetValue(accuracy) ?? 0.0);

            if (mape > 20)
                recommendations.Add("Consider using ensemble methods or more sophisticated models");
            
            if (directionalAccuracy < 60)
                recommendations.Add("Low directional accuracy - model may not capture market dynamics well");
            
            if (Math.Abs(bias) > 0.1)
                recommendations.Add($"Significant bias detected - model tends to {(bias > 0 ? "overpredict" : "underpredict")}");
            
            if (mape < 10 && directionalAccuracy > 70)
                recommendations.Add("Model shows good performance - suitable for forecasting");

            if (recommendations.Count == 0)
                recommendations.Add("Model performance is acceptable for short-term forecasting");

            return recommendations.ToArray();
        }

        private double CalculateOverallScore(object accuracy, object modelFit)
        {
            var mape = (double)(accuracy.GetType().GetProperty("mape")?.GetValue(accuracy) ?? 100.0);
            var rmse = (double)(modelFit.GetType().GetProperty("rmse")?.GetValue(modelFit) ?? 100.0);
            
            // Lower is better, so invert and normalize
            var mapeScore = Math.Max(0, 100 - mape);
            var rmseScore = Math.Max(0, 100 - rmse);
            
            return (mapeScore + rmseScore) / 2;
        }

        private string GetConfidenceLevel(object bestMethod)
        {
            if (bestMethod == null) return "Low";
            
            var score = (double)(bestMethod.GetType().GetProperty("score")?.GetValue(bestMethod) ?? 0.0);
            
            if (score > 80) return "High";
            if (score > 60) return "Moderate";
            if (score > 40) return "Low";
            return "Very Low";
        }

        private double CalculateAIC(double[] actual, double[] fitted, int parameters)
        {
            var n = actual.Length;
            var rss = actual.Select((val, i) => i < fitted.Length ? Math.Pow(val - fitted[i], 2) : 0).Sum();
            var logLikelihood = -n / 2.0 * Math.Log(rss / n);
            return 2 * parameters - 2 * logLikelihood;
        }

        private double CalculateBIC(double[] actual, double[] fitted, int parameters)
        {
            var n = actual.Length;
            var rss = actual.Select((val, i) => i < fitted.Length ? Math.Pow(val - fitted[i], 2) : 0).Sum();
            var logLikelihood = -n / 2.0 * Math.Log(rss / n);
            return parameters * Math.Log(n) - 2 * logLikelihood;
        }
    }
}
