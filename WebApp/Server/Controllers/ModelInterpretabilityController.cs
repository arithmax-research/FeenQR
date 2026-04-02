using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;
using FeenQR.Core.Models;
using FeenQR.Services;

namespace FeenQR.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ModelInterpretabilityController : ControllerBase
{
    private readonly ModelInterpretabilityService _interpretabilityService;
    private readonly ILogger<ModelInterpretabilityController> _logger;

    public ModelInterpretabilityController(
        ModelInterpretabilityService interpretabilityService,
        ILogger<ModelInterpretabilityController> logger)
    {
        _interpretabilityService = interpretabilityService;
        _logger = logger;
    }

    [HttpPost("shap-analysis")]
    public async Task<IActionResult> GetShapAnalysis([FromBody] ShapAnalysisRequest request)
    {
        try
        {
            _logger.LogInformation($"Computing SHAP values for model: {request.ModelName}");
            var result = await _interpretabilityService.AnalyzeShapValuesAsync(
                request.ModelName, 
                request.DatasetSymbol, 
                request.TopFeatures);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error computing SHAP values");
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("partial-dependence")]
    public async Task<IActionResult> GetPartialDependence([FromBody] PartialDependenceRequest request)
    {
        try
        {
            _logger.LogInformation($"Generating partial dependence plot for feature: {request.FeatureName}");
            var result = await _interpretabilityService.ComputePartialDependenceAsync(
                request.ModelName,
                request.FeatureName,
                request.GridSize);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error computing partial dependence");
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("feature-interactions")]
    public async Task<IActionResult> GetFeatureInteractions([FromBody] FeatureInteractionRequest request)
    {
        try
        {
            _logger.LogInformation("Analyzing feature interactions");
            var result = await _interpretabilityService.AnalyzeFeatureInteractionsAsync(
                request.ModelName,
                request.DatasetSymbol,
                request.TopFeaturePairs);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing feature interactions");
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("explain-prediction")]
    public async Task<IActionResult> ExplainPrediction([FromBody] ExplainPredictionRequest request)
    {
        try
        {
            _logger.LogInformation($"Explaining prediction for input: {request.InputDescription}");
            var result = await _interpretabilityService.ExplainPredictionAsync(
                request.ModelName,
                request.InputData,
                request.PredictionValue);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error explaining prediction");
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("permutation-importance")]
    public async Task<IActionResult> GetPermutationImportance([FromBody] PermutationImportanceRequest request)
    {
        try
        {
            _logger.LogInformation($"Computing permutation importance for model: {request.ModelName}");
            var result = await _interpretabilityService.ComputePermutationImportanceAsync(
                request.ModelName,
                request.DatasetSymbol,
                request.NumberOfRepeats);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error computing permutation importance");
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("model-fairness")]
    public async Task<IActionResult> GetModelFairness([FromBody] ModelFairnessRequest request)
    {
        try
        {
            _logger.LogInformation($"Analyzing model fairness for sensitive attribute: {request.SensitiveAttribute}");
            var result = await _interpretabilityService.AnalyzeModelFairnessAsync(
                request.ModelName,
                request.DatasetSymbol,
                request.SensitiveAttribute);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing model fairness");
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("interpretability-report")]
    public async Task<IActionResult> GetInterpretabilityReport([FromBody] InterpretabilityReportRequest request)
    {
        try
        {
            _logger.LogInformation($"Generating interpretability report for model: {request.ModelName}");
            var result = await _interpretabilityService.GenerateComprehensiveReportAsync(
                request.ModelName,
                request.DatasetSymbol,
                request.IncludeFairness);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating interpretability report");
            return BadRequest(new { error = ex.Message });
        }
    }
}
