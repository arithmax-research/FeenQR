using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using QuantResearchAgent.Core;
using QuantResearchAgent.Services;
using AlternativeDataModels = QuantResearchAgent.Core.AlternativeDataModels;

namespace QuantResearchAgent.Services
{
    /// <summary>
    /// Service that generates professional stock pitches using DeepSeek/LLM
    /// combined with all available research tools (fundamental data, valuation,
    /// news/sentiment, SEC filings, earnings, macro data, etc.).
    /// 
    /// Produces a structured stock pitch with:
    /// 1. Stock Recommendation (Long/Short, target price, expected return)
    /// 2. Company Overview (business model, industry, products, competitors, financials)
    /// 3. Investment Thesis (why undervalued/overvalued, unique insights)
    /// 4. Catalysts (earnings, product launches, regulatory, industry shifts)
    /// 5. Valuation & Returns (DCF, comps, scenarios, peer comparison)
    /// 6. Risks & Mitigation (competition, regulation, execution, macro)
    /// 7. Conclusion (summary, recommendation, catalysts, why now)
    /// </summary>
    public class StockPitchService
    {
        private readonly EnhancedFundamentalAnalysisService _fundamentalService;
        private readonly MarketDataService _marketDataService;
        private readonly NewsSentimentAnalysisService _sentimentService;
        private readonly SECFilingsService _secFilingsService;
        private readonly EarningsService _earningsService;
        private readonly LLMRouterService _llmService;
        private readonly ILogger<StockPitchService> _logger;
        private readonly YFinanceNewsService _newsService;
        private readonly FinancialModelingPrepService _fmpService;
        private readonly AlphaVantageService _alphaVantageService;
        private readonly TechnicalAnalysisService _technicalAnalysisService;

        public StockPitchService(
            EnhancedFundamentalAnalysisService fundamentalService,
            MarketDataService marketDataService,
            NewsSentimentAnalysisService sentimentService,
            SECFilingsService secFilingsService,
            EarningsService earningsService,
            LLMRouterService llmService,
            ILogger<StockPitchService> logger,
            YFinanceNewsService newsService,
            FinancialModelingPrepService fmpService,
            AlphaVantageService alphaVantageService,
            TechnicalAnalysisService technicalAnalysisService)
        {
            _fundamentalService = fundamentalService;
            _marketDataService = marketDataService;
            _sentimentService = sentimentService;
            _secFilingsService = secFilingsService;
            _earningsService = earningsService;
            _llmService = llmService;
            _logger = logger;
            _newsService = newsService;
            _fmpService = fmpService;
            _alphaVantageService = alphaVantageService;
            _technicalAnalysisService = technicalAnalysisService;
        }

