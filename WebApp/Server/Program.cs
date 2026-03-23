using QuantResearchAgent.Services;
using QuantResearchAgent.Services.ResearchAgents;
using QuantResearchAgent.Plugins;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Embeddings;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.Connectors.Qdrant;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Qdrant.Client;
using Microsoft.AspNetCore.StaticFiles;

#pragma warning disable SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates
#pragma warning disable SKEXP0010 // Type is for evaluation purposes only and is subject to change or removal in future updates
#pragma warning disable SKEXP0050 // Type is for evaluation purposes only and is subject to change or removal in future updates

// Web startup only.
// Add/modify web API and web host service registrations in this file.
// Do not add web registrations to root Program.cs (CLI startup).
var builder = WebApplication.CreateBuilder(args);

// Add configuration from main FeenQR project (root level)
builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile(Path.Combine(Directory.GetCurrentDirectory(), "../../appsettings.json"), optional: false, reloadOnChange: true);

// Add services to the container
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals;
        options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    });
builder.Services.AddRazorPages();
builder.Services.AddOpenApi();

// Add CORS for Blazor WebAssembly
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowBlazorWasm",
        policy => policy.AllowAnyOrigin()
                       .AllowAnyMethod()
                       .AllowAnyHeader());
});

// Register Semantic Kernel with OpenAI or DeepSeek
builder.Services.AddSingleton<Kernel>(sp => 
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var kernelBuilder = Kernel.CreateBuilder();
    
    // Try DeepSeek first, then fall back to OpenAI
    var deepSeekKey = configuration["DeepSeek:ApiKey"];
    var deepSeekModel = configuration["DeepSeek:ModelId"] ?? "deepseek-chat";
    var deepSeekEndpoint = configuration["DeepSeek:Endpoint"] ?? "https://api.deepseek.com";
    
    var openAiKey = configuration["OpenAI:ApiKey"];
    var openAiModel = configuration["OpenAI:ModelId"] ?? "gpt-4o-mini";
    
    if (!string.IsNullOrEmpty(deepSeekKey))
    {
        // Use DeepSeek with OpenAI-compatible endpoint
        kernelBuilder.AddOpenAIChatCompletion(
            modelId: deepSeekModel,
            apiKey: deepSeekKey,
            endpoint: new Uri($"{deepSeekEndpoint}/v1")
        );
    }
    else if (!string.IsNullOrEmpty(openAiKey))
    {
        kernelBuilder.AddOpenAIChatCompletion(openAiModel, openAiKey);
    }
    
    return kernelBuilder.Build();
});

// Register Qdrant client for direct access (using gRPC port 6334)
builder.Services.AddSingleton<QdrantClient>(sp => new QdrantClient("localhost", port: 6334, https: false));

// Register embedding service for RAG
builder.Services.AddSingleton<ITextEmbeddingGenerationService>(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var openAiKey = configuration["OpenAI:ApiKey"];
    
    if (string.IsNullOrEmpty(openAiKey))
    {
        throw new InvalidOperationException("OpenAI API key is required for RAG functionality");
    }
    
    return new OpenAITextEmbeddingGenerationService("text-embedding-3-small", openAiKey);
});

// Register semantic memory - Qdrant persistence via custom implementation
builder.Services.AddSingleton<ISemanticTextMemory>(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var openAiKey = configuration["OpenAI:ApiKey"];
    var embeddingModel = "text-embedding-3-small";
    
    if (string.IsNullOrEmpty(openAiKey))
    {
        throw new InvalidOperationException("OpenAI API key required for RAG");
    }
    
    // Create embedding service
    var embeddingService = new OpenAITextEmbeddingGenerationService(embeddingModel, openAiKey);
    
    // Use VolatileMemoryStore as interface, actual persistence handled by Qdrant in PaperRAGService
    var memoryStore = new VolatileMemoryStore();
    
    return new SemanticTextMemory(memoryStore, embeddingService);
});

