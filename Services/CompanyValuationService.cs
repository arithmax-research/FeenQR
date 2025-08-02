using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Text.Json;
using RestSharp;

namespace QuantResearchAgent.Services;

/// <summary>
/// Service for comprehensive company valuation and fundamental analysis
/// Consolidated from Financial_Browser_Agent/company_valuation.py
/// </summary>
public class CompanyValuationService
{
    private readonly RestClient _client;
    private readonly Kernel _kernel;

    public CompanyValuationService(Kernel kernel)
    {
        _kernel = kernel;
        _client = new RestClient();
    }

    [KernelFunction]
    [Description("Performs comprehensive stock analysis including valuation metrics, technical indicators, and fundamental data")]
    public async Task<string> AnalyzeStockAsync(
        [Description("Stock ticker symbol")] string ticker,
        [Description("Analysis period in days")] int periodDays = 365)
    {
        try
        {
            var stockData = await FetchStockDataAsync(ticker, periodDays);
            var analysis = await PerformTechnicalAnalysisAsync(stockData);
            var valuation = await CalculateValuationMetricsAsync(ticker, stockData);
            var sentiment = await GetMarketSentimentAsync(ticker);
            var recommendation = await GenerateRecommendationAsync(ticker, valuation, analysis, sentiment);
            var riskAssessment = await AssessRiskAsync(stockData);

            // Format as clean, readable text
            var result = $@"
📊 FUNDAMENTAL ANALYSIS: {ticker.ToUpper()}
═══════════════════════════════════════════════════════════

💰 CURRENT VALUATION
Current Price: ${stockData.CurrentPrice:F2}
Market Cap: ${stockData.MarketCap:N0}

📈 VALUATION METRICS
• P/E Ratio: {valuation.PERatio:F1}
• PEG Ratio: {valuation.PEGRatio:F1}
• Price to Book: {valuation.PriceToBook:F1}
• Price to Sales: {valuation.PriceToSales:F1}
• Debt to Equity: {valuation.DebtToEquity:F1}
• Return on Equity: {valuation.ReturnOnEquity:P1}
• Dividend Yield: {valuation.DividendYield:P2}
• Free Cash Flow Yield: {valuation.FreeCashFlowYield:P1}

🔍 TECHNICAL ANALYSIS
• SMA 50: ${analysis.SMA50:F2}
• SMA 200: ${analysis.SMA200:F2}
• RSI: {analysis.RSI:F1}
• Volatility: {analysis.Volatility:P1}
• Trend: {analysis.Trend}

📰 MARKET SENTIMENT
{sentiment}

💡 INVESTMENT RECOMMENDATION
{recommendation}

⚠️ RISK ASSESSMENT
{riskAssessment}

Timestamp: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC
";

            return result;
        }
        catch (Exception ex)
        {
            return $"Error analyzing stock {ticker}: {ex.Message}";
        }
    }

