using Microsoft.Extensions.Logging;
using HtmlAgilityPack;
using System.Text.Json;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using Microsoft.SemanticKernel;

namespace QuantResearchAgent.Services;

public class WebDataExtractionService
{
    private readonly ILogger<WebDataExtractionService> _logger;
    private readonly HttpClient _httpClient;
    private readonly Kernel _kernel;

    public WebDataExtractionService(ILogger<WebDataExtractionService> logger, HttpClient httpClient, Kernel kernel)
    {
        _logger = logger;
        _httpClient = httpClient;
        _kernel = kernel;
        
        // Set User-Agent to avoid 403 Forbidden errors
        if (!_httpClient.DefaultRequestHeaders.UserAgent.Any())
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", 
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
        }
    }

    public async Task<WebDataExtractionResult> ExtractStructuredDataAsync(string url, string? dataType = null)
    {
        try
        {
            _logger.LogInformation("Extracting structured data from {Url}", url);

            // Check if this is a PDF by URL pattern or by making a HEAD request
            if (await IsPdfUrl(url))
            {
                return await ExtractPdfDataAsync(url);
            }
            
            // Make HTTP request with timeout
            using var response = await _httpClient.GetAsync(url);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to fetch {Url}: {StatusCode} {ReasonPhrase}", url, response.StatusCode, response.ReasonPhrase);
                return new WebDataExtractionResult
                {
                    Url = url,
                    ExtractedAt = DateTime.UtcNow,
                    DataType = "ERROR",
                    Title = "Failed to load",
                    Content = $"HTTP {response.StatusCode}: {response.ReasonPhrase}",
                    Metadata = new Dictionary<string, string> { { "error", response.ReasonPhrase ?? "Unknown error" } },
                    StructuredData = new Dictionary<string, object>(),
                    FinancialData = new Dictionary<string, object>(),
                    Tables = new List<TableData>(),
                    Links = new List<string>()
                };
            }
            
            var html = await response.Content.ReadAsStringAsync();
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var result = new WebDataExtractionResult
            {
                Url = url,
                ExtractedAt = DateTime.UtcNow,
                DataType = DetermineDataType(url, dataType),
                Title = ExtractTitle(doc),
                Content = ExtractMainContent(doc),
                Metadata = ExtractMetadata(doc),
                StructuredData = ExtractStructuredData(doc, url),
                FinancialData = ExtractFinancialData(doc, url),
                Tables = ExtractTables(doc),
                Links = ExtractRelevantLinks(doc, url)
            };

            _logger.LogInformation("Successfully extracted data from {Url}. Found {TableCount} tables, {DataCount} structured data points", 
                url, result.Tables.Count, result.StructuredData.Count);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract data from {Url}", url);
            throw;
        }
    }

    public async Task<List<WebDataExtractionResult>> ExtractEarningsReportDataAsync(string symbol, int quarters = 4)
    {
        try
        {
            _logger.LogInformation("Extracting earnings report data for {Symbol} over {Quarters} quarters", symbol, quarters);

            var results = new List<WebDataExtractionResult>();

            // SEC EDGAR API for 10-Q and 10-K filings
            var edgarUrl = $"https://data.sec.gov/submissions/CIK{GetCikForSymbol(symbol)}.json";
            var edgarResult = await ExtractStructuredDataAsync(edgarUrl, "SEC_FILING");
            if (edgarResult != null) results.Add(edgarResult);

            // Yahoo Finance earnings page
            var yahooUrl = $"https://finance.yahoo.com/quote/{symbol}/financials";
            var yahooResult = await ExtractStructuredDataAsync(yahooUrl, "EARNINGS_REPORT");
            if (yahooResult != null) results.Add(yahooResult);

            // MarketWatch earnings page
            var marketWatchUrl = $"https://www.marketwatch.com/investing/stock/{symbol}/financials";
            var marketWatchResult = await ExtractStructuredDataAsync(marketWatchUrl, "FINANCIAL_STATEMENTS");
            if (marketWatchResult != null) results.Add(marketWatchResult);

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract earnings data for {Symbol}", symbol);
            return new List<WebDataExtractionResult>();
        }
    }

    public async Task<List<WebDataExtractionResult>> ExtractSecFilingsAsync(string symbol, string filingType = "10-K")
    {
        try
        {
            _logger.LogInformation("Extracting SEC {FilingType} filings for {Symbol}", filingType, symbol);

            var results = new List<WebDataExtractionResult>();
            var cik = GetCikForSymbol(symbol);
            
            if (!string.IsNullOrEmpty(cik))
            {
                var edgarUrl = $"https://www.sec.gov/cgi-bin/browse-edgar?action=getcompany&CIK={cik}&type={filingType}&dateb=&owner=exclude&count=10";
                var result = await ExtractStructuredDataAsync(edgarUrl, "SEC_FILING");
                if (result != null) results.Add(result);
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract SEC filings for {Symbol}", symbol);
            return new List<WebDataExtractionResult>();
        }
    }

    private string DetermineDataType(string url, string? explicitType)
    {
        if (!string.IsNullOrEmpty(explicitType)) return explicitType;

        return url.ToLower() switch
        {
            var u when u.Contains("sec.gov") => "SEC_FILING",
            var u when u.Contains("edgar") => "SEC_FILING",
            var u when u.Contains("earnings") => "EARNINGS_REPORT",
            var u when u.Contains("financials") => "FINANCIAL_STATEMENTS",
            var u when u.Contains("10-k") || u.Contains("10-q") => "SEC_FILING",
            var u when u.Contains("annual-report") => "ANNUAL_REPORT",
            var u when u.Contains("investor") => "INVESTOR_RELATIONS",
            _ => "GENERAL_WEB_DATA"
        };
    }

    private string ExtractTitle(HtmlDocument doc)
    {
        return doc.DocumentNode.SelectSingleNode("//title")?.InnerText?.Trim() ?? "";
    }

    private string ExtractMainContent(HtmlDocument doc)
    {
        var contentSelectors = new[]
        {
            "//main", "//article", "//div[@class*='content']", 
            "//div[@id*='content']", "//div[@class*='body']", "//body"
        };

        foreach (var selector in contentSelectors)
        {
            var contentNode = doc.DocumentNode.SelectSingleNode(selector);
            if (contentNode != null)
            {
                return CleanText(contentNode.InnerText);
            }
        }

        return CleanText(doc.DocumentNode.InnerText);
    }

    private Dictionary<string, string> ExtractMetadata(HtmlDocument doc)
    {
        var metadata = new Dictionary<string, string>();

        // Extract meta tags - split into separate queries to avoid XPath issues
        var nameMetaTags = doc.DocumentNode.SelectNodes("//meta[@name]");
        var propertyMetaTags = doc.DocumentNode.SelectNodes("//meta[@property]");
        
        if (nameMetaTags != null)
        {
            foreach (var tag in nameMetaTags)
            {
                var name = tag.GetAttributeValue("name", "");
                var content = tag.GetAttributeValue("content", "");
                if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(content))
                {
                    metadata[name] = content;
                }
            }
        }
        
        if (propertyMetaTags != null)
        {
            foreach (var tag in propertyMetaTags)
            {
                var property = tag.GetAttributeValue("property", "");
                var content = tag.GetAttributeValue("content", "");
                if (!string.IsNullOrEmpty(property) && !string.IsNullOrEmpty(content))
                {
                    metadata[property] = content;
                }
            }
        }

        return metadata;
    }

    private Dictionary<string, object> ExtractStructuredData(HtmlDocument doc, string url)
    {
        var structuredData = new Dictionary<string, object>();

        try
        {
            // Extract JSON-LD structured data
            var jsonLdNodes = doc.DocumentNode.SelectNodes("//script[@type='application/ld+json']");
            if (jsonLdNodes != null)
            {
                foreach (var node in jsonLdNodes)
                {
                    try
                    {
                        var jsonContent = node.InnerText;
                        var parsedJson = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonContent);
                        if (parsedJson != null)
                        {
                            foreach (var kvp in parsedJson)
                            {
                                structuredData[$"jsonld_{kvp.Key}"] = kvp.Value;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("Failed to parse JSON-LD: {Error}", ex.Message);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to extract JSON-LD data: {Error}", ex.Message);
        }

        try
        {
            // Extract microdata
            var microdataNodes = doc.DocumentNode.SelectNodes("//*[@itemscope]");
            if (microdataNodes != null)
            {
                foreach (var node in microdataNodes)
                {
                    var itemType = node.GetAttributeValue("itemtype", "");
                    if (!string.IsNullOrEmpty(itemType))
                    {
                        structuredData[$"microdata_{itemType}"] = ExtractMicrodataProperties(node);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to extract microdata: {Error}", ex.Message);
        }

        return structuredData;
    }

    private Dictionary<string, object> ExtractFinancialData(HtmlDocument doc, string url)
    {
        var financialData = new Dictionary<string, object>();

        // Look for common financial data patterns
        var patterns = new Dictionary<string, Regex>
        {
            ["revenue"] = new Regex(@"\$?(\d{1,3}(?:,\d{3})*(?:\.\d{2})?)\s*(?:million|billion|M|B)?\s*(?:revenue|sales)", RegexOptions.IgnoreCase),
            ["net_income"] = new Regex(@"\$?(\d{1,3}(?:,\d{3})*(?:\.\d{2})?)\s*(?:million|billion|M|B)?\s*(?:net income|profit)", RegexOptions.IgnoreCase),
            ["eps"] = new Regex(@"(?:EPS|earnings per share)[\s:]*\$?(\d+\.\d{2})", RegexOptions.IgnoreCase),
            ["market_cap"] = new Regex(@"(?:market cap|market capitalization)[\s:]*\$?(\d{1,3}(?:,\d{3})*(?:\.\d{2})?)\s*(?:million|billion|M|B)?", RegexOptions.IgnoreCase)
        };

        var text = doc.DocumentNode.InnerText;
        foreach (var pattern in patterns)
        {
            var matches = pattern.Value.Matches(text);
            if (matches.Count > 0)
            {
                financialData[pattern.Key] = matches[0].Groups[1].Value;
            }
        }

        return financialData;
    }

    private List<TableData> ExtractTables(HtmlDocument doc)
    {
        var tables = new List<TableData>();
        var tableNodes = doc.DocumentNode.SelectNodes("//table");

        if (tableNodes != null)
        {
            foreach (var tableNode in tableNodes)
            {
                var tableData = new TableData
                {
                    Headers = new List<string>(),
                    Rows = new List<List<string>>()
                };

                // Extract headers
                var headerNodes = tableNode.SelectNodes(".//th");
                if (headerNodes != null)
                {
                    tableData.Headers = headerNodes.Select(h => CleanText(h.InnerText)).ToList();
                }

                // Extract rows
                var rowNodes = tableNode.SelectNodes(".//tr");
                if (rowNodes != null)
                {
                    foreach (var rowNode in rowNodes)
                    {
                        var cellNodes = rowNode.SelectNodes(".//td");
                        if (cellNodes != null)
                        {
                            var rowData = cellNodes.Select(c => CleanText(c.InnerText)).ToList();
                            if (rowData.Any(cell => !string.IsNullOrWhiteSpace(cell)))
                            {
                                tableData.Rows.Add(rowData);
                            }
                        }
                    }
                }

                if (tableData.Headers.Any() || tableData.Rows.Any())
                {
                    tables.Add(tableData);
                }
            }
        }

        return tables;
    }

    private List<string> ExtractRelevantLinks(HtmlDocument doc, string baseUrl)
    {
        var links = new List<string>();
        var linkNodes = doc.DocumentNode.SelectNodes("//a[@href]");

        if (linkNodes != null)
        {
            foreach (var linkNode in linkNodes)
            {
                var href = linkNode.GetAttributeValue("href", "");
                if (!string.IsNullOrEmpty(href))
                {
                    try
                    {
                        var uri = new Uri(new Uri(baseUrl), href);
                        var linkText = CleanText(linkNode.InnerText).ToLower();
                        
                        // Filter for relevant financial links
                        if (IsRelevantFinancialLink(linkText, uri.ToString()))
                        {
                            links.Add(uri.ToString());
                        }
                    }
                    catch (Exception)
                    {
                        // Ignore invalid URLs
                    }
                }
            }
        }

        return links.Distinct().ToList();
    }

    private bool IsRelevantFinancialLink(string linkText, string url)
    {
        var relevantKeywords = new[]
        {
            "earnings", "financial", "annual report", "10-k", "10-q", "sec filing",
            "investor", "quarterly", "balance sheet", "income statement", "cash flow"
        };

        return relevantKeywords.Any(keyword => 
            linkText.Contains(keyword) || url.ToLower().Contains(keyword));
    }

    private Dictionary<string, object> ExtractMicrodataProperties(HtmlNode node)
    {
        var properties = new Dictionary<string, object>();
        var propNodes = node.SelectNodes(".//*[@itemprop]");

        if (propNodes != null)
        {
            foreach (var propNode in propNodes)
            {
                var propName = propNode.GetAttributeValue("itemprop", "");
                var propValue = propNode.InnerText.Trim();
                
                if (!string.IsNullOrEmpty(propName) && !string.IsNullOrEmpty(propValue))
                {
                    properties[propName] = propValue;
                }
            }
        }

        return properties;
    }

    private string GetCikForSymbol(string symbol)
    {
        try
        {
            // This would typically require a lookup service or API
            // For now, return empty string as placeholder
            return "";
        }
        catch
        {
            return "";
        }
    }

    private string CleanText(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        
        return Regex.Replace(text, @"\s+", " ").Trim();
    }

    private async Task<bool> IsPdfUrl(string url)
    {
        try
        {
            // First check common PDF URL patterns
            var lowerUrl = url.ToLower();
            if (lowerUrl.EndsWith(".pdf") || 
                lowerUrl.Contains("/pdf/") || 
                lowerUrl.Contains("arxiv.org/pdf/") ||
                lowerUrl.Contains(".pdf?"))
            {
                return true;
            }

            // Make a HEAD request to check content-type
            var request = new HttpRequestMessage(HttpMethod.Head, url);
            using var response = await _httpClient.SendAsync(request);
            
            if (response.IsSuccessStatusCode)
            {
                var contentType = response.Content.Headers.ContentType?.MediaType;
                return contentType == "application/pdf";
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to check if URL is PDF: {Error}", ex.Message);
        }

        return false;
    }

    private async Task<WebDataExtractionResult> ExtractPdfDataAsync(string url)
    {
        try
        {
            _logger.LogInformation("Extracting PDF data from {Url}", url);

            // Download PDF content
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var pdfBytes = await response.Content.ReadAsByteArrayAsync();
            
            using var pdfDocument = PdfDocument.Open(pdfBytes);
            
            var extractedText = new List<string>();
            var metadata = new Dictionary<string, string>();
            var tables = new List<TableData>();
            var structuredData = new Dictionary<string, object>();

            // Extract basic metadata
            if (pdfDocument.Information != null)
            {
                var info = pdfDocument.Information;
                if (!string.IsNullOrEmpty(info.Title)) metadata["title"] = info.Title;
                if (!string.IsNullOrEmpty(info.Author)) metadata["author"] = info.Author;
                if (!string.IsNullOrEmpty(info.Subject)) metadata["subject"] = info.Subject;
                if (!string.IsNullOrEmpty(info.Creator)) metadata["creator"] = info.Creator;
                if (!string.IsNullOrEmpty(info.CreationDate)) metadata["creation_date"] = info.CreationDate;
                if (!string.IsNullOrEmpty(info.ModifiedDate)) metadata["modified_date"] = info.ModifiedDate;
            }

            metadata["page_count"] = pdfDocument.NumberOfPages.ToString();
            metadata["pdf_version"] = pdfDocument.Version.ToString();

            // Extract text from all pages
            for (var pageNum = 1; pageNum <= pdfDocument.NumberOfPages; pageNum++)
            {
                try
                {
                    var page = pdfDocument.GetPage(pageNum);
                    var pageText = page.Text;
                    
                    if (!string.IsNullOrWhiteSpace(pageText))
                    {
                        extractedText.Add($"--- Page {pageNum} ---\n{pageText}");
                    }

                    // Try to detect tables and structured content
                    var lines = pageText.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    if (lines.Length > 0)
                    {
                        // Look for potential table structures (lines with multiple columns)
                        var potentialTableLines = lines.Where(line => 
                            line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length >= 3 &&
                            line.Any(char.IsDigit)).ToList();

                        if (potentialTableLines.Count >= 2)
                        {
                            var tableData = new TableData
                            {
                                Headers = new List<string> { $"Table from Page {pageNum}" },
                                Rows = potentialTableLines.Select(line => 
                                    line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries).ToList()
                                ).ToList()
                            };
                            tables.Add(tableData);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Error extracting page {PageNum}: {Error}", pageNum, ex.Message);
                }
            }

            var fullText = string.Join("\n\n", extractedText);

            // Analyze content for financial/research data
            if (IsArxivPaper(url))
            {
                structuredData = await ExtractArxivPaperDataAsync(fullText, metadata);
            }
            else if (IsFinancialDocument(fullText))
            {
                structuredData = ExtractFinancialData(fullText);
            }

            // Extract key insights
            var keyInsights = ExtractKeyInsights(fullText);
            if (keyInsights.Any())
            {
                structuredData["key_insights"] = keyInsights;
            }

            var title = metadata.ContainsKey("title") ? metadata["title"] : 
                       ExtractTitleFromText(fullText) ?? "PDF Document";

            return new WebDataExtractionResult
            {
                Url = url,
                ExtractedAt = DateTime.UtcNow,
                DataType = "PDF",
                Title = title,
                Content = fullText,
                Metadata = metadata,
                StructuredData = structuredData,
                FinancialData = new Dictionary<string, object>(),
                Tables = tables,
                Links = ExtractLinks(fullText)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting PDF data from {Url}", url);
            return new WebDataExtractionResult
            {
                Url = url,
                ExtractedAt = DateTime.UtcNow,
                DataType = "PDF",
                Title = "PDF Document (Error)",
                Content = $"Error extracting PDF: {ex.Message}",
                Metadata = new Dictionary<string, string> { { "error", ex.Message } },
                StructuredData = new Dictionary<string, object>(),
                FinancialData = new Dictionary<string, object>(),
                Tables = new List<TableData>(),
                Links = new List<string>()
            };
        }
    }

    private bool IsArxivPaper(string url)
    {
        return url.Contains("arxiv.org");
    }

    private bool IsFinancialDocument(string text)
    {
        var financialKeywords = new[] { 
            "financial", "portfolio", "investment", "trading", "market", "stock", "equity",
            "bond", "derivative", "risk", "return", "volatility", "sharpe", "alpha", "beta"
        };
        
        var lowerText = text.ToLower();
        return financialKeywords.Count(keyword => lowerText.Contains(keyword)) >= 3;
    }

    private async Task<Dictionary<string, object>> ExtractArxivPaperDataAsync(string text, Dictionary<string, string> metadata)
    {
        var data = new Dictionary<string, object>();

        // Extract basic structured elements first
        ExtractBasicPaperStructure(text, data);

        // Perform deep AI analysis
        await PerformDeepPaperAnalysis(text, data);

        data["document_type"] = "arxiv_paper";
        return data;
    }

    private void ExtractBasicPaperStructure(string text, Dictionary<string, object> data)
    {
        // Extract abstract
        var abstractMatch = Regex.Match(text, @"Abstract\s*[\r\n]+(.{100,2000}?)[\r\n]+(?:1\s+Introduction|Keywords|1\.)", 
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (abstractMatch.Success)
        {
            data["abstract"] = abstractMatch.Groups[1].Value.Trim();
        }

        // Extract keywords
        var keywordsMatch = Regex.Match(text, @"Keywords?:?\s*(.{10,200}?)[\r\n]", RegexOptions.IgnoreCase);
        if (keywordsMatch.Success)
        {
            data["keywords"] = keywordsMatch.Groups[1].Value.Split(',', ';')
                .Select(k => k.Trim()).Where(k => !string.IsNullOrEmpty(k)).ToList();
        }

        // Extract sections
        var sections = ExtractSections(text);
        if (sections.Any())
        {
            data["sections"] = sections;
        }

        // Extract references count
        var referencesMatch = Regex.Match(text, @"References\s*[\r\n]+(.*?)$", 
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (referencesMatch.Success)
        {
            var refText = referencesMatch.Groups[1].Value;
            var refCount = Regex.Matches(refText, @"^\[?\d+\]?", RegexOptions.Multiline).Count;
            data["reference_count"] = refCount;
        }
    }

    private async Task PerformDeepPaperAnalysis(string text, Dictionary<string, object> data)
    {
        try
        {
            _logger.LogInformation("Performing deep AI analysis of research paper...");

            // Create analysis prompts
            var analysisPrompts = new Dictionary<string, string>
            {
                ["summary"] = CreateSummaryPrompt(text),
                ["methodology"] = CreateMethodologyPrompt(text),
                ["strategy_blueprint"] = CreateStrategyBlueprintPrompt(text),
                ["implementation"] = CreateImplementationPrompt(text),
                ["key_contributions"] = CreateContributionsPrompt(text),
                ["practical_applications"] = CreateApplicationsPrompt(text),
                ["limitations"] = CreateLimitationsPrompt(text),
                ["future_work"] = CreateFutureWorkPrompt(text)
            };

            // Perform AI analysis for each aspect
            var analysisResults = new Dictionary<string, object>();
            
            foreach (var prompt in analysisPrompts)
            {
                try
                {
                    var result = await _kernel.InvokePromptAsync(prompt.Value);
                    analysisResults[prompt.Key] = result.ToString();
                    _logger.LogInformation("Completed {AnalysisType} analysis", prompt.Key);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Failed to analyze {AnalysisType}: {Error}", prompt.Key, ex.Message);
                    analysisResults[prompt.Key] = $"Analysis failed: {ex.Message}";
                }
            }

            data["ai_analysis"] = analysisResults;
            
            _logger.LogInformation("Deep paper analysis completed with {Count} analysis types", analysisResults.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to perform deep paper analysis");
            data["ai_analysis"] = new Dictionary<string, object> { { "error", ex.Message } };
        }
    }

    private string CreateSummaryPrompt(string text)
    {
        return $@"
Analyze this academic research paper and provide a comprehensive summary in 200-300 words.
Focus on:
1. The main research question and objectives
2. Key findings and results
3. Significance to the field
4. Novel contributions

Paper content:
{TruncateText(text, 8000)}

Provide a clear, technical summary suitable for a quantitative finance researcher.";
    }

    private string CreateMethodologyPrompt(string text)
    {
        return $@"
Extract and explain the methodology used in this research paper.
Include:
1. Data sources and datasets used
2. Mathematical models and algorithms
3. Experimental setup and parameters
4. Validation approaches
5. Performance metrics

Paper content:
{TruncateText(text, 8000)}

Provide a detailed technical explanation of the methods.";
    }

    private string CreateStrategyBlueprintPrompt(string text)
    {
        return $@"
Based on this research paper, create a high-level strategy blueprint for implementing the findings in a quantitative trading or finance context.
Include:
1. Main strategic approach
2. Key components and modules
3. Data requirements
4. Risk considerations
5. Expected outcomes

Paper content:
{TruncateText(text, 8000)}

Provide a practical blueprint that could guide implementation.";
    }

    private string CreateImplementationPrompt(string text)
    {
        return $@"
Generate pseudocode and implementation guidelines for the key algorithms or methods presented in this paper.
Focus on:
1. Core algorithm pseudocode
2. Data preprocessing steps
3. Model training/optimization
4. Inference/prediction process
5. Performance monitoring

Paper content:
{TruncateText(text, 8000)}

Provide practical implementation guidance with pseudocode examples.";
    }

    private string CreateContributionsPrompt(string text)
    {
        return $@"
Identify and explain the key contributions of this research paper:
1. Novel theoretical contributions
2. Methodological innovations
3. Empirical findings
4. Practical implications
5. Comparison with existing work

Paper content:
{TruncateText(text, 8000)}

Highlight what makes this work unique and valuable.";
    }

    private string CreateApplicationsPrompt(string text)
    {
        return $@"
Identify practical applications of this research in quantitative finance:
1. Trading strategy applications
2. Risk management use cases
3. Portfolio optimization opportunities
4. Market analysis applications
5. Real-world implementation scenarios

Paper content:
{TruncateText(text, 8000)}

Focus on actionable applications for practitioners.";
    }

    private string CreateLimitationsPrompt(string text)
    {
        return $@"
Analyze the limitations and potential challenges of this research:
1. Methodological limitations
2. Data constraints
3. Scalability issues
4. Practical implementation challenges
5. Market applicability concerns

Paper content:
{TruncateText(text, 8000)}

Provide a critical assessment of limitations.";
    }

    private string CreateFutureWorkPrompt(string text)
    {
        return $@"
Suggest future research directions and improvements based on this paper:
1. Methodological extensions
2. Additional datasets to explore
3. Integration opportunities
4. Performance improvements
5. New application domains

Paper content:
{TruncateText(text, 8000)}

Suggest concrete next steps for research and development.";
    }

    private string TruncateText(string text, int maxLength)
    {
        if (text.Length <= maxLength) return text;
        
        // Try to truncate at sentence boundaries
        var truncated = text.Substring(0, maxLength);
        var lastSentence = truncated.LastIndexOf('.');
        
        if (lastSentence > maxLength * 0.8) // If we can get at least 80% and end at sentence
        {
            return truncated.Substring(0, lastSentence + 1);
        }
        
        return truncated + "...";
    }

    private Dictionary<string, object> ExtractFinancialData(string text)
    {
        var data = new Dictionary<string, object>();

        // Extract numerical data with units
        var numberPatterns = new[]
        {
            @"(\$\d+(?:,\d{3})*(?:\.\d{2})?(?:\s*(?:million|billion|trillion|M|B|T))?)",
            @"(\d+(?:\.\d+)?%)",
            @"(\d+(?:\.\d+)?\s*(?:basis points|bps))"
        };

        var extractedNumbers = new List<string>();
        foreach (var pattern in numberPatterns)
        {
            var matches = Regex.Matches(text, pattern, RegexOptions.IgnoreCase);
            extractedNumbers.AddRange(matches.Cast<Match>().Select(m => m.Groups[1].Value));
        }

        if (extractedNumbers.Any())
        {
            data["financial_figures"] = extractedNumbers.Take(20).ToList(); // Limit to avoid too much data
        }

        data["document_type"] = "financial_document";
        return data;
    }

    private List<string> ExtractKeyInsights(string text)
    {
        var insights = new List<string>();

        // Look for conclusion or summary sections
        var conclusionMatch = Regex.Match(text, 
            @"(?:Conclusion|Summary|Key Findings?|Main Results?):?\s*[\r\n]+(.{100,1000}?)(?:[\r\n]{2,}|\Z)",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        if (conclusionMatch.Success)
        {
            insights.Add(conclusionMatch.Groups[1].Value.Trim());
        }

        // Look for bullet points or numbered insights
        var bulletMatches = Regex.Matches(text, 
            @"(?:^|\n)\s*(?:•|·|\*|-|\d+\.)\s+(.{20,200}?)(?:\n|$)", 
            RegexOptions.Multiline);

        insights.AddRange(bulletMatches.Cast<Match>()
            .Select(m => m.Groups[1].Value.Trim())
            .Where(insight => insight.Length > 10)
            .Take(10)); // Limit to top 10 insights

        return insights.Distinct().ToList();
    }

    private string? ExtractTitleFromText(string text)
    {
        // Try to find title in the first few lines
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < Math.Min(10, lines.Length); i++)
        {
            var line = lines[i].Trim();
            if (line.Length > 10 && line.Length < 200 && 
                !line.StartsWith("Page") && !line.All(char.IsDigit))
            {
                return line;
            }
        }
        return null;
    }

    private List<string> ExtractSections(string text)
    {
        var sections = new List<string>();
        
        // Common academic paper section patterns
        var sectionPatterns = new[]
        {
            @"^\s*(\d+\.?\s+[A-Z][^.\n]{5,50})\s*$",
            @"^\s*([A-Z][A-Z\s]{5,50})\s*$"
        };

        foreach (var pattern in sectionPatterns)
        {
            var matches = Regex.Matches(text, pattern, RegexOptions.Multiline);
            sections.AddRange(matches.Cast<Match>().Select(m => m.Groups[1].Value.Trim()));
        }

        return sections.Distinct().Take(20).ToList(); // Limit sections
    }

    private List<string> ExtractLinks(string text)
    {
        var links = new List<string>();
        
        // Extract URLs
        var urlPattern = @"https?://[^\s<>""{}|\\^`\[\]]+";
        var matches = Regex.Matches(text, urlPattern);
        
        links.AddRange(matches.Cast<Match>().Select(m => m.Value));
        
        return links.Distinct().Take(50).ToList(); // Limit to 50 unique links
    }
}

public class WebDataExtractionResult
{
    public string Url { get; set; } = "";
    public DateTime ExtractedAt { get; set; }
    public string DataType { get; set; } = "";
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public Dictionary<string, string> Metadata { get; set; } = new();
    public Dictionary<string, object> StructuredData { get; set; } = new();
    public Dictionary<string, object> FinancialData { get; set; } = new();
    public List<TableData> Tables { get; set; } = new();
    public List<string> Links { get; set; } = new();
}

public class TableData
{
    public List<string> Headers { get; set; } = new();
    public List<List<string>> Rows { get; set; } = new();
}