// Register FeenQR services
builder.Services.AddHttpClient().ConfigureHttpClientDefaults(http => 
{
    // Configure proxy if enabled
    var proxyEnabled = builder.Configuration.GetValue<bool>("Proxy:Enabled");
    var proxyAddress = builder.Configuration["Proxy:Address"];
    
    if (proxyEnabled && !string.IsNullOrEmpty(proxyAddress))
    {
        var proxyUsername = builder.Configuration["Proxy:Username"];
        var proxyPassword = builder.Configuration["Proxy:Password"];
        var bypassOnLocal = builder.Configuration.GetValue<bool>("Proxy:BypassOnLocal", true);
        
        http.ConfigurePrimaryHttpMessageHandler(() =>
        {
            var proxy = new System.Net.WebProxy(proxyAddress, bypassOnLocal);
            
            if (!string.IsNullOrEmpty(proxyUsername))
            {
                proxy.Credentials = new System.Net.NetworkCredential(proxyUsername, proxyPassword);
            }
            
            var handler = new HttpClientHandler
            {
                Proxy = proxy,
                UseProxy = true
            };
            
            Console.WriteLine($"✓ Proxy configured: {proxyAddress}");
            return handler;
        });
    }
    
    // Increase timeout for long-running operations like video analysis
    http.ConfigureHttpClient(client => client.Timeout = TimeSpan.FromMinutes(10));
});
builder.Services.AddSingleton<LeanDataService>();
builder.Services.AddSingleton<LeanStrategyPipelineService>();
builder.Services.AddSingleton<StrategyTextParser>();
builder.Services.AddSingleton<DataRequirementAdvisor>();
builder.Services.AddSingleton<AlpacaService>();
builder.Services.AddSingleton<AlphaVantageService>();
builder.Services.AddSingleton<FinancialModelingPrepService>();
builder.Services.AddSingleton<YahooFinanceService>();
builder.Services.AddSingleton<YFinanceApiService>();
builder.Services.AddSingleton<PolygonService>();
builder.Services.AddSingleton<DataBentoService>();
builder.Services.AddSingleton<DeepSeekService>();
builder.Services.AddSingleton<OpenAIService>();

// Register research dependencies (before EnhancedFundamentalAnalysisService)
builder.Services.AddSingleton<MarketDataService>();
builder.Services.AddSingleton<StatisticalTestingService>();
builder.Services.AddSingleton<TechnicalAnalysisService>();
builder.Services.AddSingleton<YFinanceNewsService>();
builder.Services.AddSingleton<FinvizNewsService>();
builder.Services.AddSingleton<NewsScrapingService>();
builder.Services.AddSingleton<UrlNewsScrapingService>();
builder.Services.AddSingleton<SocialMediaScrapingService>();
builder.Services.AddSingleton<NewsSentimentAnalysisService>();

// Register enhanced article sentiment analysis services
// ⚠️ IMPORTANT: These services must also be registered in main Program.cs
// See SYNC_SERVICES.md for details
builder.Services.AddHttpClient<ArticleScraperService>()
    .ConfigureHttpClient((sp, client) =>
    {
        var config = sp.GetRequiredService<IConfiguration>();
        var timeoutSeconds = config.GetValue<int>("ArticleScraper:TimeoutSeconds", 10);
        client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
    })
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        AllowAutoRedirect = true,
        MaxAutomaticRedirections = 5
    });
builder.Services.AddSingleton<ChunkGeneratorService>();
builder.Services.AddSingleton<EmbeddingService>();
builder.Services.AddSingleton<VectorStoreService>();
builder.Services.AddSingleton<NewsApiClient>();

