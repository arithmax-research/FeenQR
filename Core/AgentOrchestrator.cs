using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using QuantResearchAgent.Services;
using QuantResearchAgent.Services.ResearchAgents;
using QuantResearchAgent.Plugins;
using System.Collections.Concurrent;

namespace QuantResearchAgent.Core;

/// <summary>
/// Main orchestrator for the Quant Research Agent
/// </summary>
public class AgentOrchestrator
{
    private readonly ILogger<AgentOrchestrator> _logger;
    private readonly IConfiguration? _configuration;
    private readonly Kernel _kernel;
    private readonly YouTubeAnalysisService _youtubeService;
    private readonly TradingSignalService _signalService;
    private readonly MarketDataService _marketDataService;
    private readonly RiskManagementService _riskService;
    private readonly PortfolioService _portfolioService;
    
    // Research agent services
    private readonly MarketSentimentAgentService _marketSentimentAgent;
    private readonly StatisticalPatternAgentService _statisticalPatternAgent;
    private readonly CompanyValuationService _companyValuationService;
    private readonly HighFrequencyDataService _hfDataService;
    private readonly TradingStrategyLibraryService _strategyLibraryService;
    private readonly AlpacaService _alpacaService;
    private readonly TechnicalAnalysisService _technicalAnalysisService;
    
    private readonly ConcurrentQueue<AgentJob> _jobQueue = new();
    private readonly ConcurrentDictionary<string, AgentJob> _runningJobs = new();
    private readonly Timer? _scheduledJobTimer;
    private readonly Timer? _dataRefreshTimer;
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly SemaphoreSlim _jobSemaphore;

    public AgentOrchestrator(
        Kernel kernel,
        YouTubeAnalysisService youtubeService,
        TradingSignalService signalService,
        MarketDataService marketDataService,
        RiskManagementService riskService,
        PortfolioService portfolioService,
        MarketSentimentAgentService marketSentimentAgent,
        StatisticalPatternAgentService statisticalPatternAgent,
        CompanyValuationService companyValuationService,
        HighFrequencyDataService hfDataService,
        TradingStrategyLibraryService strategyLibraryService,
        AlpacaService alpacaService,
        TechnicalAnalysisService technicalAnalysisService,
        IConfiguration? configuration = null,
        ILogger<AgentOrchestrator>? logger = null)
    {
        _kernel = kernel;
        _youtubeService = youtubeService;
        _signalService = signalService;
        _marketDataService = marketDataService;
        _riskService = riskService;
        _portfolioService = portfolioService;
        _marketSentimentAgent = marketSentimentAgent;
        _statisticalPatternAgent = statisticalPatternAgent;
        _companyValuationService = companyValuationService;
        _hfDataService = hfDataService;
        _strategyLibraryService = strategyLibraryService;
        _alpacaService = alpacaService;
        _technicalAnalysisService = technicalAnalysisService;
        _configuration = configuration;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<AgentOrchestrator>.Instance;

        var maxConcurrentJobs = _configuration?.GetValue<int>("Agent:MaxConcurrentJobs", 5) ?? 5;
        _jobSemaphore = new SemaphoreSlim(maxConcurrentJobs, maxConcurrentJobs);

        // Initialize timers to null initially
        _scheduledJobTimer = null;
        _dataRefreshTimer = null;

        // Register core plugins
        kernel.Plugins.AddFromObject(new YouTubeAnalysisPlugin(_youtubeService));
        kernel.Plugins.AddFromObject(new TradingPlugin(_signalService, _portfolioService, _riskService));
        kernel.Plugins.AddFromObject(new MarketDataPlugin(_marketDataService));
        kernel.Plugins.AddFromObject(new RiskManagementPlugin(_riskService, _portfolioService));
        kernel.Plugins.AddFromObject(new AlpacaPlugin(_alpacaService));
        kernel.Plugins.AddFromObject(new TechnicalAnalysisPlugin(_technicalAnalysisService));
        
        // Register research agent plugins
        kernel.Plugins.AddFromObject(new MarketSentimentPlugin(_marketSentimentAgent));
        kernel.Plugins.AddFromObject(new StatisticalPatternPlugin(_statisticalPatternAgent));
        
        // Register consolidated plugins from redundant projects
        kernel.Plugins.AddFromObject(new CompanyValuationPlugin(_companyValuationService));
        kernel.Plugins.AddFromObject(new HighFrequencyDataPlugin(_hfDataService));
        kernel.Plugins.AddFromObject(new TradingStrategyLibraryPlugin(_strategyLibraryService));

        // Diagnostic: List all registered plugins and their functions
        Console.WriteLine("=== Registered Plugins and Functions ===");
        foreach (var plugin in kernel.Plugins)
        {
            Console.WriteLine($"Plugin: {plugin.Name}");
            foreach (var function in plugin)
            {
                Console.WriteLine($"  Function: {function.Name}");
            }
        }
        Console.WriteLine("========================================");
    }

