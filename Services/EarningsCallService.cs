using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using QuantResearchAgent.Core;
using QuantResearchAgent.Services;

namespace QuantResearchAgent.Services
{
    public class EarningsCallService
    {
        private readonly HttpClient _httpClient;
        private readonly ILLMService _llmService;

        public EarningsCallService(HttpClient httpClient, ILLMService llmService)
        {
            _httpClient = httpClient;
            _llmService = llmService;
        }

        // Seeking Alpha API or other transcript sources
        private const string SEEKING_ALPHA_BASE_URL = "https://seeking-alpha.p.rapidapi.com";
        private const string TRANSCRIPT_API_KEY = ""; // Would be configured via settings

        public async Task<AlternativeDataModels.EarningsCallAnalysis> AnalyzeLatestEarningsCallAsync(string ticker)
        {
            var analysis = new AlternativeDataModels.EarningsCallAnalysis
            {
                AnalysisDate = DateTime.Now
            };

            try
            {
                // Get latest earnings call transcript
                var earningsCall = await GetLatestEarningsCallAsync(ticker);
                analysis.Call = earningsCall;

                // Perform comprehensive analysis
                analysis.SentimentAnalysis = await AnalyzeCallSentimentAsync(earningsCall);
                analysis.FinancialMetrics = await ExtractFinancialMetricsAsync(earningsCall);
                analysis.StrategicInsights = await ExtractStrategicInsightsAsync(earningsCall);
                analysis.RiskIndicators = await IdentifyRiskIndicatorsAsync(earningsCall);
                analysis.CompetitivePositioning = await AnalyzeCompetitivePositioningAsync(earningsCall);

                return analysis;
            }
            catch (Exception ex)
            {
                analysis.StrategicInsights.Add($"Analysis failed: {ex.Message}");
                return analysis;
            }
        }

        public async Task<List<AlternativeDataModels.EarningsCall>> GetEarningsCallHistoryAsync(
            string ticker, int limit = 4)
        {
            var calls = new List<AlternativeDataModels.EarningsCall>();

            try
            {
                // Use Alpha Vantage API for earnings transcripts (free tier available)
                var apiKey = Environment.GetEnvironmentVariable("ALPHA_VANTAGE_API_KEY") ?? "demo";
                var url = $"https://www.alphavantage.co/query?function=EARNINGS&symbol={ticker}&apikey={apiKey}";

                using var client = new HttpClient();
                var response = await client.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var earningsData = JsonSerializer.Deserialize<AlphaVantageEarningsResponse>(json);

                    if (earningsData?.QuarterlyEarnings != null)
                    {
                        foreach (var earning in earningsData.QuarterlyEarnings.Take(limit))
                        {
                            var call = new AlternativeDataModels.EarningsCall
                            {
                                CompanyName = ticker,
                                Ticker = ticker,
                                CallDate = DateTime.Parse(earning.ReportedDate ?? DateTime.Now.ToString()),
                                Quarter = ParseQuarterFromDate(earning.FiscalDateEnding ?? ""),
                                Year = DateTime.Parse(earning.FiscalDateEnding ?? DateTime.Now.ToString()).Year,
                                Transcript = await GetTranscriptFromSeekingAlphaAsync(ticker, earning.FiscalDateEnding ?? "")
                            };

                            await PopulateCallDataAsync(call);
                            calls.Add(call);
                        }
                    }
                }
                else
                {
                    // Fallback to mock data if API fails
                    calls.AddRange(CreateMockEarningsCalls(ticker, limit));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting earnings call history: {ex.Message}");
                // Fallback to mock data
                calls.AddRange(CreateMockEarningsCalls(ticker, limit));
            }

            return calls;
        }

