using Microsoft.SemanticKernel;
using QuantResearchAgent.Services;
using QuantResearchAgent.Core;
using System.ComponentModel;

namespace QuantResearchAgent.Plugins;

/// <summary>
/// AutoML plugin for Semantic Kernel integration
/// </summary>
public class AutoMLPlugin
{
    private readonly AutoMLService _autoMLService;

    public AutoMLPlugin(AutoMLService autoMLService)
    {
        _autoMLService = autoMLService;
    }

    [KernelFunction("run_automl_pipeline")]
    [Description("Runs a complete AutoML pipeline for quantitative strategy development")]
    public async Task<string> RunAutoMLPipelineAsync(
        [Description("List of symbols to analyze")] List<string> symbols,
        [Description("Target variable type (price, returns, volatility)")] string targetType,
        [Description("Start date for training data (YYYY-MM-DD)")] string startDate,
        [Description("End date for training data (YYYY-MM-DD)")] string endDate,
        [Description("Maximum features to select")] int maxFeatures = 50)
    {
        try
        {
            var start = DateTime.Parse(startDate);
            var end = DateTime.Parse(endDate);

            var result = await _autoMLService.RunAutoMLPipelineAsync(
                symbols, targetType, start, end, maxFeatures);

            return $"AutoML Pipeline Results:\n" +
                   $"Target Type: {result.TargetType}\n" +
                   $"Symbols: {string.Join(", ", result.Symbols)}\n" +
                   $"Training Period: {result.TrainingPeriodStart:yyyy-MM-dd} to {result.TrainingPeriodEnd:yyyy-MM-dd}\n" +
                   $"Features Used: {result.FeatureCount}\n" +
                   $"Models Tested: {result.ModelsTested}\n" +
                   $"Best Model: {result.BestModel.ModelType}\n" +
                   $"Best Score: {result.BestModel.Performance.Score:F4}\n" +
                   $"Execution Time: {result.ExecutionTime:yyyy-MM-dd HH:mm:ss}";
        }
        catch (Exception ex)
        {
            return $"Error running AutoML pipeline: {ex.Message}";
        }
    }

    [KernelFunction("select_optimal_model")]
    [Description("Selects the optimal machine learning model for given data characteristics")]
    public async Task<string> SelectOptimalModelAsync(
        [Description("Data characteristics (time_series, cross_sectional, panel)")] string dataType,
        [Description("Prediction target type (regression, classification, forecasting)")] string targetType,
        [Description("Number of features available")] int featureCount,
        [Description("Sample size")] int sampleSize)
    {
        try
        {
            var recommendations = await _autoMLService.SelectOptimalModelAsync(
                dataType, targetType, featureCount, sampleSize);

            return $"Recommended Models for {dataType} {targetType}:\n" +
                   string.Join("\n", recommendations.Select((model, i) =>
                       $"{i + 1}. {model.ModelType} - Score: {model.Performance.Score:F4}"));
        }
        catch (Exception ex)
        {
            return $"Error selecting optimal model: {ex.Message}";
        }
    }

    [KernelFunction("perform_feature_selection")]
    [Description("Performs automated feature selection using multiple methods")]
    public async Task<string> PerformFeatureSelectionAsync(
        [Description("List of symbols")] List<string> symbols,
        [Description("Start date (YYYY-MM-DD)")] string startDate,
        [Description("End date (YYYY-MM-DD)")] string endDate,
        [Description("Selection method (recursive, importance, correlation)")] string method = "recursive")
    {
        try
        {
            var start = DateTime.Parse(startDate);
            var end = DateTime.Parse(endDate);

            // Get market data and prepare features
            var data = await PrepareTrainingDataAsync(symbols, start, end);
            var features = await _autoMLService._featureEngineeringService.GenerateFeaturesAsync(data);
            var target = data.Select(d => d.Returns).ToArray();
            var targetVector = Vector<double>.Build.DenseOfArray(target);
            var featureMatrix = Matrix<double>.Build.DenseOfRowArrays(
                features.Select(f => f.Values.ToArray()).ToArray());

            var result = await _autoMLService.PerformFeatureSelectionAsync(
                featureMatrix, targetVector, 20);

            return $"Feature Selection Results:\n" +
                   $"Original Features: {result.OriginalFeatureCount}\n" +
                   $"Selected Features: {result.FinalFeatureCount}\n" +
                   $"Top Features:\n" +
                   string.Join("\n", result.FeatureImportance
                       .OrderByDescending(f => f.Value)
                       .Take(10)
                       .Select(f => $"  {f.Key}: {f.Value:F4}"));
        }
        catch (Exception ex)
        {
            return $"Error performing feature selection: {ex.Message}";
        }
    }