    private void RegisterPlugins()
    {
        try
        {
            // Core plugins
            _kernel.Plugins.AddFromType<PodcastAnalysisPlugin>();
            _kernel.Plugins.AddFromType<TradingPlugin>();
            _kernel.Plugins.AddFromType<MarketDataPlugin>();
            _kernel.Plugins.AddFromType<RiskManagementPlugin>();
            
            // Research agent plugins
            _kernel.Plugins.AddFromType<MarketSentimentPlugin>();
            _kernel.Plugins.AddFromType<StatisticalPatternPlugin>();
            
            _logger.LogInformation("Successfully registered all plugins with Semantic Kernel");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register plugins with Semantic Kernel");
            throw;
        }
    }

    public async Task StartAsync()
    {
        _logger.LogInformation("Agent Orchestrator starting...");
        
        // Start background job processor
        _ = Task.Run(ProcessJobsAsync, _cancellationTokenSource.Token);
        
        // Schedule initial jobs
        await ScheduleInitialJobsAsync();
        
        _logger.LogInformation("Agent Orchestrator started successfully");
    }

    public async Task StopAsync()
    {
        _logger.LogInformation("Agent Orchestrator stopping...");
        
        _cancellationTokenSource.Cancel();
        
        // Wait for running jobs to complete (with timeout)
        var timeout = TimeSpan.FromSeconds(30);
        var completionTask = Task.Run(async () =>
        {
            while (_runningJobs.Count > 0)
            {
                await Task.Delay(100);
            }
        });
        
        await Task.WhenAny(completionTask, Task.Delay(timeout));
        
        _scheduledJobTimer?.Dispose();
        _dataRefreshTimer?.Dispose();
        _jobSemaphore?.Dispose();
        
        _logger.LogInformation("Agent Orchestrator stopped");
    }

    public async Task<string> QueueJobAsync(AgentJob job)
    {
        _jobQueue.Enqueue(job);
        _logger.LogInformation("Queued job {JobId} of type {JobType}", job.Id, job.Type);
        return await Task.FromResult(job.Id);
    }