        /// <summary>
        /// Generate a complete stock pitch for the given symbol.
        /// Orchestrates all research tools, gathers data, then uses DeepSeek/LLM
        /// to synthesize it into a structured institutional-quality pitch.
        /// </summary>
        public async Task<StockPitchResult> GenerateStockPitchAsync(string symbol, string positionType = "auto")
        {
            var startTime = DateTime.UtcNow;
            _logger.LogInformation("Generating stock pitch for {Symbol} with position type: {PositionType}", symbol, positionType);

            var result = new StockPitchResult
            {
                Symbol = symbol.ToUpper(),
                GeneratedAt = startTime,
                PositionType = positionType,
                DataSourcesUsed = new List<string>(),
                Errors = new List<string>()
            };

            try
            {
                // STEP 1: Gather all data in parallel (fire all queries at once)
                var tasks = new Dictionary<string, Task>
                {
                    ["CompanyOverview"] = TryFetchAsync("CompanyOverview", symbol, result, 
                        async () => result.CompanyOverviewData = await _fundamentalService.GetComprehensiveCompanyOverviewAsync(symbol)),
                    
                    ["FinancialStatements"] = TryFetchAsync("FinancialStatements", symbol, result,
                        async () => result.FinancialStatementsData = await _fundamentalService.GetComprehensiveFinancialStatementsAsync(symbol)),
                    
                    ["ValuationAnalysis"] = TryFetchAsync("ValuationAnalysis", symbol, result,
                        async () => result.ValuationData = await _fundamentalService.GetComprehensiveValuationAnalysisAsync(symbol)),
                    
                    ["AnalystAnalysis"] = TryFetchAsync("AnalystAnalysis", symbol, result,
                        async () => result.AnalystData = await _fundamentalService.GetComprehensiveAnalystAnalysisAsync(symbol)),
                    
                    ["MarketData"] = TryFetchAsync("MarketData", symbol, result,
                        async () => result.MarketData = await _marketDataService.GetHistoricalDataAsync(symbol, 365)),
                    
                    ["Sentiment"] = TryFetchAsync("Sentiment", symbol, result,
                        async () => result.SentimentData = await _sentimentService.AnalyzeSymbolSentimentAsync(symbol, 15)),
                    
                    ["Earnings"] = TryFetchAsync("Earnings", symbol, result,
                        async () => result.EarningsData = await _earningsService.GetComprehensiveAnalysisAsync(symbol)),
                    
                    ["SECFilings"] = TryFetchAsync("SECFilings", symbol, result,
                        async () => result.SecFilingData = await _secFilingsService.AnalyzeLatestFilingAsync(symbol, "10-K")),
                    
                    ["TechnicalAnalysis"] = TryFetchAsync("TechnicalAnalysis", symbol, result,
                        async () => result.TechnicalData = (await _technicalAnalysisService.PerformFullAnalysisAsync(symbol, 365)).ToString())
                };

                await Task.WhenAll(tasks.Values);

                // STEP 2: Build a comprehensive data context for the LLM
                var dataContext = BuildDataContext(symbol, result);

                // STEP 3: Determine position (Long/Short) if auto mode
                if (positionType.ToLower() == "auto")
                {
                    var positionPrompt = $@"Based on the following research data for {symbol}, determine whether a LONG or SHORT position is most appropriate.

RESEARCH DATA:
{dataContext}

Analyze the data and respond with ONLY one word: either 'LONG' or 'SHORT'. Do not include any other text or explanation.";
                    
                    try
                    {
                        var positionResult = await _llmService.GetChatCompletionAsync(positionPrompt);
                        var cleanedPosition = positionResult?.Trim().ToUpper();
                        if (cleanedPosition == "LONG" || cleanedPosition == "SHORT")
                            result.PositionType = cleanedPosition;
                        else
                            result.PositionType = "LONG"; // Default to long
                    }
                    catch
                    {
                        result.PositionType = "LONG";
                    }
                }

                // STEP 4: Generate the structured stock pitch using DeepSeek/LLM
                var pitchPrompt = BuildStockPitchPrompt(symbol, result.PositionType, dataContext);
                var pitchResult = await _llmService.GetChatCompletionAsync(pitchPrompt);

                // STEP 5: Parse the generated pitch into structured sections
                result = ParsePitchSections(result, pitchResult, dataContext);

                result.Success = true;
                result.GenerationTimeMs = (DateTime.UtcNow - startTime).TotalMilliseconds;
                
                _logger.LogInformation("Stock pitch for {Symbol} generated successfully in {Time:F0}ms. Position: {Position}", 
                    symbol, result.GenerationTimeMs, result.PositionType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating stock pitch for {Symbol}", symbol);
                result.Success = false;
                result.Errors.Add($"Overall generation error: {ex.Message}");
                
                // Try to generate a basic pitch even on error
                try
                {
                    var fallbackData = BuildDataContext(symbol, result);
                    var fallbackPrompt = BuildStockPitchPrompt(symbol, positionType, fallbackData);
                    var fallbackResult = await _llmService.GetChatCompletionAsync(fallbackPrompt);
                    result = ParsePitchSections(result, fallbackResult, fallbackData);
                    result.Success = true;
                }
                catch { }
            }

            result.GenerationTimeMs = (DateTime.UtcNow - startTime).TotalMilliseconds;
            return result;
        }

        /// <summary>
        /// Wraps a fetch operation with error handling and data source tracking.
        /// </summary>
        private async Task TryFetchAsync(string sourceName, string symbol, StockPitchResult result, Func<Task> fetchFunc)
        {
            try
            {
                await fetchFunc();
                result.DataSourcesUsed.Add(sourceName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Failed to fetch {Source} for {Symbol}: {Message}", sourceName, symbol, ex.Message);
                result.DataSourcesUsed.Add($"{sourceName} (unavailable)");
            }
        }

        /// <summary>
        /// Builds a comprehensive data context string summarizing all research data.
        /// </summary>
        private string BuildDataContext(string symbol, StockPitchResult result)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"=== STOCK PITCH RESEARCH DATA FOR {symbol.ToUpper()} ===");
            sb.AppendLine($"Generated at: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            sb.AppendLine();

            // Company Overview
            if (result.CompanyOverviewData != null)
            {
                var o = result.CompanyOverviewData;
                sb.AppendLine("--- COMPANY OVERVIEW ---");
                sb.AppendLine($"Company: {o.CompanyName ?? "N/A"}");
                sb.AppendLine($"Industry: {o.Industry ?? "N/A"}");
                sb.AppendLine($"Sector: {o.Sector ?? "N/A"}");
                sb.AppendLine($"Description: {o.Description ?? "N/A"}");
                sb.AppendLine($"Exchange: {o.Exchange ?? "N/A"}");
                sb.AppendLine($"CEO: {o.CEO ?? "N/A"}");
                sb.AppendLine($"Employees: {o.Employees:N0}");
                sb.AppendLine($"Market Cap: ${o.MarketCap:N0}");
                sb.AppendLine($"Shares Outstanding: {o.SharesOutstanding:N0}");
                sb.AppendLine($"Country: {o.Country ?? "N/A"}");
                sb.AppendLine();
            }

            // Valuation Data
            if (result.ValuationData != null)
            {
                var v = result.ValuationData;
                sb.AppendLine("--- VALUATION ANALYSIS ---");
                sb.AppendLine($"Current Price: ${v.CurrentPrice:F2}");
                sb.AppendLine($"Market Cap: ${v.MarketCap:N0}");
                sb.AppendLine($"P/E Ratio (Trailing): {v.PERatio:F2}");
                sb.AppendLine($"Forward P/E: {v.ForwardPE:F2}");
                sb.AppendLine($"PEG Ratio: {v.PEGRatio:F2}");
                sb.AppendLine($"Price/Book: {v.PriceToBook:F2}");
                sb.AppendLine($"Price/Sales: {v.PriceToSales:F2}");
                sb.AppendLine($"EV/EBITDA: {v.EVToEBITDA:F2}");
                sb.AppendLine($"EV/Revenue: {v.EVToRevenue:F2}");
                sb.AppendLine($"Dividend Yield: {v.DividendYield:P2}");
                sb.AppendLine($"EPS: ${v.EPS:F2}");
                sb.AppendLine($"EPS Growth: {v.EPSGrowth:P2}");
                sb.AppendLine($"Revenue Growth: {v.RevenueGrowth:P2}");
                sb.AppendLine($"ROE: {v.ROE:P2}");
                sb.AppendLine($"ROA: {v.ROA:P2}");
                sb.AppendLine($"Profit Margin: {v.ProfitMargin:P2}");
                sb.AppendLine($"Operating Margin: {v.OperatingMargin:P2}");
                sb.AppendLine($"Debt/Equity: {v.DebtToEquity:F2}");
                sb.AppendLine($"Current Ratio: {v.CurrentRatio:F2}");
                sb.AppendLine($"Interest Coverage: {v.InterestCoverage:F2}");
                sb.AppendLine($"52-Week High: ${v.FiftyTwoWeekHigh:F2}");
                sb.AppendLine($"52-Week Low: ${v.FiftyTwoWeekLow:F2}");
                sb.AppendLine($"Analyst Target: ${v.AnalystTargetPrice:F2}");
                sb.AppendLine($"Upside to Target: {v.UpsidePotential:F1}%");
                sb.AppendLine();
            }

            // Analyst Data
            if (result.AnalystData != null)
            {
                var a = result.AnalystData;
                sb.AppendLine("--- ANALYST CONSENSUS ---");
                sb.AppendLine($"Consensus Rating: {a.ConsensusRating ?? "N/A"}");
                sb.AppendLine($"Number of Analysts: {a.NumberOfAnalysts}");
                sb.AppendLine($"Avg Target: ${a.AverageTargetPrice:F2}");
                sb.AppendLine($"High Target: ${a.HighTargetPrice:F2}");
                sb.AppendLine($"Low Target: ${a.LowTargetPrice:F2}");
                sb.AppendLine($"Buy Ratings: {a.BuyRatings}");
                sb.AppendLine($"Hold Ratings: {a.HoldRatings}");
                sb.AppendLine($"Sell Ratings: {a.SellRatings}");
                sb.AppendLine($"Upgrades: {a.Upgrades}");
                sb.AppendLine($"Downgrades: {a.Downgrades}");
                sb.AppendLine();
            }

            // Sentiment Data
            if (result.SentimentData != null)
            {
                var s = result.SentimentData;
                sb.AppendLine("--- NEWS & SENTIMENT ---");
                sb.AppendLine($"Overall Sentiment: {s.OverallSentiment ?? "N/A"} (Score: {s.SentimentScore:F2})");
                sb.AppendLine($"Trading Signal: {s.TradingSignal ?? "N/A"}");
                sb.AppendLine($"Confidence: {s.Confidence:F2}");
                if (!string.IsNullOrEmpty(s.Summary))
                {
                    sb.AppendLine($"Summary: {s.Summary}");
                }
                if (s.NewsItems?.Any() == true)
                {
                    sb.AppendLine($"Recent Articles ({s.NewsItems.Count}):");
                    foreach (var article in s.NewsItems.Take(5))
                    {
                        sb.AppendLine($"  - [{article.PublishedDate:MMM dd}] {article.Title} (Sentiment: {article.SentimentLabel})");
                    }
                }
                sb.AppendLine();
            }

            // Earnings Data
            if (result.EarningsData != null)
            {
                var e = result.EarningsData;
                sb.AppendLine("--- EARNINGS ANALYSIS ---");
                if (e.EarningsAnalysis != null)
                {
                    sb.AppendLine($"Latest EPS: ${e.EarningsAnalysis.Eps:F2} (Est: ${e.EarningsAnalysis.EpsEstimate:F2}, Beat: {e.EarningsAnalysis.EpsBeats})");
                    sb.AppendLine($"Latest Revenue: ${e.EarningsAnalysis.Revenue:N0} (Est: ${e.EarningsAnalysis.RevenueEstimate:N0}, Beat: {e.EarningsAnalysis.RevenueBeats})");
                    sb.AppendLine($"Guidance: {e.EarningsAnalysis.GuidanceFromManagement ?? "N/A"}");
                    if (e.EarningsAnalysis.KeyTakeaways?.Any() == true)
                    {
                        sb.AppendLine("Key Takeaways:");
                        foreach (var kt in e.EarningsAnalysis.KeyTakeaways)
                            sb.AppendLine($"  - {kt}");
                    }
                }
                if (e.SentimentAnalysis != null)
                {
                    sb.AppendLine($"Earnings Call Sentiment: {e.SentimentAnalysis.OverallSentiment ?? "N/A"} (Score: {e.SentimentAnalysis.SentimentScore:F2})");
                    sb.AppendLine($"Management Tone: {e.SentimentAnalysis.ManagementTone ?? "N/A"}");
                }
                if (e.StrategicInsights != null)
                {
                    sb.AppendLine($"Competitive Position: {e.StrategicInsights.CompetitivePosition ?? "N/A"}");
                }
                sb.AppendLine($"Recommendation: {e.InvestmentRecommendation ?? "N/A"}");
                sb.AppendLine();
            }

            // SEC Filings Data
            if (result.SecFilingData != null)
            {
                var sec = result.SecFilingData;
                sb.AppendLine("--- SEC FILINGS ---");
                if (sec.Filing != null)
                {
                    sb.AppendLine($"Latest Filing: {sec.Filing.FilingType} on {sec.Filing.FilingDate:yyyy-MM-dd}");
                }
                if (sec.KeyFindings?.Any() == true)
                {
                    sb.AppendLine("Key Findings from Latest Filing:");
                    foreach (var kf in sec.KeyFindings)
                        sb.AppendLine($"  - {kf}");
                }
                if (sec.ContentSentiment != null)
                {
                    sb.AppendLine($"Filing Sentiment Score: {sec.ContentSentiment.OverallScore:F2} (Confidence: {sec.ContentSentiment.Confidence:P0})");
                }
                sb.AppendLine();
            }

            // Technical Data
            if (result.TechnicalData != null)
            {
                sb.AppendLine("--- TECHNICAL ANALYSIS ---");
                sb.AppendLine(result.TechnicalData);
                sb.AppendLine();
            }

            return sb.ToString();
        }

        /// <summary>
        /// Builds the structured prompt for the LLM to generate the stock pitch.
        /// </summary>
        private string BuildStockPitchPrompt(string symbol, string positionType, string dataContext)
        {
            return $@"You are a top-tier sell-side equity research analyst at a leading investment bank. 
Your task is to produce an institutional-quality stock pitch for {symbol.ToUpper()}.

POSITION: {positionType.ToUpper()} ({(positionType.ToUpper() == "LONG" ? "Buy recommendation - stock is undervalued" : "Sell/Short recommendation - stock is overvalued")})

Use the following real research data gathered from multiple financial data sources to build your pitch.

RESEARCH DATA:
{dataContext}

INSTRUCTIONS:
Produce a professional, comprehensive stock pitch with the following EXACT sections. 
Use ONLY the data provided above. Do not make up numbers. If data is missing, state Data not available
Format the response in clear sections as described below. Do not use markdown formatting like asterisks or hashtags.
Use plain text with section headers like:

STOCK RECOMMENDATION
COMPANY OVERVIEW
INVESTMENT THESIS
CATALYSTS
VALUATION & RETURNS
RISKS & MITIGATION
CONCLUSION

Here are the detailed requirements for each section:

1. STOCK RECOMMENDATION
State upfront whether you recommend {positionType.ToUpper()} position. Provide:
- Direction: {positionType.ToUpper()}
- Target Price: Based on analyst consensus, DCF analysis, or peer comparison from the data above
- Expected Return: Percentage upside/downside to target
- Time Horizon: 6-12 months
- Conviction: High/Medium/Low based on data confidence

2. COMPANY OVERVIEW
Provide a brief but comprehensive snapshot:
- Business model and primary revenue drivers
- Industry/sector and competitive position
- Key product lines and geographic exposure
- Major competitors
- Key financial snapshot (Market Cap, Revenue, EPS, P/E from data)
- Management quality indicators if available

3. INVESTMENT THESIS
The heart of the pitch. Explain why the stock is {(positionType.ToUpper() == "LONG" ? "undervalued" : "overvalued")}:
- Unique insights from the data (market inefficiencies, overlooked growth drivers, misunderstood risks)
- Competitive advantages or disadvantages
- Industry tailwinds or headwinds
- Why the market is mispricing this stock
- Support your thesis with specific data points from the research above

4. CATALYSTS
Identify specific near-term events that will unlock value:
- Upcoming earnings reports and expected catalysts
- Product launches, new contracts, or expansion plans
- Regulatory approvals or policy changes
- Industry shifts or secular trends
- Potential M&A activity
- For each catalyst, note the expected timeframe

5. VALUATION & RETURNS
Support your thesis with valuation analysis:
- Current valuation metrics and how they compare to historical averages
- Comparison to peer companies (is it cheap or expensive relative to peers?)
- Analyst consensus and price targets from the data
- Scenario analysis: Base case, Bull case, Bear case with specific price targets
- Expected return calculation

6. RISKS & MITIGATION
Acknowledge potential risks and how they are mitigated:
- Competitive threats
- Regulatory or political risks
- Execution challenges
- Macroeconomic risks
- Valuation risk (what if thesis is wrong?)
- For each risk, explain the mitigation or why the risk is overstated

7. CONCLUSION
End with a crisp summary:
- Restate recommendation and target price
- Key catalysts and timeframe
- Why this opportunity is compelling NOW
- Risk/reward assessment

IMPORTANT: 
- Write in a confident, professional tone suitable for institutional investors
- Reference specific data points from the RESEARCH DATA section to support your claims
- If certain data is missing, note it transparently
- Keep the total response under 3000 words
- Use ONLY plain text - NO markdown formatting, NO bullet symbols, NO asterisks
- Use numbered lists or dashes for bullets instead";
        }

        /// <summary>
        /// Parses the LLM-generated pitch text into structured sections.
        /// </summary>
        private StockPitchResult ParsePitchSections(StockPitchResult result, string pitchText, string dataContext)
        {
            result.RawPitchText = pitchText;

            if (string.IsNullOrEmpty(pitchText))
            {
                result.Errors.Add("Empty pitch generated by LLM");
                return result;
            }

            // Extract sections by known headers
            var sections = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var currentSection = "PREAMBLE";
            var currentText = new StringBuilder();

            foreach (var line in pitchText.Split('\n'))
            {
                var trimmedLine = line.Trim();
                
                // Check for section headers
                if (trimmedLine.StartsWith("STOCK RECOMMENDATION") || 
                    trimmedLine.Equals("STOCK RECOMMENDATION", StringComparison.OrdinalIgnoreCase))
                {
                    if (currentText.Length > 0)
                        sections[currentSection] = currentText.ToString().Trim();
                    currentSection = "StockRecommendation";
                    currentText.Clear();
                }
                else if (trimmedLine.StartsWith("COMPANY OVERVIEW") || 
                         trimmedLine.Equals("COMPANY OVERVIEW", StringComparison.OrdinalIgnoreCase))
                {
                    if (currentText.Length > 0)
                        sections[currentSection] = currentText.ToString().Trim();
                    currentSection = "CompanyOverview";
                    currentText.Clear();
                }
                else if (trimmedLine.StartsWith("INVESTMENT THESIS") || 
                         trimmedLine.Equals("INVESTMENT THESIS", StringComparison.OrdinalIgnoreCase))
                {
                    if (currentText.Length > 0)
                        sections[currentSection] = currentText.ToString().Trim();
                    currentSection = "InvestmentThesis";
                    currentText.Clear();
                }
                else if (trimmedLine.StartsWith("CATALYSTS") || 
                         trimmedLine.Equals("CATALYSTS", StringComparison.OrdinalIgnoreCase))
                {
                    if (currentText.Length > 0)
                        sections[currentSection] = currentText.ToString().Trim();
                    currentSection = "Catalysts";
                    currentText.Clear();
                }
                else if (trimmedLine.StartsWith("VALUATION & RETURNS") || 
                         trimmedLine.StartsWith("VALUATION AND RETURNS") ||
                         trimmedLine.Equals("VALUATION & RETURNS", StringComparison.OrdinalIgnoreCase))
                {
                    if (currentText.Length > 0)
                        sections[currentSection] = currentText.ToString().Trim();
                    currentSection = "ValuationAndReturns";
                    currentText.Clear();
                }
                else if (trimmedLine.StartsWith("RISKS & MITIGATION") || 
                         trimmedLine.StartsWith("RISKS AND MITIGATION") ||
                         trimmedLine.Equals("RISKS & MITIGATION", StringComparison.OrdinalIgnoreCase))
                {
                    if (currentText.Length > 0)
                        sections[currentSection] = currentText.ToString().Trim();
                    currentSection = "RisksAndMitigation";
                    currentText.Clear();
                }
                else if (trimmedLine.StartsWith("CONCLUSION") || 
                         trimmedLine.Equals("CONCLUSION", StringComparison.OrdinalIgnoreCase))
                {
                    if (currentText.Length > 0)
                        sections[currentSection] = currentText.ToString().Trim();
                    currentSection = "Conclusion";
                    currentText.Clear();
                }
                else
                {
                    currentText.AppendLine(trimmedLine);
                }
            }

            // Save last section
            if (currentText.Length > 0)
                sections[currentSection] = currentText.ToString().Trim();

            // Map sections to result
            result.StockRecommendation = sections.GetValueOrDefault("StockRecommendation", "");
            result.CompanyOverview = sections.GetValueOrDefault("CompanyOverview", "");
            result.InvestmentThesis = sections.GetValueOrDefault("InvestmentThesis", "");
            result.Catalysts = sections.GetValueOrDefault("Catalysts", "");
            result.ValuationAndReturns = sections.GetValueOrDefault("ValuationAndReturns", "");
            result.RisksAndMitigation = sections.GetValueOrDefault("RisksAndMitigation", "");
            result.Conclusion = sections.GetValueOrDefault("Conclusion", "");

            // If the pitch is missing headers, try to extract from the raw text
            if (string.IsNullOrEmpty(result.StockRecommendation) && string.IsNullOrEmpty(result.CompanyOverview))
            {
                _logger.LogWarning("Could not parse structured sections for {Symbol}, using raw text", result.Symbol);
                result.StockRecommendation = pitchText.Length > 2000 ? pitchText.Substring(0, 2000) : pitchText;
            }

            // Auto-extract recommendation details
            ExtractRecommendationDetails(result);

            return result;
        }

        /// <summary>
        /// Extracts target price, expected return, and conviction from the recommendation section.
        /// </summary>
        private void ExtractRecommendationDetails(StockPitchResult result)
        {
            var text = result.StockRecommendation ?? "";

            // Try to extract target price
            var targetMatch = System.Text.RegularExpressions.Regex.Match(text, 
                @"(?:target|price target|target price)[:\s]*\$?(\d+[.,]?\d*)", 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (targetMatch.Success && decimal.TryParse(targetMatch.Groups[1].Value.Replace(",", ""), 
                System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var tp))
            {
                result.TargetPrice = tp;
            }

            // Try to extract expected return
            var returnMatch = System.Text.RegularExpressions.Regex.Match(text,
                @"(?:expected return|return|upside|downside)[:\s]*([+-]?\d+[.,]?\d*)\s*%",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (returnMatch.Success && decimal.TryParse(returnMatch.Groups[1].Value.Replace(",", "."),
                System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var ret))
            {
                result.ExpectedReturn = ret;
            }

            // Try to extract conviction
            var convictionMatch = System.Text.RegularExpressions.Regex.Match(text,
                @"(?:conviction)[:\s]*(High|Medium|Low)",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (convictionMatch.Success)
            {
                result.Conviction = convictionMatch.Groups[1].Value;
            }

            // Try to extract time horizon
            var horizonMatch = System.Text.RegularExpressions.Regex.Match(text,
                @"(?:time horizon|horizon|timeframe)[:\s]*(\d+\s*(?:month|year)s?)",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (horizonMatch.Success)
            {
                result.TimeHorizon = horizonMatch.Groups[1].Value;
            }
        }
    }

    /// <summary>
    /// Represents the complete structured stock pitch result.
    /// </summary>
    public class StockPitchResult
    {
        public string Symbol { get; set; } = string.Empty;
        public DateTime GeneratedAt { get; set; }
        public double GenerationTimeMs { get; set; }
        public bool Success { get; set; }
        public string PositionType { get; set; } = "auto";
        public string TargetPriceFormatted => TargetPrice > 0 ? $"${TargetPrice:F2}" : "N/A";
        public string ExpectedReturnFormatted => $"{ExpectedReturn:F1}%";
        public decimal TargetPrice { get; set; }
        public decimal ExpectedReturn { get; set; }
        public string Conviction { get; set; } = "Medium";
        public string TimeHorizon { get; set; } = "6-12 months";
        
        // Structured sections
        public string StockRecommendation { get; set; } = string.Empty;
        public string CompanyOverview { get; set; } = string.Empty;
        public string InvestmentThesis { get; set; } = string.Empty;
        public string Catalysts { get; set; } = string.Empty;
        public string ValuationAndReturns { get; set; } = string.Empty;
        public string RisksAndMitigation { get; set; } = string.Empty;
        public string Conclusion { get; set; } = string.Empty;
        
        // Raw data
        public string RawPitchText { get; set; } = string.Empty;
        
        // Underlying research data (used for context)
        public EnhancedCompanyOverview? CompanyOverviewData { get; set; }
        public EnhancedFinancialStatements? FinancialStatementsData { get; set; }
        public EnhancedValuationAnalysis? ValuationData { get; set; }
        public EnhancedAnalystAnalysis? AnalystData { get; set; }
        public List<MarketData>? MarketData { get; set; }
        public SymbolSentimentAnalysis? SentimentData { get; set; }
        public ComprehensiveEarningsAnalysis? EarningsData { get; set; }
        public AlternativeDataModels.SECFilingAnalysis? SecFilingData { get; set; }
        public string? TechnicalData { get; set; }
        
        // Metadata
        public List<string> DataSourcesUsed { get; set; } = new();
        public List<string> Errors { get; set; } = new();
    }

}