builder.Services.AddSingleton<WebDataExtractionService>();
builder.Services.AddSingleton<GoogleWebSearchPlugin>();
builder.Services.AddSingleton<IWebSearchPlugin, GoogleWebSearchPlugin>();
builder.Services.AddSingleton<IFinancialDataPlugin, YahooFinanceDataPlugin>();
builder.Services.AddSingleton<LLMRouterService>();
builder.Services.AddSingleton<ILLMService>(sp => sp.GetRequiredService<LLMRouterService>());
builder.Services.AddSingleton<AcademicResearchPaperAgent>();

// Register Fundamental Analysis services (after dependencies)
builder.Services.AddSingleton<EnhancedFundamentalAnalysisService>(sp =>
    new EnhancedFundamentalAnalysisService(
        sp.GetRequiredService<AlphaVantageService>(),
        sp.GetRequiredService<FinancialModelingPrepService>(),
        sp.GetRequiredService<YFinanceApiService>(),
        sp.GetRequiredService<AlpacaService>(),
        sp.GetRequiredService<DataBentoService>(),
        sp.GetRequiredService<ILogger<EnhancedFundamentalAnalysisService>>(),
        sp.GetRequiredService<LLMRouterService>()
    )
);

// Register Research services
builder.Services.AddSingleton<ConversationalResearchService>();
builder.Services.AddSingleton<FeenRAGenticService>();
builder.Services.AddSingleton<AcademicResearchService>();
builder.Services.AddSingleton<YouTubeAnalysisService>();
builder.Services.AddSingleton<ReportGenerationService>();
builder.Services.AddSingleton<LinkedInScrapingService>();
builder.Services.AddSingleton<PaperRAGService>();
builder.Services.AddSingleton<MarketSentimentAgentService>();

// Register RedditScrapingService with HttpClient
builder.Services.AddHttpClient<RedditScrapingService>()
    .ConfigureHttpClient((sp, client) =>
    {
        var config = sp.GetRequiredService<IConfiguration>();
        var userAgent = config["Reddit:UserAgent"] ?? "QuantResearchAgent/1.0 (Financial Research Application)";
        client.DefaultRequestHeaders.Add("User-Agent", userAgent);
        client.Timeout = TimeSpan.FromSeconds(30);
    });

builder.Services.AddSingleton<SatelliteImageryAnalysisService>();
// Satellite imagery service enabled with Google OAuth and ESA Copernicus credentials
builder.Services.AddSingleton<PortContainerAnalysisService>();

// Register Portfolio Management services
builder.Services.AddSingleton<PortfolioService>();
builder.Services.AddSingleton<PortfolioOptimizationService>();
builder.Services.AddSingleton<RiskManagementService>();
builder.Services.AddSingleton<MonteCarloService>();

// Register Machine Learning service
builder.Services.AddSingleton<MachineLearningService>();

var app = builder.Build();

// Initialize VectorStoreService for Qdrant
using (var scope = app.Services.CreateScope())
{
    var vectorStore = scope.ServiceProvider.GetRequiredService<VectorStoreService>();
    await vectorStore.InitializeAsync();
}

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

// Configure static files with custom MIME types for Blazor WASM
var provider = new FileExtensionContentTypeProvider();
provider.Mappings[".dat"] = "application/octet-stream";
provider.Mappings[".wasm"] = "application/wasm";
provider.Mappings[".blat"] = "application/octet-stream";
provider.Mappings[".dll"] = "application/octet-stream";
provider.Mappings[".json"] = "application/json";

app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = provider,
    ServeUnknownFileTypes = true,
    DefaultContentType = "application/octet-stream"
});

// Serve uploaded PDFs
var uploadsPath = Path.Combine(Directory.GetCurrentDirectory(), "uploads");
Directory.CreateDirectory(uploadsPath);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(uploadsPath),
    RequestPath = "/uploads",
    ContentTypeProvider = provider
});

app.UseCors("AllowBlazorWasm");
app.UseRouting();

// API routes must come BEFORE the fallback
app.MapControllers();

// Only fallback to SPA for non-API routes
app.MapFallbackToFile("index.html");

app.Run();