    private async Task ProcessJobsAsync()
    {
        while (!_cancellationTokenSource.Token.IsCancellationRequested)
        {
            try
            {
                if (_jobQueue.TryDequeue(out var job))
                {
                    await _jobSemaphore.WaitAsync(_cancellationTokenSource.Token);
                    
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await ProcessJobAsync(job);
                        }
                        finally
                        {
                            _jobSemaphore.Release();
                        }
                    }, _cancellationTokenSource.Token);
                }
                else
                {
                    await Task.Delay(1000, _cancellationTokenSource.Token);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in job processing loop");
            }
        }
    }

    private async Task ProcessJobAsync(AgentJob job)
    {
        _runningJobs[job.Id] = job;
        job.Status = JobStatus.Running;
        job.StartedAt = DateTime.UtcNow;
        
        _logger.LogInformation("Processing job {JobId} of type {JobType}", job.Id, job.Type);
        
        try
        {
            string result = job.Type switch
            {
                "podcast_analysis" => await ProcessPodcastAnalysisJob(job),
                "signal_generation" => await ProcessSignalGenerationJob(job),
                "market_data_update" => await ProcessMarketDataUpdateJob(job),
                "portfolio_analysis" => await ProcessPortfolioAnalysisJob(job),
                "risk_assessment" => await ProcessRiskAssessmentJob(job),
                _ => throw new InvalidOperationException($"Unknown job type: {job.Type}")
            };
            
            job.Result = result;
            job.Status = JobStatus.Completed;
            job.CompletedAt = DateTime.UtcNow;
            
            _logger.LogInformation("Completed job {JobId} successfully", job.Id);
        }
        catch (Exception ex)
        {
            job.Status = JobStatus.Failed;
            job.ErrorMessage = ex.Message;
            job.CompletedAt = DateTime.UtcNow;
            
            _logger.LogError(ex, "Failed to process job {JobId} of type {JobType}", job.Id, job.Type);
        }
        finally
        {
            _runningJobs.TryRemove(job.Id, out _);
        }
    }

    private async Task<string> ProcessPodcastAnalysisJob(AgentJob job)
    {
        var videoUrl = job.Parameters["videoUrl"].ToString()!;
        var episode = await _youtubeService.AnalyzeVideoAsync(videoUrl);
        
        // Generate trading signals from video insights
        var signals = await _signalService.GenerateSignalsAsync();
        
        return $"Analyzed video: {episode.Name}. Generated {signals.Count} trading signals.";
    }

    private async Task<string> ProcessSignalGenerationJob(AgentJob job)
    {
        var symbol = job.Parameters.GetValueOrDefault("symbol")?.ToString();
        
        if (string.IsNullOrEmpty(symbol))
        {
            // Generate signals for all tracked symbols
            var signals = await _signalService.GenerateSignalsAsync();
            return $"Generated {signals.Count} trading signals for all symbols";
        }
        else
        {
            // Generate signals for specific symbol
            var signal = await _signalService.GenerateSignalAsync(symbol);
            return $"Generated signal for {symbol}: {signal?.Type} with strength {signal?.Strength:F2}";
        }
    }

    private async Task<string> ProcessMarketDataUpdateJob(AgentJob job)
    {
        await _marketDataService.RefreshMarketDataAsync();
        return "Market data refreshed successfully";
    }

    private async Task<string> ProcessPortfolioAnalysisJob(AgentJob job)
    {
        var metrics = await _portfolioService.CalculateMetricsAsync();
        return $"Portfolio analysis completed. Total value: ${metrics.TotalValue:F2}, P&L: {metrics.TotalPnLPercent:F2}%";
    }

    private async Task<string> ProcessRiskAssessmentJob(AgentJob job)
    {
        var riskAssessment = await _riskService.AssessPortfolioRiskAsync();
        return $"Risk assessment completed. Current risk level: {riskAssessment}";
    }

    private async Task ScheduleInitialJobsAsync()
    {
        // Schedule periodic podcast analysis
        var podcastAnalysisJob = new AgentJob
        {
            Type = "podcast_analysis",
            Parameters = new Dictionary<string, object>
            {
                ["videoUrl"] = "https://www.youtube.com/watch?v=dQw4w9WgXcQ"
            },
            Priority = 3
        };
        await QueueJobAsync(podcastAnalysisJob);

        // Schedule market data update
        var marketDataJob = new AgentJob
        {
            Type = "market_data_update",
            Priority = 1
        };
        await QueueJobAsync(marketDataJob);

        // Schedule signal generation
        var signalJob = new AgentJob
        {
            Type = "signal_generation",
            Priority = 2
        };
        await QueueJobAsync(signalJob);
    }

    private async void ScheduledJobCallback(object? state)
    {
        try
        {
            var podcastIntervalHours = _configuration?.GetValue<int>("Agent:PodcastAnalysisIntervalHours", 6) ?? 6;
            var lastPodcastAnalysis = DateTime.UtcNow; // TODO: Track this properly
            
            if (DateTime.UtcNow - lastPodcastAnalysis > TimeSpan.FromHours(podcastIntervalHours))
            {
                var podcastJob = new AgentJob
                {
                    Type = "podcast_analysis",
                    Parameters = new Dictionary<string, object>
                    {
                        ["videoUrl"] = "https://open.spotify.com/episode/69tcEMbTyOEcPfgEJ95xos"
                    },
                    Priority = 3
                };
                await QueueJobAsync(podcastJob);
            }
            
            // Schedule regular signal generation
            var signalJob = new AgentJob
            {
                Type = "signal_generation",
                Priority = 2
            };
            await QueueJobAsync(signalJob);
            
            // Schedule portfolio analysis
            var portfolioJob = new AgentJob
            {
                Type = "portfolio_analysis",
                Priority = 4
            };
            await QueueJobAsync(portfolioJob);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in scheduled job callback");
        }
    }

    private async void DataRefreshCallback(object? state)
    {
        try
        {
            var marketDataJob = new AgentJob
            {
                Type = "market_data_update",
                Priority = 1
            };
            await QueueJobAsync(marketDataJob);
            
            var riskJob = new AgentJob
            {
                Type = "risk_assessment",
                Priority = 2
            };
            await QueueJobAsync(riskJob);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in data refresh callback");
        }
    }
}
