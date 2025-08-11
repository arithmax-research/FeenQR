using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using QuantResearchAgent.Core;
using QuantResearchAgent.Services;
using QuantResearchAgent.Services.ResearchAgents;
using QuantResearchAgent.Plugins;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Builder;

namespace QuantResearchAgent
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // Check if we should run as web API
            if (args.Length > 0 && args[0] == "--web")
            {
                await RunWebApiAsync(args);
                return;
            }

            // Run as CLI by default
            await RunCliAsync(args);
        }

        static async Task RunWebApiAsync(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Build configuration
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            // Build semantic kernel
            var openAiApiKey = configuration["OpenAI:ApiKey"];
            var modelId = configuration["OpenAI:ModelId"] ?? "gpt-4o";
            
            var kernel = Kernel.CreateBuilder()
                .AddOpenAIChatCompletion(modelId, openAiApiKey!)
                .Build();

            // Configure services
            ConfigureServices(builder.Services, configuration, kernel);

            // Add API controllers
            builder.Services.AddControllers();
            builder.Services.AddCors(options =>
            {
                options.AddDefaultPolicy(policy =>
                {
                    policy.AllowAnyOrigin()
                          .AllowAnyMethod()
                          .AllowAnyHeader();
                });
            });

            // Register web search and financial data plugins
            builder.Services.AddHttpClient<QuantResearchAgent.Plugins.GoogleWebSearchPlugin>();
            builder.Services.AddSingleton<QuantResearchAgent.Plugins.IWebSearchPlugin>(sp =>
                sp.GetRequiredService<QuantResearchAgent.Plugins.GoogleWebSearchPlugin>());

            builder.Services.AddHttpClient<QuantResearchAgent.Plugins.YahooFinanceDataPlugin>();
            builder.Services.AddSingleton<QuantResearchAgent.Plugins.IFinancialDataPlugin>(sp =>
                sp.GetRequiredService<QuantResearchAgent.Plugins.YahooFinanceDataPlugin>());

            var app = builder.Build();

            // Configure middleware
            app.UseCors();
            app.UseRouting();
            app.MapControllers();

            // Get the orchestrator and start
            var orchestrator = app.Services.GetRequiredService<AgentOrchestrator>();
            await orchestrator.StartAsync();

            Console.WriteLine("Arithmax Research Agent API started at http://localhost:5000");
            Console.WriteLine("Frontend available at http://localhost:4321");
            
            await app.RunAsync("http://localhost:5000");
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
            services.AddSingleton<NewsSentimentAnalysisService>();
            services.AddSingleton<YahooFinanceService>();
            services.AddSingleton<RedditScrapingService>();
            services.AddSingleton<PortfolioOptimizationService>();
            services.AddSingleton<SocialMediaScrapingService>();
            services.AddSingleton<WebDataExtractionService>();
            services.AddSingleton<ReportGenerationService>();
            services.AddSingleton<SatelliteImageryAnalysisService>();

            // Add research agents
            services.AddSingleton<NewsScrapingService>();
            services.AddSingleton<MarketSentimentAgentService>();
            services.AddSingleton<StatisticalPatternAgentService>();
            services.AddSingleton<ComprehensiveStockAnalysisAgent>();
            services.AddSingleton<AcademicResearchPaperAgent>();
        }
    }
}