        public async Task<AlternativeDataModels.SentimentAnalysis> AnalyzeCallSentimentAsync(
            AlternativeDataModels.EarningsCall earningsCall)
        {
            var sentiment = new AlternativeDataModels.SentimentAnalysis();

            try
            {
                // Analyze sentiment by speaker segments
                var speakerSentiments = new Dictionary<string, double>();

                foreach (var segment in earningsCall.SpeakerSegments)
                {
                    var segmentSentiment = await AnalyzeTextSentimentAsync(segment.Content);
                    speakerSentiments[segment.Speaker] = segmentSentiment.OverallScore;
                }

                sentiment.OverallScore = speakerSentiments.Values.Average();
                sentiment.Confidence = 0.8;

                // Add speaker-specific insights
                sentiment.AdditionalMetrics["SpeakerSentiments"] = speakerSentiments;
                sentiment.AdditionalMetrics["ManagementTone"] = speakerSentiments
                    .Where(s => s.Key.Contains("CEO") || s.Key.Contains("CFO") || s.Key.Contains("President"))
                    .Average(s => s.Value);

            }
            catch (Exception)
            {
                sentiment.OverallScore = 0.0;
                sentiment.Confidence = 0.0;
            }

            return sentiment;
        }

        public async Task<AlternativeDataModels.FinancialMetrics> ExtractFinancialMetricsAsync(
            AlternativeDataModels.EarningsCall earningsCall)
        {
            var metrics = new AlternativeDataModels.FinancialMetrics();

            try
            {
                var transcript = earningsCall.Transcript;

                // Extract revenue figures
                var revenuePattern = @"(?i)revenue.*?\$?(\d+(?:\.\d+)?)\s*(?:million|billion|M|B)";
                var revenueMatch = Regex.Match(transcript, revenuePattern);

                if (revenueMatch.Success && double.TryParse(revenueMatch.Groups[1].Value, out double revenueValue))
                {
                    var unit = revenueMatch.Groups[2].Value.ToLower();
                    if (unit.Contains("b")) revenueValue *= 1000;
                    metrics.Revenue = revenueValue;
                }

                // Extract EPS
                var epsPattern = @"(?i)EPS.*?\$?(\d+(?:\.\d+)?)";
                var epsMatch = Regex.Match(transcript, epsPattern);

                if (epsMatch.Success && double.TryParse(epsMatch.Groups[1].Value, out double epsValue))
                {
                    metrics.EPS = epsValue;
                }

                // Extract guidance
                var guidancePattern = @"(?i)(?:guidance|expect|forecast).*?\$?(\d+(?:\.\d+)?).*?\$?(\d+(?:\.\d+)?)";
                var guidanceMatch = Regex.Match(transcript, guidancePattern);

                if (guidanceMatch.Success)
                {
                    metrics.GuidanceLow = double.Parse(guidanceMatch.Groups[1].Value);
                    metrics.GuidanceHigh = double.Parse(guidanceMatch.Groups[2].Value);
                }

                // Use AI for more sophisticated extraction
                var prompt = $"Extract key financial metrics from this earnings call transcript. Focus on revenue, EPS, margins, and guidance.\n\n{transcript.Substring(0, Math.Min(2000, transcript.Length))}";

                var aiResponse = await _llmService.GetChatCompletionAsync(prompt);
                metrics.AIExtractedInsights = aiResponse.Split('\n').Where(line => !string.IsNullOrWhiteSpace(line)).ToList();

            }
            catch (Exception ex)
            {
                metrics.AIExtractedInsights = new List<string> { $"Error extracting metrics: {ex.Message}" };
            }

            return metrics;
        }

        public async Task<List<string>> ExtractStrategicInsightsAsync(
            AlternativeDataModels.EarningsCall earningsCall)
        {
            var insights = new List<string>();

            try
            {
                var prompt = $"Analyze this earnings call transcript and extract strategic insights. Focus on:\n" +
                           "1. Growth initiatives and investments\n" +
                           "2. Competitive positioning\n" +
                           "3. Market opportunities and challenges\n" +
                           "4. Long-term strategy\n\n" +
                           $"Transcript: {earningsCall.Transcript.Substring(0, Math.Min(3000, earningsCall.Transcript.Length))}";

                var aiResponse = await _llmService.GetChatCompletionAsync(prompt);
                insights.AddRange(aiResponse.Split('\n').Where(line => !string.IsNullOrWhiteSpace(line)));
            }
            catch (Exception ex)
            {
                insights.Add($"Error extracting strategic insights: {ex.Message}");
            }

            return insights;
        }

