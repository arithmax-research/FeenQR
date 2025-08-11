using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using System.Text;
using System.Text.Json;

namespace QuantResearchAgent.Services;

public class ReportGenerationService
{
    private readonly ILogger<ReportGenerationService> _logger;
    private readonly Kernel _kernel;
    private readonly YahooFinanceService _yahooFinanceService;
    private readonly TechnicalAnalysisService _technicalAnalysisService;
    private readonly NewsSentimentAnalysisService _sentimentService;
    private readonly WebDataExtractionService _webDataService;

    public ReportGenerationService(
        ILogger<ReportGenerationService> logger,
        Kernel kernel,
        YahooFinanceService yahooFinanceService,
        TechnicalAnalysisService technicalAnalysisService,
        NewsSentimentAnalysisService sentimentService,
        WebDataExtractionService webDataService)
    {
        _logger = logger;
        _kernel = kernel;
        _yahooFinanceService = yahooFinanceService;
        _technicalAnalysisService = technicalAnalysisService;
        _sentimentService = sentimentService;
        _webDataService = webDataService;
    }

    public async Task<ResearchReportResult> GenerateComprehensiveReportAsync(string symbol, ReportType reportType = ReportType.StockAnalysis)
    {
        try
        {
            _logger.LogInformation("Generating {ReportType} report for {Symbol}", reportType, symbol);

            var report = new ResearchReportResult
            {
                Symbol = symbol,
                ReportType = reportType,
                GeneratedAt = DateTime.UtcNow,
                Title = $"{reportType} Report: {symbol}",
                Sections = new List<ReportSection>()
            };

            switch (reportType)
            {
                case ReportType.StockAnalysis:
                    await GenerateStockAnalysisReport(report);
                    break;
                case ReportType.EarningsAnalysis:
                    await GenerateEarningsAnalysisReport(report);
                    break;
                case ReportType.TechnicalAnalysis:
                    await GenerateTechnicalAnalysisReport(report);
                    break;
                case ReportType.PortfolioAnalysis:
                    await GeneratePortfolioAnalysisReport(report);
                    break;
                case ReportType.MarketSector:
                    await GenerateMarketSectorReport(report);
                    break;
                default:
                    await GenerateStockAnalysisReport(report);
                    break;
            }

            report.ExecutiveSummary = await GenerateExecutiveSummary(report);
            report.HtmlContent = await GenerateHtmlReport(report);
            report.MarkdownContent = await GenerateMarkdownReport(report);

            // Save the report to files
            var savedFiles = await SaveReportToFilesAsync(report);
            report.SavedFilePaths = savedFiles;

            _logger.LogInformation("Successfully generated {ReportType} report for {Symbol} with {SectionCount} sections. Saved to: {FilePaths}", 
                reportType, symbol, report.Sections.Count, string.Join(", ", savedFiles.Values));

            return report;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate report for {Symbol}", symbol);
            throw;
        }
    }

    private async Task GenerateStockAnalysisReport(ResearchReportResult report)
    {
        // Executive Summary Section
        var executiveSection = await GenerateExecutiveSummarySection(report.Symbol);
        report.Sections.Add(executiveSection);

        // Market Data Section
        var marketDataSection = await GenerateMarketDataSection(report.Symbol);
        report.Sections.Add(marketDataSection);

        // Technical Analysis Section
        var technicalSection = await GenerateTechnicalAnalysisSection(report.Symbol);
        report.Sections.Add(technicalSection);

        // Fundamental Analysis Section
        var fundamentalSection = await GenerateFundamentalAnalysisSection(report.Symbol);
        report.Sections.Add(fundamentalSection);

        // News & Sentiment Section
        var sentimentSection = await GenerateNewsSentimentSection(report.Symbol);
        report.Sections.Add(sentimentSection);

        // Risk Assessment Section
        var riskSection = await GenerateRiskAssessmentSection(report.Symbol);
        report.Sections.Add(riskSection);

        // Investment Recommendation Section
        var recommendationSection = await GenerateRecommendationSection(report.Symbol);
        report.Sections.Add(recommendationSection);
    }

