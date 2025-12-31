using QuantResearchAgent.Services;
using Microsoft.SemanticKernel;
using Microsoft.AspNetCore.StaticFiles;

var builder = WebApplication.CreateBuilder(args);

// Add configuration from main FeenQR project
builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile(Path.Combine(Directory.GetCurrentDirectory(), "../../FeenQR/appsettings.json"), optional: true, reloadOnChange: true);

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

// Register Semantic Kernel (required by some services)
builder.Services.AddSingleton<Kernel>(sp => 
{
    var kernelBuilder = Kernel.CreateBuilder();
    // Optional: Add AI services if needed
    return kernelBuilder.Build();
});

// Register FeenQR services
builder.Services.AddHttpClient();
builder.Services.AddSingleton<LeanDataService>();
builder.Services.AddSingleton<AlpacaService>();
builder.Services.AddSingleton<AlphaVantageService>();
builder.Services.AddSingleton<YahooFinanceService>();
builder.Services.AddSingleton<PolygonService>();
builder.Services.AddSingleton<DataBentoService>();
builder.Services.AddSingleton<DeepSeekService>();
builder.Services.AddSingleton<OpenAIService>();

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
app.MapControllers();
app.MapFallbackToFile("index.html");

app.Run();
