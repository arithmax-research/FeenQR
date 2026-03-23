using Microsoft.AspNetCore.Mvc;
using QuantResearchAgent.Services;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StrategyBuilderController : ControllerBase
{
    private readonly ILogger<StrategyBuilderController> _logger;
    private readonly LeanStrategyPipelineService _pipelineService;
    private readonly LeanDataService _leanDataService;
    private readonly StrategyTextParser _textParser;
    private readonly DataRequirementAdvisor _dataAdvisor;

    public StrategyBuilderController(
        ILogger<StrategyBuilderController> logger,
        LeanStrategyPipelineService pipelineService,
        LeanDataService leanDataService,
        StrategyTextParser textParser,
        DataRequirementAdvisor dataAdvisor)
    {
        _logger = logger;
        _pipelineService = pipelineService;
        _leanDataService = leanDataService;
        _textParser = textParser;
        _dataAdvisor = dataAdvisor;
    }

    [HttpPost("plan")]
    public async Task<IActionResult> BuildPlan([FromBody] StrategyPlanRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.StrategyText))
        {
            return BadRequest(new { error = "strategyText is required" });
        }

        // Parse strategy with context-aware extraction
        var extracted = _textParser.Parse(request.StrategyText);
        _logger.LogInformation("Extracted strategy with {ConfidenceLevel:P} confidence. Warnings: {WarningCount}", 
            extracted.OverallConfidence, extracted.Warnings.Count);

        // Also run legacy parser for backward compat
        var plan = _pipelineService.ParseStrategyText(request.StrategyText);
        var dataPlan = _pipelineService.BuildDataPlan(plan);

        // Use AI to determine required data types
        var requiredDataTypes = await _dataAdvisor.AnalyzeDataRequirementsAsync(request.StrategyText);
        _logger.LogInformation("AI-determined data requirements: {Types}", string.Join(", ", requiredDataTypes));

        var coverage = await _leanDataService.CheckDataCoverageAsync(
            plan.Symbol,
            dataPlan.AssetClass.Equals("crypto", StringComparison.OrdinalIgnoreCase),
            dataPlan.Resolution,
            dataPlan.StartDate,
            dataPlan.EndDate,
            requiredDataTypes);

        return Ok(new
        {
            type = "strategy-plan",
            timestamp = DateTime.UtcNow,
            plan,
            dataPlan,
            dataAvailability = new
            {
                symbol = plan.Symbol,
                hasLocalLeanData = coverage.HasAnyLocalData,
                hasRequestedRangeCoverage = coverage.HasRequestedRangeCoverage,
                coverageEstimated = coverage.CoverageEstimated,
                earliestAvailableDate = coverage.EarliestAvailableDate,
                latestAvailableDate = coverage.LatestAvailableDate,
                coverageMessage = coverage.Message,
                auxiliaryDataAvailable = coverage.AuxiliaryDataAvailable,
                requiresDownload = !coverage.HasRequestedRangeCoverage
            },
            extraction = new
            {
                overallConfidence = extracted.OverallConfidence,
                indicators = extracted.Indicators.Select(i => new
                {
                    type = i.IndicatorType,
                    parameters = i.Parameters,
                    confidence = i.ConfidenceLevel
                }),
                entrySignals = extracted.EntrySignals.Select(s => new
                {
                    type = s.SignalType,
                    confidence = s.ConfidenceLevel
                }),
                exitSignals = extracted.ExitSignals.Select(s => new
                {
                    type = s.SignalType,
                    confidence = s.ConfidenceLevel
                }),
                positionSizing = extracted.PositionSizing != null ? new
                {
                    method = extracted.PositionSizing.Method,
                    parameters = extracted.PositionSizing.Parameters,
                    confidence = extracted.PositionSizing.ConfidenceLevel
                } : null,
                riskManagement = extracted.RiskManagement,
                warnings = extracted.Warnings,
                symbolConfidence = extracted.SymbolConfidence,
                assetClassConfidence = extracted.AssetClassConfidence,
                dataSourceConfidence = extracted.DataSourceConfidence
            }
        });
    }

    [HttpPost("run")]
    public async Task<IActionResult> RunStrategy([FromBody] StrategyRunRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.StrategyText))
        {
            return BadRequest(new { error = "strategyText is required" });
        }

        var plan = _pipelineService.ParseStrategyText(request.StrategyText);

        if (request.StartDate.HasValue) plan.StartDate = request.StartDate.Value.Date;
        if (request.EndDate.HasValue) plan.EndDate = request.EndDate.Value.Date;
        if (request.InitialCapital.HasValue) plan.InitialCapital = request.InitialCapital.Value;
        if (!string.IsNullOrWhiteSpace(request.Resolution)) plan.Resolution = request.Resolution!.Trim().ToLowerInvariant();
        if (request.PositionSizePercent.HasValue) plan.PositionSizePercent = request.PositionSizePercent.Value;

        var dataPlan = _pipelineService.BuildDataPlan(plan);

        // Use AI to determine required data types
        var requiredDataTypes = await _dataAdvisor.AnalyzeDataRequirementsAsync(request.StrategyText);
        _logger.LogInformation("AI-determined data requirements: {Types}", string.Join(", ", requiredDataTypes));

        var coverage = await _leanDataService.CheckDataCoverageAsync(
            plan.Symbol,
            dataPlan.AssetClass.Equals("crypto", StringComparison.OrdinalIgnoreCase),
            dataPlan.Resolution,
            dataPlan.StartDate,
            dataPlan.EndDate,
            requiredDataTypes);

        var dataDownloadTriggered = false;
        var dataDownloadSucceeded = coverage.HasRequestedRangeCoverage;

        if (!coverage.HasRequestedRangeCoverage)
        {
            if (!request.ConfirmDataDownload)
            {
                return Ok(new
                {
                    type = "strategy-run-blocked",
                    timestamp = DateTime.UtcNow,
                    message = "Data missing. Confirm data download first.",
                    plan,
                    dataPlan,
                    dataAvailability = new
                    {
                        symbol = plan.Symbol,
                        hasLocalLeanData = coverage.HasAnyLocalData,
                        hasRequestedRangeCoverage = coverage.HasRequestedRangeCoverage,
                        coverageEstimated = coverage.CoverageEstimated,
                        earliestAvailableDate = coverage.EarliestAvailableDate,
                        latestAvailableDate = coverage.LatestAvailableDate,
                        coverageMessage = coverage.Message,
                        requiresDownload = true
                    }
                });
            }

            dataDownloadTriggered = true;
            dataDownloadSucceeded = await _leanDataService.TriggerDataDownloadAsync(
                plan.Symbol,
                dataPlan.PreferredSource,
                dataPlan.Resolution,
                dataPlan.StartDate,
                dataPlan.EndDate);
        }

        var runId = $"{Sanitize(plan.Symbol)}_{DateTime.UtcNow:yyyyMMdd_HHmmss}";
        var strategyDir = Path.Combine(Directory.GetCurrentDirectory(), "strategies", "generated");
        var backtestDir = Path.Combine(Directory.GetCurrentDirectory(), "backtests", runId);

        Directory.CreateDirectory(strategyDir);
        Directory.CreateDirectory(backtestDir);

        var algorithmCode = await _pipelineService.GenerateLeanPythonAlgorithmAsync(plan);
        var strategyFilePath = Path.Combine(strategyDir, $"{runId}.py");
        await System.IO.File.WriteAllTextAsync(strategyFilePath, algorithmCode, cancellationToken);

        var strategyMeta = new
        {
            runId,
            generatedAtUtc = DateTime.UtcNow,
            plan,
            dataPlan,
            dataDownloadTriggered,
            dataDownloadSucceeded,
            strategyFilePath
        };

        var strategyMetaPath = Path.Combine(strategyDir, $"{runId}.json");
        await System.IO.File.WriteAllTextAsync(
            strategyMetaPath,
            JsonSerializer.Serialize(strategyMeta, new JsonSerializerOptions { WriteIndented = true }),
            cancellationToken);

        var leanResult = await _pipelineService.RunLeanBacktestAsync(strategyFilePath, backtestDir, cancellationToken);

        return Ok(new
        {
            type = "strategy-run-result",
            timestamp = DateTime.UtcNow,
            runId,
            plan,
            dataPlan,
            dataDownload = new
            {
                triggered = dataDownloadTriggered,
                succeeded = dataDownloadSucceeded
            },
            artifacts = new
            {
                strategyFilePath,
                strategyMetaPath,
                backtestOutputDirectory = backtestDir
            },
            lean = leanResult
        });
    }

    [HttpPost("run-agentic")]
    public async Task<IActionResult> RunStrategyAgentic([FromBody] StrategyRunRequest request, CancellationToken cancellationToken)
    {
        var timeline = new List<AgenticProgressEvent>();

        // Extract strategy info upfront
        var extracted = _textParser.Parse(request.StrategyText);
        _logger.LogInformation("Agentic run parsing strategy with {Confidence:P} overall confidence", extracted.OverallConfidence);

        var outcome = await ExecuteAgenticRunAsync(
            request,
            extracted,
            progress =>
            {
                timeline.Add(progress);
                return Task.CompletedTask;
            },
            cancellationToken);

        return Ok(BuildOutcomeResponse(outcome, timeline));
    }

    [HttpPost("run-agentic-stream")]
    public async Task RunStrategyAgenticStream([FromBody] StrategyRunRequest request, CancellationToken cancellationToken)
    {
        Response.StatusCode = 200;
        Response.ContentType = "application/x-ndjson";

        async Task EmitStreamEventAsync(object payload)
        {
            var json = JsonSerializer.Serialize(payload);
            await Response.WriteAsync(json + "\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }

        var timeline = new List<AgenticProgressEvent>();

        try
        {
            var extracted = _textParser.Parse(request.StrategyText);
            _logger.LogInformation("Agentic stream parsing strategy with {Confidence:P} overall confidence", extracted.OverallConfidence);

            var outcome = await ExecuteAgenticRunAsync(
                request,
                extracted,
                async progress =>
                {
                    timeline.Add(progress);
                    await EmitStreamEventAsync(progress);
                },
                cancellationToken);

            await EmitStreamEventAsync(new
            {
                type = "final",
                payload = BuildOutcomeResponse(outcome, timeline)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Agentic strategy stream failed");

            await EmitStreamEventAsync(new
            {
                type = "error",
                agent = "Backtest Runner",
                phase = "run",
                timestamp = DateTime.UtcNow,
                message = ex.Message
            });

            await EmitStreamEventAsync(new
            {
                type = "final",
                payload = new
                {
                    type = "strategy-run-error",
                    timestamp = DateTime.UtcNow,
                    message = ex.Message,
                    timeline
                }
            });
        }
    }

    private async Task<AgenticRunOutcome> ExecuteAgenticRunAsync(
        StrategyRunRequest request,
        ExtractedStrategy extracted,
        Func<AgenticProgressEvent, Task>? emit,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.StrategyText))
        {
            throw new InvalidOperationException("strategyText is required");
        }

        async Task EmitAsync(string agent, string phase, string message, object? data = null)
        {
            if (emit is null)
            {
                return;
            }

            await emit(new AgenticProgressEvent
            {
                Agent = agent,
                Phase = phase,
                Message = message,
                Data = data
            });
        }

        await EmitAsync(
            "DeepSeek Strategist",
            "plan",
            "Analyzing strategy description and extracting Lean parameters.");

        var plan = _pipelineService.ParseStrategyText(request.StrategyText);

        if (request.StartDate.HasValue) plan.StartDate = request.StartDate.Value.Date;
        if (request.EndDate.HasValue) plan.EndDate = request.EndDate.Value.Date;
        if (request.InitialCapital.HasValue) plan.InitialCapital = request.InitialCapital.Value;
        if (!string.IsNullOrWhiteSpace(request.Resolution)) plan.Resolution = request.Resolution!.Trim().ToLowerInvariant();
        if (request.PositionSizePercent.HasValue) plan.PositionSizePercent = request.PositionSizePercent.Value;

        await EmitAsync(
            "DeepSeek Strategist",
            "plan",
            $"Strategy parsed for symbol {plan.Symbol}.",
            new { plan.Symbol, plan.StartDate, plan.EndDate, plan.Resolution });

        var dataPlan = _pipelineService.BuildDataPlan(plan);

        await EmitAsync(
            "Data Scout",
            "data",
            "Checking local Lean data folders and source requirements.",
            new { plan.Symbol, dataPlan.PreferredSource, dataPlan.Resolution });

        // Use AI to determine required data types
        var requiredDataTypes = await _dataAdvisor.AnalyzeDataRequirementsAsync(request.StrategyText);
        _logger.LogInformation("AI-determined data requirements: {Types}", string.Join(", ", requiredDataTypes));

        var coverage = await _leanDataService.CheckDataCoverageAsync(
            plan.Symbol,
            dataPlan.AssetClass.Equals("crypto", StringComparison.OrdinalIgnoreCase),
            dataPlan.Resolution,
            dataPlan.StartDate,
            dataPlan.EndDate,
            requiredDataTypes);

        await EmitAsync(
            "Data Scout",
            "data",
            coverage.HasRequestedRangeCoverage
                ? "Requested range appears covered by local data (best effort check)."
                : "Could not confirm local coverage for requested range.",
            new
            {
                plan.Symbol,
                hasLocalLeanData = coverage.HasAnyLocalData,
                hasRequestedRangeCoverage = coverage.HasRequestedRangeCoverage,
                coverageEstimated = coverage.CoverageEstimated,
                earliestAvailableDate = coverage.EarliestAvailableDate,
                latestAvailableDate = coverage.LatestAvailableDate,
                coverageMessage = coverage.Message,
                requiresDownload = !coverage.HasRequestedRangeCoverage
            });

        var outcome = new AgenticRunOutcome
        {
            Type = "strategy-run-result",
            Plan = plan,
            DataPlan = dataPlan,
            HasLocalLeanData = coverage.HasAnyLocalData,
            HasRequestedRangeCoverage = coverage.HasRequestedRangeCoverage,
            CoverageEstimated = coverage.CoverageEstimated,
            EarliestAvailableDate = coverage.EarliestAvailableDate,
            LatestAvailableDate = coverage.LatestAvailableDate,
            CoverageMessage = coverage.Message,
            RequiresDownload = !coverage.HasRequestedRangeCoverage,
            DataDownloadSucceeded = coverage.HasRequestedRangeCoverage
        };

        if (!coverage.HasRequestedRangeCoverage)
        {
            if (!request.ConfirmDataDownload)
            {
                outcome.Type = "strategy-run-blocked";
                outcome.Message = "Data missing. Confirm data download first.";

                await EmitAsync(
                    "Data Scout",
                    "data",
                    "Execution paused because download confirmation is required.");

                return outcome;
            }

            outcome.DataDownloadTriggered = true;

            await EmitAsync(
                "Data Scout",
                "data",
                "Downloading missing data before Lean execution.");

            outcome.DataDownloadSucceeded = await _leanDataService.TriggerDataDownloadAsync(
                plan.Symbol,
                dataPlan.PreferredSource,
                dataPlan.Resolution,
                dataPlan.StartDate,
                dataPlan.EndDate);

            await EmitAsync(
                "Data Scout",
                "data",
                outcome.DataDownloadSucceeded
                    ? "Data download completed."
                    : "Data download attempted but did not fully succeed.",
                new { outcome.DataDownloadTriggered, outcome.DataDownloadSucceeded });
        }

        var runId = $"{Sanitize(plan.Symbol)}_{DateTime.UtcNow:yyyyMMdd_HHmmss}";
        var strategyDir = Path.Combine(Directory.GetCurrentDirectory(), "strategies", "generated");
        var backtestDir = Path.Combine(Directory.GetCurrentDirectory(), "backtests", runId);

        Directory.CreateDirectory(strategyDir);
        Directory.CreateDirectory(backtestDir);

        await EmitAsync(
            "Lean Builder",
            "build",
            "Generating Lean algorithm and run metadata artifacts.");

        var algorithmCode = await _pipelineService.GenerateLeanPythonAlgorithmAsync(plan);
        var strategyFilePath = Path.Combine(strategyDir, $"{runId}.py");
        await System.IO.File.WriteAllTextAsync(strategyFilePath, algorithmCode, cancellationToken);

        var strategyMeta = new
        {
            runId,
            generatedAtUtc = DateTime.UtcNow,
            plan,
            dataPlan,
            dataDownloadTriggered = outcome.DataDownloadTriggered,
            dataDownloadSucceeded = outcome.DataDownloadSucceeded,
            strategyFilePath
        };

        var strategyMetaPath = Path.Combine(strategyDir, $"{runId}.json");
        await System.IO.File.WriteAllTextAsync(
            strategyMetaPath,
            JsonSerializer.Serialize(strategyMeta, new JsonSerializerOptions { WriteIndented = true }),
            cancellationToken);

        outcome.RunId = runId;
        outcome.StrategyFilePath = strategyFilePath;
        outcome.StrategyMetaPath = strategyMetaPath;
        outcome.BacktestOutputDirectory = backtestDir;

        await EmitAsync(
            "Backtest Runner",
            "run",
            "Executing Lean backtest engine.",
            new { runId, strategyFilePath, backtestDir });

        var leanResult = await _pipelineService.RunLeanBacktestAsync(strategyFilePath, backtestDir, cancellationToken);
        outcome.Lean = leanResult;

        await EmitAsync(
            "Backtest Runner",
            "run",
            leanResult.Success
                ? "Lean backtest completed successfully."
                : "Lean backtest completed with errors.",
            new { leanResult.Success, leanResult.ExitCode, leanResult.Message });

        return outcome;
    }

    private static object BuildOutcomeResponse(AgenticRunOutcome outcome, IReadOnlyCollection<AgenticProgressEvent> timeline)
    {
        if (outcome.Type.Equals("strategy-run-blocked", StringComparison.OrdinalIgnoreCase))
        {
            return new
            {
                type = outcome.Type,
                timestamp = DateTime.UtcNow,
                message = outcome.Message,
                plan = outcome.Plan,
                dataPlan = outcome.DataPlan,
                dataAvailability = new
                {
                    symbol = outcome.Plan?.Symbol,
                    hasLocalLeanData = outcome.HasLocalLeanData,
                    hasRequestedRangeCoverage = outcome.HasRequestedRangeCoverage,
                    coverageEstimated = outcome.CoverageEstimated,
                    earliestAvailableDate = outcome.EarliestAvailableDate,
                    latestAvailableDate = outcome.LatestAvailableDate,
                    coverageMessage = outcome.CoverageMessage,
                    requiresDownload = outcome.RequiresDownload
                },
                timeline
            };
        }

        return new
        {
            type = outcome.Type,
            timestamp = DateTime.UtcNow,
            runId = outcome.RunId,
            plan = outcome.Plan,
            dataPlan = outcome.DataPlan,
            dataAvailability = new
            {
                symbol = outcome.Plan?.Symbol,
                hasLocalLeanData = outcome.HasLocalLeanData,
                hasRequestedRangeCoverage = outcome.HasRequestedRangeCoverage,
                coverageEstimated = outcome.CoverageEstimated,
                earliestAvailableDate = outcome.EarliestAvailableDate,
                latestAvailableDate = outcome.LatestAvailableDate,
                coverageMessage = outcome.CoverageMessage,
                requiresDownload = outcome.RequiresDownload
            },
            dataDownload = new
            {
                triggered = outcome.DataDownloadTriggered,
                succeeded = outcome.DataDownloadSucceeded
            },
            artifacts = new
            {
                strategyFilePath = outcome.StrategyFilePath,
                strategyMetaPath = outcome.StrategyMetaPath,
                backtestOutputDirectory = outcome.BacktestOutputDirectory
            },
            lean = outcome.Lean,
            timeline
        };
    }

    private static string Sanitize(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(value.Where(ch => !invalid.Contains(ch)).ToArray());
    }
}

public class AgenticProgressEvent
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "agent-step";
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    [JsonPropertyName("agent")]
    public string Agent { get; set; } = string.Empty;
    [JsonPropertyName("phase")]
    public string Phase { get; set; } = string.Empty;
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
    [JsonPropertyName("data")]
    public object? Data { get; set; }
}

