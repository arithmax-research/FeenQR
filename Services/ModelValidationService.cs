using Microsoft.Extensions.Logging;
using QuantResearchAgent.Core;
using System.Collections.Generic;
using System.Linq;
using MathNet.Numerics.Statistics;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;

namespace QuantResearchAgent.Services;

public class ModelValidationService
{
    private readonly ILogger<ModelValidationService> _logger;

    public ModelValidationService(ILogger<ModelValidationService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Perform k-fold cross-validation
    /// </summary>
    public ModelValidationResult PerformCrossValidation(
        IEnumerable<double> features,
        IEnumerable<double> targets,
        int k = 5,
        string modelType = "LinearRegression")
    {
        try
        {
            var featureArray = features.ToArray();
            var targetArray = targets.ToArray();

            if (featureArray.Length != targetArray.Length || featureArray.Length < k)
            {
                throw new ArgumentException("Insufficient data for cross-validation");
            }

            var foldSize = featureArray.Length / k;
            var trainingErrors = new List<double>();
            var validationErrors = new List<double>();
            var testErrors = new List<double>();

            for (int fold = 0; fold < k; fold++)
            {
                var (trainFeatures, trainTargets, valFeatures, valTargets) =
                    SplitDataForFold(featureArray, targetArray, fold, foldSize);

                // Simple linear regression for demonstration
                var model = FitLinearModel(trainFeatures, trainTargets);
                var trainPredictions = PredictLinearModel(model, trainFeatures);
                var valPredictions = PredictLinearModel(model, valFeatures);

                // Calculate errors
                trainingErrors.Add(CalculateMAE(trainTargets, trainPredictions));
                validationErrors.Add(CalculateMAE(valTargets, valPredictions));

                // Use validation error as test error for simplicity
                testErrors.Add(validationErrors.Last());
            }

            var performanceMetrics = new Dictionary<string, double>
            {
                ["Mean_Training_Error"] = trainingErrors.Average(),
                ["Mean_Validation_Error"] = validationErrors.Average(),
                ["Mean_Test_Error"] = testErrors.Average(),
                ["Training_Error_Std"] = Statistics.StandardDeviation(trainingErrors),
                ["Validation_Error_Std"] = Statistics.StandardDeviation(validationErrors),
                ["Validation_Stability"] = 1.0 / (1.0 + Statistics.StandardDeviation(validationErrors))
            };

            return new ModelValidationResult
            {
                ModelType = modelType,
                ValidationType = ValidationType.CrossValidation,
                TrainingErrors = trainingErrors,
                ValidationErrors = validationErrors,
                TestErrors = testErrors,
                PerformanceMetrics = performanceMetrics
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing cross-validation");
            throw;
        }
    }

    /// <summary>
    /// Perform walk-forward analysis for time series models
    /// </summary>
    public ModelValidationResult PerformWalkForwardAnalysis(
        IEnumerable<double> features,
        IEnumerable<double> targets,
        int initialTrainSize = 50,
        int testSize = 10,
        string modelType = "TimeSeries")
    {
        try
        {
            var featureArray = features.ToArray();
            var targetArray = targets.ToArray();

            if (featureArray.Length < initialTrainSize + testSize)
            {
                throw new ArgumentException("Insufficient data for walk-forward analysis");
            }

            var walkForwardResults = new List<WalkForwardResult>();
            var currentTrainSize = initialTrainSize;

            while (currentTrainSize + testSize <= featureArray.Length)
            {
                var trainFeatures = featureArray.Take(currentTrainSize).ToArray();
                var trainTargets = targetArray.Take(currentTrainSize).ToArray();
                var testFeatures = featureArray.Skip(currentTrainSize).Take(testSize).ToArray();
                var testTargets = targetArray.Skip(currentTrainSize).Take(testSize).ToArray();

                // Fit model and make predictions
                var model = FitLinearModel(trainFeatures, trainTargets);
                var trainPredictions = PredictLinearModel(model, trainFeatures);
                var testPredictions = PredictLinearModel(model, testFeatures);

                // Calculate scores
                var trainingScore = CalculateMAE(trainTargets, trainPredictions);
                var validationScore = CalculateMAE(testTargets, testPredictions);

                walkForwardResults.Add(new WalkForwardResult
                {
                    FoldNumber = walkForwardResults.Count + 1,
                    TrainingEndDate = DateTime.Now.AddDays(-featureArray.Length + currentTrainSize),
                    ValidationStartDate = DateTime.Now.AddDays(-featureArray.Length + currentTrainSize),
                    ValidationEndDate = DateTime.Now.AddDays(-featureArray.Length + currentTrainSize + testSize),
                    TrainingScore = trainingScore,
                    ValidationScore = validationScore,
                    Parameters = new Dictionary<string, double> { ["train_size"] = currentTrainSize }
                });

                currentTrainSize += testSize;
            }

            var trainingErrors = walkForwardResults.Select(r => r.TrainingScore).ToList();
            var validationErrors = walkForwardResults.Select(r => r.ValidationScore).ToList();

            var performanceMetrics = new Dictionary<string, double>
            {
                ["Mean_Training_Error"] = trainingErrors.Average(),
                ["Mean_Validation_Error"] = validationErrors.Average(),
                ["Walk_Forward_Stability"] = 1.0 / (1.0 + Statistics.StandardDeviation(validationErrors)),
                ["Total_Folds"] = walkForwardResults.Count,
                ["Average_Improvement"] = trainingErrors.Zip(validationErrors, (t, v) => t - v).Average()
            };

            return new ModelValidationResult
            {
                ModelType = modelType,
                ValidationType = ValidationType.WalkForward,
                TrainingErrors = trainingErrors,
                ValidationErrors = validationErrors,
                TestErrors = validationErrors, // Same as validation for walk-forward
                PerformanceMetrics = performanceMetrics,
                WalkForwardResults = walkForwardResults
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing walk-forward analysis");
            throw;
        }
    }

    /// <summary>
    /// Perform out-of-sample testing
    /// </summary>
    public ModelValidationResult PerformOutOfSampleTesting(
        IEnumerable<double> features,
        IEnumerable<double> targets,
        double trainRatio = 0.7,
        string modelType = "Regression")
    {
        try
        {
            var featureArray = features.ToArray();
            var targetArray = targets.ToArray();

            var trainSize = (int)(featureArray.Length * trainRatio);
            var testSize = featureArray.Length - trainSize;

            var trainFeatures = featureArray.Take(trainSize).ToArray();
            var trainTargets = targetArray.Take(trainSize).ToArray();
            var testFeatures = featureArray.Skip(trainSize).ToArray();
            var testTargets = targetArray.Skip(trainSize).ToArray();

            // Fit model on training data
            var model = FitLinearModel(trainFeatures, trainTargets);

            // Make predictions
            var trainPredictions = PredictLinearModel(model, trainFeatures);
            var testPredictions = PredictLinearModel(model, testFeatures);

            // Calculate metrics
            var trainingError = CalculateMAE(trainTargets, trainPredictions);
            var testError = CalculateMAE(testTargets, testPredictions);
            var overfittingRatio = testError / trainingError;

            var performanceMetrics = new Dictionary<string, double>
            {
                ["Training_Error"] = trainingError,
                ["Test_Error"] = testError,
                ["Overfitting_Ratio"] = overfittingRatio,
                ["Train_Size"] = trainSize,
                ["Test_Size"] = testSize,
                ["Train_Test_Ratio"] = trainRatio
            };

            // Calculate additional metrics
            performanceMetrics["Training_RMSE"] = CalculateRMSE(trainTargets, trainPredictions);
            performanceMetrics["Test_RMSE"] = CalculateRMSE(testTargets, testPredictions);
            performanceMetrics["Training_R2"] = CalculateR2(trainTargets, trainPredictions);
            performanceMetrics["Test_R2"] = CalculateR2(testTargets, testPredictions);

            return new ModelValidationResult
            {
                ModelType = modelType,
                ValidationType = ValidationType.OutOfSample,
                TrainingErrors = new List<double> { trainingError },
                ValidationErrors = new List<double> { testError },
                TestErrors = new List<double> { testError },
                PerformanceMetrics = performanceMetrics
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing out-of-sample testing");
            throw;
        }
    }

    /// <summary>
    /// Calculate comprehensive model performance metrics
    /// </summary>
    public Dictionary<string, double> CalculateModelMetrics(
        IEnumerable<double> actual,
        IEnumerable<double> predicted,
        string modelName = "Model")
    {
        try
        {
            var actualArray = actual.ToArray();
            var predictedArray = predicted.ToArray();

            if (actualArray.Length != predictedArray.Length)
            {
                throw new ArgumentException("Actual and predicted arrays must have the same length");
            }

            var metrics = new Dictionary<string, double>
            {
                ["MAE"] = CalculateMAE(actualArray, predictedArray),
                ["RMSE"] = CalculateRMSE(actualArray, predictedArray),
                ["MAPE"] = CalculateMAPE(actualArray, predictedArray),
                ["SMAPE"] = CalculateSMAPE(actualArray, predictedArray),
                ["R2"] = CalculateR2(actualArray, predictedArray),
                ["Adjusted_R2"] = CalculateAdjustedR2(actualArray, predictedArray, 1), // Assuming 1 predictor
                ["MSE"] = CalculateMSE(actualArray, predictedArray),
                ["MedAE"] = CalculateMedAE(actualArray, predictedArray)
            };

            // Additional statistical tests
            var residuals = actualArray.Zip(predictedArray, (a, p) => a - p).ToArray();
            metrics["Residual_Mean"] = residuals.Average();
            metrics["Residual_Std"] = Statistics.StandardDeviation(residuals);
            metrics["Residual_Skewness"] = Statistics.Skewness(residuals);
            metrics["Residual_Kurtosis"] = Statistics.Kurtosis(residuals);

            // Information criteria (simplified)
            var n = actualArray.Length;
            var k = 1; // Number of parameters (simplified)
            var logLikelihood = CalculateLogLikelihood(residuals);
            metrics["AIC"] = 2 * k - 2 * logLikelihood;
            metrics["BIC"] = k * Math.Log(n) - 2 * logLikelihood;

            return metrics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating model metrics");
            throw;
        }
    }

    /// <summary>
    /// Perform model comparison and selection
    /// </summary>
    public string CompareModels(Dictionary<string, ModelValidationResult> models)
    {
        try
        {
            var comparison = "Model Comparison Results:\n\n";
            comparison += "| Model | Validation Type | Mean Error | Stability | R² |\n";
            comparison += "|-------|----------------|------------|-----------|----|\n";

            foreach (var model in models)
            {
                var result = model.Value;
                var meanError = result.PerformanceMetrics.GetValueOrDefault("Mean_Validation_Error", 0);
                var stability = result.PerformanceMetrics.GetValueOrDefault("Validation_Stability", 0);
                var r2 = result.PerformanceMetrics.GetValueOrDefault("Test_R2", 0);

                comparison += $"| {model.Key} | {result.ValidationType} | {meanError:F4} | {stability:F4} | {r2:F4} |\n";
            }

            // Find best model
            var bestModel = models.OrderBy(m => m.Value.PerformanceMetrics.GetValueOrDefault("Mean_Validation_Error", double.MaxValue)).First();

            comparison += $"\nRecommended Model: {bestModel.Key}\n";
            comparison += $"- Best validation performance\n";
            comparison += $"- Error: {bestModel.Value.PerformanceMetrics.GetValueOrDefault("Mean_Validation_Error", 0):F4}\n";
            comparison += $"- Stability: {bestModel.Value.PerformanceMetrics.GetValueOrDefault("Validation_Stability", 0):F4}\n";

            return comparison;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error comparing models");
            throw;
        }
    }

    // Helper methods

    private (double[], double[], double[], double[]) SplitDataForFold(
        double[] features, double[] targets, int fold, int foldSize)
    {
        var startIdx = fold * foldSize;
        var endIdx = Math.Min(startIdx + foldSize, features.Length);

        var valFeatures = features.Skip(startIdx).Take(foldSize).ToArray();
        var valTargets = targets.Skip(startIdx).Take(foldSize).ToArray();

        var trainFeatures = features.Take(startIdx).Concat(features.Skip(endIdx)).ToArray();
        var trainTargets = targets.Take(startIdx).Concat(targets.Skip(endIdx)).ToArray();

        return (trainFeatures, trainTargets, valFeatures, valTargets);
    }

    private double[] FitLinearModel(double[] features, double[] targets)
    {
        // Simple linear regression: y = mx + b
        var n = features.Length;
        var sumX = features.Sum();
        var sumY = targets.Sum();
        var sumXY = features.Zip(targets, (x, y) => x * y).Sum();
        var sumX2 = features.Sum(x => x * x);

        var slope = (n * sumXY - sumX * sumY) / (n * sumX2 - sumX * sumX);
        var intercept = (sumY - slope * sumX) / n;

        return new[] { slope, intercept };
    }

    private double[] PredictLinearModel(double[] model, double[] features)
    {
        var slope = model[0];
        var intercept = model[1];
        return features.Select(x => slope * x + intercept).ToArray();
    }

    private double CalculateMAE(double[] actual, double[] predicted)
    {
        return actual.Zip(predicted, (a, p) => Math.Abs(a - p)).Average();
    }

    private double CalculateRMSE(double[] actual, double[] predicted)
    {
        var squaredErrors = actual.Zip(predicted, (a, p) => Math.Pow(a - p, 2));
        return Math.Sqrt(squaredErrors.Average());
    }

    private double CalculateMAPE(double[] actual, double[] predicted)
    {
        return actual.Zip(predicted, (a, p) => a != 0 ? Math.Abs((a - p) / a) : 0).Average();
    }

    private double CalculateSMAPE(double[] actual, double[] predicted)
    {
        return actual.Zip(predicted, (a, p) =>
        {
            var denominator = (Math.Abs(a) + Math.Abs(p)) / 2;
            return denominator != 0 ? Math.Abs(a - p) / denominator : 0;
        }).Average();
    }

    private double CalculateR2(double[] actual, double[] predicted)
    {
        var mean = actual.Average();
        var ssRes = actual.Zip(predicted, (a, p) => Math.Pow(a - p, 2)).Sum();
        var ssTot = actual.Sum(a => Math.Pow(a - mean, 2));
        return ssTot != 0 ? 1 - (ssRes / ssTot) : 0;
    }

    private double CalculateAdjustedR2(double[] actual, double[] predicted, int numPredictors)
    {
        var r2 = CalculateR2(actual, predicted);
        var n = actual.Length;
        return 1 - ((1 - r2) * (n - 1) / (n - numPredictors - 1));
    }

    private double CalculateMSE(double[] actual, double[] predicted)
    {
        return actual.Zip(predicted, (a, p) => Math.Pow(a - p, 2)).Average();
    }

    private double CalculateMedAE(double[] actual, double[] predicted)
    {
        var absErrors = actual.Zip(predicted, (a, p) => Math.Abs(a - p)).ToArray();
        Array.Sort(absErrors);
        var mid = absErrors.Length / 2;
        return absErrors.Length % 2 == 0
            ? (absErrors[mid - 1] + absErrors[mid]) / 2
            : absErrors[mid];
    }

    private double CalculateLogLikelihood(double[] residuals)
    {
        var n = residuals.Length;
        var sigma2 = residuals.Sum(r => r * r) / n;
        var logLikelihood = -0.5 * n * Math.Log(2 * Math.PI * sigma2) - (1 / (2 * sigma2)) * residuals.Sum(r => r * r);
        return logLikelihood;
    }
}