    [KernelFunction]
    [Description("Compares multiple stocks for relative valuation and investment opportunities")]
    public async Task<string> CompareStocksAsync(
        [Description("Comma-separated list of stock tickers")] string tickers,
        [Description("Comparison criteria (valuation, growth, risk, momentum)")] string criteria = "valuation")
    {
        var tickerList = tickers.Split(',').Select(t => t.Trim().ToUpper()).ToList();
        var comparisons = new List<object>();

        foreach (var ticker in tickerList)
        {
            var analysis = await AnalyzeStockAsync(ticker, 365);
            var stockData = JsonSerializer.Deserialize<JsonElement>(analysis);
            comparisons.Add(new
            {
                Ticker = ticker,
                Score = await CalculateComparisonScoreAsync(stockData, criteria),
                Data = stockData
            });
        }

        var rankedStocks = comparisons.OrderByDescending(c => 
            ((JsonElement)c.GetType().GetProperty("Score")!.GetValue(c)!).GetDouble()).ToList();

        var recommendation = await _kernel.InvokePromptAsync($@"
            Based on this stock comparison data: {JsonSerializer.Serialize(rankedStocks, new JsonSerializerOptions { WriteIndented = true })}
            
            Provide a comprehensive investment recommendation including:
            1. Top 3 picks with reasoning
            2. Risk-adjusted portfolio allocation
            3. Key catalysts to watch
            4. Market timing considerations
            
            Format as a professional investment research note.
        ");

        return recommendation.ToString();
    }

    private Task<StockData> FetchStockDataAsync(string ticker, int periodDays)
    {
        // Implementation for fetching comprehensive stock data
        // This would integrate with financial data APIs (Alpha Vantage, Yahoo Finance, etc.)
        
        var request = new RestRequest($"https://api.example.com/stock/{ticker}/data");
        request.AddParameter("period", periodDays);
        
        // Mock implementation with sample historical prices - replace with actual API calls
        var random = new Random();
        var basePrice = 150.0m;
        var historicalPrices = new List<decimal>();
        
        // Generate mock historical prices for the past 200 days
        for (int i = 0; i < Math.Min(periodDays, 200); i++)
        {
            var change = (decimal)(random.NextDouble() - 0.5) * 2; // -1 to +1
            basePrice += change;
            if (basePrice < 1) basePrice = 1; // Ensure price doesn't go negative
            historicalPrices.Add(basePrice);
        }
        
        var stockData = new StockData
        {
            Ticker = ticker,
            CurrentPrice = basePrice,
            HistoricalPrices = historicalPrices,
            Volume = 1000000,
            MarketCap = 1000000000,
            PERatio = 25.0m,
            DividendYield = 0.02m
        };
        
        return Task.FromResult(stockData);
    }

    private Task<TechnicalAnalysis> PerformTechnicalAnalysisAsync(StockData stockData)
    {
        // Calculate technical indicators
        var analysis = new TechnicalAnalysis
        {
            SMA50 = CalculateSMA(stockData.HistoricalPrices, 50),
            SMA200 = CalculateSMA(stockData.HistoricalPrices, 200),
            RSI = CalculateRSI(stockData.HistoricalPrices, 14),
            MACD = CalculateMACD(stockData.HistoricalPrices),
            BollingerBands = CalculateBollingerBands(stockData.HistoricalPrices, 20),
            Volatility = CalculateVolatility(stockData.HistoricalPrices),
            Trend = DetermineTrend(stockData.HistoricalPrices)
        };
        
        return Task.FromResult(analysis);
    }

    private async Task<ValuationMetrics> CalculateValuationMetricsAsync(string ticker, StockData stockData)
    {
        return new ValuationMetrics
        {
            PERatio = stockData.PERatio,
            PEGRatio = await CalculatePEGRatioAsync(ticker),
            PriceToBook = await CalculatePriceToBookAsync(ticker),
            PriceToSales = await CalculatePriceToSalesAsync(ticker),
            DebtToEquity = await CalculateDebtToEquityAsync(ticker),
            ReturnOnEquity = await CalculateROEAsync(ticker),
            DividendYield = stockData.DividendYield,
            FreeCashFlowYield = await CalculateFCFYieldAsync(ticker)
        };
    }

    private async Task<string> GetMarketSentimentAsync(string ticker)
    {
        var prompt = $@"
            Analyze current market sentiment for {ticker} based on recent news, analyst ratings, and market trends.
            Consider both fundamental and technical factors.
            Provide a sentiment score from 1-10 and brief explanation in plain text format.
            Keep the response concise and avoid markdown formatting.
        ";

        var sentiment = await _kernel.InvokePromptAsync(prompt);
        return sentiment.ToString();
    }

    private async Task<string> GenerateRecommendationAsync(string ticker, ValuationMetrics valuation, 
        TechnicalAnalysis technical, string sentiment)
    {
        var prompt = $@"
            Generate a concise investment recommendation for {ticker} based on:
            
            Valuation Metrics:
            - P/E Ratio: {valuation.PERatio:F1}
            - PEG Ratio: {valuation.PEGRatio:F1}
            - Price to Book: {valuation.PriceToBook:F1}
            - ROE: {valuation.ReturnOnEquity:P1}
            
            Technical Analysis:
            - RSI: {technical.RSI:F1}
            - Trend: {technical.Trend}
            - Volatility: {technical.Volatility:P1}
            
            Market Sentiment: {sentiment}
            
            Provide a clear BUY/HOLD/SELL recommendation with brief reasoning and price targets.
            Use plain text format without markdown or special formatting.
        ";

        var recommendation = await _kernel.InvokePromptAsync(prompt);
        return recommendation.ToString();
    }

    private Task<double> CalculateComparisonScoreAsync(JsonElement stockData, string criteria)
    {
        // Implementation for scoring stocks based on criteria
        var score = criteria.ToLower() switch
        {
            "valuation" => CalculateValuationScore(stockData),
            "growth" => CalculateGrowthScore(stockData),
            "risk" => CalculateRiskScore(stockData),
            "momentum" => CalculateMomentumScore(stockData),
            _ => CalculateCompositeScore(stockData)
        };
        
        return Task.FromResult(score);
    }

    private double CalculateValuationScore(JsonElement stockData) => Random.Shared.NextDouble() * 10;
    private double CalculateGrowthScore(JsonElement stockData) => Random.Shared.NextDouble() * 10;
    private double CalculateRiskScore(JsonElement stockData) => Random.Shared.NextDouble() * 10;
    private double CalculateMomentumScore(JsonElement stockData) => Random.Shared.NextDouble() * 10;
    private double CalculateCompositeScore(JsonElement stockData) => Random.Shared.NextDouble() * 10;

    // Technical indicator calculations
    private decimal CalculateSMA(List<decimal> prices, int period) => 
        prices.Any() ? prices.TakeLast(Math.Min(period, prices.Count)).Average() : 0;

    private decimal CalculateRSI(List<decimal> prices, int period) => 50.0m; // Placeholder
    private object CalculateMACD(List<decimal> prices) => new { MACD = 0, Signal = 0, Histogram = 0 };
    private object CalculateBollingerBands(List<decimal> prices, int period) => 
        new { Upper = 155m, Lower = 145m, Middle = 150m };
    private decimal CalculateVolatility(List<decimal> prices) => 0.25m; // 25% annualized
    private string DetermineTrend(List<decimal> prices) => "Bullish";

    // Valuation calculations
    private Task<decimal> CalculatePEGRatioAsync(string ticker) => Task.FromResult(1.5m);
    private Task<decimal> CalculatePriceToBookAsync(string ticker) => Task.FromResult(3.0m);
    private Task<decimal> CalculatePriceToSalesAsync(string ticker) => Task.FromResult(5.0m);
    private Task<decimal> CalculateDebtToEquityAsync(string ticker) => Task.FromResult(0.3m);
    private Task<decimal> CalculateROEAsync(string ticker) => Task.FromResult(0.15m);
    private Task<decimal> CalculateFCFYieldAsync(string ticker) => Task.FromResult(0.05m);
    private Task<string> AssessRiskAsync(StockData stockData) 
    {
        var riskLevel = stockData.PERatio > 30 ? "High" : 
                       stockData.PERatio > 20 ? "Moderate" : "Low";
        
        var riskFactors = new List<string>();
        
        if (stockData.PERatio > 25) riskFactors.Add("High P/E ratio indicates growth expectations");
        if (stockData.DividendYield < 0.01m) riskFactors.Add("No dividend provides no income cushion");
        if (stockData.HistoricalPrices.Count > 0)
        {
            var volatility = CalculateVolatility(stockData.HistoricalPrices);
            if (volatility > 0.3m) riskFactors.Add("High volatility increases price risk");
        }
        
        var riskAssessment = $"{riskLevel} Risk";
        if (riskFactors.Any())
        {
            riskAssessment += $"\nKey Risk Factors: {string.Join(", ", riskFactors)}";
        }
        
        return Task.FromResult(riskAssessment);
    }

    public class StockData
    {
        public string Ticker { get; set; } = string.Empty;
        public decimal CurrentPrice { get; set; }
        public List<decimal> HistoricalPrices { get; set; } = new();
        public long Volume { get; set; }
        public decimal MarketCap { get; set; }
        public decimal PERatio { get; set; }
        public decimal DividendYield { get; set; }
    }

    public class TechnicalAnalysis
    {
        public decimal SMA50 { get; set; }
        public decimal SMA200 { get; set; }
        public decimal RSI { get; set; }
        public object MACD { get; set; } = new();
        public object BollingerBands { get; set; } = new();
        public decimal Volatility { get; set; }
        public string Trend { get; set; } = string.Empty;
    }

    public class ValuationMetrics
    {
        public decimal PERatio { get; set; }
        public decimal PEGRatio { get; set; }
        public decimal PriceToBook { get; set; }
        public decimal PriceToSales { get; set; }
        public decimal DebtToEquity { get; set; }
        public decimal ReturnOnEquity { get; set; }
        public decimal DividendYield { get; set; }
        public decimal FreeCashFlowYield { get; set; }
    }
}
