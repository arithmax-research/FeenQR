using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using QuantResearchAgent.Plugins;
using QuantResearchAgent.Services;
using System.Text;
using System.Text.Json;

namespace QuantResearchAgent.Services.ResearchAgents
{
    /// <summary>
    /// Comprehensive stock analysis agent that combines web search, sentiment analysis, 
    /// fundamental analysis, and technical analysis to provide investment recommendations
    /// </summary>
    public class ComprehensiveStockAnalysisAgent
    {
        private readonly Kernel _kernel;
        private readonly ILogger<ComprehensiveStockAnalysisAgent> _logger;
        private readonly IWebSearchPlugin _webSearchPlugin;
        private readonly IFinancialDataPlugin _financialDataPlugin;
        private readonly TechnicalAnalysisService _technicalAnalysisService;
        private readonly CompanyValuationService _companyValuationService;
        private readonly MarketSentimentAgentService _sentimentService;
        private readonly AlpacaService _alpacaService;
        private readonly YahooFinanceService _yahooFinanceService;

        public ComprehensiveStockAnalysisAgent(
            Kernel kernel,
            ILogger<ComprehensiveStockAnalysisAgent> logger,
            IWebSearchPlugin webSearchPlugin,
            IFinancialDataPlugin financialDataPlugin,
            TechnicalAnalysisService technicalAnalysisService,
            CompanyValuationService companyValuationService,
            MarketSentimentAgentService sentimentService,
            AlpacaService alpacaService,
            YahooFinanceService yahooFinanceService)
        {
            _kernel = kernel;
            _logger = logger;
            _webSearchPlugin = webSearchPlugin;
            _financialDataPlugin = financialDataPlugin;
            _technicalAnalysisService = technicalAnalysisService;
            _companyValuationService = companyValuationService;
            _sentimentService = sentimentService;
            _alpacaService = alpacaService;
            _yahooFinanceService = yahooFinanceService;
        }

        /// <summary>
        /// Performs comprehensive analysis and provides investment recommendation
        /// </summary>
        public async Task<string> AnalyzeAndRecommendAsync(string symbol, string assetType = "stock")
        {
            var analysisBuilder = new StringBuilder();
            var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC");

            analysisBuilder.AppendLine(new string('=', 80));
            analysisBuilder.AppendLine($"COMPREHENSIVE INVESTMENT ANALYSIS: {symbol.ToUpper()}");
            analysisBuilder.AppendLine($"Asset Type: {assetType.ToUpper()}");
            analysisBuilder.AppendLine($"Analysis Date: {timestamp}");
            analysisBuilder.AppendLine(new string('=', 80));

            try
            {
                // 1. Current Market Data
                var marketData = await GetCurrentMarketDataAsync(symbol);
                analysisBuilder.AppendLine("\nANALYSIS: CURRENT MARKET DATA");
                analysisBuilder.AppendLine(new string('-', 40));
                analysisBuilder.AppendLine(marketData);

                // 2. Web Search for Recent News and Sentiment
                var newsAnalysis = await PerformWebSearchAnalysisAsync(symbol, assetType);
                analysisBuilder.AppendLine("\nNEWS & MARKET SENTIMENT");
                analysisBuilder.AppendLine(new string('-', 40));
                analysisBuilder.AppendLine(newsAnalysis);

                // 3. Technical Analysis
                var technicalAnalysis = await PerformTechnicalAnalysisAsync(symbol);
                analysisBuilder.AppendLine("\n TECHNICAL ANALYSIS");
                analysisBuilder.AppendLine(new string('-', 40));
                analysisBuilder.AppendLine(technicalAnalysis);

                // 4. Fundamental Analysis (for stocks)
                string fundamentalAnalysis = "";
                if (assetType.ToLower() == "stock")
                {
                    fundamentalAnalysis = await PerformFundamentalAnalysisAsync(symbol);
                    // Remove risk assessment section from fundamental analysis to avoid duplication
                    fundamentalAnalysis = RemoveRiskAssessmentSection(fundamentalAnalysis);
                    analysisBuilder.AppendLine("\nBUSINESS: FUNDAMENTAL ANALYSIS");
                    analysisBuilder.AppendLine(new string('-', 40));
                    analysisBuilder.AppendLine(fundamentalAnalysis);
                }

                // 5. Comprehensive Risk Assessment (combining technical and fundamental risks)
                var riskAssessment = await PerformComprehensiveRiskAssessmentAsync(symbol, assetType, fundamentalAnalysis);
                analysisBuilder.AppendLine("\nWARNING: COMPREHENSIVE RISK ASSESSMENT");
                analysisBuilder.AppendLine(new string('-', 40));
                analysisBuilder.AppendLine(riskAssessment);

                // 6. Generate AI-Powered Investment Recommendation
                var recommendation = await GenerateInvestmentRecommendationAsync(symbol, assetType, analysisBuilder.ToString());
                analysisBuilder.AppendLine("\nTARGET: INVESTMENT RECOMMENDATION");
                analysisBuilder.AppendLine(new string('-', 40));
                analysisBuilder.AppendLine(recommendation);

                analysisBuilder.AppendLine("\n" + new string('=', 80));
                analysisBuilder.AppendLine("END OF ANALYSIS");
                analysisBuilder.AppendLine(new string('=', 80));

                return analysisBuilder.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error performing comprehensive analysis for {symbol}");
                return $"Error performing analysis for {symbol}: {ex.Message}";
            }
        }

