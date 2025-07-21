using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
            var kernel = Kernel.CreateBuilder()
                .AddOpenAIChatCompletion("gpt-4o", Environment.GetEnvironmentVariable("OPENAI_API_KEY")!)
                .Build();

            ConfigureServices(services, configuration, kernel);

            // Build service provider
            var serviceProvider = services.BuildServiceProvider();

            // Get the orchestrator and start
            var orchestrator = serviceProvider.GetRequiredService<AgentOrchestrator>();
            
            // Keep the application running
            await orchestrator.StartAsync();
            
            Console.WriteLine("QuantResearch Agent is running. Press any key to exit...");
            Console.ReadKey();
        }

        static void ConfigureServices(IServiceCollection services, IConfiguration configuration, Kernel kernel)
        {
            // Add configuration
            services.AddSingleton(configuration);
            services.AddSingleton(kernel);

            // Add core services
            services.AddSingleton<AgentOrchestrator>();
            services.AddSingleton<PodcastAnalysisService>();
            services.AddSingleton<MarketDataService>();
            services.AddSingleton<TradingSignalService>();
            services.AddSingleton<PortfolioService>();
            services.AddSingleton<RiskManagementService>();
            services.AddSingleton<CompanyValuationService>();
            services.AddSingleton<HighFrequencyDataService>();
            services.AddSingleton<TradingStrategyLibraryService>();

            // Add research agents
            services.AddSingleton<ArxivResearchAgentService>();
            services.AddSingleton<MarketSentimentAgentService>();
            services.AddSingleton<StatisticalPatternAgentService>();

            // Register plugins
            kernel.Plugins.AddFromType<PodcastAnalysisPlugin>();
            kernel.Plugins.AddFromType<MarketDataPlugin>();
            kernel.Plugins.AddFromType<TradingPlugin>();
            kernel.Plugins.AddFromType<RiskManagementPlugin>();
            kernel.Plugins.AddFromType<ArxivResearchPlugin>();
            kernel.Plugins.AddFromType<MarketSentimentPlugin>();
            kernel.Plugins.AddFromType<StatisticalPatternPlugin>();
            kernel.Plugins.AddFromType<CompanyValuationPlugin>();
            kernel.Plugins.AddFromType<HighFrequencyDataPlugin>();
            kernel.Plugins.AddFromType<TradingStrategyLibraryPlugin>();
        }
    }
}