    private async Task GenerateEarningsAnalysisReport(ResearchReportResult report)
    {
        // Earnings Overview
        var earningsSection = await GenerateEarningsOverviewSection(report.Symbol);
        report.Sections.Add(earningsSection);

        // Financial Performance Trends
        var trendsSection = await GenerateFinancialTrendsSection(report.Symbol);
        report.Sections.Add(trendsSection);

        // Earnings Forecast
        var forecastSection = await GenerateEarningsForecastSection(report.Symbol);
        report.Sections.Add(forecastSection);
    }

    private async Task GenerateTechnicalAnalysisReport(ResearchReportResult report)
    {
        // Price Action Analysis
        var priceActionSection = await GeneratePriceActionSection(report.Symbol);
        report.Sections.Add(priceActionSection);

        // Technical Indicators
        var indicatorsSection = await GenerateTechnicalIndicatorsSection(report.Symbol);
        report.Sections.Add(indicatorsSection);

        // Support & Resistance Levels
        var levelsSection = await GenerateSupportResistanceSection(report.Symbol);
        report.Sections.Add(levelsSection);

        // Trading Signals
        var signalsSection = await GenerateTradingSignalsSection(report.Symbol);
        report.Sections.Add(signalsSection);
    }

    private async Task GeneratePortfolioAnalysisReport(ResearchReportResult report)
    {
        // Portfolio Composition
        var compositionSection = await GeneratePortfolioCompositionSection(report.Symbol);
        report.Sections.Add(compositionSection);

        // Performance Metrics
        var performanceSection = await GeneratePortfolioPerformanceSection(report.Symbol);
        report.Sections.Add(performanceSection);

        // Risk Analysis
        var riskSection = await GeneratePortfolioRiskSection(report.Symbol);
        report.Sections.Add(riskSection);
    }

    private async Task GenerateMarketSectorReport(ResearchReportResult report)
    {
        // Sector Overview
        var sectorSection = await GenerateSectorOverviewSection(report.Symbol);
        report.Sections.Add(sectorSection);

        // Sector Performance
        var performanceSection = await GenerateSectorPerformanceSection(report.Symbol);
        report.Sections.Add(performanceSection);

        // Key Players Analysis
        var playersSection = await GenerateKeyPlayersSection(report.Symbol);
        report.Sections.Add(playersSection);
    }

    private async Task<ReportSection> GenerateExecutiveSummarySection(string symbol)
    {
        var marketData = await _yahooFinanceService.GetMarketDataAsync(symbol);
        
        var content = new StringBuilder();
        content.AppendLine($"## Executive Summary - {symbol}");
        content.AppendLine();
        
        if (marketData != null)
        {
            content.AppendLine($"**Current Price:** ${marketData.CurrentPrice:F2}");
            content.AppendLine($"**24h Change:** {marketData.ChangePercent24h:F2}%");
            content.AppendLine($"**Volume:** {marketData.Volume:N0}");
            content.AppendLine($"**Day Range:** ${marketData.Low24h:F2} - ${marketData.High24h:F2}");
        }

        return new ReportSection
        {
            Title = "Executive Summary",
            Content = content.ToString(),
            SectionType = "summary",
            Charts = new List<ChartData>(),
            Tables = new List<TableData>()
        };
    }

    private async Task<ReportSection> GenerateMarketDataSection(string symbol)
    {
        var marketData = await _yahooFinanceService.GetMarketDataAsync(symbol);
        
        var content = new StringBuilder();
        content.AppendLine($"## Market Data - {symbol}");
        content.AppendLine();

        if (marketData != null)
        {
            content.AppendLine("### Key Metrics");
            content.AppendLine($"- **Symbol:** {symbol}");
            content.AppendLine($"- **Current Price:** ${marketData.CurrentPrice:F2}");
            content.AppendLine($"- **24h Change:** {marketData.Change24h:F2} ({marketData.ChangePercent24h:F2}%)");
            content.AppendLine($"- **Day Range:** ${marketData.Low24h:F2} - ${marketData.High24h:F2}");
            content.AppendLine($"- **Volume:** {marketData.Volume:N0}");
            content.AppendLine($"- **Last Updated:** {marketData.LastUpdated:g}");
        }

        return new ReportSection
        {
            Title = "Market Data",
            Content = content.ToString(),
            SectionType = "data",
            Charts = await GeneratePriceCharts(symbol),
            Tables = new List<TableData>()
        };
    }