public class AgenticRunOutcome
{
    public string Type { get; set; } = string.Empty;
    public string? Message { get; set; }
    public StrategyPlan? Plan { get; set; }
    public DataPlan? DataPlan { get; set; }
    public bool HasLocalLeanData { get; set; }
    public bool HasRequestedRangeCoverage { get; set; }
    public bool CoverageEstimated { get; set; }
    public DateTime? EarliestAvailableDate { get; set; }
    public DateTime? LatestAvailableDate { get; set; }
    public string CoverageMessage { get; set; } = string.Empty;
    public bool RequiresDownload { get; set; }
    public bool DataDownloadTriggered { get; set; }
    public bool DataDownloadSucceeded { get; set; }
    public string RunId { get; set; } = string.Empty;
    public string StrategyFilePath { get; set; } = string.Empty;
    public string StrategyMetaPath { get; set; } = string.Empty;
    public string BacktestOutputDirectory { get; set; } = string.Empty;
    public LeanExecutionResult? Lean { get; set; }
}

public class StrategyPlanRequest
{
    public string StrategyText { get; set; } = string.Empty;
}

public class StrategyRunRequest
{
    public string StrategyText { get; set; } = string.Empty;
    public bool ConfirmDataDownload { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public decimal? InitialCapital { get; set; }
    public string? Resolution { get; set; }
    public int? RsiPeriod { get; set; }
    public decimal? EntryRsi { get; set; }
    public decimal? ExitRsi { get; set; }
    public decimal? PositionSizePercent { get; set; }
}
