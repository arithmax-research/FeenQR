using Microsoft.SemanticKernel;
using QuantResearchAgent.Services;
using QuantResearchAgent.Core;
using System.ComponentModel;
using MathNet.Numerics.LinearAlgebra;

namespace QuantResearchAgent.Plugins;

/// <summary>
/// Model interpretability plugin for Semantic Kernel integration
/// </summary>
public class ModelInterpretabilityPlugin
{
    private readonly ModelInterpretabilityService _interpretabilityService;

    public ModelInterpretabilityPlugin(ModelInterpretabilityService interpretabilityService)
    {
        _interpretabilityService = interpretabilityService;
    }

    [KernelFunction("calculate_shap_values")]
    [Description("Calculates SHAP values to explain model predictions")]
    public async Task<string> CalculateSHAPValuesAsync(
        [Description("List of symbols for analysis")] List<string> symbols,
        [Description("Start date (YYYY-MM-DD)")] string startDate,
        [Description("End date (YYYY-MM-DD)")] string endDate,
        [Description("Maximum evaluations for SHAP calculation")] int maxEvaluations = 100)
    {
        try
        {
            var start = DateTime.Parse(startDate);
            var end = DateTime.Parse(endDate);

            // Get feature matrix and predictions (simplified - would need actual model)
            var (featureMatrix, predictions, featureNames) = await GetModelDataAsync(symbols, start, end);

            var result = await _interpretabilityService.CalculateSHAPValuesAsync(
                featureMatrix, predictions, featureNames, maxEvaluations);

            return $"SHAP Analysis Results:\n" +
                   $"Base Value: {result.BaseValue:F4}\n" +
                   $"Instances Analyzed: {result.InstanceCount}\n" +
                   $"Analysis Date: {result.AnalysisDate:yyyy-MM-dd HH:mm:ss}\n\n" +
                   $"Top Feature Importance:\n" +
                   string.Join("\n", result.FeatureImportance
                       .Take(10)
                       .Select(f => $"  {f.Key}: {f.Value:F4}"));
        }
        catch (Exception ex)
        {
            return $"Error calculating SHAP values: {ex.Message}";
        }
    }

    [KernelFunction("generate_partial_dependence")]
    [Description("Generates partial dependence plots for feature analysis")]
    public async Task<string> GeneratePartialDependenceAsync(
        [Description("List of symbols")] List<string> symbols,
        [Description("Target feature for analysis")] string targetFeature,
        [Description("Start date (YYYY-MM-DD)")] string startDate,
        [Description("End date (YYYY-MM-DD)")] string endDate,
        [Description("Number of grid points")] int gridPoints = 20)
    {
        try
        {
            var start = DateTime.Parse(startDate);
            var end = DateTime.Parse(endDate);

            var (featureMatrix, predictions, featureNames) = await GetModelDataAsync(symbols, start, end);

            var result = await _interpretabilityService.GeneratePartialDependenceAsync(
                featureMatrix, predictions, featureNames, targetFeature, gridPoints);

            return $"Partial Dependence Analysis for {targetFeature}:\n" +
                   $"Grid Points: {result.GridPoints}\n" +
                   $"Feature Range: {result.FeatureGrid.Min():F2} to {result.FeatureGrid.Max():F2}\n" +
                   $"Generation Date: {result.GenerationDate:yyyy-MM-dd HH:mm:ss}\n\n" +
                   $"Partial Dependence Values (first 10):\n" +
                   string.Join("\n", result.PartialDependenceValues
                       .Take(10)
                       .Select((v, i) => $"  {result.FeatureGrid[i]:F2}: {v:F4}"));
        }
        catch (Exception ex)
        {
            return $"Error generating partial dependence: {ex.Message}";
        }
    }

    [KernelFunction("analyze_feature_interactions")]
    [Description("Analyzes interactions between features")]
    public async Task<string> AnalyzeFeatureInteractionsAsync(
        [Description("List of symbols")] List<string> symbols,
        [Description("Start date (YYYY-MM-DD)")] string startDate,
        [Description("End date (YYYY-MM-DD)")] string endDate,
        [Description("Maximum interactions to analyze")] int maxInteractions = 10)
    {
        try
        {
            var start = DateTime.Parse(startDate);
            var end = DateTime.Parse(endDate);

            var (featureMatrix, predictions, featureNames) = await GetModelDataAsync(symbols, start, end);

            var result = await _interpretabilityService.AnalyzeFeatureInteractionsAsync(
                featureMatrix, predictions, featureNames, maxInteractions);

            return $"Feature Interaction Analysis:\n" +
                   $"Total Interactions Analyzed: {result.TotalInteractionsAnalyzed}\n" +
                   $"Top Interactions Shown: {result.TopInteractions}\n" +
                   $"Analysis Date: {result.AnalysisDate:yyyy-MM-dd HH:mm:ss}\n\n" +
                   $"Top Feature Interactions:\n" +
                   string.Join("\n", result.Interactions.Select(i =>
                       $"  {i.Feature1} × {i.Feature2}: {i.InteractionStrength:F4}"));
        }
        catch (Exception ex)
        {
            return $"Error analyzing feature interactions: {ex.Message}";
        }
    }