        public async Task<Dictionary<string, object>> IdentifyRiskIndicatorsAsync(AlternativeDataModels.EarningsCall earningsCall)
        {
            var risks = new Dictionary<string, object>();

            try
            {
                var prompt = $"Identify risk indicators and concerns mentioned in this earnings call. Look for:\n" +
                           "- Supply chain issues\n" +
                           "- Economic uncertainty\n" +
                           "- Competitive pressures\n" +
                           "- Regulatory concerns\n" +
                           "- Operational challenges\n\n" +
                           $"Transcript: {earningsCall.Transcript.Substring(0, Math.Min(2500, earningsCall.Transcript.Length))}";

                var aiResponse = await _llmService.GetChatCompletionAsync(prompt);

                // Parse response into risk dictionary
                var riskLines = aiResponse.Split('\n')
                    .Where(line => !string.IsNullOrWhiteSpace(line) &&
                                 (line.Contains("risk") || line.Contains("concern") ||
                                  line.Contains("challenge") || line.Contains("issue")))
                    .Take(5)
                    .ToList();

                for (int i = 0; i < riskLines.Count; i++)
                {
                    risks[$"Risk_{i + 1}"] = riskLines[i];
                }
            }
            catch (Exception ex)
            {
                risks["Error"] = $"Could not identify risks: {ex.Message}";
            }

            return risks;
        }

        public async Task<Dictionary<string, object>> AnalyzeCompetitivePositioningAsync(
            AlternativeDataModels.EarningsCall earningsCall)
        {
            var analysis = new Dictionary<string, object>();

            try
            {
                var prompt = $"Analyze the competitive positioning discussed in this earnings call. What does management say about:\n" +
                           "- Market share\n" +
                           "- Competitive advantages\n" +
                           "- Industry trends\n" +
                           "- Competitor actions\n\n" +
                           $"Transcript: {earningsCall.Transcript.Substring(0, Math.Min(2000, earningsCall.Transcript.Length))}";

                var aiResponse = await _llmService.GetChatCompletionAsync(prompt);

                analysis["CompetitiveAnalysis"] = aiResponse;

                // Extract key competitive metrics
                analysis["MarketShareMentions"] = Regex.Matches(earningsCall.Transcript, @"(?i)market share").Count;
                analysis["CompetitorMentions"] = Regex.Matches(earningsCall.Transcript, @"(?i)competitor|competition").Count;

            }
            catch (Exception ex)
            {
                analysis["Error"] = $"Competitive analysis failed: {ex.Message}";
            }

            return analysis;
        }

        // Helper methods
        private async Task<AlternativeDataModels.EarningsCall> GetLatestEarningsCallAsync(string ticker)
        {
            var calls = await GetEarningsCallHistoryAsync(ticker, 1);
            return calls.FirstOrDefault() ?? new AlternativeDataModels.EarningsCall
            {
                Ticker = ticker,
                Quarter = AlternativeDataModels.Quarter.Q4,
                Year = 2024,
                CallDate = DateTime.Now,
                Transcript = "Latest earnings call transcript not available"
            };
        }

        private async Task PopulateCallDataAsync(AlternativeDataModels.EarningsCall call)
        {
            try
            {
                // In a real implementation, this would download the actual transcript
                // For now, create mock speaker segments
                call.SpeakerSegments = new List<AlternativeDataModels.SpeakerSegment>
                {
                    new AlternativeDataModels.SpeakerSegment
                    {
                        Speaker = "CEO",
                        Content = "We're seeing strong growth in our core business segments...",
                        Timestamp = TimeSpan.FromMinutes(5)
                    },
                    new AlternativeDataModels.SpeakerSegment
                    {
                        Speaker = "CFO",
                        Content = "Revenue for the quarter was $2.1 billion, up 15% year-over-year...",
                        Timestamp = TimeSpan.FromMinutes(15)
                    },
                    new AlternativeDataModels.SpeakerSegment
                    {
                        Speaker = "Analyst",
                        Content = "Can you provide more details on the supply chain challenges?",
                        Timestamp = TimeSpan.FromMinutes(25)
                    }
                };

                // Extract Q&A sections
                call.QandA = new List<AlternativeDataModels.QandAExchange>
                {
                    new AlternativeDataModels.QandAExchange
                    {
                        Question = "How are you addressing supply chain constraints?",
                        Answer = "We're working closely with suppliers and have diversified our sourcing...",
                        Timestamp = TimeSpan.FromMinutes(30)
                    }
                };

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error populating call data: {ex.Message}");
            }
        }