    [KernelFunction("generate_ensemble_prediction")]
    [Description("Generates ensemble predictions using multiple models")]
    public async Task<string> GenerateEnsemblePredictionAsync(
        [Description("List of symbols")] List<string> symbols,
        [Description("Ensemble method (SimpleAverage, WeightedAverage, Stacking)")] string method,
        [Description("Number of models to include")] int modelCount = 5)
    {
        try
        {
            var ensembleMethod = Enum.Parse<EnsembleMethod>(method);
            
            // Create dummy models and data for demonstration
            var models = new List<ModelResult>();
            var random = new Random(42);
            for (int i = 0; i < modelCount; i++)
            {
                models.Add(new ModelResult
                {
                    ModelType = $"Model_{i}",
                    Accuracy = 0.5 + random.NextDouble() * 0.3,
                    Predictions = Vector<double>.Build.Dense(100, j => random.NextDouble())
                });
            }
            
            var featureMatrix = Matrix<double>.Build.Dense(modelCount, 10, (i, j) => random.NextDouble());
            
            var result = await _autoMLService.GenerateEnsemblePredictionAsync(
                models, featureMatrix, ensembleMethod);

            return $"Ensemble Prediction Results:\n" +
                   $"Method: {result.Method}\n" +
                   $"Models Used: {result.ModelCount}\n" +
                   $"Confidence: {result.Confidence:F4}\n" +
                   $"Predictions Generated: {result.Predictions.Count}\n" +
                   $"Generation Date: {result.GenerationDate:yyyy-MM-dd HH:mm:ss}";
        }
        catch (Exception ex)
        {
            return $"Error generating ensemble prediction: {ex.Message}";
        }
    }

    [KernelFunction("perform_cross_validation")]
    [Description("Performs cross-validation for model evaluation")]
    public async Task<string> PerformCrossValidationAsync(
        [Description("Model type to evaluate")] string modelType,
        [Description("List of symbols")] List<string> symbols,
        [Description("Number of folds")] int folds = 5)
    {
        try
        {
            var result = await _autoMLService.PerformCrossValidationAsync(
                modelType, symbols, folds);

            return $"Cross-Validation Results ({modelType}):\n" +
                   $"Folds: {result.FoldCount}\n" +
                   $"Average Score: {result.AverageScore:F4}\n" +
                   $"Score Std Dev: {result.ScoreStdDev:F4}\n" +
                   $"Fold Results:\n" +
                   string.Join("\n", result.FoldResults.Select(f =>
                       $"  Fold {f.FoldIndex + 1}: {f.Score:F4} (Train: {f.TrainingSize}, Test: {f.TestSize})"));
        }
        catch (Exception ex)
        {
            return $"Error performing cross-validation: {ex.Message}";
        }
    }

    [KernelFunction("optimize_hyperparameters")]
    [Description("Optimizes hyperparameters for a given model")]
    public async Task<string> OptimizeHyperparametersAsync(
        [Description("Model type")] string modelType,
        [Description("List of symbols")] List<string> symbols,
        [Description("Optimization method (grid, random, bayesian)")] string method = "grid")
    {
        try
        {
            var result = await _autoMLService.OptimizeHyperparametersAsync(
                modelType, symbols, method);

            return $"Hyperparameter Optimization Results ({modelType}):\n" +
                   $"Method: {method}\n" +
                   $"Combinations Tested: {result.CombinationsTested}\n" +
                   $"Best Score: {result.BestScore:F4}\n" +
                   $"Best Parameters:\n" +
                   string.Join("\n", result.BestParameters.Select(p => $"  {p.Key}: {p.Value:F4}"));
        }
        catch (Exception ex)
        {
            return $"Error optimizing hyperparameters: {ex.Message}";
        }
    }

    [KernelFunction("evaluate_model_performance")]
    [Description("Evaluates model performance with comprehensive metrics")]
    public async Task<string> EvaluateModelPerformanceAsync(
        [Description("Model type")] string modelType,
        [Description("List of symbols")] List<string> symbols,
        [Description("Start date (YYYY-MM-DD)")] string startDate,
        [Description("End date (YYYY-MM-DD)")] string endDate)
    {
        try
        {
            var start = DateTime.Parse(startDate);
            var end = DateTime.Parse(endDate);

            var result = await _autoMLService.EvaluateModelPerformanceAsync(
                modelType, symbols, start, end);

            return $"Model Performance Evaluation ({modelType}):\n" +
                   $"Score: {result.Score:F4}\n" +
                   $"MSE: {result.MSE:F6}\n" +
                   $"MAE: {result.MAE:F6}\n" +
                   $"R²: {result.R2:F4}";
        }
        catch (Exception ex)
        {
            return $"Error evaluating model performance: {ex.Message}";
        }
    }
}