using Microsoft.AspNetCore.Mvc;
using QuantResearchAgent.Services;
using QuantResearchAgent.Core;

namespace Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MachineLearningController : ControllerBase
{
    private readonly MachineLearningService _mlService;

    public MachineLearningController(MachineLearningService mlService)
    {
        _mlService = mlService;
    }

    [HttpPost("feature-engineer")]
    public async Task<ActionResult<FeatureEngineeringResult>> FeatureEngineer([FromBody] FeatureEngineerRequest request)
    {
        try
        {
            var result = await _mlService.PerformFeatureEngineeringAsync(request.Symbol, request.Days);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest($"Feature engineering failed: {ex.Message}");
        }
    }

    [HttpPost("feature-importance")]
    public async Task<ActionResult<FeatureImportanceResult>> FeatureImportance([FromBody] FeatureImportanceRequest request)
    {
        try
        {
            var featureData = await _mlService.PerformFeatureEngineeringAsync(request.Symbol, request.Days);
            var result = _mlService.AnalyzeFeatureImportance(featureData);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest($"Feature importance analysis failed: {ex.Message}");
        }
    }

    [HttpPost("feature-select")]
    public async Task<ActionResult<FeatureSelectionResult>> FeatureSelect([FromBody] FeatureSelectionRequest request)
    {
        try
        {
            var featureData = await _mlService.PerformFeatureEngineeringAsync(request.Symbol, request.Days);
            var result = _mlService.SelectFeatures(featureData, request.TopK);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest($"Feature selection failed: {ex.Message}");
        }
    }

    [HttpPost("feature-selection")]
    public async Task<ActionResult<FeatureSelectionResult>> FeatureSelection([FromBody] FeatureSelectionRequest request)
    {
        return await FeatureSelect(request);
    }

    [HttpPost("validate-model")]
    public async Task<ActionResult<ModelValidationResult>> ValidateModel([FromBody] ModelValidationRequest request)
    {
        try
        {
            var featureData = await _mlService.PerformFeatureEngineeringAsync(request.Symbol, request.Days);
            var selectedFeatures = _mlService.SelectFeatures(featureData, request.TopK);
            var result = _mlService.ValidateModel(selectedFeatures);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest($"Model validation failed: {ex.Message}");
        }
    }

    [HttpPost("cross-validate")]
    public async Task<ActionResult<CrossValidationResult>> CrossValidate([FromBody] CrossValidationRequest request)
    {
        try
        {
            var featureData = await _mlService.PerformFeatureEngineeringAsync(request.Symbol, request.Days);
            var result = _mlService.PerformCrossValidation(featureData, request.Folds);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest($"Cross-validation failed: {ex.Message}");
        }
    }

    [HttpPost("cross-validation")]
    public async Task<ActionResult<CrossValidationResult>> CrossValidation([FromBody] CrossValidationRequest request)
    {
        return await CrossValidate(request);
    }

    [HttpPost("model-metrics")]
    public ActionResult<ModelMetricsResult> ModelMetrics([FromBody] ModelMetricsRequest request)
    {
        try
        {
            var result = _mlService.CalculatePerformanceMetrics(request.Predictions, request.Actuals);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest($"Model metrics calculation failed: {ex.Message}");
        }
    }

    [HttpPost("automl-pipeline")]
    public async Task<ActionResult<AutoMLResult>> AutoMLPipeline([FromBody] AutoMLRequest request)
    {
        try
        {
            var featureData = await _mlService.PerformFeatureEngineeringAsync(request.Symbol, request.Days);
            var result = await _mlService.RunAutoMLPipelineAsync(featureData);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest($"AutoML pipeline failed: {ex.Message}");
        }
    }

