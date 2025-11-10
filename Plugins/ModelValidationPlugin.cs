using Microsoft.SemanticKernel;
using QuantResearchAgent.Services;
using QuantResearchAgent.Core;
using System.ComponentModel;
using System.Text.Json;

namespace QuantResearchAgent.Plugins;

/// <summary>
/// Semantic Kernel plugin for model validation and performance analysis
/// </summary>
public class ModelValidationPlugin
{
    private readonly ModelValidationService _modelValidationService;

    public ModelValidationPlugin(ModelValidationService modelValidationService)
    {
        _modelValidationService = modelValidationService;
    }

    [KernelFunction, Description("Perform k-fold cross-validation on a model")]
    public async Task<string> PerformCrossValidationAsync(
        [Description("Array of feature values")] double[] features,
        [Description("Array of target values")] double[] targets,
        [Description("Number of folds for cross-validation (default: 5)")] int k = 5,
        [Description("Model type name")] string modelType = "LinearRegression")
    {
        try
        {
            var result = _modelValidationService.PerformCrossValidation(features, targets, k, modelType);

            var summary = $"Cross-Validation Results for {result.ModelType}:\n\n" +
                         $"Validation Type: {result.ValidationType}\n" +
                         $"Number of Folds: {k}\n" +
                         $"Data Points: {features.Length}\n\n" +
                         $"Performance Metrics:\n" +
                         $"- Mean Training Error: {result.PerformanceMetrics["Mean_Training_Error"]:F4}\n" +
                         $"- Mean Validation Error: {result.PerformanceMetrics["Mean_Validation_Error"]:F4}\n" +
                         $"- Mean Test Error: {result.PerformanceMetrics["Mean_Test_Error"]:F4}\n" +
                         $"- Training Error Std: {result.PerformanceMetrics["Training_Error_Std"]:F4}\n" +
                         $"- Validation Error Std: {result.PerformanceMetrics["Validation_Error_Std"]:F4}\n" +
                         $"- Validation Stability: {result.PerformanceMetrics["Validation_Stability"]:F4}\n\n";

            // Add interpretation
            var stability = result.PerformanceMetrics["Validation_Stability"];
            var overfitting = result.PerformanceMetrics["Mean_Validation_Error"] > result.PerformanceMetrics["Mean_Training_Error"];

            summary += "Interpretation:\n";
            if (stability > 0.8)
                summary += "- High validation stability - model performs consistently\n";
            else if (stability > 0.6)
                summary += "- Moderate validation stability - acceptable performance\n";
            else
                summary += "- Low validation stability - model may be unstable\n";

            if (overfitting)
                summary += "- Signs of overfitting - validation error > training error\n";
            else
                summary += "- No strong overfitting detected\n";

            summary += "\nRecommendations:\n";
            if (stability < 0.7)
                summary += "- Consider using more regularization\n";
            if (overfitting)
                summary += "- Try reducing model complexity or using early stopping\n";
            summary += "- Consider ensemble methods for better stability";

            return summary;
        }
        catch (Exception ex)
        {
            return $"Error performing cross-validation: {ex.Message}";
        }
    }

    [KernelFunction, Description("Perform walk-forward analysis for time series models")]
    public async Task<string> PerformWalkForwardAnalysisAsync(
        [Description("Array of feature values")] double[] features,
        [Description("Array of target values")] double[] targets,
        [Description("Initial training set size (default: 50)")] int initialTrainSize = 50,
        [Description("Test set size for each fold (default: 10)")] int testSize = 10,
        [Description("Model type name")] string modelType = "TimeSeries")
    {
        try
        {
            var result = _modelValidationService.PerformWalkForwardAnalysis(features, targets, initialTrainSize, testSize, modelType);

            var summary = $"Walk-Forward Analysis Results for {result.ModelType}:\n\n" +
                         $"Validation Type: {result.ValidationType}\n" +
                         $"Initial Training Size: {initialTrainSize}\n" +
                         $"Test Size per Fold: {testSize}\n" +
                         $"Total Folds: {result.WalkForwardResults.Count}\n\n" +
                         $"Performance Metrics:\n" +
                         $"- Mean Training Error: {result.PerformanceMetrics["Mean_Training_Error"]:F4}\n" +
                         $"- Mean Validation Error: {result.PerformanceMetrics["Mean_Validation_Error"]:F4}\n" +
                         $"- Walk-Forward Stability: {result.PerformanceMetrics["Walk_Forward_Stability"]:F4}\n" +
                         $"- Average Improvement: {result.PerformanceMetrics["Average_Improvement"]:F4}\n\n";

            // Show last few folds
            summary += "Recent Fold Results:\n";
            var recentFolds = result.WalkForwardResults.TakeLast(3).ToList();
            foreach (var fold in recentFolds)
            {
                summary += $"Fold {fold.FoldNumber}: Train Error = {fold.TrainingScore:F4}, Val Error = {fold.ValidationScore:F4}\n";
            }

            // Add interpretation
            var stability = result.PerformanceMetrics["Walk_Forward_Stability"];
            var improvement = result.PerformanceMetrics["Average_Improvement"];

            summary += "\nInterpretation:\n";
            if (stability > 0.8)
                summary += "- High temporal stability - model adapts well to new data\n";
            else if (stability > 0.6)
                summary += "- Moderate temporal stability - acceptable adaptation\n";
            else
                summary += "- Low temporal stability - model struggles with new data\n";

            if (improvement > 0)
                summary += "- Model shows improvement over time\n";
            else
                summary += "- Model performance may degrade over time\n";

            summary += "\nWalk-forward analysis is ideal for:\n" +
                      "- Time series forecasting models\n" +
                      "- Models where temporal ordering matters\n" +
                      "- Detecting concept drift or changing relationships";

            return summary;
        }
        catch (Exception ex)
        {
            return $"Error performing walk-forward analysis: {ex.Message}";
        }
    }

