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

            var comprehensiveAnalysis = new
            {
                Ticker = ticker,
                CurrentPrice = stockData.CurrentPrice,
                ValuationMetrics = valuation,
                TechnicalAnalysis = analysis,
                MarketSentiment = sentiment,
                Recommendation = await GenerateRecommendationAsync(ticker, valuation, analysis, sentiment),
                RiskAssessment = await AssessRiskAsync(stockData),
                Timestamp = DateTime.UtcNow
            };

            return JsonSerializer.Serialize(comprehensiveAnalysis, new JsonSerializerOptions { WriteIndented = true });
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
        
        // Mock implementation - replace with actual API calls
        var stockData = new StockData
        {
            Ticker = ticker,
            CurrentPrice = 150.0m,
            HistoricalPrices = new List<decimal>(),
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
            Provide a sentiment score from 1-10 and brief explanation.
        ";

        var sentiment = await _kernel.InvokePromptAsync(prompt);
        return sentiment.ToString();
    }

    private async Task<string> GenerateRecommendationAsync(string ticker, ValuationMetrics valuation, 
        TechnicalAnalysis technical, string sentiment)
    {
        var prompt = $@"
            Generate an investment recommendation for {ticker} based on:
            
            Valuation: {JsonSerializer.Serialize(valuation)}
            Technical: {JsonSerializer.Serialize(technical)}
            Sentiment: {sentiment}
            
            Provide: BUY/HOLD/SELL recommendation with price targets and risk assessment.
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
        prices.TakeLast(period).Average();

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
    private Task<string> AssessRiskAsync(StockData stockData) => Task.FromResult("Moderate Risk");

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