    [KernelFunction("explain_prediction")]
    [Description("Provides detailed explanation for a specific prediction")]
    public async Task<string> ExplainPredictionAsync(
        [Description("Symbol for prediction")] string symbol,
        [Description("Prediction date (YYYY-MM-DD)")] string predictionDate)
    {
        try
        {
            var date = DateTime.Parse(predictionDate);

            // Get instance features and prediction (simplified)
            var instanceFeatures = await GetInstanceFeaturesAsync(symbol, date);
            var prediction = await GetPredictionAsync(symbol, date);
            var featureNames = await GetFeatureNamesAsync();

            var result = await _interpretabilityService.ExplainPredictionAsync(
                instanceFeatures, featureNames, prediction);

            return $"Prediction Explanation for {symbol} on {predictionDate}:\n" +
                   $"Predicted Value: {result.Prediction:F4}\n" +
                   $"Base Value: {result.BaseValue:F4}\n" +
                   $"Explanation Date: {result.ExplanationDate:yyyy-MM-dd HH:mm:ss}\n\n" +
                   $"Top Positive Contributors:\n" +
                   string.Join("\n", result.TopPositiveFeatures.Select(f => $"  {f.Key}: +{f.Value:F4}")) +
                   $"\n\nTop Negative Contributors:\n" +
                   string.Join("\n", result.TopNegativeFeatures.Select(f => $"  {f.Key}: {f.Value:F4}"));
        }
        catch (Exception ex)
        {
            return $"Error explaining prediction: {ex.Message}";
        }
    }

    [KernelFunction("calculate_permutation_importance")]
    [Description("Calculates feature importance using permutation method")]
    public async Task<string> CalculatePermutationImportanceAsync(
        [Description("List of symbols")] List<string> symbols,
        [Description("Start date (YYYY-MM-DD)")] string startDate,
        [Description("End date (YYYY-MM-DD)")] string endDate,
        [Description("Number of permutations")] int permutations = 100)
    {
        try
        {
            var start = DateTime.Parse(startDate);
            var end = DateTime.Parse(endDate);

            var (featureMatrix, predictions, featureNames) = await GetModelDataAsync(symbols, start, end);

            var result = await _interpretabilityService.CalculatePermutationImportanceAsync(
                featureMatrix, predictions, featureNames, permutations);

            return $"Permutation Feature Importance:\n" +
                   $"Base Score: {result.BaseScore:F4}\n" +
                   $"Permutations Used: {result.PermutationsUsed}\n" +
                   $"Calculation Date: {result.CalculationDate:yyyy-MM-dd HH:mm:ss}\n\n" +
                   $"Feature Importance (drop in performance):\n" +
                   string.Join("\n", result.FeatureImportance
                       .Take(10)
                       .Select(f => $"  {f.Key}: {f.Value:F6}"));
        }
        catch (Exception ex)
        {
            return $"Error calculating permutation importance: {ex.Message}";
        }
    }

    [KernelFunction("analyze_model_fairness")]
    [Description("Analyzes model fairness across different groups")]
    public async Task<string> AnalyzeModelFairnessAsync(
        [Description("List of symbols")] List<string> symbols,
        [Description("Start date (YYYY-MM-DD)")] string startDate,
        [Description("End date (YYYY-MM-DD)")] string endDate,
        [Description("Protected group definitions")] Dictionary<string, List<int>> protectedGroups)
    {
        try
        {
            var start = DateTime.Parse(startDate);
            var end = DateTime.Parse(endDate);

            var (featureMatrix, predictions, _) = await GetModelDataAsync(symbols, start, end);
            var actuals = await GetActualValuesAsync(symbols, start, end);

            var result = await _interpretabilityService.AnalyzeModelFairnessAsync(
                featureMatrix, predictions, actuals, protectedGroups);

            return $"Model Fairness Analysis:\n" +
                   $"Protected Groups: {string.Join(", ", result.ProtectedGroups)}\n" +
                   $"Overall Fairness Score: {result.OverallFairness:F4}\n" +
                   $"Analysis Date: {result.AnalysisDate:yyyy-MM-dd HH:mm:ss}\n\n" +
                   $"Group Metrics:\n" +
                   string.Join("\n", result.GroupMetrics.Select(g =>
                       $"  {g.Key}: Accuracy={g.Value.Accuracy:F4}, F1={g.Value.F1Score:F4}"));
        }
        catch (Exception ex)
        {
            return $"Error analyzing model fairness: {ex.Message}";
        }
    }