    [KernelFunction, Description("Perform out-of-sample testing to evaluate model generalization")]
    public async Task<string> PerformOutOfSampleTestingAsync(
        [Description("Array of feature values")] double[] features,
        [Description("Array of target values")] double[] targets,
        [Description("Training data ratio (0.0-1.0, default: 0.7)")] double trainRatio = 0.7,
        [Description("Model type name")] string modelType = "Regression")
    {
        try
        {
            var result = _modelValidationService.PerformOutOfSampleTesting(features, targets, trainRatio, modelType);

            var summary = $"Out-of-Sample Testing Results for {result.ModelType}:\n\n" +
                         $"Validation Type: {result.ValidationType}\n" +
                         $"Training Ratio: {trainRatio:P1}\n" +
                         $"Training Size: {result.PerformanceMetrics["Train_Size"]:F0}\n" +
                         $"Test Size: {result.PerformanceMetrics["Test_Size"]:F0}\n\n" +
                         $"Performance Metrics:\n" +
                         $"- Training Error (MAE): {result.PerformanceMetrics["Training_Error"]:F4}\n" +
                         $"- Test Error (MAE): {result.PerformanceMetrics["Test_Error"]:F4}\n" +
                         $"- Overfitting Ratio: {result.PerformanceMetrics["Overfitting_Ratio"]:F4}\n" +
                         $"- Training RMSE: {result.PerformanceMetrics["Training_RMSE"]:F4}\n" +
                         $"- Test RMSE: {result.PerformanceMetrics["Test_RMSE"]:F4}\n" +
                         $"- Training R²: {result.PerformanceMetrics["Training_R2"]:F4}\n" +
                         $"- Test R²: {result.PerformanceMetrics["Test_R2"]:F4}\n\n";

            // Add interpretation
            var overfittingRatio = result.PerformanceMetrics["Overfitting_Ratio"];
            var testR2 = result.PerformanceMetrics["Test_R2"];

            summary += "Interpretation:\n";
            if (overfittingRatio < 1.2)
                summary += "- Low overfitting - good generalization\n";
            else if (overfittingRatio < 2.0)
                summary += "- Moderate overfitting - acceptable generalization\n";
            else
                summary += "- High overfitting - poor generalization\n";

            if (testR2 > 0.8)
                summary += "- Excellent explanatory power on unseen data\n";
            else if (testR2 > 0.6)
                summary += "- Good explanatory power on unseen data\n";
            else if (testR2 > 0.3)
                summary += "- Moderate explanatory power on unseen data\n";
            else
                summary += "- Poor explanatory power on unseen data\n";

            summary += "\nRecommendations:\n";
            if (overfittingRatio > 1.5)
                summary += "- Consider regularization techniques\n- Reduce model complexity\n- Gather more training data\n";
            if (testR2 < 0.5)
                summary += "- Model may not capture important patterns\n- Consider feature engineering\n- Try different algorithms\n";

            return summary;
        }
        catch (Exception ex)
        {
            return $"Error performing out-of-sample testing: {ex.Message}";
        }
    }

