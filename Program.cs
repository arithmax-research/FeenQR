using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using QuantResearchAgent.Core;
using QuantResearchAgent.Services;
using QuantResearchAgent.Services.ResearchAgents;
using QuantResearchAgent.Plugins;

namespace QuantResearchAgent
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // Build configuration
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            // Create service collection
            var services = new ServiceCollection();
            
            // Build semantic kernel
            var openAiApiKey = configuration["OpenAI:ApiKey"];
            var modelId = configuration["OpenAI:ModelId"] ?? "gpt-4o";
            
            var kernel = Kernel.CreateBuilder()
                .AddOpenAIChatCompletion(modelId, openAiApiKey!)
                .Build();

            ConfigureServices(services, configuration, kernel);

            // Register web search and financial data plugins
            services.AddHttpClient<QuantResearchAgent.Plugins.GoogleWebSearchPlugin>();
            services.AddSingleton<QuantResearchAgent.Plugins.IWebSearchPlugin>(sp =>
                sp.GetRequiredService<QuantResearchAgent.Plugins.GoogleWebSearchPlugin>());

            services.AddHttpClient<QuantResearchAgent.Plugins.YahooFinanceDataPlugin>();
            services.AddSingleton<QuantResearchAgent.Plugins.IFinancialDataPlugin>(sp =>
                sp.GetRequiredService<QuantResearchAgent.Plugins.YahooFinanceDataPlugin>());

            // Build service provider
            var serviceProvider = services.BuildServiceProvider();

            // Get the orchestrator and start (it will register its own plugins)
            var orchestrator = serviceProvider.GetRequiredService<AgentOrchestrator>();
            
            // Start the orchestrator
            await orchestrator.StartAsync();
            
            // Start the interactive CLI
            var cli = serviceProvider.GetRequiredService<InteractiveCLI>();
            await cli.RunAsync();
        }

        static void ConfigureServices(IServiceCollection services, IConfiguration configuration, Kernel kernel)
        {
            // Add configuration
            services.AddSingleton(configuration);
            services.AddSingleton(kernel);

            // Ensure logs directory exists for file logging
            var logDir = Path.Combine(Directory.GetCurrentDirectory(), "logs");
            if (!Directory.Exists(logDir))
            {
                Directory.CreateDirectory(logDir);
            }

            // Suppress all logging output
            services.AddLogging(builder =>
            {
                builder.ClearProviders();
                builder.SetMinimumLevel(LogLevel.Critical);
            });

            // Add InteractiveCLI
            services.AddSingleton<InteractiveCLI>();

            // Add core services
            services.AddSingleton<LeanDataService>();
            services.AddSingleton<AgentOrchestrator>();
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
            services.AddSingleton<YahooFinanceService>();

            // Add research agents
            services.AddSingleton<NewsScrapingService>();
            services.AddSingleton<MarketSentimentAgentService>();
            services.AddSingleton<StatisticalPatternAgentService>();
            services.AddSingleton<ComprehensiveStockAnalysisAgent>();
            services.AddSingleton<AcademicResearchPaperAgent>();
        }
    }
}
