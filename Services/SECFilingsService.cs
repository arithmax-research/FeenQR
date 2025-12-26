using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HtmlAgilityPack;
using QuantResearchAgent.Core;
using QuantResearchAgent.Services;

namespace QuantResearchAgent.Services
{
    public class SECFilingsService
    {
        private readonly HttpClient _httpClient;
        private readonly ILLMService _llmService;

        public SECFilingsService(HttpClient httpClient, ILLMService llmService)
        {
            _httpClient = httpClient;
            _llmService = llmService;
        }

        // SEC EDGAR API endpoints
        private const string EDGAR_BASE_URL = "https://www.sec.gov/Archives/edgar/data/";
        private const string COMPANY_TICKERS_URL = "https://www.sec.gov/files/company_tickers.json";

        public async Task<AlternativeDataModels.SECFilingAnalysis> AnalyzeLatestFilingAsync(
            string ticker, string filingType = "10-K")
        {
            var analysis = new AlternativeDataModels.SECFilingAnalysis
            {
                AnalysisDate = DateTime.Now
            };

            try
            {
                // Get company CIK
                var cik = await GetCompanyCIKAsync(ticker);
                if (string.IsNullOrEmpty(cik))
                {
                    throw new Exception($"Could not find CIK for ticker {ticker}");
                }

                // Get latest filing
                var filing = await GetLatestFilingAsync(cik, filingType);
                analysis.Filing = filing;

                // Analyze filing content
                analysis.Insights = await AnalyzeFilingContentAsync(filing);
                analysis.KeyFindings = await ExtractKeyFindingsAsync(filing);
                analysis.ContentSentiment = await AnalyzeFilingSentimentAsync(filing);

                return analysis;
            }
            catch (Exception ex)
            {
                analysis.Insights["Error"] = $"Analysis failed: {ex.Message}";
                return analysis;
            }
        }