    private async Task<ReportSection> GenerateTechnicalAnalysisSection(string symbol)
    {
        var content = new StringBuilder();
        content.AppendLine($"## Technical Analysis - {symbol}");
        content.AppendLine();

        try
        {
            // This would integrate with your existing TechnicalAnalysisService
            content.AppendLine("### Technical Indicators");
            content.AppendLine("- **RSI:** Analyzing momentum");
            content.AppendLine("- **MACD:** Trend analysis");
            content.AppendLine("- **Moving Averages:** Support/resistance levels");
            content.AppendLine("- **Bollinger Bands:** Volatility analysis");
            
            content.AppendLine();
            content.AppendLine("### Analysis Summary");
            content.AppendLine("Technical analysis indicates...");
        }
        catch (Exception ex)
        {
            content.AppendLine($"Technical analysis unavailable: {ex.Message}");
        }

        return new ReportSection
        {
            Title = "Technical Analysis",
            Content = content.ToString(),
            SectionType = "technical",
            Charts = await GenerateTechnicalCharts(symbol),
            Tables = new List<TableData>()
        };
    }

    private async Task<ReportSection> GenerateFundamentalAnalysisSection(string symbol)
    {
        var content = new StringBuilder();
        content.AppendLine($"## Fundamental Analysis - {symbol}");
        content.AppendLine();

        // Extract financial data from web sources
        try
        {
            var earningsData = await _webDataService.ExtractEarningsReportDataAsync(symbol, 4);
            
            content.AppendLine("### Financial Health");
            content.AppendLine("- **Revenue Growth:** Analyzing quarterly trends");
            content.AppendLine("- **Profitability:** Margin analysis");
            content.AppendLine("- **Debt Levels:** Balance sheet strength");
            content.AppendLine("- **Cash Flow:** Operating efficiency");
            
            if (earningsData.Any())
            {
                content.AppendLine();
                content.AppendLine($"### Recent Filings ({earningsData.Count} documents analyzed)");
                foreach (var filing in earningsData.Take(3))
                {
                    content.AppendLine($"- {filing.Title} ({filing.ExtractedAt:MMM dd, yyyy})");
                }
            }
        }
        catch (Exception ex)
        {
            content.AppendLine($"Fundamental analysis data unavailable: {ex.Message}");
        }

        return new ReportSection
        {
            Title = "Fundamental Analysis",
            Content = content.ToString(),
            SectionType = "fundamental",
            Charts = new List<ChartData>(),
            Tables = await GenerateFinancialTables(symbol)
        };
    }