    [KernelFunction, Description("Calculate comprehensive model performance metrics")]
    public async Task<string> CalculateModelMetricsAsync(
        [Description("Array of actual values")] double[] actual,
        [Description("Array of predicted values")] double[] predicted,
        [Description("Model name for reporting")] string modelName = "Model")
    {
        try
        {
            var metrics = _modelValidationService.CalculateModelMetrics(actual, predicted, modelName);

            var summary = $"Comprehensive Metrics for {modelName}:\n\n" +
                         $"Basic Error Metrics:\n" +
                         $"- MAE (Mean Absolute Error): {metrics["MAE"]:F4}\n" +
                         $"- RMSE (Root Mean Square Error): {metrics["RMSE"]:F4}\n" +
                         $"- MAPE (Mean Absolute % Error): {metrics["MAPE"]:P2}\n" +
                         $"- SMAPE (Symmetric MAPE): {metrics["SMAPE"]:P2}\n" +
                         $"- MedAE (Median Absolute Error): {metrics["MedAE"]:F4}\n\n" +
                         $"Goodness of Fit:\n" +
                         $"- R² (Coefficient of Determination): {metrics["R2"]:F4}\n" +
                         $"- Adjusted R²: {metrics["Adjusted_R2"]:F4}\n" +
                         $"- MSE (Mean Squared Error): {metrics["MSE"]:F4}\n\n" +
                         $"Residual Analysis:\n" +
                         $"- Residual Mean: {metrics["Residual_Mean"]:F6}\n" +
                         $"- Residual Std Dev: {metrics["Residual_Std"]:F4}\n" +
                         $"- Residual Skewness: {metrics["Residual_Skewness"]:F4}\n" +
                         $"- Residual Kurtosis: {metrics["Residual_Kurtosis"]:F4}\n\n" +
                         $"Information Criteria:\n" +
                         $"- AIC (Akaike): {metrics["AIC"]:F2}\n" +
                         $"- BIC (Bayesian): {metrics["BIC"]:F2}\n\n";

            // Add interpretation
            summary += "Interpretation:\n";

            var r2 = metrics["R2"];
            if (r2 > 0.8)
                summary += "- Excellent fit (R² > 0.8)\n";
            else if (r2 > 0.6)
                summary += "- Good fit (R² > 0.6)\n";
            else if (r2 > 0.3)
                summary += "- Moderate fit (R² > 0.3)\n";
            else
                summary += "- Poor fit (R² < 0.3)\n";

            var residualSkewness = metrics["Residual_Skewness"];
            if (Math.Abs(residualSkewness) < 0.5)
                summary += "- Residuals approximately symmetric\n";
            else if (residualSkewness > 0.5)
                summary += "- Residuals right-skewed (positive skew)\n";
            else
                summary += "- Residuals left-skewed (negative skew)\n";

            var residualKurtosis = metrics["Residual_Kurtosis"];
            if (Math.Abs(residualKurtosis - 3) < 0.5)
                summary += "- Residuals approximately normal\n";
            else if (residualKurtosis > 3.5)
                summary += "- Residuals have heavy tails (leptokurtic)\n";
            else if (residualKurtosis < 2.5)
                summary += "- Residuals have light tails (platykurtic)\n";

            return summary;
        }
        catch (Exception ex)
        {
            return $"Error calculating model metrics: {ex.Message}";
        }
    }

    [KernelFunction, Description("Compare multiple models and recommend the best one")]
    public async Task<string> CompareModelsAsync(
        [Description("Dictionary of model results as JSON string")] string modelsJson)
    {
        try
        {
            var models = JsonSerializer.Deserialize<Dictionary<string, ModelValidationResult>>(modelsJson);

            if (models == null || !models.Any())
                return "Error: Invalid models JSON or no models provided";

            var comparison = _modelValidationService.CompareModels(models);

            return comparison + "\n\n" +
                   "Model Selection Guidelines:\n" +
                   "- Choose model with lowest validation error\n" +
                   "- Prefer models with high stability scores\n" +
                   "- Consider computational complexity and interpretability\n" +
                   "- Validate final choice with out-of-sample testing\n" +
                   "- Use ensemble methods if no single model dominates";
        }
        catch (Exception ex)
        {
            return $"Error comparing models: {ex.Message}";
        }
    }

