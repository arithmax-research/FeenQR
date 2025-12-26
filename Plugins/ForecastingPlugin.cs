using Microsoft.SemanticKernel;
using QuantResearchAgent.Services;
using QuantResearchAgent.Core;
using System.ComponentModel;
using System.Text.Json;

namespace QuantResearchAgent.Plugins;

/// <summary>
/// Semantic Kernel plugin for time series forecasting
/// </summary>
public class ForecastingPlugin
{
    private readonly TimeSeriesForecastingService _forecastingService;

    public ForecastingPlugin(TimeSeriesForecastingService forecastingService)
    {
        _forecastingService = forecastingService;
    }

    [KernelFunction, Description("Perform ARIMA/SARIMA time series forecasting on historical data")]
    public async Task<string> ForecastWithArimaAsync(
        [Description("Array of historical values to forecast from")] double[] historicalData,
        [Description("Number of periods to forecast (default: 10)")] int forecastHorizon = 10,
        [Description("ARIMA p parameter (autoregressive order, default: 1)")] int p = 1,
        [Description("ARIMA d parameter (differencing order, default: 1)")] int d = 1,
        [Description("ARIMA q parameter (moving average order, default: 1)")] int q = 1)
    {
        try
        {
            var parameters = new ArimaParameters
            {
                P = p,
                D = d,
                Q = q
            };

            var result = _forecastingService.ForecastArima(historicalData, forecastHorizon, parameters);

            var forecastSummary = $"ARIMA({p},{d},{q}) Forecast Results:\n" +
                                 $"Historical Data Points: {result.HistoricalData.Count}\n" +
                                 $"Forecast Horizon: {forecastHorizon}\n\n" +
                                 $"Forecasted Values: {string.Join(", ", result.ForecastedValues.Select(v => v.ToString("F4")))}\n\n" +
                                 $"Accuracy Metrics:\n" +
                                 $"- MAE: {result.Metrics.MAE:F4}\n" +
                                 $"- RMSE: {result.Metrics.RMSE:F4}\n" +
                                 $"- MAPE: {result.Metrics.MAPE:P2}\n" +
                                 $"- R²: {result.Metrics.R2:F4}\n\n";

            if (result.UpperBounds.Any() && result.LowerBounds.Any())
            {
                forecastSummary += $"95% Confidence Intervals:\n" +
                                  $"Upper: {string.Join(", ", result.UpperBounds.Select(v => v.ToString("F4")))}\n" +
                                  $"Lower: {string.Join(", ", result.LowerBounds.Select(v => v.ToString("F4")))}\n\n";
            }

            forecastSummary += "Interpretation:\n" +
                              $"- The model explains {result.Metrics.R2:P1} of the variance in the data\n" +
                              $"- Average forecast error is {result.Metrics.MAE:F4} units\n" +
                              $"- Forecasts are most reliable for short-term predictions";

            return forecastSummary;
        }
        catch (Exception ex)
        {
            return $"Error performing ARIMA forecast: {ex.Message}";
        }
    }

    [KernelFunction, Description("Perform exponential smoothing forecasting (simple, double, or triple)")]
    public async Task<string> ForecastWithExponentialSmoothingAsync(
        [Description("Array of historical values to forecast from")] double[] historicalData,
        [Description("Number of periods to forecast (default: 10)")] int forecastHorizon = 10,
        [Description("Smoothing type: Simple, Double, or Triple (default: Double)")] string smoothingType = "Double",
        [Description("Alpha parameter for level smoothing (0-1, default: 0.3)")] double alpha = 0.3,
        [Description("Beta parameter for trend smoothing (0-1, default: 0.1)")] double beta = 0.1,
        [Description("Gamma parameter for seasonal smoothing (0-1, default: 0.1)")] double gamma = 0.1,
        [Description("Seasonal period length (default: 12)")] int seasonLength = 12)
    {
        try
        {
            if (!Enum.TryParse<SmoothingType>(smoothingType, true, out var type))
            {
                type = SmoothingType.Double;
            }

            var parameters = new ExponentialSmoothingParameters
            {
                Type = type,
                Alpha = alpha,
                Beta = beta,
                Gamma = gamma,
                SeasonLength = seasonLength
            };

            var result = _forecastingService.ForecastExponentialSmoothing(historicalData, forecastHorizon, parameters);

            var forecastSummary = $"{type} Exponential Smoothing Forecast Results:\n" +
                                 $"Historical Data Points: {result.HistoricalData.Count}\n" +
                                 $"Forecast Horizon: {forecastHorizon}\n\n" +
                                 $"Parameters: α={alpha:F2}, β={beta:F2}, γ={gamma:F2}\n" +
                                 $"Seasonal Period: {seasonLength}\n\n" +
                                 $"Forecasted Values: {string.Join(", ", result.ForecastedValues.Select(v => v.ToString("F4")))}\n\n" +
                                 $"Accuracy Metrics:\n" +
                                 $"- MAE: {result.Metrics.MAE:F4}\n" +
                                 $"- RMSE: {result.Metrics.RMSE:F4}\n" +
                                 $"- MAPE: {result.Metrics.MAPE:P2}\n\n";

            forecastSummary += "Interpretation:\n" +
                              $"- {type} exponential smoothing {(type == SmoothingType.Triple ? "includes seasonal components" : "captures trend and level")}\n" +
                              $"- Higher α values give more weight to recent observations\n" +
                              $"- This method is good for data with {(type == SmoothingType.Simple ? "no clear trend" : "trend patterns")}";

            return forecastSummary;
        }
        catch (Exception ex)
        {
            return $"Error performing exponential smoothing forecast: {ex.Message}";
        }
    }

