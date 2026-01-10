using QuantResearchAgent.Services;
using QuantResearchAgent.Services.ResearchAgents;
using QuantResearchAgent.Plugins;
using Microsoft.SemanticKernel;
using Microsoft.AspNetCore.StaticFiles;

var builder = WebApplication.CreateBuilder(args);

// Add configuration from main FeenQR project (root level)
builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile(Path.Combine(Directory.GetCurrentDirectory(), "../../appsettings.json"), optional: false, reloadOnChange: true);

// Add services to the container
builder.Services.AddControllers();
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

// Register Semantic Kernel with OpenAI text generation (required by YouTubeAnalysisService)
builder.Services.AddSingleton<Kernel>(sp => 
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var kernelBuilder = Kernel.CreateBuilder();
    
    // Add OpenAI chat completion (ITextGenerationService)
    var openAiKey = configuration["OpenAI:ApiKey"];
    var openAiModel = configuration["OpenAI:ModelId"] ?? "gpt-4o-mini";
    
    if (!string.IsNullOrEmpty(openAiKey))
    {
        kernelBuilder.AddOpenAIChatCompletion(openAiModel, openAiKey);
    }
    
    return kernelBuilder.Build();
});

// Register FeenQR services
builder.Services.AddHttpClient();
builder.Services.AddSingleton<LeanDataService>();
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
builder.Services.AddSingleton<NewsSentimentAnalysisService>();
builder.Services.AddSingleton<WebDataExtractionService>();
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
builder.Services.AddSingleton<AcademicResearchService>();
builder.Services.AddSingleton<YouTubeAnalysisService>();
builder.Services.AddSingleton<ReportGenerationService>();
builder.Services.AddSingleton<LinkedInScrapingService>();

// Register Portfolio Management services
builder.Services.AddSingleton<PortfolioService>();
builder.Services.AddSingleton<PortfolioOptimizationService>();
builder.Services.AddSingleton<RiskManagementService>();
builder.Services.AddSingleton<MonteCarloService>();

var app = builder.Build();

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

app.UseCors("AllowBlazorWasm");
app.UseRouting();

// API routes must come BEFORE the fallback
app.MapControllers();

// Only fallback to SPA for non-API routes
app.MapFallbackToFile("index.html");

app.Run();
