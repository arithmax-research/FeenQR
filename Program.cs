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

            // Add logging
            services.AddLogging();

            // Add InteractiveCLI
            services.AddSingleton<InteractiveCLI>();

            // Add core services
            services.AddSingleton<AgentOrchestrator>();
            services.AddSingleton<YouTubeAnalysisService>();
            services.AddSingleton<MarketDataService>();
            services.AddSingleton<TradingSignalService>();
            services.AddSingleton<PortfolioService>();
            services.AddSingleton<RiskManagementService>();
            services.AddSingleton<CompanyValuationService>();
            services.AddSingleton<HighFrequencyDataService>();
            services.AddSingleton<TradingStrategyLibraryService>();
            services.AddSingleton<HttpClient>();

            // Add research agents
            services.AddSingleton<MarketSentimentAgentService>();
            services.AddSingleton<StatisticalPatternAgentService>();
        }
    }
}