    [KernelFunction, Description("Perform Singular Spectrum Analysis (SSA) forecasting for complex time series")]
    public async Task<string> ForecastWithSSAAsync(
        [Description("Array of historical values to forecast from")] double[] historicalData,
        [Description("Number of periods to forecast (default: 10)")] int forecastHorizon = 10,
        [Description("Window size for SSA decomposition (default: 10)")] int windowSize = 10)
    {
        try
        {
            var result = _forecastingService.ForecastSSA(historicalData, forecastHorizon, windowSize);

            var forecastSummary = $"SSA (Singular Spectrum Analysis) Forecast Results:\n" +
                                 $"Historical Data Points: {result.HistoricalData.Count}\n" +
                                 $"Forecast Horizon: {forecastHorizon}\n" +
                                 $"Window Size: {windowSize}\n\n" +
                                 $"Forecasted Values: {string.Join(", ", result.ForecastedValues.Select(v => v.ToString("F4")))}\n\n" +
                                 $"Accuracy Metrics:\n" +
                                 $"- MAE: {result.Metrics.MAE:F4}\n" +
                                 $"- RMSE: {result.Metrics.RMSE:F4}\n" +
                                 $"- R²: {result.Metrics.R2:F4}\n\n";

            forecastSummary += "Interpretation:\n" +
                              "- SSA decomposes the time series into trend, seasonal, and noise components\n" +
                              "- It's effective for complex series with multiple patterns\n" +
                              "- Window size determines the decomposition resolution";

            return forecastSummary;
        }
        catch (Exception ex)
        {
            return $"Error performing SSA forecast: {ex.Message}";
        }
    }

    [KernelFunction, Description("Compare multiple forecasting models and recommend the best one")]
    public async Task<string> CompareForecastingModelsAsync(
        [Description("Array of historical values to forecast from")] double[] historicalData,
        [Description("Number of periods to forecast (default: 10)")] int forecastHorizon = 10)
    {
        try
        {
            var models = new List<(string Name, ForecastResult Result)>
            {
                ("ARIMA(1,1,1)", _forecastingService.ForecastArima(historicalData, forecastHorizon, new ArimaParameters { P = 1, D = 1, Q = 1 })),
                ("Simple Exp Smoothing", _forecastingService.ForecastExponentialSmoothing(historicalData, forecastHorizon,
                    new ExponentialSmoothingParameters { Type = SmoothingType.Simple, Alpha = 0.3 })),
                ("Double Exp Smoothing", _forecastingService.ForecastExponentialSmoothing(historicalData, forecastHorizon,
                    new ExponentialSmoothingParameters { Type = SmoothingType.Double, Alpha = 0.3, Beta = 0.1 })),
                ("SSA", _forecastingService.ForecastSSA(historicalData, forecastHorizon, 10))
            };

            var comparison = "Forecasting Model Comparison:\n" +
                           $"Data Points: {historicalData.Length}, Forecast Horizon: {forecastHorizon}\n\n" +
                           "| Model | MAE | RMSE | MAPE | R² |\n" +
                           "|-------|-----|------|------|----|\n";

            foreach (var (name, result) in models)
            {
                comparison += $"| {name} | {result.Metrics.MAE:F4} | {result.Metrics.RMSE:F4} | {result.Metrics.MAPE:P2} | {result.Metrics.R2:F4} |\n";
            }

            // Find best model based on MAE
            var bestModel = models.OrderBy(m => m.Result.Metrics.MAE).First();

            comparison += $"\nRecommended Model: {bestModel.Name}\n" +
                         $"- Lowest MAE: {bestModel.Result.Metrics.MAE:F4}\n" +
                         $"- Best fit (R²): {bestModel.Result.Metrics.R2:F4}\n\n" +
                         $"Best Model Forecast: {string.Join(", ", bestModel.Result.ForecastedValues.Select(v => v.ToString("F4")))}";

            return comparison;
        }
        catch (Exception ex)
        {
            return $"Error comparing forecasting models: {ex.Message}";
        }
    }

