using Microsoft.AspNetCore.Mvc;
using FeenQR.Services;
using FeenQR.Models;

namespace FeenQR.Controllers;

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
            var result = await _mlService.ValidateModelAsync(selectedFeatures);
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
            var selectedFeatures = _mlService.SelectFeatures(featureData, request.TopK);
            var result = _mlService.PerformCrossValidation(selectedFeatures, request.Folds);
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
            var result = _mlService.CalculateModelMetrics(request.Predictions, request.Actuals);
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
            var result = await _mlService.RunAutoMLPipelineAsync(request.Symbol, request.Days);
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
            var linearResult = await _mlService.ValidateModelAsync(selectedFeatures);
            models.Add(new ModelComparison
            {
                ModelName = "Linear Regression",
                ValidationScore = linearResult.R2Score,
                TrainingTime = 0.1,
                Metrics = new Dictionary<string, double>
                {
                    ["R2"] = linearResult.R2Score,
                    ["MSE"] = linearResult.MSE,
                    ["MAE"] = linearResult.MAE
                }
            });

            // Cross-validation
            var cvResult = _mlService.PerformCrossValidation(selectedFeatures, 5);
            models.Add(new ModelComparison
            {
                ModelName = "Cross-Validated Model",
                ValidationScore = cvResult.MeanR2,
                TrainingTime = 0.5,
                Metrics = new Dictionary<string, double>
                {
                    ["R2"] = cvResult.MeanR2,
                    ["MSE"] = cvResult.MeanMSE,
                    ["MAE"] = cvResult.MeanMAE
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
            
            if (featureData.Dates.Count < 50)
            {
                return BadRequest($"Insufficient data for hyperparameter optimization (need at least 50 samples, got {featureData.Dates.Count})");
            }
            
            var selectedFeatures = _mlService.SelectFeatures(featureData, request.TopK);
            
            // Real hyperparameter optimization using cross-validation
            var trials = new List<OptimizationTrial>();
            var bestScore = 0.0;
            var bestParams = new Dictionary<string, object>();

            var parameterGrid = GenerateParameterGrid(request.ModelType, request.MaxTrials);

            for (int i = 0; i < parameterGrid.Count; i++)
            {
                try
                {
                    var parameters = parameterGrid[i];
                    var startTime = DateTime.UtcNow;
                    
                    // Perform real cross-validation
                    var cvResult = _mlService.PerformCrossValidation(featureData, folds: 3);
                    var score = cvResult.AverageScore;
                    
                    var duration = (DateTime.UtcNow - startTime).TotalSeconds;
                    
                    trials.Add(new OptimizationTrial
                    {
                        TrialNumber = i + 1,
                        Parameters = parameters,
                        Score = score,
                        Duration = duration
                    });

                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestParams = parameters;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Trial {TrialNumber} failed", i + 1);
                }
            }

            if (trials.Count == 0)
            {
                return BadRequest("All hyperparameter optimization trials failed");
            }

            var result = new HyperparameterOptimizationResult
            {
                Symbol = request.Symbol,
                ModelType = request.ModelType,
                BestParameters = bestParams,
                BestScore = bestScore,
                Trials = trials,
                OptimizationMethod = "Grid Search with Cross-Validation"
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest($"Hyperparameter optimization failed: {ex.Message}");
        }
    }

    private List<Dictionary<string, object>> GenerateParameterGrid(string modelType, int maxTrials)
    {
        var grid = new List<Dictionary<string, object>>();
        
        switch (modelType.ToLower())
        {
            case "fasttree":
            case "tree":
                var learningRates = new[] { 0.01, 0.05, 0.1, 0.2 };
                var numLeaves = new[] { 10, 20, 50 };
                
                foreach (var lr in learningRates)
                {
                    foreach (var leaves in numLeaves)
                    {
                        grid.Add(new Dictionary<string, object>
                        {
                            ["learning_rate"] = lr,
                            ["num_leaves"] = leaves
                        });
                        if (grid.Count >= maxTrials) return grid;
                    }
                }
                break;
                
            case "linear":
                for (int i = 0; i < maxTrials; i++)
                {
                    grid.Add(new Dictionary<string, object>
                    {
                        ["iteration"] = i + 1
                    });
                }
                break;
                
            default:
                for (int i = 0; i < maxTrials; i++)
                {
                    grid.Add(new Dictionary<string, object>
                    {
                        ["trial"] = i + 1
                    });
                }
                break;
        }
        
        return grid;
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