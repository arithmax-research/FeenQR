using System;
using System.Collections.Generic;

namespace QuantResearchAgent.Core;

/// <summary>
/// Represents a time series forecast result
/// </summary>
public class ForecastResult
{
    public string Symbol { get; set; } = string.Empty;
    public string ModelType { get; set; } = string.Empty; // ARIMA, SARIMA, ExponentialSmoothing, Prophet
    public DateTime ForecastDate { get; set; }
    public List<double> HistoricalData { get; set; } = new();
    public List<double> ForecastedValues { get; set; } = new();
    public List<double> UpperBounds { get; set; } = new();
    public List<double> LowerBounds { get; set; } = new();
    public Dictionary<string, double> ModelParameters { get; set; } = new();
    public ForecastMetrics Metrics { get; set; } = new();
    public List<DateTime> ForecastDates { get; set; } = new();
}

/// <summary>
/// Forecast accuracy and performance metrics
/// </summary>
public class ForecastMetrics
{
    public double MAE { get; set; } // Mean Absolute Error
    public double RMSE { get; set; } // Root Mean Square Error
    public double MAPE { get; set; } // Mean Absolute Percentage Error
    public double SMAPE { get; set; } // Symmetric Mean Absolute Percentage Error
    public double R2 { get; set; } // R-squared
    public double TheilU { get; set; } // Theil's U statistic
    public int ForecastHorizon { get; set; }
}

/// <summary>
/// ARIMA model parameters
/// </summary>
public class ArimaParameters
{
    public int P { get; set; } // Autoregressive order
    public int D { get; set; } // Differencing order
    public int Q { get; set; } // Moving average order
    public bool IncludeIntercept { get; set; } = true;
    public int SeasonalP { get; set; } // Seasonal autoregressive order
    public int SeasonalD { get; set; } // Seasonal differencing order
    public int SeasonalQ { get; set; } // Seasonal moving average order
    public int SeasonLength { get; set; } // Seasonal period length
}

/// <summary>
/// Exponential smoothing parameters
/// </summary>
public class ExponentialSmoothingParameters
{
    public double Alpha { get; set; } = 0.3; // Level smoothing parameter
    public double Beta { get; set; } = 0.1; // Trend smoothing parameter
    public double Gamma { get; set; } = 0.1; // Seasonal smoothing parameter
    public int SeasonLength { get; set; } = 12;
    public SmoothingType Type { get; set; } = SmoothingType.Simple;
}

/// <summary>
/// Types of exponential smoothing
/// </summary>
public enum SmoothingType
{
    Simple,
    Double,
    Triple
}

/// <summary>
/// Feature engineering result
/// </summary>
public class FeatureEngineeringResult
{
    public string Symbol { get; set; } = string.Empty;
    public Dictionary<string, List<double>> TechnicalIndicators { get; set; } = new();
    public Dictionary<string, List<double>> LaggedFeatures { get; set; } = new();
    public Dictionary<string, List<double>> RollingStatistics { get; set; } = new();
    public List<DateTime> Dates { get; set; } = new();
    public FeatureImportanceResult FeatureImportance { get; set; } = new();
}

/// <summary>
/// Feature importance analysis result
/// </summary>
public class FeatureImportanceResult
{
    public Dictionary<string, double> FeatureScores { get; set; } = new();
    public string Method { get; set; } = string.Empty; // Correlation, MutualInformation, TreeBased
}

/// <summary>
/// Model validation result
/// </summary>
public class ModelValidationResult
{
    public string ModelType { get; set; } = string.Empty;
    public ValidationType ValidationType { get; set; }
    public List<double> TrainingErrors { get; set; } = new();
    public List<double> ValidationErrors { get; set; } = new();
    public List<double> TestErrors { get; set; } = new();
    public Dictionary<string, double> PerformanceMetrics { get; set; } = new();
    public List<WalkForwardResult> WalkForwardResults { get; set; } = new();
}

/// <summary>
/// Walk-forward analysis result
/// </summary>
public class WalkForwardResult
{
    public int FoldNumber { get; set; }
    public DateTime TrainingEndDate { get; set; }
    public DateTime ValidationStartDate { get; set; }
    public DateTime ValidationEndDate { get; set; }
    public double TrainingScore { get; set; }
    public double ValidationScore { get; set; }
    public Dictionary<string, double> Parameters { get; set; } = new();
}

/// <summary>
/// Types of model validation
/// </summary>
public enum ValidationType
{
    CrossValidation,
    WalkForward,
    OutOfSample
}