    [KernelFunction("generate_interpretability_report")]
    [Description("Generates a comprehensive model interpretability report")]
    public async Task<string> GenerateInterpretabilityReportAsync(
        [Description("List of symbols")] List<string> symbols,
        [Description("Start date (YYYY-MM-DD)")] string startDate,
        [Description("End date (YYYY-MM-DD)")] string endDate)
    {
        try
        {
            var start = DateTime.Parse(startDate);
            var end = DateTime.Parse(endDate);

            var (featureMatrix, predictions, featureNames) = await GetModelDataAsync(symbols, start, end);

            // Generate multiple analyses
            var shapResult = await _interpretabilityService.CalculateSHAPValuesAsync(
                featureMatrix, predictions, featureNames, 50);

            var permutationResult = await _interpretabilityService.CalculatePermutationImportanceAsync(
                featureMatrix, predictions, featureNames, 50);

            var interactionResult = await _interpretabilityService.AnalyzeFeatureInteractionsAsync(
                featureMatrix, predictions, featureNames, 5);

            return $"Comprehensive Model Interpretability Report\n" +
                   $"=====================================\n\n" +
                   $"Analysis Period: {start:yyyy-MM-dd} to {end:yyyy-MM-dd}\n" +
                   $"Symbols: {string.Join(", ", symbols)}\n\n" +
                   $"SHAP ANALYSIS:\n" +
                   $"- Base Value: {shapResult.BaseValue:F4}\n" +
                   $"- Instances: {shapResult.InstanceCount}\n" +
                   $"- Top Features: {string.Join(", ", shapResult.FeatureImportance.Keys.Take(5))}\n\n" +
                   $"FEATURE IMPORTANCE (Permutation):\n" +
                   $"- Base Score: {permutationResult.BaseScore:F4}\n" +
                   $"- Top Features: {string.Join(", ", permutationResult.FeatureImportance.Keys.Take(5))}\n\n" +
                   $"FEATURE INTERACTIONS:\n" +
                   string.Join("\n", interactionResult.Interactions.Select(i =>
                       $"- {i.Feature1} × {i.Feature2}: {i.InteractionStrength:F4}")) +
                   $"\n\nReport Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}";
        }
        catch (Exception ex)
        {
            return $"Error generating interpretability report: {ex.Message}";
        }
    }

    // Helper methods (simplified implementations)
    private async Task<(Matrix<double>, Vector<double>, List<string>)> GetModelDataAsync(
        List<string> symbols, DateTime start, DateTime end)
    {
        // Simplified - would integrate with actual data services
        var random = new Random();
        var featureCount = 20;
        var sampleCount = 100;

        var featureMatrix = Matrix<double>.Build.Dense(sampleCount, featureCount);
        var predictions = Vector<double>.Build.Dense(sampleCount);
        var featureNames = new List<string>();

        for (int i = 0; i < featureCount; i++)
        {
            featureNames.Add($"Feature_{i + 1}");
        }

        for (int i = 0; i < sampleCount; i++)
        {
            for (int j = 0; j < featureCount; j++)
            {
                featureMatrix[i, j] = random.NextDouble();
            }
            predictions[i] = random.NextDouble();
        }

        return (featureMatrix, predictions, featureNames);
    }

    private async Task<Vector<double>> GetInstanceFeaturesAsync(string symbol, DateTime date)
    {
        // Simplified
        var random = new Random();
        return Vector<double>.Build.Dense(20, i => random.NextDouble());
    }

    private async Task<double> GetPredictionAsync(string symbol, DateTime date)
    {
        // Simplified
        var random = new Random();
        return random.NextDouble();
    }

    private async Task<List<string>> GetFeatureNamesAsync()
    {
        return Enumerable.Range(1, 20).Select(i => $"Feature_{i}").ToList();
    }

    private async Task<Vector<double>> GetActualValuesAsync(List<string> symbols, DateTime start, DateTime end)
    {
        // Simplified
        var random = new Random();
        return Vector<double>.Build.Dense(100, i => random.NextDouble());
    }
}