        private async Task<string> GetCurrentMarketDataAsync(string symbol)
        {
            try
            {
                var result = new StringBuilder();
                bool hasData = false;
                
                // Try to get current quote from Alpaca
                try
                {
                    var marketData = await _alpacaService.GetMarketDataAsync(symbol);
                    if (marketData != null)
                    {
                        result.AppendLine($"Current Price: ${marketData.Price:F2}");
                        result.AppendLine($"Daily Change: {marketData.Change24h:F2} ({marketData.ChangePercent24h:F2}%)");
                        result.AppendLine($"Volume: {marketData.Volume:N0}");
                        result.AppendLine($"High 24h: ${marketData.High24h:F2} | Low 24h: ${marketData.Low24h:F2}");
                        result.AppendLine($"Data Source: {marketData.Source}");
                        result.AppendLine($"Last Updated: {marketData.Timestamp:yyyy-MM-dd HH:mm:ss}");
                        hasData = true;
                    }
                }
                catch (Exception ex)
                {
                    result.AppendLine($"Alpaca market data retrieval failed: {ex.Message}");
                    _logger.LogWarning($"Alpaca API failed for {symbol}: {ex.Message}");
                }

                // Try to get multiple quotes from Alpaca as fallback
                if (!hasData)
                {
                    try
                    {
                        var multipleData = await _alpacaService.GetMultipleSymbolDataAsync(symbol);
                        if (multipleData?.Any() == true)
                        {
                            var data = multipleData.First();
                            result.AppendLine($"Fallback Data - Current Price: ${data.Price:F2}");
                            result.AppendLine($"Volume: {data.Volume:N0}");
                            result.AppendLine($"Data Source: {data.Source}");
                            hasData = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Alpaca multiple symbol API failed for {symbol}: {ex.Message}");
                    }
                }

                // Try Yahoo Finance as final fallback
                if (!hasData)
                {
                    try
                    {
                        var yahooData = await _yahooFinanceService.GetMarketDataAsync(symbol);
                        if (yahooData != null)
                        {
                            result.AppendLine($"Current Price: ${yahooData.CurrentPrice:F2}");
                            result.AppendLine($"Daily Change: ${yahooData.Change24h:F2} ({yahooData.ChangePercent24h:+0.00;-0.00;0.00}%)");
                            result.AppendLine($"Volume: {yahooData.Volume:N0}");
                            result.AppendLine($"High 24h: ${yahooData.High24h:F2} | Low 24h: ${yahooData.Low24h:F2}");
                            result.AppendLine($"Data Source: Yahoo Finance");
                            result.AppendLine($"Last Updated: {yahooData.LastUpdated:yyyy-MM-dd HH:mm:ss}");
                            hasData = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Yahoo Finance fallback failed for {symbol}: {ex.Message}");
                    }
                }

                // If still no data, provide general market context
                if (!hasData)
                {
                    result.AppendLine($"Market data for {symbol} is not available from configured sources.");
                    result.AppendLine("This could be due to:");
                    result.AppendLine("• Symbol may be delisted or inactive");
                    result.AppendLine("• API limitations in paper trading mode");
                    result.AppendLine("• Symbol format may need adjustment (e.g., try class shares like PLTR.A)");
                    result.AppendLine("• Market may be closed");
                    
                    // Add some general market context
                    result.AppendLine($"\nNote: Analysis will continue with available data for {symbol}");
                }

                // Try to get news from financial data plugin
                try
                {
                    var yahooNews = await _financialDataPlugin.GetNewsAsync(symbol, 3);
                    if (yahooNews?.Any() == true)
                    {
                        result.AppendLine("\nRecent Financial News:");
                        foreach (var news in yahooNews.Take(3))
                        {
                            result.AppendLine($"• {news.Headline} ({news.Source})");
                        }
                    }
                    else
                    {
                        result.AppendLine("\nNo recent financial news found for this symbol.");
                    }
                }
                catch (Exception ex)
                {
                    result.AppendLine($"\nFinancial news retrieval failed: {ex.Message}");
                    result.AppendLine("Note: This may be due to API server not running or configuration issues.");
                }

                return result.ToString();
            }
            catch (Exception ex)
            {
                return $"Unable to retrieve current market data: {ex.Message}";
            }
        }

        private async Task<string> PerformWebSearchAnalysisAsync(string symbol, string assetType)
        {
            try
            {
                var searchQueries = new[]
                {
                    $"{symbol} stock news today outlook",
                    $"{symbol} earnings report analysis",
                    $"{symbol} analyst recommendations 2025",
                    $"{symbol} price target forecast",
                    $"{symbol} company financial performance"
                };

                var allResults = new List<WebSearchResult>();
                var searchErrors = new List<string>();
                
                foreach (var query in searchQueries)
                {
                    try
                    {
                        var results = await _webSearchPlugin.SearchAsync(query, 2);
                        if (results?.Any() == true)
                        {
                            allResults.AddRange(results);
                        }
                    }
                    catch (Exception ex)
                    {
                        searchErrors.Add($"Query '{query}': {ex.Message}");
                        _logger.LogWarning($"Web search failed for query '{query}': {ex.Message}");
                    }
                }

                var result = new StringBuilder();

                if (!allResults.Any())
                {
                    result.AppendLine("No recent news or analysis found from web search.");
                    result.AppendLine("Possible issues:");
                    result.AppendLine("• Google Search API quota may be exceeded");
                    result.AppendLine("• Search engine ID may be misconfigured");
                    result.AppendLine("• Symbol may be too new or have limited coverage");
                    result.AppendLine("• Network connectivity issues");
                    
                    if (searchErrors.Any())
                    {
                        result.AppendLine("\nDetailed errors:");
                        foreach (var error in searchErrors.Take(3))
                        {
                            result.AppendLine($"• {error}");
                        }
                    }
                    
                    // Provide some manual guidance
                    result.AppendLine($"\nManual research suggestions for {symbol}:");
                    result.AppendLine($"• Check financial news sites: Yahoo Finance, Bloomberg, Reuters");
                    result.AppendLine($"• Visit company investor relations page");
                    result.AppendLine($"• Check SEC filings on EDGAR database");
                    result.AppendLine($"• Review analyst reports from major brokerages");
                }
                else
                {
                    result.AppendLine($"Found {allResults.Count} news articles and analysis:");
                    foreach (var item in allResults.Take(8))
                    {
                        result.AppendLine($"• {item.Title}");
                        if (!string.IsNullOrEmpty(item.Snippet))
                        {
                            result.AppendLine($"  Summary: {item.Snippet}");
                        }
                        result.AppendLine($"  Source: {item.Url}");
                        result.AppendLine();
                    }
                }

                // Always try sentiment analysis (it may have internal data sources)
                try
                {
                    var sentimentReport = await _sentimentService.AnalyzeMarketSentimentAsync("stock", symbol);
                    
                    if (!allResults.Any())
                    {
                        result.AppendLine("\nANALYSIS: General Market Sentiment Analysis:");
                        result.AppendLine("(Based on overall market conditions and general sentiment indicators)");
                        result.AppendLine("Note: This sentiment analysis uses broader market data since specific news wasn't found.");
                    }
                    else
                    {
                        result.AppendLine("\nANALYSIS: Market Sentiment Analysis:");
                        result.AppendLine("(Based on news analysis and market sentiment indicators)");
                    }
                    
                    result.AppendLine($"Overall Sentiment: {sentimentReport.OverallSentiment.Label}");
                    result.AppendLine($"Sentiment Score: {sentimentReport.OverallSentiment.Score:F2}");
                    result.AppendLine($"Confidence: {sentimentReport.OverallSentiment.Confidence:F2}");
                    result.AppendLine($"Market Direction: {sentimentReport.MarketDirection}");
                    
                    if (sentimentReport.TradingRecommendations?.Any() == true)
                    {
                        result.AppendLine($"Trading Recommendations: {string.Join(", ", sentimentReport.TradingRecommendations)}");
                    }
                    
                    // Add news sentiment if available
                    if (sentimentReport.NewsSentiment.Score != 0)
                    {
                        result.AppendLine($"News Sentiment: {sentimentReport.NewsSentiment.Label} ({sentimentReport.NewsSentiment.Score:F2})");
                    }
                    
                    // Add social media sentiment if available
                    if (sentimentReport.SocialMediaSentiment.Score != 0)
                    {
                        result.AppendLine($"Social Media Sentiment: {sentimentReport.SocialMediaSentiment.Label} ({sentimentReport.SocialMediaSentiment.Score:F2})");
                    }
                }
                catch (Exception ex)
                {
                    result.AppendLine($"\nSentiment analysis failed: {ex.Message}");
                    result.AppendLine("Note: Sentiment analysis requires market data and may not work for all symbols.");
                }

                return result.ToString();
            }
            catch (Exception ex)
            {
                return $"Error performing web search analysis: {ex.Message}";
            }
        }

        private async Task<string> PerformTechnicalAnalysisAsync(string symbol)
        {
            try
            {
                var result = new StringBuilder();

                // Short-term technical analysis
                var shortTermTA = await _technicalAnalysisService.PerformFullAnalysisAsync(symbol, 100);
                result.AppendLine("Short-term Analysis (100 days):");
                result.AppendLine($"Signal: {shortTermTA.OverallSignal}");
                result.AppendLine($"Signal Strength: {shortTermTA.SignalStrength:F2}");
                result.AppendLine($"Reasoning: {shortTermTA.Reasoning}");
                result.AppendLine();

                // Long-term technical analysis
                var longTermTA = await _technicalAnalysisService.PerformFullAnalysisAsync(symbol, 1800);
                result.AppendLine("Long-term Analysis (7 years):");
                result.AppendLine($"Signal: {longTermTA.OverallSignal}");
                result.AppendLine($"Signal Strength: {longTermTA.SignalStrength:F2}");
                result.AppendLine($"Reasoning: {longTermTA.Reasoning}");

                return result.ToString();
            }
            catch (Exception ex)
            {
                return $"Error performing technical analysis: {ex.Message}";
            }
        }

        private async Task<string> PerformFundamentalAnalysisAsync(string symbol)
        {
            try
            {
                var fundamentalAnalysis = await _companyValuationService.AnalyzeStockAsync(symbol, 365);
                return fundamentalAnalysis;
            }
            catch (Exception ex)
            {
                return $"Error performing fundamental analysis: {ex.Message}";
            }
        }

        private async Task<string> PerformRiskAssessmentAsync(string symbol, string assetType)
        {
            try
            {
                var result = new StringBuilder();

                // Get volatility metrics
                var historicalData = await _alpacaService.GetHistoricalBarsAsync(symbol, 252); // 1 year
                if (historicalData?.Any() == true)
                {
                    var prices = historicalData.Select(d => (decimal)d.Close).ToList();
                    var volatility = CalculateVolatility(prices);
                    var sharpeRatio = CalculateSharpeRatio(prices);

                    result.AppendLine($"Annual Volatility: {volatility:P2}");
                    result.AppendLine($"Sharpe Ratio: {sharpeRatio:F2}");
                    
                    var riskLevel = volatility switch
                    {
                        < 0.15 => "LOW",
                        < 0.25 => "MODERATE",
                        < 0.35 => "HIGH",
                        _ => "VERY HIGH"
                    };
                    
                    result.AppendLine($"Risk Level: {riskLevel}");
                }

                // Asset-specific risk factors
                result.AppendLine($"\n{assetType.ToUpper()} Specific Risk Factors:");
                switch (assetType.ToLower())
                {
                    case "stock":
                        result.AppendLine("• Company-specific operational risks");
                        result.AppendLine("• Sector rotation risk");
                        result.AppendLine("• Earnings volatility");
                        break;
                    case "etf":
                        result.AppendLine("• Tracking error risk");
                        result.AppendLine("• Underlying asset concentration");
                        result.AppendLine("• Liquidity risk");
                        break;
                    case "futures":
                        result.AppendLine("• High leverage risk");
                        result.AppendLine("• Margin call risk");
                        result.AppendLine("• Expiration risk");
                        break;
                }

                return result.ToString();
            }
            catch (Exception ex)
            {
                return $"Error performing risk assessment: {ex.Message}";
            }
        }

        private async Task<string> PerformComprehensiveRiskAssessmentAsync(string symbol, string assetType, string fundamentalAnalysis)
        {
            try
            {
                var result = new StringBuilder();

                // Extract fundamental risk factors from the fundamental analysis
                var fundamentalRisks = ExtractFundamentalRiskFactors(fundamentalAnalysis);
                if (!string.IsNullOrEmpty(fundamentalRisks))
                {
                    result.AppendLine("FUNDAMENTAL RISK FACTORS:");
                    result.AppendLine(fundamentalRisks);
                    result.AppendLine();
                }

                // Get technical/quantitative risk metrics
                result.AppendLine("TECHNICAL RISK METRICS:");
                var historicalData = await _alpacaService.GetHistoricalBarsAsync(symbol, 252); // 1 year
                if (historicalData?.Any() == true)
                {
                    var prices = historicalData.Select(d => (decimal)d.Close).ToList();
                    var volatility = CalculateVolatility(prices);
                    var sharpeRatio = CalculateSharpeRatio(prices);

                    result.AppendLine($"• Annual Volatility: {volatility:P2}");
                    result.AppendLine($"• Sharpe Ratio: {sharpeRatio:F2}");
                    
                    var riskLevel = volatility switch
                    {
                        < 0.15 => "LOW",
                        < 0.25 => "MODERATE",
                        < 0.35 => "HIGH",
                        _ => "VERY HIGH"
                    };
                    
                    result.AppendLine($"• Overall Risk Level: {riskLevel}");
                }
                else
                {
                    result.AppendLine("• Unable to calculate volatility metrics (insufficient historical data)");
                }

                // Asset-specific risk factors
                result.AppendLine($"\n{assetType.ToUpper()}-SPECIFIC RISK FACTORS:");
                switch (assetType.ToLower())
                {
                    case "stock":
                        result.AppendLine("• Company-specific operational risks");
                        result.AppendLine("• Sector rotation risk");
                        result.AppendLine("• Earnings volatility");
                        result.AppendLine("• Management execution risk");
                        result.AppendLine("• Competitive positioning risk");
                        break;
                    case "etf":
                        result.AppendLine("• Tracking error risk");
                        result.AppendLine("• Underlying asset concentration");
                        result.AppendLine("• Liquidity risk");
                        result.AppendLine("• Management fee impact");
                        break;
                    case "futures":
                        result.AppendLine("• High leverage risk");
                        result.AppendLine("• Margin call risk");
                        result.AppendLine("• Expiration risk");
                        result.AppendLine("• Basis risk");
                        break;
                }

                return result.ToString();
            }
            catch (Exception ex)
            {
                return $"Error performing comprehensive risk assessment: {ex.Message}";
            }
        }

        private string RemoveRiskAssessmentSection(string fundamentalAnalysis)
        {
            if (string.IsNullOrEmpty(fundamentalAnalysis))
                return fundamentalAnalysis;

            // Remove lines that contain risk assessment information to avoid duplication
            var lines = fundamentalAnalysis.Split('\n');
            var filteredLines = new List<string>();
            bool inRiskSection = false;

            foreach (var line in lines)
            {
                var lowerLine = line.ToLower();
                
                // Start of risk section
                if (lowerLine.Contains("WARNING:") && lowerLine.Contains("risk"))
                {
                    inRiskSection = true;
                    continue;
                }
                
                // End of risk section (next section starts)
                if (inRiskSection && (lowerLine.Contains("timestamp:") || 
                    lowerLine.StartsWith("=") || 
                    lowerLine.Contains("ANALYSIS:") || 
                    lowerLine.Contains("MONEY:") ||
                    lowerLine.Contains("")))
                {
                    inRiskSection = false;
                }

                // Skip risk-related lines
                if (!inRiskSection && !lowerLine.Contains("high risk") && !lowerLine.Contains("key risk factors"))
                {
                    filteredLines.Add(line);
                }
            }

            return string.Join('\n', filteredLines);
        }

        private string ExtractFundamentalRiskFactors(string fundamentalAnalysis)
        {
            if (string.IsNullOrEmpty(fundamentalAnalysis))
                return "";

            var result = new StringBuilder();
            var lines = fundamentalAnalysis.Split('\n');

            foreach (var line in lines)
            {
                var lowerLine = line.ToLower();
                
                if (lowerLine.Contains("risk") && !lowerLine.Contains("WARNING:"))
                {
                    // Extract specific risk mentions
                    if (lowerLine.Contains("p/e") && lowerLine.Contains("high"))
                    {
                        result.AppendLine("• High P/E ratio suggests elevated valuation risk");
                    }
                    if (lowerLine.Contains("dividend") && lowerLine.Contains("no"))
                    {
                        result.AppendLine("• No dividend income provides no downside cushion");
                    }
                    if (lowerLine.Contains("debt"))
                    {
                        result.AppendLine($"• {line.Trim()}");
                    }
                    if (lowerLine.Contains("margin") || lowerLine.Contains("profitability"))
                    {
                        result.AppendLine($"• {line.Trim()}");
                    }
                }
            }

            return result.ToString();
        }

        private async Task<string> GenerateInvestmentRecommendationAsync(string symbol, string assetType, string analysisContext)
        {
            try
            {
                var prompt = $@"
Based on the comprehensive analysis provided below, generate a clear investment recommendation for {symbol} ({assetType}):

{analysisContext}

Provide a recommendation that includes:
1. BUY/HOLD/SELL recommendation with confidence level (1-10)
2. Target price range (if applicable)
3. Time horizon for the recommendation
4. Key risk factors to monitor
5. Entry/exit strategy suggestions
6. Position sizing recommendations

Format the response in plain text without markdown formatting. Use simple bullet points and clear sections.
";

                var response = await _kernel.InvokePromptAsync(prompt);
                return response.GetValue<string>() ?? "Unable to generate recommendation.";
            }
            catch (Exception ex)
            {
                return $"Error generating AI recommendation: {ex.Message}";
            }
        }

        private double CalculateVolatility(List<decimal> prices)
        {
            if (prices.Count < 2) return 0;

            var returns = new List<double>();
            for (int i = 1; i < prices.Count; i++)
            {
                var returnVal = (double)(prices[i] - prices[i - 1]) / (double)prices[i - 1];
                returns.Add(returnVal);
            }

            var mean = returns.Average();
            var variance = returns.Select(r => Math.Pow(r - mean, 2)).Average();
            return Math.Sqrt(variance * 252); // Annualized volatility
        }

        private double CalculateSharpeRatio(List<decimal> prices, double riskFreeRate = 0.02)
        {
            if (prices.Count < 2) return 0;

            var returns = new List<double>();
            for (int i = 1; i < prices.Count; i++)
            {
                var returnVal = (double)(prices[i] - prices[i - 1]) / (double)prices[i - 1];
                returns.Add(returnVal);
            }

            var avgReturn = returns.Average() * 252; // Annualized
            var volatility = CalculateVolatility(prices.Select(p => p).ToList());
            
            return volatility > 0 ? (avgReturn - riskFreeRate) / volatility : 0;
        }
    }
}