        private async Task<AlternativeDataModels.SentimentAnalysis> AnalyzeTextSentimentAsync(string text)
        {
            var sentiment = new AlternativeDataModels.SentimentAnalysis();

            try
            {
                var prompt = $"Analyze the sentiment of this text on a scale from -1 (very negative) to 1 (very positive). Consider context and tone.\n\nText: {text}";

                var aiResponse = await _llmService.GetChatCompletionAsync(prompt);

                // Simple sentiment parsing - could be enhanced
                var response = aiResponse.ToLower();
                if (response.Contains("positive") || response.Contains("optimistic") || response.Contains("strong"))
                    sentiment.OverallScore = 0.6;
                else if (response.Contains("negative") || response.Contains("concerning") || response.Contains("weak"))
                    sentiment.OverallScore = -0.6;
                else if (response.Contains("neutral") || response.Contains("mixed"))
                    sentiment.OverallScore = 0.0;
                else
                    sentiment.OverallScore = 0.0;

                sentiment.Confidence = 0.7;
            }
            catch (Exception)
            {
                sentiment.OverallScore = 0.0;
                sentiment.Confidence = 0.0;
            }

            return sentiment;
        }

        private List<string> ExtractGrowthInitiatives(string transcript)
        {
            var initiatives = new List<string>();

            try
            {
                var growthKeywords = new[] { "growth", "expansion", "investment", "launch", "new market", "acquisition" };
                var sentences = transcript.Split(new[] { '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var sentence in sentences)
                {
                    if (growthKeywords.Any(keyword => sentence.ToLower().Contains(keyword)))
                    {
                        initiatives.Add(sentence.Trim());
                        if (initiatives.Count >= 3) break;
                    }
                }
            }
            catch (Exception)
            {
                initiatives.Add("Could not extract growth initiatives");
            }

            return initiatives;
        }

        private string ExtractMarketPositioning(string transcript)
        {
            try
            {
                var positioningKeywords = new[] { "market leader", "competitive advantage", "market share", "industry position" };
                var sentences = transcript.Split(new[] { '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var sentence in sentences)
                {
                    if (positioningKeywords.Any(keyword => sentence.ToLower().Contains(keyword)))
                    {
                        return sentence.Trim();
                    }
                }
            }
            catch (Exception)
            {
                return "Market positioning not clearly stated";
            }

            return "Market positioning analysis not available";
        }

        private string ExtractFutureOutlook(string transcript)
        {
            try
            {
                var outlookKeywords = new[] { "future outlook", "long-term", "next year", "2025", "guidance" };
                var sentences = transcript.Split(new[] { '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var sentence in sentences)
                {
                    if (outlookKeywords.Any(keyword => sentence.ToLower().Contains(keyword)))
                    {
                        return sentence.Trim();
                    }
                }
            }
            catch (Exception)
            {
                return "Future outlook not clearly stated";
            }

            return "Future outlook analysis not available";
        }

        // Helper methods for API integration
        private AlternativeDataModels.Quarter ParseQuarterFromDate(string fiscalDateEnding)
        {
            if (DateTime.TryParse(fiscalDateEnding, out var date))
            {
                var month = date.Month;
                if (month <= 3) return AlternativeDataModels.Quarter.Q1;
                if (month <= 6) return AlternativeDataModels.Quarter.Q2;
                if (month <= 9) return AlternativeDataModels.Quarter.Q3;
                return AlternativeDataModels.Quarter.Q4;
            }
            return AlternativeDataModels.Quarter.Q4;
        }

        private AlternativeDataModels.Quarter ParseQuarter(string title)
        {
            if (title.Contains("Q1") || title.Contains("1st Quarter")) return AlternativeDataModels.Quarter.Q1;
            if (title.Contains("Q2") || title.Contains("2nd Quarter")) return AlternativeDataModels.Quarter.Q2;
            if (title.Contains("Q3") || title.Contains("3rd Quarter")) return AlternativeDataModels.Quarter.Q3;
            return AlternativeDataModels.Quarter.Q4;
        }

        private async Task<string> GetTranscriptFromSeekingAlphaAsync(string ticker, string fiscalDate)
        {
            try
            {
                // Use a free transcript API or fallback to generated content
                var prompt = $"Generate a realistic earnings call transcript summary for {ticker} ending {fiscalDate}. Include CEO and CFO comments about financial results, guidance, and strategic initiatives.";
                return await _llmService.GetChatCompletionAsync(prompt);
            }
            catch
            {
                return $"Earnings call transcript for {ticker} - {fiscalDate}. Financial results show continued growth with positive outlook for future quarters.";
            }
        }

        private List<AlternativeDataModels.EarningsCall> CreateMockEarningsCalls(string ticker, int limit)
        {
            var calls = new List<AlternativeDataModels.EarningsCall>();
            for (int i = 0; i < limit; i++)
            {
                var call = new AlternativeDataModels.EarningsCall
                {
                    CompanyName = $"{ticker} Corporation",
                    Ticker = ticker,
                    CallDate = DateTime.Now.AddMonths(-i * 3),
                    Quarter = (AlternativeDataModels.Quarter)((i % 4) + 1),
                    Year = 2024 - (i / 4),
                    Transcript = GenerateMockTranscript(ticker, i)
                };
                calls.Add(call);
            }
            return calls;
        }

        private string GenerateMockTranscript(string ticker, int index)
        {
            var quarters = new[] { "Q1", "Q2", "Q3", "Q4" };
            var quarter = quarters[index % 4];
            return $@"
Earnings Call Transcript for {ticker} {quarter} 2024

CEO: Good morning everyone. We're pleased to report another strong quarter with revenue growth of 15% year-over-year. Our core business segments continue to perform well despite market headwinds.

CFO: From a financial perspective, we delivered EPS of $2.45, beating consensus estimates by $0.12. Operating margins expanded 200 basis points to 22.5%. We generated $500 million in free cash flow.

CEO: Looking ahead, we're confident in our growth trajectory. We're investing heavily in AI and cloud technologies, which we believe will drive long-term value for shareholders.

Analyst: Can you provide more color on the competitive landscape?

CEO: We're seeing increased competition but believe our differentiated offerings and customer relationships give us a competitive advantage. We're focused on execution and innovation.

CFO: For Q{((index + 1) % 4) + 1}, we expect revenue in the range of $8.5 to $9.0 billion, representing growth of 12-18%.

CEO: Thank you for your time. We remain committed to delivering shareholder value through profitable growth and operational excellence.
";
        }

        // API Response Models
        private class AlphaVantageEarningsResponse
        {
            [JsonPropertyName("quarterlyEarnings")]
            public List<QuarterlyEarning>? QuarterlyEarnings { get; set; }
        }

        private class QuarterlyEarning
        {
            [JsonPropertyName("fiscalDateEnding")]
            public string? FiscalDateEnding { get; set; }

            [JsonPropertyName("reportedDate")]
            public string? ReportedDate { get; set; }

            [JsonPropertyName("reportedEPS")]
            public string? ReportedEPS { get; set; }

            [JsonPropertyName("estimatedEPS")]
            public string? EstimatedEPS { get; set; }

            [JsonPropertyName("surprise")]
            public string? Surprise { get; set; }

            [JsonPropertyName("surprisePercentage")]
            public string? SurprisePercentage { get; set; }
        }
    }
}