    [HttpPost("model-selection")]
    public async Task<ActionResult<ModelSelectionResult>> ModelSelection([FromBody] ModelSelectionRequest request)
    {
        try
        {
            var featureData = await _mlService.PerformFeatureEngineeringAsync(request.Symbol, request.Days);
            var selectedFeatures = _mlService.SelectFeatures(featureData, request.TopK);
            
            // Run multiple models for comparison
            var models = new List<ModelComparison>();
            
            // Linear Regression
            var linearResult = _mlService.ValidateModel(selectedFeatures);
            models.Add(new ModelComparison
            {
                ModelName = "Linear Regression",
                ValidationScore = linearResult.PerformanceMetrics.GetValueOrDefault("R2", 0),
                TrainingTime = 0.1,
                Metrics = new Dictionary<string, double>
                {
                    ["R2"] = linearResult.PerformanceMetrics.GetValueOrDefault("R2", 0),
                    ["MSE"] = linearResult.PerformanceMetrics.GetValueOrDefault("MSE", 0),
                    ["MAE"] = linearResult.PerformanceMetrics.GetValueOrDefault("MAE", 0)
                }
            });

            // Cross-validation
            var cvResult = _mlService.PerformCrossValidation(featureData, 5);
            models.Add(new ModelComparison
            {
                ModelName = "Cross-Validated Model",
                ValidationScore = cvResult.AverageScore,
                TrainingTime = 0.5,
                Metrics = new Dictionary<string, double>
                {
                    ["AvgScore"] = cvResult.AverageScore,
                    ["StdDev"] = cvResult.ScoreStdDev,
                    ["Folds"] = cvResult.FoldCount
                }
            });

            var bestModel = models.OrderByDescending(m => m.ValidationScore).First();

            var result = new ModelSelectionResult
            {
                Symbol = request.Symbol,
                ModelComparisons = models,
                BestModel = bestModel.ModelName,
                BestScore = bestModel.ValidationScore,
                SelectionCriteria = "Highest R2 Score"
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest($"Model selection failed: {ex.Message}");
        }
    }

    [HttpPost("ensemble-prediction")]
    public async Task<ActionResult<EnsemblePredictionResult>> EnsemblePrediction([FromBody] EnsemblePredictionRequest request)
    {
        try
        {
            var result = _mlService.GenerateEnsemblePredictions(request.Models, request.Features);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest($"Ensemble prediction failed: {ex.Message}");
        }
    }

    [HttpPost("hyperparameter-opt")]
    public async Task<ActionResult<HyperparameterOptimizationResult>> HyperparameterOptimization([FromBody] HyperparameterOptRequest request)
    {
        try
        {
            var featureData = await _mlService.PerformFeatureEngineeringAsync(request.Symbol, request.Days);
            var selectedFeatures = _mlService.SelectFeatures(featureData, request.TopK);
            
            // Simulate hyperparameter optimization
            var trials = new List<OptimizationTrial>();
            var bestScore = 0.0;
            var bestParams = new Dictionary<string, object>();

            for (int i = 0; i < request.MaxTrials; i++)
            {
                var parameters = GenerateRandomParameters(request.ModelType);
                var score = await SimulateModelTraining(selectedFeatures, parameters);
                
                trials.Add(new OptimizationTrial
                {
                    TrialNumber = i + 1,
                    Parameters = parameters,
                    Score = score,
                    Duration = Random.Shared.NextDouble() * 10
                });

                if (score > bestScore)
                {
                    bestScore = score;
                    bestParams = parameters;
                }
            }

            var result = new HyperparameterOptimizationResult
            {
                Symbol = request.Symbol,
                ModelType = request.ModelType,
                BestParameters = bestParams,
                BestScore = bestScore,
                Trials = trials,
                OptimizationMethod = "Random Search"
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest($"Hyperparameter optimization failed: {ex.Message}");
        }
    }

    private Dictionary<string, object> GenerateRandomParameters(string modelType)
    {
        var random = Random.Shared;
        return modelType.ToLower() switch
        {
            "linear" => new Dictionary<string, object>
            {
                ["alpha"] = random.NextDouble() * 0.1,
                ["fit_intercept"] = random.Next(2) == 1
            },
            "randomforest" => new Dictionary<string, object>
            {
                ["n_estimators"] = random.Next(50, 200),
                ["max_depth"] = random.Next(3, 20),
                ["min_samples_split"] = random.Next(2, 10)
            },
            _ => new Dictionary<string, object>
            {
                ["learning_rate"] = random.NextDouble() * 0.1 + 0.01,
                ["regularization"] = random.NextDouble() * 0.01
            }
        };
    }

    private async Task<double> SimulateModelTraining(FeatureSelectionResult features, Dictionary<string, object> parameters)
    {
        // Simulate model training with random performance
        await Task.Delay(10); // Simulate training time
        return Random.Shared.NextDouble() * 0.8 + 0.1; // Random R2 between 0.1 and 0.9
    }
}

// Request models
public class FeatureEngineerRequest
{
    public string Symbol { get; set; } = "BTCUSDT";
    public int Days { get; set; } = 252;
}

public class FeatureImportanceRequest
{
    public string Symbol { get; set; } = "BTCUSDT";
    public int Days { get; set; } = 252;
}

public class FeatureSelectionRequest
{
    public string Symbol { get; set; } = "BTCUSDT";
    public int Days { get; set; } = 252;
    public int TopK { get; set; } = 10;
}

public class ModelValidationRequest
{
    public string Symbol { get; set; } = "BTCUSDT";
    public int Days { get; set; } = 252;
    public int TopK { get; set; } = 10;
}

public class CrossValidationRequest
{
    public string Symbol { get; set; } = "BTCUSDT";
    public int Days { get; set; } = 252;
    public int TopK { get; set; } = 10;
    public int Folds { get; set; } = 5;
}

public class ModelMetricsRequest
{
    public List<double> Predictions { get; set; } = new();
    public List<double> Actuals { get; set; } = new();
}

public class AutoMLRequest
{
    public string Symbol { get; set; } = "BTCUSDT";
    public int Days { get; set; } = 252;
}

public class ModelSelectionRequest
{
    public string Symbol { get; set; } = "BTCUSDT";
    public int Days { get; set; } = 252;
    public int TopK { get; set; } = 10;
}

public class EnsemblePredictionRequest
{
    public List<ModelResult> Models { get; set; } = new();
    public Dictionary<string, object> Features { get; set; } = new();
}

public class HyperparameterOptRequest
{
    public string Symbol { get; set; } = "BTCUSDT";
    public int Days { get; set; } = 252;
    public int TopK { get; set; } = 10;
    public string ModelType { get; set; } = "linear";
    public int MaxTrials { get; set; } = 20;
}

// Response models
public class ModelComparison
{
    public string ModelName { get; set; } = string.Empty;
    public double ValidationScore { get; set; }
    public double TrainingTime { get; set; }
    public Dictionary<string, double> Metrics { get; set; } = new();
}

public class ModelSelectionResult
{
    public string Symbol { get; set; } = string.Empty;
    public List<ModelComparison> ModelComparisons { get; set; } = new();
    public string BestModel { get; set; } = string.Empty;
    public double BestScore { get; set; }
    public string SelectionCriteria { get; set; } = string.Empty;
}

public class HyperparameterOptimizationResult
{
    public string Symbol { get; set; } = string.Empty;
    public string ModelType { get; set; } = string.Empty;
    public Dictionary<string, object> BestParameters { get; set; } = new();
    public double BestScore { get; set; }
    public List<OptimizationTrial> Trials { get; set; } = new();
    public string OptimizationMethod { get; set; } = string.Empty;
}

public class OptimizationTrial
{
    public int TrialNumber { get; set; }
    public Dictionary<string, object> Parameters { get; set; } = new();
    public double Score { get; set; }
    public double Duration { get; set; }
}