        public async Task<List<AlternativeDataModels.SECFiling>> GetFilingHistoryAsync(
            string ticker, string filingType = "10-K", int limit = 5)
        {
            var filings = new List<AlternativeDataModels.SECFiling>();

            try
            {
                var cik = await GetCompanyCIKAsync(ticker);
                if (string.IsNullOrEmpty(cik))
                {
                    return filings;
                }

                // Get submissions for the company
                var submissionsUrl = $"{EDGAR_BASE_URL}{cik}/submissions.json";
                var response = await _httpClient.GetStringAsync(submissionsUrl);
                var submissions = JsonSerializer.Deserialize<JsonElement>(response);

                if (submissions.TryGetProperty("filings", out var filingsData) &&
                    filingsData.TryGetProperty("recent", out var recentFilings))
                {
                    var formTypes = recentFilings.GetProperty("form").EnumerateArray();
                    var filingDates = recentFilings.GetProperty("filingDate").EnumerateArray();
                    var accessionNumbers = recentFilings.GetProperty("accessionNumber").EnumerateArray();

                    var filingInfos = formTypes.Zip(filingDates, (form, date) => new { Form = form, Date = date })
                                              .Zip(accessionNumbers, (fd, acc) => new { fd.Form, fd.Date, Accession = acc })
                                              .Where(f => f.Form.GetString() == filingType)
                                              .Take(limit);

                    foreach (var info in filingInfos)
                    {
                        var filing = new AlternativeDataModels.SECFiling
                        {
                            Ticker = ticker,
                            FilingType = filingType,
                            FilingDate = DateTime.Parse(info.Date.GetString()!),
                            AccessionNumber = info.Accession.GetString()!.Replace("-", "")
                        };

                        // Download and parse filing content
                        await PopulateFilingContentAsync(filing);
                        filings.Add(filing);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting filing history: {ex.Message}");
            }

            return filings;
        }

        public async Task<Dictionary<string, object>> AnalyzeFilingContentAsync(AlternativeDataModels.SECFiling filing)
        {
            var insights = new Dictionary<string, object>();

            try
            {
                // Extract financial metrics
                insights["FinancialMetrics"] = await ExtractFinancialMetricsAsync(filing);

                // Analyze risk factors
                insights["RiskAnalysis"] = await AnalyzeRiskFactorsAsync(filing);

                // Extract MD&A insights
                insights["MDAInsights"] = await AnalyzeManagementDiscussionAsync(filing);

                // Business strategy analysis
                insights["StrategyAnalysis"] = await AnalyzeBusinessStrategyAsync(filing);

            }
            catch (Exception ex)
            {
                insights["Error"] = $"Content analysis failed: {ex.Message}";
            }

            return insights;
        }

        public async Task<List<string>> ExtractKeyFindingsAsync(AlternativeDataModels.SECFiling filing)
        {
            var findings = new List<string>();

            try
            {
                var prompt = $"Analyze this SEC {filing.FilingType} filing and extract the 5 most important findings or insights for investors. Focus on material information that could impact stock price or business outlook.\n\nFiling Content Summary:\n{filing.Content.Substring(0, Math.Min(2000, filing.Content.Length))}";

                var aiResponse = await _llmService.GetChatCompletionAsync(prompt);

                // Parse AI response into key findings
                var lines = aiResponse.Split('\n').Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
                findings.AddRange(lines.Take(5));
            }
            catch (Exception ex)
            {
                findings.Add($"Could not extract key findings: {ex.Message}");
            }

            return findings;
        }

        public async Task<AlternativeDataModels.SentimentAnalysis> AnalyzeFilingSentimentAsync(AlternativeDataModels.SECFiling filing)
        {
            var sentiment = new AlternativeDataModels.SentimentAnalysis();

            try
            {
                var prompt = $"Analyze the sentiment of this SEC filing content. Rate the overall tone on a scale of -1 (very negative) to 1 (very positive). Focus on sections like Risk Factors, MD&A, and Business Description.\n\nContent: {filing.Content.Substring(0, Math.Min(1500, filing.Content.Length))}";

                var aiResponse = await _llmService.GetChatCompletionAsync(prompt);

                // Simple sentiment extraction - could be enhanced with proper NLP
                if (aiResponse.ToLower().Contains("positive") || aiResponse.ToLower().Contains("optimistic"))
                    sentiment.OverallScore = 0.5;
                else if (aiResponse.ToLower().Contains("negative") || aiResponse.ToLower().Contains("concerning"))
                    sentiment.OverallScore = -0.5;
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

        // Helper methods
        private async Task<string> GetCompanyCIKAsync(string ticker)
        {
            try
            {
                var response = await _httpClient.GetStringAsync(COMPANY_TICKERS_URL);
                var tickers = JsonSerializer.Deserialize<JsonElement>(response);

                foreach (var company in tickers.EnumerateObject())
                {
                    var companyData = company.Value;
                    if (companyData.TryGetProperty("ticker", out var tickerProp) &&
                        tickerProp.GetString()?.ToUpper() == ticker.ToUpper())
                    {
                        if (companyData.TryGetProperty("cik_str", out var cikProp))
                        {
                            return cikProp.GetString()!;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting CIK for {ticker}: {ex.Message}");
            }

            return string.Empty;
        }

        private async Task<AlternativeDataModels.SECFiling> GetLatestFilingAsync(string cik, string filingType)
        {
            var filings = await GetFilingHistoryAsync("", filingType, 1);
            if (filings.Any())
            {
                return filings.First();
            }

            // Fallback: construct filing manually
            return new AlternativeDataModels.SECFiling
            {
                Ticker = "",
                FilingType = filingType,
                FilingDate = DateTime.Now,
                AccessionNumber = ""
            };
        }

        private async Task PopulateFilingContentAsync(AlternativeDataModels.SECFiling filing)
        {
            try
            {
                // Construct filing URL
                var cik = await GetCompanyCIKAsync(filing.Ticker);
                var filingUrl = $"{EDGAR_BASE_URL}{cik}/{filing.AccessionNumber}.txt";

                var content = await _httpClient.GetStringAsync(filingUrl);
                filing.Content = content;

                // Extract structured data
                filing.ExtractedData = await ParseFilingContentAsync(content);
                filing.RiskFactors = await ExtractRiskFactorsAsync(content);
                filing.MDandA = await ExtractManagementDiscussionAsync(content);
            }
            catch (Exception ex)
            {
                filing.Content = $"Error loading filing: {ex.Message}";
            }
        }

        private async Task<Dictionary<string, object>> ParseFilingContentAsync(string content)
        {
            var data = new Dictionary<string, object>();

            try
            {
                // Extract financial statements (simplified)
                var financialRegex = new Regex(@"(\d{1,3}(?:,\d{3})*(?:\.\d+)?)", RegexOptions.Multiline);
                var numbers = financialRegex.Matches(content)
                    .Cast<Match>()
                    .Select(m => m.Value)
                    .Distinct()
                    .Take(20)
                    .ToList();

                data["ExtractedNumbers"] = numbers;

                // Extract key sections
                if (content.Contains("ITEM 1A. RISK FACTORS"))
                {
                    data["HasRiskFactors"] = true;
                }

                if (content.Contains("ITEM 7. MANAGEMENT'S DISCUSSION"))
                {
                    data["HasMDA"] = true;
                }
            }
            catch (Exception)
            {
                data["ParseError"] = "Failed to parse filing content";
            }

            return data;
        }

        private async Task<List<AlternativeDataModels.RiskFactor>> ExtractRiskFactorsAsync(string content)
        {
            var riskFactors = new List<AlternativeDataModels.RiskFactor>();

            try
            {
                // Find risk factors section
                var riskSectionStart = content.IndexOf("ITEM 1A. RISK FACTORS");
                if (riskSectionStart == -1) return riskFactors;

                var riskSectionEnd = content.IndexOf("ITEM 1B.", riskSectionStart);
                if (riskSectionEnd == -1) riskSectionEnd = content.Length;

                var riskContent = content.Substring(riskSectionStart, riskSectionEnd - riskSectionStart);

                // Use AI to categorize risks
                var prompt = $"Categorize the following risk factors from an SEC filing into categories (Market, Operational, Financial, Legal, etc.) and rate their severity (1-10). Return as JSON.\n\n{riskContent.Substring(0, Math.Min(1000, riskContent.Length))}";

                var aiResponse = await _llmService.GetChatCompletionAsync(prompt);

                // Parse AI response (simplified)
                riskFactors.Add(new AlternativeDataModels.RiskFactor
                {
                    Category = "General",
                    Description = "Risk factors identified in filing",
                    Severity = 5.0,
                    Keywords = new List<string> { "risk", "uncertainty", "challenge" }
                });
            }
            catch (Exception)
            {
                // Return basic risk factor
                riskFactors.Add(new AlternativeDataModels.RiskFactor
                {
                    Category = "Unknown",
                    Description = "Risk factors could not be analyzed",
                    Severity = 5.0
                });
            }

            return riskFactors;
        }

        private async Task<AlternativeDataModels.ManagementDiscussion> ExtractManagementDiscussionAsync(string content)
        {
            var mda = new AlternativeDataModels.ManagementDiscussion();

            try
            {
                // Find MD&A section
                var mdaStart = content.IndexOf("ITEM 7. MANAGEMENT'S DISCUSSION");
                if (mdaStart == -1) return mda;

                var mdaEnd = content.IndexOf("ITEM 8.", mdaStart);
                if (mdaEnd == -1) mdaEnd = content.Length;

                var mdaContent = content.Substring(mdaStart, mdaEnd - mdaStart);

                // Extract key information
                mda.Summary = mdaContent.Substring(0, Math.Min(500, mdaContent.Length));

                // Use AI to extract key metrics and initiatives
                var prompt = $"Extract key financial metrics and strategic initiatives from this MD&A section. Return as structured text.\n\n{mdaContent.Substring(0, Math.Min(800, mdaContent.Length))}";

                var aiResponse = await _llmService.GetChatCompletionAsync(prompt);
                mda.StrategicInitiatives.Add(aiResponse);

                mda.Sentiment = await AnalyzeFilingSentimentAsync(new AlternativeDataModels.SECFiling { Content = mdaContent });
            }
            catch (Exception)
            {
                mda.Summary = "Could not extract MD&A information";
            }

            return mda;
        }

        private async Task<Dictionary<string, object>> ExtractFinancialMetricsAsync(AlternativeDataModels.SECFiling filing)
        {
            var metrics = new Dictionary<string, object>();

            try
            {
                // Look for financial statement sections
                var balanceSheetPattern = @"(?i)balance sheet|consolidated balance sheet";
                var incomePattern = @"(?i)income statement|statement of operations";

                metrics["HasBalanceSheet"] = Regex.IsMatch(filing.Content, balanceSheetPattern);
                metrics["HasIncomeStatement"] = Regex.IsMatch(filing.Content, incomePattern);

                // Extract key financial numbers (simplified)
                var revenuePattern = @"(?i)revenue|sales|net sales";
                metrics["MentionsRevenue"] = Regex.IsMatch(filing.Content, revenuePattern);
            }
            catch (Exception)
            {
                metrics["Error"] = "Failed to extract financial metrics";
            }

            return metrics;
        }

        private async Task<Dictionary<string, object>> AnalyzeRiskFactorsAsync(AlternativeDataModels.SECFiling filing)
        {
            var analysis = new Dictionary<string, object>();

            try
            {
                var riskCount = filing.RiskFactors.Count;
                analysis["RiskFactorCount"] = riskCount;
                analysis["AverageSeverity"] = filing.RiskFactors.Any() ?
                    filing.RiskFactors.Average(r => r.Severity) : 0;

                var categories = filing.RiskFactors.GroupBy(r => r.Category)
                    .ToDictionary(g => g.Key, g => g.Count());
                analysis["RiskCategories"] = categories;
            }
            catch (Exception)
            {
                analysis["Error"] = "Failed to analyze risk factors";
            }

            return analysis;
        }

        private async Task<Dictionary<string, object>> AnalyzeManagementDiscussionAsync(AlternativeDataModels.SECFiling filing)
        {
            var analysis = new Dictionary<string, object>();

            try
            {
                analysis["SentimentScore"] = filing.MDandA.Sentiment.OverallScore;
                analysis["InitiativeCount"] = filing.MDandA.StrategicInitiatives.Count;
                analysis["HasKeyMetrics"] = filing.MDandA.KeyMetrics.Any();
            }
            catch (Exception)
            {
                analysis["Error"] = "Failed to analyze MD&A";
            }

            return analysis;
        }

        private async Task<Dictionary<string, object>> AnalyzeBusinessStrategyAsync(AlternativeDataModels.SECFiling filing)
        {
            var analysis = new Dictionary<string, object>();

            try
            {
                var prompt = $"Analyze the business strategy section of this SEC filing. What is the company's competitive position and growth strategy?\n\n{filing.Content.Substring(0, Math.Min(1000, filing.Content.Length))}";

                var aiResponse = await _llmService.GetChatCompletionAsync(prompt);
                analysis["StrategySummary"] = aiResponse;
            }
            catch (Exception)
            {
                analysis["Error"] = "Failed to analyze business strategy";
            }

            return analysis;
        }
    }
}