    [KernelFunction, Description("Generate model validation report with recommendations")]
    public async Task<string> GenerateValidationReportAsync(
        [Description("Array of feature values")] double[] features,
        [Description("Array of target values")] double[] targets,
        [Description("Model type name")] string modelType = "Regression")
    {
        try
        {
            // Perform multiple validation techniques
            var crossVal = _modelValidationService.PerformCrossValidation(features, targets, 5, modelType);
            var outOfSample = _modelValidationService.PerformOutOfSampleTesting(features, targets, 0.7, modelType);

            // Simple model fit for metrics
            var trainSize = (int)(features.Length * 0.7);
            var trainFeatures = features.Take(trainSize).ToArray();
            var trainTargets = targets.Take(trainSize).ToArray();
            var testFeatures = features.Skip(trainSize).ToArray();
            var testTargets = targets.Skip(trainSize).ToArray();

            // Fit simple model
            var model = FitSimpleLinearModel(trainFeatures, trainTargets);
            var predictions = PredictSimpleLinearModel(model, testFeatures);
            var metrics = _modelValidationService.CalculateModelMetrics(testTargets, predictions, modelType);

            var report = $"MODEL VALIDATION REPORT - {modelType.ToUpper()}\n" +
                        $"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n\n" +
                        $"DATA OVERVIEW\n" +
                        $"- Total observations: {features.Length}\n" +
                        $"- Training set: {trainSize} ({(trainSize/(double)features.Length):P1})\n" +
                        $"- Test set: {features.Length - trainSize} ({((features.Length - trainSize)/(double)features.Length):P1})\n\n" +
                        $"CROSS-VALIDATION RESULTS (5-fold)\n" +
                        $"- Mean training error: {crossVal.PerformanceMetrics["Mean_Training_Error"]:F4}\n" +
                        $"- Mean validation error: {crossVal.PerformanceMetrics["Mean_Validation_Error"]:F4}\n" +
                        $"- Validation stability: {crossVal.PerformanceMetrics["Validation_Stability"]:F4}\n\n" +
                        $"OUT-OF-SAMPLE PERFORMANCE\n" +
                        $"- Training MAE: {outOfSample.PerformanceMetrics["Training_Error"]:F4}\n" +
                        $"- Test MAE: {outOfSample.PerformanceMetrics["Test_Error"]:F4}\n" +
                        $"- Overfitting ratio: {outOfSample.PerformanceMetrics["Overfitting_Ratio"]:F4}\n" +
                        $"- Test R²: {outOfSample.PerformanceMetrics["Test_R2"]:F4}\n\n" +
                        $"MODEL METRICS\n" +
                        $"- MAE: {metrics["MAE"]:F4}\n" +
                        $"- RMSE: {metrics["RMSE"]:F4}\n" +
                        $"- R²: {metrics["R2"]:F4}\n" +
                        $"- AIC: {metrics["AIC"]:F2}\n\n" +
                        $"RECOMMENDATIONS\n";

            // Generate recommendations based on results
            var testR2 = outOfSample.PerformanceMetrics["Test_R2"];
            var overfittingRatio = outOfSample.PerformanceMetrics["Overfitting_Ratio"];
            var stability = crossVal.PerformanceMetrics["Validation_Stability"];

            if (testR2 > 0.7 && overfittingRatio < 1.3 && stability > 0.7)
            {
                report += "✓ MODEL APPROVED: Good fit, low overfitting, stable performance\n";
                report += "  → Ready for production use with monitoring\n";
            }
            else if (testR2 > 0.5 && overfittingRatio < 2.0)
            {
                report += "⚠ MODEL NEEDS IMPROVEMENT: Acceptable performance but can be enhanced\n";
                if (overfittingRatio > 1.5) report += "  → Add regularization or reduce complexity\n";
                if (stability < 0.6) report += "  → Improve stability through ensemble methods\n";
                if (testR2 < 0.6) report += "  → Enhance feature engineering\n";
            }
            else
            {
                report += "✗ MODEL REJECTED: Poor performance, high overfitting, or unstable\n";
                report += "  → Consider different algorithm or more data\n";
                report += "  → Review feature selection and preprocessing\n";
            }

            return report;
        }
        catch (Exception ex)
        {
            return $"Error generating validation report: {ex.Message}";
        }
    }

    // Helper methods
    private double[] FitSimpleLinearModel(double[] features, double[] targets)
    {
        var n = features.Length;
        var sumX = features.Sum();
        var sumY = targets.Sum();
        var sumXY = features.Zip(targets, (x, y) => x * y).Sum();
        var sumX2 = features.Sum(x => x * x);

        var slope = (n * sumXY - sumX * sumY) / (n * sumX2 - sumX * sumX);
        var intercept = (sumY - slope * sumX) / n;

        return new[] { slope, intercept };
    }

    private double[] PredictSimpleLinearModel(double[] model, double[] features)
    {
        var slope = model[0];
        var intercept = model[1];
        return features.Select(x => slope * x + intercept).ToArray();
    }
}