    [KernelFunction, Description("Analyze forecast accuracy and provide recommendations for improvement")]
    public async Task<string> AnalyzeForecastAccuracyAsync(
        [Description("Array of actual values")] double[] actualValues,
        [Description("Array of forecasted values")] double[] forecastValues,
        [Description("Name of the forecasting model used")] string modelName = "Unknown")
    {
        try
        {
            if (actualValues.Length != forecastValues.Length)
            {
                return "Error: Actual and forecast arrays must have the same length";
            }

            var errors = actualValues.Zip(forecastValues, (a, f) => a - f).ToArray();
            var absErrors = errors.Select(Math.Abs).ToArray();
            var squaredErrors = errors.Select(e => e * e).ToArray();

            var mae = absErrors.Average();
            var rmse = Math.Sqrt(squaredErrors.Average());
            var mape = actualValues.Zip(absErrors, (a, e) => a != 0 ? e / Math.Abs(a) : 0).Average();
            var smape = actualValues.Zip(forecastValues, (a, f) =>
            {
                var denominator = (Math.Abs(a) + Math.Abs(f)) / 2;
                return denominator != 0 ? Math.Abs(a - f) / denominator : 0;
            }).Average();

            var meanActual = actualValues.Average();
            var ssRes = squaredErrors.Sum();
            var ssTot = actualValues.Sum(a => Math.Pow(a - meanActual, 2));
            var r2 = ssTot != 0 ? 1 - (ssRes / ssTot) : 0;

            // Calculate Theil's U statistic
            var naiveErrors = new List<double>();
            for (int i = 1; i < actualValues.Length; i++)
            {
                naiveErrors.Add(actualValues[i] - actualValues[i - 1]);
            }
            var theilU = naiveErrors.Any() ? rmse / naiveErrors.Select(Math.Abs).Average() : double.MaxValue;

            var analysis = $"Forecast Accuracy Analysis for {modelName}:\n\n" +
                          $"Sample Size: {actualValues.Length}\n\n" +
                          $"Accuracy Metrics:\n" +
                          $"- Mean Absolute Error (MAE): {mae:F4}\n" +
                          $"- Root Mean Square Error (RMSE): {rmse:F4}\n" +
                          $"- Mean Absolute Percentage Error (MAPE): {mape:P2}\n" +
                          $"- Symmetric MAPE (SMAPE): {smape:P2}\n" +
                          $"- R-squared (R²): {r2:F4}\n" +
                          $"- Theil's U: {theilU:F4}\n\n";

            // Performance interpretation
            analysis += "Performance Interpretation:\n";

            if (mae < actualValues.Average() * 0.05)
                analysis += "- Excellent accuracy (MAE < 5% of mean)\n";
            else if (mae < actualValues.Average() * 0.10)
                analysis += "- Good accuracy (MAE < 10% of mean)\n";
            else if (mae < actualValues.Average() * 0.20)
                analysis += "- Moderate accuracy (MAE < 20% of mean)\n";
            else
                analysis += "- Poor accuracy (MAE > 20% of mean)\n";

            if (r2 > 0.8)
                analysis += "- Strong explanatory power (R² > 0.8)\n";
            else if (r2 > 0.6)
                analysis += "- Moderate explanatory power (R² > 0.6)\n";
            else
                analysis += "- Weak explanatory power (R² < 0.6)\n";

            if (theilU < 1)
                analysis += "- Better than naive forecast (Theil's U < 1)\n";
            else
                analysis += "- Worse than naive forecast (Theil's U > 1)\n";

            analysis += "\nRecommendations:\n";
            if (mae > actualValues.Average() * 0.15)
                analysis += "- Consider using a different forecasting model\n";
            if (r2 < 0.5)
                analysis += "- The model may not be capturing important patterns\n";
            if (theilU > 1.2)
                analysis += "- Even a simple naive forecast performs better\n";

            analysis += "- Consider ensemble methods or model averaging\n" +
                       "- Validate on out-of-sample data before deployment";

            return analysis;
        }
        catch (Exception ex)
        {
            return $"Error analyzing forecast accuracy: {ex.Message}";
        }
    }
}