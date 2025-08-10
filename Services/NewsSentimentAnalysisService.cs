using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using QuantResearchAgent.Services;
using System.Text;
using System.Text.Json;

namespace QuantResearchAgent.Services
{
    /// <summary>
    /// Advanced news sentiment analysis service that analyzes financial news 
    /// from multiple sources and provides comprehensive sentiment insights
    /// </summary>
    public class NewsSentimentAnalysisService
    {
        private readonly ILogger<NewsSentimentAnalysisService> _logger;
        private readonly Kernel _kernel;
        private readonly YFinanceNewsService _yfinanceNewsService;
        private readonly FinvizNewsService _finvizNewsService;
        private readonly HttpClient _httpClient;

        public NewsSentimentAnalysisService(
            ILogger<NewsSentimentAnalysisService> logger,
            Kernel kernel,
            YFinanceNewsService yfinanceNewsService,
            FinvizNewsService finvizNewsService,
            HttpClient httpClient)
        {
            _logger = logger;
            _kernel = kernel;
            _yfinanceNewsService = yfinanceNewsService;
            _finvizNewsService = finvizNewsService;
            _httpClient = httpClient;
        }

        /// <summary>
        /// Analyze sentiment for a specific symbol using multi-source news
        /// </summary>
        public async Task<SymbolSentimentAnalysis> AnalyzeSymbolSentimentAsync(string symbol, int newsLimit = 10)
        {
            try
            {
                _logger.LogInformation($"Starting sentiment analysis for {symbol}");

                // Gather news from multiple sources
                var yahooNewsTask = _yfinanceNewsService.GetNewsAsync(symbol, newsLimit);
                var finvizNewsTask = _finvizNewsService.GetNewsAsync(symbol, newsLimit);

                await Task.WhenAll(yahooNewsTask, finvizNewsTask);

                var yahooNews = yahooNewsTask.Result;
                var finvizNews = finvizNewsTask.Result;

                // Combine and analyze all news
                var allNews = new List<NewsItem>();
                
                // Convert Yahoo Finance news
                allNews.AddRange(yahooNews.Select(yn => new NewsItem
                {
                    Title = yn.Title,
                    Summary = yn.Summary,
                    Publisher = yn.Publisher,
                    PublishedDate = yn.PublishedDate,
                    Link = yn.Link,
                    Source = "Yahoo Finance"
                }));

                // Convert Finviz news
                allNews.AddRange(finvizNews.Select(fn => new NewsItem
                {
                    Title = fn.Title,
                    Summary = fn.Summary,
                    Publisher = fn.Publisher,
                    PublishedDate = fn.PublishedDate,
                    Link = fn.Link,
                    Source = "Finviz"
                }));

                // Remove duplicates and sort by date
                var uniqueNews = allNews
                    .GroupBy(n => n.Title.ToLower().Trim())
                    .Select(g => g.OrderByDescending(n => n.PublishedDate).First())
                    .OrderByDescending(n => n.PublishedDate)
                    .Take(newsLimit)
                    .ToList();

                // Analyze sentiment for each news item
                var sentimentTasks = uniqueNews.Select(AnalyzeNewsItemSentimentAsync).ToList();
                var sentimentResults = await Task.WhenAll(sentimentTasks);

                // Combine results
                for (int i = 0; i < uniqueNews.Count; i++)
                {
                    uniqueNews[i].SentimentScore = sentimentResults[i].Score;
                    uniqueNews[i].SentimentLabel = sentimentResults[i].Label;
                    uniqueNews[i].KeyTopics = sentimentResults[i].KeyTopics;
                    uniqueNews[i].Impact = sentimentResults[i].Impact;
                }

                // Generate overall analysis
                var overallAnalysis = await GenerateOverallSentimentAnalysisAsync(symbol, uniqueNews);

                return new SymbolSentimentAnalysis
                {
                    Symbol = symbol,
                    AnalysisDate = DateTime.Now,
                    NewsItems = uniqueNews,
                    OverallSentiment = overallAnalysis.OverallSentiment,
                    SentimentScore = overallAnalysis.SentimentScore,
                    Confidence = overallAnalysis.Confidence,
                    TrendDirection = overallAnalysis.TrendDirection,
                    KeyThemes = overallAnalysis.KeyThemes,
                    TradingSignal = overallAnalysis.TradingSignal,
                    RiskFactors = overallAnalysis.RiskFactors,
                    Summary = overallAnalysis.Summary,
                    VolatilityIndicator = overallAnalysis.VolatilityIndicator,
                    PriceTargetBias = overallAnalysis.PriceTargetBias,
                    InstitutionalSentiment = overallAnalysis.InstitutionalSentiment,
                    RetailSentiment = overallAnalysis.RetailSentiment,
                    AnalystConsensus = overallAnalysis.AnalystConsensus,
                    EarningsImpact = overallAnalysis.EarningsImpact,
                    SectorComparison = overallAnalysis.SectorComparison,
                    MomentumSignal = overallAnalysis.MomentumSignal
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error analyzing sentiment for {symbol}");
                throw;
            }
        }

        /// <summary>
        /// Analyze market-wide sentiment using general financial news
        /// </summary>
        public async Task<MarketSentimentAnalysis> AnalyzeMarketSentimentAsync(int newsLimit = 20)
        {
            try
            {
                _logger.LogInformation("Starting market-wide sentiment analysis");

                // Get market news from both sources
                var yahooNewsTask = _yfinanceNewsService.GetMarketNewsAsync(newsLimit);
                var finvizNewsTask = _finvizNewsService.GetMarketNewsAsync(newsLimit);

                await Task.WhenAll(yahooNewsTask, finvizNewsTask);

                var yahooNews = yahooNewsTask.Result;
                var finvizNews = finvizNewsTask.Result;

                // Combine all news
                var allNews = new List<NewsItem>();
                
                allNews.AddRange(yahooNews.Select(yn => new NewsItem
                {
                    Title = yn.Title,
                    Summary = yn.Summary,
                    Publisher = yn.Publisher,
                    PublishedDate = yn.PublishedDate,
                    Link = yn.Link,
                    Source = "Yahoo Finance"
                }));

                allNews.AddRange(finvizNews.Select(fn => new NewsItem
                {
                    Title = fn.Title,
                    Summary = fn.Summary,
                    Publisher = fn.Publisher,
                    PublishedDate = fn.PublishedDate,
                    Link = fn.Link,
                    Source = "Finviz"
                }));

                // Remove duplicates and analyze sentiment
                var uniqueNews = allNews
                    .GroupBy(n => n.Title.ToLower().Trim())
                    .Select(g => g.First())
                    .OrderByDescending(n => n.PublishedDate)
                    .Take(newsLimit)
                    .ToList();

                // Analyze sentiment for each news item
                var sentimentTasks = uniqueNews.Select(AnalyzeNewsItemSentimentAsync).ToList();
                var sentimentResults = await Task.WhenAll(sentimentTasks);

                for (int i = 0; i < uniqueNews.Count; i++)
                {
                    uniqueNews[i].SentimentScore = sentimentResults[i].Score;
                    uniqueNews[i].SentimentLabel = sentimentResults[i].Label;
                    uniqueNews[i].KeyTopics = sentimentResults[i].KeyTopics;
                    uniqueNews[i].Impact = sentimentResults[i].Impact;
                }

                // Generate market analysis
                var marketAnalysis = await GenerateMarketSentimentAnalysisAsync(uniqueNews);

                return new MarketSentimentAnalysis
                {
                    AnalysisDate = DateTime.Now,
                    NewsItems = uniqueNews,
                    OverallSentiment = marketAnalysis.OverallSentiment,
                    SentimentScore = marketAnalysis.SentimentScore,
                    Confidence = marketAnalysis.Confidence,
                    MarketMood = marketAnalysis.MarketMood,
                    SectorSentiments = marketAnalysis.SectorSentiments,
                    KeyThemes = marketAnalysis.KeyThemes,
                    RiskLevel = marketAnalysis.RiskLevel,
                    Summary = marketAnalysis.Summary
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing market sentiment");
                throw;
            }
        }

        private async Task<NewsItemSentiment> AnalyzeNewsItemSentimentAsync(NewsItem newsItem)
        {
            try
            {
                var prompt = $@"
Analyze the financial sentiment of this news article:

Title: {newsItem.Title}
Summary: {newsItem.Summary}
Publisher: {newsItem.Publisher}
Date: {newsItem.PublishedDate:yyyy-MM-dd}

Provide analysis in this exact JSON format:
{{
    ""score"": <number between -1.0 and 1.0>,
    ""label"": ""<Very Negative|Negative|Neutral|Positive|Very Positive>"",
    ""keyTopics"": [""topic1"", ""topic2"", ""topic3""],
    ""impact"": ""<Low|Medium|High>"",
    ""reasoning"": ""<brief explanation>""
}}

Consider:
- Financial keywords (earnings, revenue, growth, losses, etc.)
- Market impact words (surge, plummet, rise, fall, etc.)
- Forward-looking statements
- Analyst opinions and price targets
- Company performance indicators
";

                var response = await _kernel.InvokePromptAsync(prompt);
                var jsonResponse = response.ToString();

                // Clean and parse JSON
                var cleanJson = ExtractJsonFromResponse(jsonResponse);
                var sentimentData = JsonSerializer.Deserialize<JsonElement>(cleanJson);

                return new NewsItemSentiment
                {
                    Score = sentimentData.GetProperty("score").GetDouble(),
                    Label = sentimentData.GetProperty("label").GetString() ?? "Neutral",
                    KeyTopics = sentimentData.GetProperty("keyTopics").EnumerateArray()
                        .Select(t => t.GetString() ?? "").Where(t => !string.IsNullOrEmpty(t)).ToList(),
                    Impact = sentimentData.GetProperty("impact").GetString() ?? "Medium"
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Error analyzing sentiment for news item: {newsItem.Title}");
                return new NewsItemSentiment
                {
                    Score = 0.0,
                    Label = "Neutral",
                    KeyTopics = new List<string>(),
                    Impact = "Medium"
                };
            }
        }

        private async Task<OverallSentimentResult> GenerateOverallSentimentAnalysisAsync(string symbol, List<NewsItem> newsItems)
        {
            var prompt = $@"
As a quantitative financial analyst, provide comprehensive sentiment analysis for {symbol} based on these news articles:

{string.Join("\n\n", newsItems.Take(10).Select(n => $"Title: {n.Title}\nSentiment: {n.SentimentLabel} ({n.SentimentScore:F2})\nTopics: {string.Join(", ", n.KeyTopics)}\nImpact: {n.Impact}"))}

Provide QUANTITATIVE analysis in this JSON format:
{{
    ""overallSentiment"": ""<Very Negative|Negative|Neutral|Positive|Very Positive>"",
    ""sentimentScore"": <-1.0 to 1.0>,
    ""confidence"": <0.0 to 1.0>,
    ""trendDirection"": ""<Improving|Stable|Declining>"",
    ""keyThemes"": [""theme1"", ""theme2"", ""theme3""],
    ""tradingSignal"": ""<Strong Buy|Buy|Hold|Sell|Strong Sell>"",
    ""riskFactors"": [""risk1"", ""risk2""],
    ""summary"": ""<detailed quantitative summary>"",
    ""volatilityIndicator"": ""<Low|Medium|High>"",
    ""priceTargetBias"": ""<Bullish|Neutral|Bearish>"",
    ""institutionalSentiment"": ""<Positive|Neutral|Negative>"",
    ""retailSentiment"": ""<Positive|Neutral|Negative>"",
    ""analystConsensus"": ""<Buy|Hold|Sell>"",
    ""earningsImpact"": ""<Positive|Neutral|Negative>"",
    ""sectorComparison"": ""<Outperform|Inline|Underperform>"",
    ""momentumSignal"": ""<Strong Positive|Positive|Neutral|Negative|Strong Negative>""
}}

Focus on:
- Quantitative metrics and financial ratios mentioned
- Analyst price targets and revisions
- Earnings estimates and guidance changes
- Institutional activity (insider trading, institutional buying/selling)
- Technical indicators mentioned in news
- Sector rotation and relative performance
- Options flow and volatility expectations
- Macroeconomic factors affecting the stock
- Revenue/profit margin trends
- Market share and competitive positioning";

            var response = await _kernel.InvokePromptAsync(prompt);
            var jsonResponse = ExtractJsonFromResponse(response.ToString());
            var data = JsonSerializer.Deserialize<JsonElement>(jsonResponse);

            return new OverallSentimentResult
            {
                OverallSentiment = data.GetProperty("overallSentiment").GetString() ?? "Neutral",
                SentimentScore = data.GetProperty("sentimentScore").GetDouble(),
                Confidence = data.GetProperty("confidence").GetDouble(),
                TrendDirection = data.GetProperty("trendDirection").GetString() ?? "Stable",
                KeyThemes = data.GetProperty("keyThemes").EnumerateArray()
                    .Select(t => t.GetString() ?? "").Where(t => !string.IsNullOrEmpty(t)).ToList(),
                TradingSignal = data.GetProperty("tradingSignal").GetString() ?? "Hold",
                RiskFactors = data.GetProperty("riskFactors").EnumerateArray()
                    .Select(r => r.GetString() ?? "").Where(r => !string.IsNullOrEmpty(r)).ToList(),
                Summary = data.GetProperty("summary").GetString() ?? "",
                VolatilityIndicator = data.TryGetProperty("volatilityIndicator", out var vol) ? vol.GetString() ?? "Medium" : "Medium",
                PriceTargetBias = data.TryGetProperty("priceTargetBias", out var pt) ? pt.GetString() ?? "Neutral" : "Neutral",
                InstitutionalSentiment = data.TryGetProperty("institutionalSentiment", out var inst) ? inst.GetString() ?? "Neutral" : "Neutral",
                RetailSentiment = data.TryGetProperty("retailSentiment", out var retail) ? retail.GetString() ?? "Neutral" : "Neutral",
                AnalystConsensus = data.TryGetProperty("analystConsensus", out var analyst) ? analyst.GetString() ?? "Hold" : "Hold",
                EarningsImpact = data.TryGetProperty("earningsImpact", out var earnings) ? earnings.GetString() ?? "Neutral" : "Neutral",
                SectorComparison = data.TryGetProperty("sectorComparison", out var sector) ? sector.GetString() ?? "Inline" : "Inline",
                MomentumSignal = data.TryGetProperty("momentumSignal", out var momentum) ? momentum.GetString() ?? "Neutral" : "Neutral"
            };
        }

        private async Task<MarketSentimentResult> GenerateMarketSentimentAnalysisAsync(List<NewsItem> newsItems)
        {
            var prompt = $@"
Analyze overall market sentiment based on these financial news articles:

{string.Join("\n\n", newsItems.Take(15).Select(n => $"Title: {n.Title}\nSentiment: {n.SentimentLabel} ({n.SentimentScore:F2})"))}

Provide analysis in this JSON format:
{{
    ""overallSentiment"": ""<Very Negative|Negative|Neutral|Positive|Very Positive>"",
    ""sentimentScore"": <-1.0 to 1.0>,
    ""confidence"": <0.0 to 1.0>,
    ""marketMood"": ""<Fear|Caution|Neutral|Optimism|Euphoria>"",
    ""sectorSentiments"": {{
        ""technology"": <-1.0 to 1.0>,
        ""finance"": <-1.0 to 1.0>,
        ""healthcare"": <-1.0 to 1.0>
    }},
    ""keyThemes"": [""theme1"", ""theme2"", ""theme3""],
    ""riskLevel"": ""<Low|Medium|High>"",
    ""summary"": ""<2-3 sentence market summary>""
}}";

            var response = await _kernel.InvokePromptAsync(prompt);
            var jsonResponse = ExtractJsonFromResponse(response.ToString());
            var data = JsonSerializer.Deserialize<JsonElement>(jsonResponse);

            var sectorSentiments = new Dictionary<string, double>();
            if (data.TryGetProperty("sectorSentiments", out var sectors))
            {
                foreach (var sector in sectors.EnumerateObject())
                {
                    sectorSentiments[sector.Name] = sector.Value.GetDouble();
                }
            }

            return new MarketSentimentResult
            {
                OverallSentiment = data.GetProperty("overallSentiment").GetString() ?? "Neutral",
                SentimentScore = data.GetProperty("sentimentScore").GetDouble(),
                Confidence = data.GetProperty("confidence").GetDouble(),
                MarketMood = data.GetProperty("marketMood").GetString() ?? "Neutral",
                SectorSentiments = sectorSentiments,
                KeyThemes = data.GetProperty("keyThemes").EnumerateArray()
                    .Select(t => t.GetString() ?? "").Where(t => !string.IsNullOrEmpty(t)).ToList(),
                RiskLevel = data.GetProperty("riskLevel").GetString() ?? "Medium",
                Summary = data.GetProperty("summary").GetString() ?? ""
            };
        }

        private string ExtractJsonFromResponse(string response)
        {
            // Extract JSON from AI response, handling markdown code blocks
            var lines = response.Split('\n');
            var jsonLines = new List<string>();
            bool inJson = false;

            foreach (var line in lines)
            {
                if (line.Trim().StartsWith("{") && !inJson)
                {
                    inJson = true;
                    jsonLines.Add(line);
                }
                else if (inJson)
                {
                    jsonLines.Add(line);
                    if (line.Trim().EndsWith("}") && CountBraces(string.Join("\n", jsonLines)) == 0)
                    {
                        break;
                    }
                }
            }

            return string.Join("\n", jsonLines);
        }

        private int CountBraces(string json)
        {
            int count = 0;
            foreach (char c in json)
            {
                if (c == '{') count++;
                else if (c == '}') count--;
            }
            return count;
        }
    }

    // Data models for sentiment analysis
    public class SymbolSentimentAnalysis
    {
        public string Symbol { get; set; } = string.Empty;
        public DateTime AnalysisDate { get; set; }
        public List<NewsItem> NewsItems { get; set; } = new();
        public string OverallSentiment { get; set; } = string.Empty;
        public double SentimentScore { get; set; }
        public double Confidence { get; set; }
        public string TrendDirection { get; set; } = string.Empty;
        public List<string> KeyThemes { get; set; } = new();
        public string TradingSignal { get; set; } = string.Empty;
        public List<string> RiskFactors { get; set; } = new();
        public string Summary { get; set; } = string.Empty;
        public string VolatilityIndicator { get; set; } = string.Empty;
        public string PriceTargetBias { get; set; } = string.Empty;
        public string InstitutionalSentiment { get; set; } = string.Empty;
        public string RetailSentiment { get; set; } = string.Empty;
        public string AnalystConsensus { get; set; } = string.Empty;
        public string EarningsImpact { get; set; } = string.Empty;
        public string SectorComparison { get; set; } = string.Empty;
        public string MomentumSignal { get; set; } = string.Empty;
    }

    public class MarketSentimentAnalysis
    {
        public DateTime AnalysisDate { get; set; }
        public List<NewsItem> NewsItems { get; set; } = new();
        public string OverallSentiment { get; set; } = string.Empty;
        public double SentimentScore { get; set; }
        public double Confidence { get; set; }
        public string MarketMood { get; set; } = string.Empty;
        public Dictionary<string, double> SectorSentiments { get; set; } = new();
        public List<string> KeyThemes { get; set; } = new();
        public string RiskLevel { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
    }

    public class NewsItem
    {
        public string Title { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public string Publisher { get; set; } = string.Empty;
        public DateTime PublishedDate { get; set; }
        public string Link { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public double SentimentScore { get; set; }
        public string SentimentLabel { get; set; } = string.Empty;
        public List<string> KeyTopics { get; set; } = new();
        public string Impact { get; set; } = string.Empty;
    }

    public class NewsItemSentiment
    {
        public double Score { get; set; }
        public string Label { get; set; } = string.Empty;
        public List<string> KeyTopics { get; set; } = new();
        public string Impact { get; set; } = string.Empty;
    }

    public class OverallSentimentResult
    {
        public string OverallSentiment { get; set; } = string.Empty;
        public double SentimentScore { get; set; }
        public double Confidence { get; set; }
        public string TrendDirection { get; set; } = string.Empty;
        public List<string> KeyThemes { get; set; } = new();
        public string TradingSignal { get; set; } = string.Empty;
        public List<string> RiskFactors { get; set; } = new();
        public string Summary { get; set; } = string.Empty;
        public string VolatilityIndicator { get; set; } = string.Empty;
        public string PriceTargetBias { get; set; } = string.Empty;
        public string InstitutionalSentiment { get; set; } = string.Empty;
        public string RetailSentiment { get; set; } = string.Empty;
        public string AnalystConsensus { get; set; } = string.Empty;
        public string EarningsImpact { get; set; } = string.Empty;
        public string SectorComparison { get; set; } = string.Empty;
        public string MomentumSignal { get; set; } = string.Empty;
    }

    public class MarketSentimentResult
    {
        public string OverallSentiment { get; set; } = string.Empty;
        public double SentimentScore { get; set; }
        public double Confidence { get; set; }
        public string MarketMood { get; set; } = string.Empty;
        public Dictionary<string, double> SectorSentiments { get; set; } = new();
        public List<string> KeyThemes { get; set; } = new();
        public string RiskLevel { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
    }
}
