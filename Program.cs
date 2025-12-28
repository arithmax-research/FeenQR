using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using QuantResearchAgent.Core;
using QuantResearchAgent.Services;
using Feen.Services;
using QuantResearchAgent.Services.ResearchAgents;
using QuantResearchAgent.Plugins;
using Feen.Plugins;

namespace QuantResearchAgent
{
    class Program
    {
        static async Task Main(string[] args)
        {
            await RunCliAsync(args);
        }

        static async Task RunCliAsync(string[] args)
        {
            // Build configuration
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            // Create service collection
            var services = new ServiceCollection();

            // Configure services (DeepSeekService is used for LLM completions)
            // Add configuration
            services.AddSingleton<IConfiguration>(configuration);
            // Register Kernel for DI with AI service configured
            services.AddSingleton<Kernel>(sp => 
            {
                var kernelBuilder = Kernel.CreateBuilder();
                var config = sp.GetRequiredService<IConfiguration>();
                
                // Add OpenAI service to kernel
                var openAiKey = config["OpenAI:ApiKey"];
                var openAiModel = config["OpenAI:ModelId"] ?? "gpt-4o-mini";
                
                if (!string.IsNullOrEmpty(openAiKey))
                {
                    kernelBuilder.AddOpenAIChatCompletion(openAiModel, openAiKey);
                }
                
                return kernelBuilder.Build();
            });
            // Register LLM services
            services.AddSingleton<OpenAIService>();
            services.AddSingleton<DeepSeekService>();
            services.AddSingleton<LLMRouterService>();
            services.AddSingleton<ILLMService, LLMRouterService>();
            services.AddSingleton<StrategyGeneratorService>();

            // Ensure logs directory exists for file logging
            var logDir = Path.Combine(Directory.GetCurrentDirectory(), "logs");
            if (!Directory.Exists(logDir))
            {
                Directory.CreateDirectory(logDir);
            }

            // Configure logging
            services.AddLogging(builder =>
            {
                builder.ClearProviders();
                // Remove console logging to suppress all output
                // builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.None);
                builder.AddFilter("Microsoft", LogLevel.None);
                builder.AddFilter("System", LogLevel.None);
                builder.AddFilter("QuantResearchAgent", LogLevel.None);
            });

            // Add IntelligentAIAssistantService (must be registered before InteractiveCLI)
            services.AddSingleton<IntelligentAIAssistantService>();

            // Add InteractiveCLI
            // (Removed default registration; using factory registration below)
            // Replace InteractiveCLI registration to inject ILLMService (LLMRouterService)
            services.AddSingleton<InteractiveCLI>(sp =>
                new InteractiveCLI(
                    sp.GetRequiredService<Kernel>(),
                    sp.GetRequiredService<AgentOrchestrator>(),
                    sp.GetRequiredService<ILogger<InteractiveCLI>>(),
                    sp.GetRequiredService<ComprehensiveStockAnalysisAgent>(),
                    sp.GetRequiredService<AcademicResearchPaperAgent>(),
                    sp.GetRequiredService<YahooFinanceService>(),
                    sp.GetRequiredService<AlpacaService>(),
                    sp.GetRequiredService<PolygonService>(),
                    sp.GetRequiredService<MarketDataService>(),
                    sp.GetRequiredService<DataBentoService>(),
                    sp.GetRequiredService<YFinanceNewsService>(),
                    sp.GetRequiredService<FinvizNewsService>(),
                    sp.GetRequiredService<NewsSentimentAnalysisService>(),
                    sp.GetRequiredService<RedditScrapingService>(),
                    sp.GetRequiredService<PortfolioOptimizationService>(),
                    sp.GetRequiredService<SocialMediaScrapingService>(),
                    sp.GetRequiredService<WebDataExtractionService>(),
                    sp.GetRequiredService<ReportGenerationService>(),
                    sp.GetRequiredService<SatelliteImageryAnalysisService>(),
                    sp.GetRequiredService<ILLMService>(),
                    sp.GetRequiredService<TechnicalAnalysisService>(),
                    sp.GetRequiredService<IntelligentAIAssistantService>(),
                    sp.GetRequiredService<TradingTemplateGeneratorAgent>(),
                    sp.GetRequiredService<StatisticalTestingService>(),
                    sp.GetRequiredService<TimeSeriesAnalysisService>(),
                    sp.GetRequiredService<CointegrationAnalysisService>(),
                    sp.GetRequiredService<TimeSeriesForecastingService>(),
                    sp.GetRequiredService<FeatureEngineeringService>(),
                    sp.GetRequiredService<ModelValidationService>(),
                    sp.GetRequiredService<FactorModelService>(),
                    sp.GetRequiredService<AdvancedOptimizationService>(),
                    sp.GetRequiredService<AdvancedRiskService>(),
                    sp.GetRequiredService<SECFilingsService>(),
                    sp.GetRequiredService<EarningsCallService>(),
                    sp.GetRequiredService<SupplyChainService>(),
                    sp.GetRequiredService<OrderBookAnalysisService>(),
                    sp.GetRequiredService<MarketImpactService>(),
                    sp.GetRequiredService<ExecutionService>(),
                    sp.GetRequiredService<MonteCarloService>(),
                    sp.GetRequiredService<StrategyBuilderService>(),
                    sp.GetRequiredService<NotebookService>(),
                    sp.GetRequiredService<DataValidationService>(),
                    sp.GetRequiredService<CorporateActionService>(),
                    sp.GetRequiredService<TimezoneService>(),
                    sp.GetRequiredService<FREDService>(),
                    sp.GetRequiredService<WorldBankService>(),
                    sp.GetRequiredService<AdvancedAlpacaService>(),
                    sp.GetRequiredService<FactorResearchService>(),
                    sp.GetRequiredService<AcademicResearchService>(),
                    sp.GetRequiredService<AutoMLService>(),
                    sp.GetRequiredService<ModelInterpretabilityService>(),
                    sp.GetRequiredService<ReinforcementLearningService>(),
                    sp.GetRequiredService<FIXService>(),
                    sp.GetRequiredService<WebIntelligenceService>(),
                    sp.GetRequiredService<PatentAnalysisService>(),
                    sp.GetRequiredService<FederalReserveService>(),
                    sp.GetRequiredService<GlobalEconomicService>(),
                    sp.GetRequiredService<GeopoliticalRiskService>(),
                    sp.GetRequiredService<WebIntelligencePlugin>(),
                    sp.GetRequiredService<PatentAnalysisPlugin>(),
                    sp.GetRequiredService<FederalReservePlugin>(),
                    sp.GetRequiredService<GlobalEconomicPlugin>(),
                    // sp.GetRequiredService<GeopoliticalRiskPlugin>(), // Temporarily disabled
                    sp.GetRequiredService<OptionsFlowService>(),
                    sp.GetRequiredService<VolatilityTradingService>(),
                    sp.GetRequiredService<AdvancedMicrostructureService>(),
                    sp.GetRequiredService<LatencyArbitrageService>(),
                    sp.GetRequiredService<OptionsFlowPlugin>(),
                    sp.GetRequiredService<VolatilityTradingPlugin>(),
                    sp.GetRequiredService<AdvancedMicrostructurePlugin>(),
                    sp.GetRequiredService<LatencyArbitragePlugin>(),
                    sp.GetRequiredService<ConversationalResearchPlugin>(),
                    sp.GetRequiredService<AutomatedReportingPlugin>(),
                    sp.GetRequiredService<MarketRegimePlugin>(),
                    sp.GetRequiredService<AnomalyDetectionPlugin>(),
                    sp.GetRequiredService<DynamicFactorPlugin>(),
                    sp.GetRequiredService<TradingTemplateGeneratorPlugin>(),
                    sp.GetRequiredService<AlphaVantageService>(),
                    sp.GetRequiredService<IEXCloudService>(),
                    sp.GetRequiredService<FinancialModelingPrepService>(),
                    sp.GetRequiredService<EnhancedFundamentalAnalysisService>(),
                    sp.GetRequiredService<AlphaVantagePlugin>(),
                    sp.GetRequiredService<IEXCloudPlugin>(),
                    sp.GetRequiredService<FinancialModelingPrepPlugin>(),
                    sp.GetRequiredService<EnhancedFundamentalAnalysisPlugin>(),
                    sp.GetRequiredService<AdvancedRiskAnalyticsService>(),
                    sp.GetRequiredService<CounterpartyRiskService>(),
                    sp.GetRequiredService<PerformanceAttributionService>(),
                    sp.GetRequiredService<BenchmarkingService>(),
                    sp.GetRequiredService<AdvancedRiskAnalyticsPlugin>(),
                    sp.GetRequiredService<CounterpartyRiskPlugin>(),
                    sp.GetRequiredService<PerformanceAttributionPlugin>(),
                    sp.GetRequiredService<BenchmarkingPlugin>(),
                    sp.GetRequiredService<LiveStrategyService>(),
                    sp.GetRequiredService<EventDrivenTradingService>(),
                    sp.GetRequiredService<RealTimeAlertingService>(),
                    sp.GetRequiredService<ComplianceMonitoringService>(),
                    sp.GetRequiredService<LiveStrategyPlugin>(),
                    sp.GetRequiredService<EventDrivenTradingPlugin>(),
                    sp.GetRequiredService<RealTimeAlertingPlugin>(),
                    sp.GetRequiredService<ComplianceMonitoringPlugin>()
                )
            );

            // Add Semantic Kernel memory for RAG capabilities
            services.AddSingleton<ISemanticTextMemory>(sp =>
            {
                var kernel = sp.GetRequiredService<Kernel>();
                var embeddingService = kernel.GetRequiredService<Microsoft.SemanticKernel.Embeddings.ITextEmbeddingGenerationService>();
                var memoryStore = new Microsoft.SemanticKernel.Memory.VolatileMemoryStore();
                return new Microsoft.SemanticKernel.Memory.SemanticTextMemory(memoryStore, embeddingService);
            });

            // Add RAG and Agentic services
            services.AddSingleton<RAGService>();
            services.AddSingleton<AgenticOrchestrator>();

            // Add core services
            services.AddSingleton<LeanDataService>();
            services.AddSingleton<AgentOrchestrator>(sp =>
                new AgentOrchestrator(
                    sp.GetRequiredService<Kernel>(),
                    sp.GetRequiredService<YouTubeAnalysisService>(),
                    sp.GetRequiredService<TradingSignalService>(),
                    sp.GetRequiredService<MarketDataService>(),
                    sp.GetRequiredService<RiskManagementService>(),
                    sp.GetRequiredService<PortfolioService>(),
                    sp.GetRequiredService<RAGService>(),
                    sp.GetRequiredService<AgenticOrchestrator>(),
                    sp.GetRequiredService<MarketSentimentAgentService>(),
                    sp.GetRequiredService<StatisticalPatternAgentService>(),
                    sp.GetRequiredService<CompanyValuationService>(),
                    sp.GetRequiredService<HighFrequencyDataService>(),
                    sp.GetRequiredService<TradingStrategyLibraryService>(),
                    sp.GetRequiredService<AlpacaService>(),
                    sp.GetRequiredService<TechnicalAnalysisService>(),
                    sp.GetRequiredService<RedditScrapingService>(),
                    sp.GetRequiredService<StrategyGeneratorService>(),
                    sp.GetRequiredService<TradingTemplateGeneratorAgent>(),
                    sp.GetRequiredService<OptionsFlowService>(),
                    sp.GetRequiredService<VolatilityTradingService>(),
                    sp.GetRequiredService<AdvancedMicrostructureService>(),
                    sp.GetRequiredService<LatencyArbitrageService>(),
                    sp.GetRequiredService<IConfiguration>(),
                    sp.GetRequiredService<ILogger<AgentOrchestrator>>(),
                    sp.GetRequiredService<StatisticalTestingService>(),
                    sp.GetRequiredService<TimeSeriesForecastingService>(),
                    sp.GetRequiredService<FeatureEngineeringService>(),
                    sp.GetRequiredService<ModelValidationService>(),
                    sp.GetRequiredService<FactorModelService>(),
                    sp.GetRequiredService<AdvancedOptimizationService>(),
                    sp.GetRequiredService<AdvancedRiskService>(),
                    sp.GetRequiredService<OrderBookAnalysisService>(),
                    sp.GetRequiredService<MarketImpactService>(),
                    sp.GetRequiredService<ExecutionService>(),
                    sp.GetRequiredService<MonteCarloService>(),
                    sp.GetRequiredService<StrategyBuilderService>(),
                    sp.GetRequiredService<NotebookService>(),
                    sp.GetRequiredService<FREDService>(),
                    sp.GetRequiredService<IMFService>(),
                    sp.GetRequiredService<OECDService>(),
                    sp.GetRequiredService<WorldBankService>(),
                    sp.GetRequiredService<AdvancedAlpacaService>(),
                    sp.GetRequiredService<FIXService>(),
                    sp.GetRequiredService<WebIntelligenceService>(),
                    sp.GetRequiredService<PatentAnalysisService>(),
                    sp.GetRequiredService<FederalReserveService>(),
                    sp.GetRequiredService<GlobalEconomicService>(),
                    sp.GetRequiredService<GeopoliticalRiskService>()
                )
            );
            services.AddSingleton<YouTubeAnalysisService>();
            services.AddSingleton<MarketDataService>(sp =>
                new MarketDataService(
                    sp.GetRequiredService<ILogger<MarketDataService>>(),
                    sp.GetRequiredService<IConfiguration>(),
                    sp.GetRequiredService<AlpacaService>(),
                    sp.GetRequiredService<LeanDataService>()
                )
            );
            services.AddSingleton<TradingSignalService>();
            services.AddSingleton<PortfolioService>();
            services.AddSingleton<RiskManagementService>();
            services.AddSingleton<CompanyValuationService>(sp =>
                new CompanyValuationService(
                    sp.GetRequiredService<Kernel>(),
                    sp.GetRequiredService<ILogger<CompanyValuationService>>(),
                    sp.GetRequiredService<AlpacaService>(),
                    sp.GetRequiredService<QuantResearchAgent.Plugins.YahooFinanceDataPlugin>(),
                    sp.GetRequiredService<HttpClient>()
                )
            );
            services.AddSingleton<HighFrequencyDataService>();
            services.AddSingleton<TradingStrategyLibraryService>();
            services.AddSingleton<AlpacaService>();
            services.AddSingleton<TechnicalAnalysisService>();
            services.AddSingleton<HttpClient>();
            services.AddSingleton<PolygonService>();
            services.AddSingleton<DataBentoService>();
            services.AddSingleton<YFinanceNewsService>();
            services.AddSingleton<FinvizNewsService>();
            services.AddSingleton<NewsSentimentAnalysisService>(); // Uses DeepSeekService now
            services.AddSingleton<YahooFinanceService>();
            services.AddSingleton<RedditScrapingService>();
            services.AddSingleton<PortfolioOptimizationService>();
            services.AddSingleton<SocialMediaScrapingService>();
            services.AddSingleton<WebDataExtractionService>();
            services.AddSingleton<ReportGenerationService>();
            services.AddSingleton<SatelliteImageryAnalysisService>();

            // Add statistical testing service
            services.AddSingleton<StatisticalTestingService>();

            // Add time series analysis service
            services.AddSingleton<TimeSeriesAnalysisService>();

            // Add cointegration analysis service
            services.AddSingleton<CointegrationAnalysisService>();

            // Add Phase 2 ML services
            services.AddSingleton<TimeSeriesForecastingService>();
            services.AddSingleton<FeatureEngineeringService>();
            services.AddSingleton<ModelValidationService>();

            // Add Phase 3 Factor Model service
            services.AddSingleton<FactorModelService>();

            // Add Phase 3.2 Advanced Optimization services
            services.AddSingleton<AdvancedOptimizationService>();

            // Add Phase 3.3 Advanced Risk services
            services.AddSingleton<AdvancedRiskService>();

            // Add Phase 4 Alternative Data services
            services.AddSingleton<SECFilingsService>();
            services.AddSingleton<EarningsCallService>();
            services.AddSingleton<SupplyChainService>();

            // Add Phase 5 High-Frequency & Market Microstructure services
            services.AddSingleton<OrderBookAnalysisService>();
            services.AddSingleton<MarketImpactService>();
            services.AddSingleton<ExecutionService>();

            // Add Phase 6 Research & Strategy Development Tools
            services.AddSingleton<MonteCarloService>();
            services.AddSingleton<StrategyBuilderService>();
            services.AddSingleton<NotebookService>();

            // Add Phase 7 Data Quality & Management services
            services.AddSingleton<DataValidationService>();
            services.AddSingleton<CorporateActionService>();
            services.AddSingleton<TimezoneService>();

            // Add Phase 8 Free Institutional Data services
            services.AddSingleton<FREDService>(sp =>
                new FREDService(
                    sp.GetRequiredService<HttpClient>(),
                    sp.GetRequiredService<ILogger<FREDService>>(),
                    sp.GetRequiredService<IConfiguration>()
                )
            );
            services.AddSingleton<IMFService>(sp =>
                new IMFService(
                    sp.GetRequiredService<HttpClient>(),
                    sp.GetRequiredService<ILogger<IMFService>>(),
                    sp.GetRequiredService<IConfiguration>()
                )
            );
            services.AddSingleton<OECDService>(sp =>
                new OECDService(
                    sp.GetRequiredService<HttpClient>(),
                    sp.GetRequiredService<ILogger<OECDService>>(),
                    sp.GetRequiredService<IConfiguration>()
                )
            );
            services.AddSingleton<WorldBankService>(sp =>
                new WorldBankService(
                    sp.GetRequiredService<HttpClient>(),
                    sp.GetRequiredService<ILogger<WorldBankService>>(),
                    sp.GetRequiredService<IConfiguration>()
                )
            );
            services.AddSingleton<AdvancedAlpacaService>(sp =>
                new AdvancedAlpacaService(
                    sp.GetRequiredService<HttpClient>(),
                    sp.GetRequiredService<ILogger<AdvancedAlpacaService>>(),
                    sp.GetRequiredService<IConfiguration>(),
                    sp.GetRequiredService<AlpacaService>()
                )
            );

            // Add Phase 9 Advanced Research Tools services
            services.AddSingleton<FactorResearchService>();
            services.AddSingleton<AcademicResearchService>();
            services.AddSingleton<AutoMLService>();
            services.AddSingleton<ModelInterpretabilityService>();
            services.AddSingleton<ReinforcementLearningService>();

            // Add Phase 10 Web & Alternative Data Integration services
            services.AddSingleton<WebIntelligenceService>();
            services.AddSingleton<PatentAnalysisService>();
            services.AddSingleton<FederalReserveService>();
            services.AddSingleton<GlobalEconomicService>();
            services.AddSingleton<GeopoliticalRiskService>();

            // Add Phase 11 Derivatives & Options Analytics services
            services.AddSingleton<OptionsFlowService>();
            services.AddSingleton<VolatilityTradingService>();
            services.AddSingleton<AdvancedMicrostructureService>();
            services.AddSingleton<LatencyArbitrageService>();

            // Add Phase 10 Web & Alternative Data Integration plugins
            services.AddSingleton<WebIntelligencePlugin>();
            services.AddSingleton<PatentAnalysisPlugin>();
            services.AddSingleton<GoogleWebSearchPlugin>();
            services.AddSingleton<IWebSearchPlugin, GoogleWebSearchPlugin>();
            services.AddSingleton<YahooFinanceDataPlugin>();
            services.AddSingleton<IFinancialDataPlugin, YahooFinanceDataPlugin>();
            services.AddSingleton<FederalReservePlugin>();
            services.AddSingleton<GlobalEconomicPlugin>();
            // services.AddSingleton<GeopoliticalRiskPlugin>(); // Temporarily disabled due to mock data removal

            // Add Phase 11 Derivatives & Options Analytics plugins
            services.AddSingleton<OptionsFlowPlugin>();
            services.AddSingleton<VolatilityTradingPlugin>();
            services.AddSingleton<AdvancedMicrostructurePlugin>();
            services.AddSingleton<LatencyArbitragePlugin>();

            // Add Phase 8.3 FIX Protocol service
            services.AddSingleton<FIXService>();

            // Add Phase 14 AI-Enhanced Research services
            services.AddSingleton<ConversationalResearchService>();
            services.AddSingleton<AutomatedReportingService>();
            services.AddSingleton<MarketRegimeService>();
            services.AddSingleton<AnomalyDetectionService>();
            services.AddSingleton<DynamicFactorService>();

            // Add Phase 14 AI-Enhanced Research plugins
            services.AddSingleton<ConversationalResearchPlugin>();
            services.AddSingleton<AutomatedReportingPlugin>();
            services.AddSingleton<MarketRegimePlugin>();
            services.AddSingleton<AnomalyDetectionPlugin>();
            services.AddSingleton<DynamicFactorPlugin>();
            services.AddSingleton<TradingTemplateGeneratorPlugin>();

            // Add Phase 15 Specialized Quantitative Tools services
            services.AddSingleton<AdvancedRiskAnalyticsService>();
            services.AddSingleton<CounterpartyRiskService>();
            services.AddSingleton<PerformanceAttributionService>();
            services.AddSingleton<BenchmarkingService>();

            // Add Phase 15 Specialized Quantitative Tools plugins
            services.AddSingleton<AdvancedRiskAnalyticsPlugin>();
            services.AddSingleton<CounterpartyRiskPlugin>();
            services.AddSingleton<PerformanceAttributionPlugin>();
            services.AddSingleton<BenchmarkingPlugin>();

            // Add Phase 12 Research Platforms Integration services (Free Alternatives)
            services.AddSingleton<AlphaVantageService>(sp =>
                new AlphaVantageService(
                    sp.GetRequiredService<HttpClient>(),
                    sp.GetRequiredService<ILogger<AlphaVantageService>>(),
                    sp.GetRequiredService<IConfiguration>()
                )
            );
            services.AddSingleton<IEXCloudService>(sp =>
                new IEXCloudService(
                    sp.GetRequiredService<HttpClient>(),
                    sp.GetRequiredService<ILogger<IEXCloudService>>(),
                    sp.GetRequiredService<IConfiguration>()
                )
            );
            services.AddSingleton<FinancialModelingPrepService>(sp =>
                new FinancialModelingPrepService(
                    sp.GetRequiredService<HttpClient>(),
                    sp.GetRequiredService<ILogger<FinancialModelingPrepService>>(),
                    sp.GetRequiredService<IConfiguration>()
                )
            );
            services.AddSingleton<EnhancedFundamentalAnalysisService>(sp =>
                new EnhancedFundamentalAnalysisService(
                    sp.GetRequiredService<AlphaVantageService>(),
                    sp.GetRequiredService<IEXCloudService>(),
                    sp.GetRequiredService<FinancialModelingPrepService>(),
                    sp.GetRequiredService<ILogger<EnhancedFundamentalAnalysisService>>()
                )
            );

            // Add Phase 12 Research Platforms Integration plugins
            services.AddSingleton<AlphaVantagePlugin>();
            services.AddSingleton<IEXCloudPlugin>();
            services.AddSingleton<FinancialModelingPrepPlugin>();
            services.AddSingleton<EnhancedFundamentalAnalysisPlugin>();

            // Add Phase 13 Real-Time & Live Features services
            services.AddSingleton<LiveStrategyService>(sp =>
                new LiveStrategyService(
                    sp.GetRequiredService<HttpClient>(),
                    sp.GetRequiredService<ILogger<LiveStrategyService>>(),
                    sp.GetRequiredService<IConfiguration>(),
                    sp.GetRequiredService<AdvancedAlpacaService>(),
                    sp.GetRequiredService<MarketDataService>(),
                    sp.GetRequiredService<AdvancedRiskService>()
                )
            );
            services.AddSingleton<EventDrivenTradingService>(sp =>
                new EventDrivenTradingService(
                    sp.GetRequiredService<HttpClient>(),
                    sp.GetRequiredService<ILogger<EventDrivenTradingService>>(),
                    sp.GetRequiredService<IConfiguration>(),
                    sp.GetRequiredService<AdvancedAlpacaService>(),
                    sp.GetRequiredService<NewsSentimentAnalysisService>(),
                    sp.GetRequiredService<FederalReserveService>(),
                    sp.GetRequiredService<GeopoliticalRiskService>()
                )
            );
            services.AddSingleton<RealTimeAlertingService>(sp =>
                new RealTimeAlertingService(
                    sp.GetRequiredService<HttpClient>(),
                    sp.GetRequiredService<ILogger<RealTimeAlertingService>>(),
                    sp.GetRequiredService<IConfiguration>(),
                    sp.GetRequiredService<AdvancedAlpacaService>(),
                    sp.GetRequiredService<MarketDataService>(),
                    sp.GetRequiredService<TechnicalAnalysisService>()
                )
            );
            services.AddSingleton<ComplianceMonitoringService>(sp =>
                new ComplianceMonitoringService(
                    sp.GetRequiredService<HttpClient>(),
                    sp.GetRequiredService<ILogger<ComplianceMonitoringService>>(),
                    sp.GetRequiredService<IConfiguration>(),
                    sp.GetRequiredService<AdvancedAlpacaService>(),
                    sp.GetRequiredService<AdvancedRiskService>()
                )
            );

            // Add Phase 13 Real-Time & Live Features plugins
            services.AddSingleton<LiveStrategyPlugin>();
            services.AddSingleton<EventDrivenTradingPlugin>();
            services.AddSingleton<RealTimeAlertingPlugin>();
            services.AddSingleton<ComplianceMonitoringPlugin>();

            // Add RAG and Agentic plugins
            services.AddSingleton<RAGPlugin>();
            services.AddSingleton<AgenticPlugin>();

            // Add research agents
            services.AddSingleton<NewsScrapingService>();
            services.AddSingleton<MarketSentimentAgentService>();
            services.AddSingleton<StatisticalPatternAgentService>();
            services.AddSingleton<ComprehensiveStockAnalysisAgent>();
            services.AddSingleton<AcademicResearchPaperAgent>();
            services.AddSingleton<TradingTemplateGeneratorAgent>(sp =>
                new TradingTemplateGeneratorAgent(
                    sp.GetRequiredService<Kernel>(),
                    sp.GetRequiredService<ILogger<TradingTemplateGeneratorAgent>>(),
                    sp.GetRequiredService<IConfiguration>(),
                    sp.GetRequiredService<MarketDataService>(),
                    sp.GetRequiredService<CompanyValuationService>(),
                    sp.GetRequiredService<TechnicalAnalysisService>(),
                    sp.GetRequiredService<NewsSentimentAnalysisService>(),
                    sp.GetRequiredService<QuantResearchAgent.Plugins.IWebSearchPlugin>(),
                    sp.GetRequiredService<HttpClient>(),
                    sp.GetRequiredService<ILLMService>(),
                    sp.GetRequiredService<WebDataExtractionService>(),
                    sp.GetRequiredService<DeepSeekService>()
                )
            );

            // Build the service provider
            var serviceProvider = services.BuildServiceProvider();

            // Get the CLI and run it
            var cli = serviceProvider.GetRequiredService<InteractiveCLI>();
            await cli.RunAsync(args);
        }
    }
}
