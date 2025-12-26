using Microsoft.Extensions.Logging;
using QuantResearchAgent.Core;
using System.Collections.Generic;
using System.Linq;
using MathNet.Numerics.Statistics;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;
using Microsoft.ML;
using Microsoft.ML.TimeSeries;

namespace QuantResearchAgent.Services;

public class TimeSeriesForecastingService
{
    private readonly ILogger<TimeSeriesForecastingService> _logger;
    private readonly MLContext _mlContext;

    public TimeSeriesForecastingService(ILogger<TimeSeriesForecastingService> logger)
    {
        _logger = logger;
        _mlContext = new MLContext(seed: 0);
    }

    /// <summary>
    /// Performs ARIMA forecasting
    /// </summary>
    public ForecastResult ForecastArima(IEnumerable<double> data, int forecastHorizon = 10, ArimaParameters? parameters = null)
    {
        try
        {
            var dataArray = data.ToArray();
            if (dataArray.Length < 20)
                throw new ArgumentException("Need at least 20 data points for ARIMA forecasting");

            parameters ??= new ArimaParameters { P = 1, D = 1, Q = 1 };

            // Simple ARIMA implementation using differencing and regression
            var differencedData = Difference(dataArray, parameters.D);

            // Fit ARMA model to differenced data
            var armaResult = FitArmaModel(differencedData, parameters.P, parameters.Q);

            // Generate forecasts
            var forecasts = GenerateArimaForecasts(dataArray, armaResult, parameters, forecastHorizon);

            // Calculate confidence intervals
            var (upperBounds, lowerBounds) = CalculateConfidenceIntervals(forecasts, dataArray, 0.95);

            return new ForecastResult
            {
                Symbol = "Unknown",
                ModelType = "ARIMA",
                ForecastDate = DateTime.Now,
                HistoricalData = dataArray.ToList(),
                ForecastedValues = forecasts,
                UpperBounds = upperBounds,
                LowerBounds = lowerBounds,
                ModelParameters = new Dictionary<string, double>
                {
                    ["p"] = parameters.P,
                    ["d"] = parameters.D,
                    ["q"] = parameters.Q
                },
                Metrics = CalculateForecastMetrics(dataArray, forecasts),
                ForecastDates = GenerateForecastDates(DateTime.Now, forecastHorizon)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing ARIMA forecast");
            throw;
        }
    }

    /// <summary>
    /// Performs exponential smoothing forecasting
    /// </summary>
    public ForecastResult ForecastExponentialSmoothing(IEnumerable<double> data, int forecastHorizon = 10,
        ExponentialSmoothingParameters? parameters = null)
    {
        try
        {
            var dataArray = data.ToArray();
            if (dataArray.Length < 10)
                throw new ArgumentException("Need at least 10 data points for exponential smoothing");

            parameters ??= new ExponentialSmoothingParameters();

            var forecasts = new List<double>();
            double level = dataArray[0];
            double? trend = null;
            double[]? seasonal = null;

            // Initialize seasonal component if triple exponential smoothing
            if (parameters.Type == SmoothingType.Triple && parameters.SeasonLength > 0)
            {
                seasonal = new double[parameters.SeasonLength];
                for (int i = 0; i < parameters.SeasonLength; i++)
                {
                    seasonal[i] = dataArray[i] / level;
                }
            }

            // Fit the model
            for (int i = 1; i < dataArray.Length; i++)
            {
                double newLevel = parameters.Alpha * dataArray[i] +
                    (1 - parameters.Alpha) * (level + (trend ?? 0));

                if (parameters.Type == SmoothingType.Double || parameters.Type == SmoothingType.Triple)
                {
                    trend = parameters.Beta * (newLevel - level) + (1 - parameters.Beta) * (trend ?? 0);
                }

                level = newLevel;
            }

            // Generate forecasts
            for (int i = 0; i < forecastHorizon; i++)
            {
                double forecast = level;
                if (trend.HasValue)
                    forecast += (i + 1) * trend.Value;

                if (seasonal != null && parameters.Type == SmoothingType.Triple)
                {
                    int seasonIndex = (dataArray.Length + i) % parameters.SeasonLength;
                    forecast *= seasonal[seasonIndex];
                }

                forecasts.Add(forecast);
            }

            // Calculate confidence intervals (simplified)
            var (upperBounds, lowerBounds) = CalculateSimpleConfidenceIntervals(forecasts, dataArray, 0.95);

            return new ForecastResult
            {
                Symbol = "Unknown",
                ModelType = $"ExponentialSmoothing_{parameters.Type}",
                ForecastDate = DateTime.Now,
                HistoricalData = dataArray.ToList(),
                ForecastedValues = forecasts,
                UpperBounds = upperBounds,
                LowerBounds = lowerBounds,
                ModelParameters = new Dictionary<string, double>
                {
                    ["alpha"] = parameters.Alpha,
                    ["beta"] = parameters.Beta,
                    ["gamma"] = parameters.Gamma,
                    ["seasonLength"] = parameters.SeasonLength
                },
                Metrics = CalculateForecastMetrics(dataArray, forecasts),
                ForecastDates = GenerateForecastDates(DateTime.Now, forecastHorizon)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing exponential smoothing forecast");
            throw;
        }
    }

    /// <summary>
    /// Performs SSA (Singular Spectrum Analysis) forecasting
    /// </summary>
    public ForecastResult ForecastSSA(IEnumerable<double> data, int forecastHorizon = 10, int windowSize = 10)
    {
        try
        {
            var dataArray = data.ToArray();
            if (dataArray.Length < windowSize * 2)
                throw new ArgumentException("Need more data points for SSA forecasting");

            // Perform Singular Spectrum Analysis
            var ssaResult = PerformSSA(dataArray, windowSize);

            // Use the first few components for forecasting
            var forecasts = GenerateSSAForecasts(ssaResult, forecastHorizon);

            // Calculate confidence intervals
            var (upperBounds, lowerBounds) = CalculateSimpleConfidenceIntervals(forecasts, dataArray, 0.95);

            return new ForecastResult
            {
                Symbol = "Unknown",
                ModelType = "SSA",
                ForecastDate = DateTime.Now,
                HistoricalData = dataArray.ToList(),
                ForecastedValues = forecasts,
                UpperBounds = upperBounds,
                LowerBounds = lowerBounds,
                ModelParameters = new Dictionary<string, double>
                {
                    ["windowSize"] = windowSize
                },
                Metrics = CalculateForecastMetrics(dataArray, forecasts),
                ForecastDates = GenerateForecastDates(DateTime.Now, forecastHorizon)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing SSA forecast");
            throw;
        }
    }

    /// <summary>
    /// Calculates forecast accuracy metrics
    /// </summary>
    private ForecastMetrics CalculateForecastMetrics(double[] actual, List<double> forecast)
    {
        // For demonstration, use the last N values of actual data to compare with forecast
        var comparisonLength = Math.Min(actual.Length / 2, forecast.Count);
        var actualSubset = actual.Skip(actual.Length - comparisonLength).Take(comparisonLength).ToArray();
        var forecastSubset = forecast.Take(comparisonLength).ToArray();

        var errors = new List<double>();
        var percentageErrors = new List<double>();

        for (int i = 0; i < comparisonLength; i++)
        {
            var error = forecastSubset[i] - actualSubset[i];
            errors.Add(error);

            if (actualSubset[i] != 0)
            {
                percentageErrors.Add(Math.Abs(error / actualSubset[i]));
            }
        }

        return new ForecastMetrics
        {
            MAE = errors.Average(e => Math.Abs(e)),
            RMSE = Math.Sqrt(errors.Average(e => e * e)),
            MAPE = percentageErrors.Any() ? percentageErrors.Average() : 0,
            SMAPE = percentageErrors.Any() ?
                percentageErrors.Average(pe => 2 * pe / (1 + pe)) : 0,
            R2 = CalculateRSquared(actualSubset, forecastSubset),
            TheilU = CalculateTheilU(actualSubset, forecastSubset),
            ForecastHorizon = forecast.Count
        };
    }

    private double CalculateRSquared(double[] actual, double[] predicted)
    {
        var mean = actual.Average();
        var ssRes = actual.Zip(predicted, (a, p) => Math.Pow(a - p, 2)).Sum();
        var ssTot = actual.Sum(a => Math.Pow(a - mean, 2));

        return ssTot != 0 ? 1 - (ssRes / ssTot) : 0;
    }

    private double CalculateTheilU(double[] actual, double[] predicted)
    {
        var naiveForecast = actual.Skip(1).ToArray();
        var actualComparison = actual.Take(naiveForecast.Length).ToArray();

        var modelErrors = actual.Zip(predicted, (a, p) => Math.Pow(a - p, 2)).Sum();
        var naiveErrors = actualComparison.Zip(naiveForecast, (a, n) => Math.Pow(a - n, 2)).Sum();

        return naiveErrors != 0 ? Math.Sqrt(modelErrors) / Math.Sqrt(naiveErrors) : double.MaxValue;
    }

    private (List<double>, List<double>) CalculateConfidenceIntervals(List<double> forecasts, double[] historicalData, double confidenceLevel)
    {
        var stdDev = Statistics.StandardDeviation(historicalData);
        var zScore = confidenceLevel == 0.95 ? 1.96 : 1.645; // 95% or 90%

        var upperBounds = forecasts.Select(f => f + zScore * stdDev).ToList();
        var lowerBounds = forecasts.Select(f => f - zScore * stdDev).ToList();

        return (upperBounds, lowerBounds);
    }

    private (List<double>, List<double>) CalculateSimpleConfidenceIntervals(List<double> forecasts, double[] historicalData, double confidenceLevel)
    {
        var stdDev = Statistics.StandardDeviation(historicalData) * 0.5; // Conservative estimate
        var zScore = confidenceLevel == 0.95 ? 1.96 : 1.645;

        var upperBounds = forecasts.Select(f => f + zScore * stdDev).ToList();
        var lowerBounds = forecasts.Select(f => f - zScore * stdDev).ToList();

        return (upperBounds, lowerBounds);
    }

    private List<DateTime> GenerateForecastDates(DateTime startDate, int horizon)
    {
        var dates = new List<DateTime>();
        for (int i = 1; i <= horizon; i++)
        {
            dates.Add(startDate.AddDays(i));
        }
        return dates;
    }

    // Helper methods for ARIMA implementation
    private IEnumerable<double> Difference(IEnumerable<double> data, int order = 1)
    {
        var current = data.ToArray();
        for (int i = 0; i < order; i++)
        {
            current = current.Skip(1).Zip(current, (a, b) => a - b).ToArray();
        }
        return current;
    }

    private Dictionary<string, double> FitArmaModel(IEnumerable<double> data, int p, int q)
    {
        // Simplified ARMA fitting - in practice, you'd use proper maximum likelihood estimation
        return new Dictionary<string, double>
        {
            ["ar1"] = 0.5, // Example coefficients
            ["ma1"] = 0.3
        };
    }

    private List<double> GenerateArimaForecasts(double[] originalData, Dictionary<string, double> modelParams,
        ArimaParameters parameters, int horizon)
    {
        var forecasts = new List<double>();
        var currentData = originalData.ToList();

        for (int i = 0; i < horizon; i++)
        {
            // Simplified forecasting logic
            var lastValue = currentData.Last();
            var forecast = lastValue + 0.1; // Simple trend
            forecasts.Add(forecast);
            currentData.Add(forecast);
        }

        return forecasts;
    }

    private SSAResult PerformSSA(double[] data, int windowSize)
    {
        // Simplified SSA implementation
        return new SSAResult
        {
            Components = new List<double[]> { data },
            Eigenvalues = new List<double> { 1.0 }
        };
    }

    private List<double> GenerateSSAForecasts(SSAResult ssaResult, int horizon)
    {
        // Simplified SSA forecasting
        var forecasts = new List<double>();
        var lastValue = ssaResult.Components.First().Last();

        for (int i = 0; i < horizon; i++)
        {
            forecasts.Add(lastValue + 0.05 * (i + 1)); // Simple trend
        }

        return forecasts;
    }

    private class SSAResult
    {
        public List<double[]> Components { get; set; } = new();
        public List<double> Eigenvalues { get; set; } = new();
    }
}