    private async Task<ReportSection> GenerateNewsSentimentSection(string symbol)
    {
        var content = new StringBuilder();
        content.AppendLine($"## News & Sentiment Analysis - {symbol}");
        content.AppendLine();

        try
        {
            var sentiment = await _sentimentService.AnalyzeSymbolSentimentAsync(symbol);
            
            if (sentiment != null)
            {
                content.AppendLine("### Sentiment Overview");
                content.AppendLine($"- **Overall Sentiment:** {sentiment.OverallSentiment}");
                content.AppendLine($"- **Sentiment Score:** {sentiment.SentimentScore:F2}");
                content.AppendLine($"- **Confidence Level:** {sentiment.Confidence:F1}%");
                
                if (sentiment.KeyThemes?.Any() == true)
                {
                    content.AppendLine();
                    content.AppendLine("### Key Themes");
                    foreach (var theme in sentiment.KeyThemes.Take(5))
                    {
                        content.AppendLine($"- {theme}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            content.AppendLine($"Sentiment analysis unavailable: {ex.Message}");
        }

        return new ReportSection
        {
            Title = "News & Sentiment",
            Content = content.ToString(),
            SectionType = "sentiment",
            Charts = await GenerateSentimentCharts(symbol),
            Tables = new List<TableData>()
        };
    }

    private async Task<ReportSection> GenerateRiskAssessmentSection(string symbol)
    {
        var content = new StringBuilder();
        content.AppendLine($"## Risk Assessment - {symbol}");
        content.AppendLine();

        var marketData = await _yahooFinanceService.GetMarketDataAsync(symbol);
        
        content.AppendLine("### Risk Metrics");
        content.AppendLine("- **Volatility:** Historical price volatility analysis");
        content.AppendLine("- **Beta:** Market correlation risk");
        content.AppendLine("- **Liquidity Risk:** Trading volume analysis");
        content.AppendLine("- **Sector Risk:** Industry-specific risks");
        
        if (marketData != null)
        {
            var volatility = CalculateVolatility(marketData);
            content.AppendLine();
            content.AppendLine($"### Current Risk Profile");
            content.AppendLine($"- **Estimated Volatility:** {volatility:F2}%");
            content.AppendLine($"- **Risk Rating:** {GetRiskRating(volatility)}");
        }

        return new ReportSection
        {
            Title = "Risk Assessment",
            Content = content.ToString(),
            SectionType = "risk",
            Charts = new List<ChartData>(),
            Tables = new List<TableData>()
        };
    }

    private async Task<ReportSection> GenerateRecommendationSection(string symbol)
    {
        var content = new StringBuilder();
        content.AppendLine($"## Investment Recommendation - {symbol}");
        content.AppendLine();

        // Generate AI-powered recommendation
        var prompt = $@"Based on the comprehensive analysis of {symbol}, provide an investment recommendation.
Consider technical analysis, fundamental analysis, market sentiment, and risk factors.
Provide a clear recommendation (Buy/Hold/Sell) with reasoning and price targets.";

        try
        {
            var recommendation = await _kernel.InvokePromptAsync(prompt);
            content.AppendLine("### AI-Powered Analysis");
            content.AppendLine(recommendation.ToString());
        }
        catch (Exception ex)
        {
            content.AppendLine($"AI recommendation unavailable: {ex.Message}");
            content.AppendLine();
            content.AppendLine("### Manual Analysis Required");
            content.AppendLine("Please review all sections above to form your investment decision.");
        }

        return new ReportSection
        {
            Title = "Investment Recommendation",
            Content = content.ToString(),
            SectionType = "recommendation",
            Charts = new List<ChartData>(),
            Tables = new List<TableData>()
        };
    }

    private async Task<string> GenerateExecutiveSummary(ResearchReportResult report)
    {
        var prompt = $@"Generate a concise executive summary for the {report.ReportType} report on {report.Symbol}.
Highlight the key findings, investment thesis, and main recommendation.
Keep it under 200 words and focus on actionable insights.";

        try
        {
            var summary = await _kernel.InvokePromptAsync(prompt);
            return summary.ToString();
        }
        catch
        {
            return $"Executive summary for {report.Symbol} {report.ReportType} report generated on {report.GeneratedAt:MMM dd, yyyy}.";
        }
    }

    private async Task<string> GenerateHtmlReport(ResearchReportResult report)
    {
        var html = new StringBuilder();
        
        html.AppendLine("<!DOCTYPE html>");
        html.AppendLine("<html>");
        html.AppendLine("<head>");
        html.AppendLine($"<title>{report.Title}</title>");
        html.AppendLine("<style>");
        html.AppendLine(GetReportCss());
        html.AppendLine("</style>");
        html.AppendLine("</head>");
        html.AppendLine("<body>");
        
        html.AppendLine($"<div class='report-header'>");
        html.AppendLine($"<h1>{report.Title}</h1>");
        html.AppendLine($"<p class='report-meta'>Generated on {report.GeneratedAt:MMMM dd, yyyy HH:mm} UTC</p>");
        html.AppendLine("</div>");
        
        html.AppendLine("<div class='executive-summary'>");
        html.AppendLine("<h2>Executive Summary</h2>");
        html.AppendLine($"<p>{report.ExecutiveSummary}</p>");
        html.AppendLine("</div>");
        
        foreach (var section in report.Sections)
        {
            html.AppendLine("<div class='report-section'>");
            html.AppendLine($"<div class='section-content'>{section.Content.Replace("\n", "<br/>")}</div>");
            html.AppendLine("</div>");
        }
        
        html.AppendLine("</body>");
        html.AppendLine("</html>");
        
        return html.ToString();
    }

    private async Task<string> GenerateMarkdownReport(ResearchReportResult report)
    {
        var markdown = new StringBuilder();
        
        markdown.AppendLine($"# {report.Title}");
        markdown.AppendLine();
        markdown.AppendLine($"*Generated on {report.GeneratedAt:MMMM dd, yyyy HH:mm} UTC*");
        markdown.AppendLine();
        
        markdown.AppendLine("## Executive Summary");
        markdown.AppendLine();
        markdown.AppendLine(report.ExecutiveSummary);
        markdown.AppendLine();
        
        foreach (var section in report.Sections)
        {
            markdown.AppendLine(section.Content);
            markdown.AppendLine();
        }
        
        return markdown.ToString();
    }

    private async Task<Dictionary<string, string>> SaveReportToFilesAsync(ResearchReportResult report)
    {
        var savedFiles = new Dictionary<string, string>();
        
        try
        {
            // Create reports directory structure
            var reportsDir = Path.Combine(Directory.GetCurrentDirectory(), "reports");
            var dateDir = Path.Combine(reportsDir, report.GeneratedAt.ToString("yyyy-MM-dd"));
            var symbolDir = Path.Combine(dateDir, report.Symbol.ToUpper());
            
            Directory.CreateDirectory(symbolDir);
            
            // Generate file names
            var timestamp = report.GeneratedAt.ToString("HHmmss");
            var reportTypeStr = report.ReportType.ToString().ToLower();
            var htmlFileName = $"{report.Symbol}_{reportTypeStr}_{timestamp}.html";
            var markdownFileName = $"{report.Symbol}_{reportTypeStr}_{timestamp}.md";
            var jsonFileName = $"{report.Symbol}_{reportTypeStr}_{timestamp}.json";
            
            var htmlPath = Path.Combine(symbolDir, htmlFileName);
            var markdownPath = Path.Combine(symbolDir, markdownFileName);
            var jsonPath = Path.Combine(symbolDir, jsonFileName);
            
            // Save HTML report
            await File.WriteAllTextAsync(htmlPath, report.HtmlContent);
            savedFiles["html"] = htmlPath;
            
            // Save Markdown report
            await File.WriteAllTextAsync(markdownPath, report.MarkdownContent);
            savedFiles["markdown"] = markdownPath;
            
            // Save JSON report (structured data)
            var jsonReport = new
            {
                report.Symbol,
                ReportType = report.ReportType.ToString(),
                report.Title,
                GeneratedAt = report.GeneratedAt.ToString("O"),
                report.ExecutiveSummary,
                Sections = report.Sections.Select(s => new
                {
                    s.Title,
                    s.Content,
                    s.SectionType,
                    ChartsCount = s.Charts?.Count ?? 0,
                    TablesCount = s.Tables?.Count ?? 0
                }).ToList(),
                Metadata = new
                {
                    HtmlContentLength = report.HtmlContent?.Length ?? 0,
                    MarkdownContentLength = report.MarkdownContent?.Length ?? 0,
                    SectionCount = report.Sections?.Count ?? 0
                }
            };
            
            var jsonContent = JsonSerializer.Serialize(jsonReport, new JsonSerializerOptions 
            { 
                WriteIndented = true 
            });
            await File.WriteAllTextAsync(jsonPath, jsonContent);
            savedFiles["json"] = jsonPath;
            
            _logger.LogInformation("Report saved to {DirectoryPath} with files: {FileNames}", 
                symbolDir, string.Join(", ", savedFiles.Keys));
            
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save report files for {Symbol}", report.Symbol);
            throw;
        }
        
        return savedFiles;
    }

    // Placeholder methods for missing sections
    private async Task<ReportSection> GenerateEarningsOverviewSection(string symbol) => 
        new ReportSection { Title = "Earnings Overview", Content = $"Earnings analysis for {symbol}", SectionType = "earnings" };
    
    private async Task<ReportSection> GenerateFinancialTrendsSection(string symbol) => 
        new ReportSection { Title = "Financial Trends", Content = $"Financial trends for {symbol}", SectionType = "trends" };
    
    private async Task<ReportSection> GenerateEarningsForecastSection(string symbol) => 
        new ReportSection { Title = "Earnings Forecast", Content = $"Earnings forecast for {symbol}", SectionType = "forecast" };

    private async Task<ReportSection> GeneratePriceActionSection(string symbol) => 
        new ReportSection { Title = "Price Action", Content = $"Price action analysis for {symbol}", SectionType = "price" };

    private async Task<ReportSection> GenerateTechnicalIndicatorsSection(string symbol) => 
        new ReportSection { Title = "Technical Indicators", Content = $"Technical indicators for {symbol}", SectionType = "indicators" };

    private async Task<ReportSection> GenerateSupportResistanceSection(string symbol) => 
        new ReportSection { Title = "Support & Resistance", Content = $"Support and resistance levels for {symbol}", SectionType = "levels" };

    private async Task<ReportSection> GenerateTradingSignalsSection(string symbol) => 
        new ReportSection { Title = "Trading Signals", Content = $"Trading signals for {symbol}", SectionType = "signals" };

    private async Task<ReportSection> GeneratePortfolioCompositionSection(string symbol) => 
        new ReportSection { Title = "Portfolio Composition", Content = $"Portfolio composition analysis", SectionType = "composition" };

    private async Task<ReportSection> GeneratePortfolioPerformanceSection(string symbol) => 
        new ReportSection { Title = "Portfolio Performance", Content = $"Portfolio performance metrics", SectionType = "performance" };

    private async Task<ReportSection> GeneratePortfolioRiskSection(string symbol) => 
        new ReportSection { Title = "Portfolio Risk", Content = $"Portfolio risk analysis", SectionType = "risk" };

    private async Task<ReportSection> GenerateSectorOverviewSection(string symbol) => 
        new ReportSection { Title = "Sector Overview", Content = $"Sector overview and analysis", SectionType = "sector" };

    private async Task<ReportSection> GenerateSectorPerformanceSection(string symbol) => 
        new ReportSection { Title = "Sector Performance", Content = $"Sector performance analysis", SectionType = "performance" };

    private async Task<ReportSection> GenerateKeyPlayersSection(string symbol) => 
        new ReportSection { Title = "Key Players", Content = $"Key players in the sector", SectionType = "players" };

    private async Task<List<ChartData>> GeneratePriceCharts(string symbol) => new List<ChartData>();
    private async Task<List<ChartData>> GenerateTechnicalCharts(string symbol) => new List<ChartData>();
    private async Task<List<ChartData>> GenerateSentimentCharts(string symbol) => new List<ChartData>();
    private async Task<List<TableData>> GenerateFinancialTables(string symbol) => new List<TableData>();

    private double CalculateVolatility(YahooMarketData marketData) => 
        Math.Abs(marketData.ChangePercent24h) * 10; // Simplified volatility calculation

    private string GetRiskRating(double volatility) => 
        volatility switch
        {
            < 10 => "Low",
            < 25 => "Medium",
            < 50 => "High",
            _ => "Very High"
        };

    private string GetReportCss() => @"
        body { font-family: Arial, sans-serif; margin: 40px; line-height: 1.6; }
        .report-header { border-bottom: 2px solid #333; padding-bottom: 20px; margin-bottom: 30px; }
        .report-header h1 { color: #333; margin-bottom: 10px; }
        .report-meta { color: #666; font-style: italic; }
        .executive-summary { background: #f5f5f5; padding: 20px; border-radius: 5px; margin-bottom: 30px; }
        .report-section { margin-bottom: 30px; }
        .section-content { margin-bottom: 20px; }
        h2 { color: #444; border-bottom: 1px solid #ddd; padding-bottom: 10px; }
        h3 { color: #555; }
        ul { margin-left: 20px; }
        .chart-container { margin: 20px 0; text-align: center; }
        .table-container { margin: 20px 0; overflow-x: auto; }
        table { width: 100%; border-collapse: collapse; }
        th, td { border: 1px solid #ddd; padding: 8px; text-align: left; }
        th { background-color: #f2f2f2; }
    ";
}

public class ResearchReportResult
{
    public string Symbol { get; set; } = "";
    public ReportType ReportType { get; set; }
    public string Title { get; set; } = "";
    public DateTime GeneratedAt { get; set; }
    public string ExecutiveSummary { get; set; } = "";
    public List<ReportSection> Sections { get; set; } = new();
    public string HtmlContent { get; set; } = "";
    public string MarkdownContent { get; set; } = "";
    public Dictionary<string, string> SavedFilePaths { get; set; } = new();
}

public class ReportSection
{
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public string SectionType { get; set; } = "";
    public List<ChartData> Charts { get; set; } = new();
    public List<TableData> Tables { get; set; } = new();
}

public class ChartData
{
    public string Title { get; set; } = "";
    public string Type { get; set; } = ""; // line, bar, candlestick, etc.
    public List<DataPoint> DataPoints { get; set; } = new();
}

public class DataPoint
{
    public DateTime Date { get; set; }
    public double Value { get; set; }
    public string Label { get; set; } = "";
}

public enum ReportType
{
    StockAnalysis,
    EarningsAnalysis,
    TechnicalAnalysis,
    PortfolioAnalysis,
    MarketSector
}
