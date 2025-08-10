using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using QuantResearchAgent.Plugins;
using System.Text;
using System.Text.RegularExpressions;
using System.Net.Http;

namespace QuantResearchAgent.Services.ResearchAgents
{
    /// <summary>
    /// Agent that analyzes academic finance/quantitative research papers and generates 
    /// implementation blueprints and pseudocode for trading strategies
    /// </summary>
    public class AcademicResearchPaperAgent
    {
        private readonly Kernel _kernel;
        private readonly ILogger<AcademicResearchPaperAgent> _logger;
        private readonly IWebSearchPlugin _webSearchPlugin;
        private readonly HttpClient _httpClient;

        public AcademicResearchPaperAgent(
            Kernel kernel,
            ILogger<AcademicResearchPaperAgent> logger,
            IWebSearchPlugin webSearchPlugin,
            HttpClient httpClient)
        {
            _kernel = kernel;
            _logger = logger;
            _webSearchPlugin = webSearchPlugin;
            _httpClient = httpClient;
        }

        /// <summary>
        /// Search for academic papers on a specific topic
        /// </summary>
        public async Task<string> SearchAcademicPapersAsync(string topic, int maxResults = 5)
        {
            try
            {
                _logger.LogInformation($"Starting search for topic: {topic}");
                
                var searchQueries = new[]
                {
                    $"site:arxiv.org {topic} quantitative finance",
                    $"site:ssrn.com {topic} algorithmic trading",
                    $"site:papers.ssrn.com {topic} mathematical finance",
                    $"\"{topic}\" filetype:pdf finance strategy",
                    $"{topic} high frequency trading research paper"
                };

                var allResults = new List<WebSearchResult>();
                
                foreach (var query in searchQueries)
                {
                    _logger.LogInformation($"Searching with query: {query}");
                    var results = await _webSearchPlugin.SearchAsync(query, maxResults);
                    _logger.LogInformation($"Found {results.Count} results for query: {query}");
                    allResults.AddRange(results);
                }

                _logger.LogInformation($"Total results before deduplication: {allResults.Count}");

                var uniqueResults = allResults
                    .GroupBy(r => r.Url)
                    .Select(g => g.First())
                    .Take(maxResults * 2)
                    .ToList();

                _logger.LogInformation($"Total unique results after deduplication: {uniqueResults.Count}");

                var resultBuilder = new StringBuilder();
                resultBuilder.AppendLine(new string('=', 80));
                resultBuilder.AppendLine($"ACADEMIC PAPERS SEARCH: {topic.ToUpper()}");
                resultBuilder.AppendLine(new string('=', 80));

                if (!uniqueResults.Any())
                {
                    resultBuilder.AppendLine("No academic papers found for the specified topic.");
                    return resultBuilder.ToString();
                }

                for (int i = 0; i < uniqueResults.Count; i++)
                {
                    var paper = uniqueResults[i];
                    resultBuilder.AppendLine($"\n{i + 1}. {paper.Title}");
                    resultBuilder.AppendLine($"   URL: {paper.Url}");
                    resultBuilder.AppendLine($"   Abstract/Summary: {paper.Snippet}");
                    resultBuilder.AppendLine();
                }

                return resultBuilder.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error searching academic papers for topic: {topic}");
                return $"Error searching academic papers: {ex.Message}";
            }
        }

        /// <summary>
        /// Optimized search for faster synthesis - uses fewer queries and faster processing
        /// </summary>
        private async Task<string> SearchAcademicPapersOptimizedAsync(string topic, int maxResults = 5)
        {
            try
            {
                _logger.LogInformation($"Starting optimized search for topic: {topic}");
                
                // Use fewer, more targeted queries for faster results
                var searchQueries = new[]
                {
                    $"site:arxiv.org {topic} quantitative finance",
                    $"\"{topic}\" algorithmic trading research paper filetype:pdf"
                };

                var allResults = new List<WebSearchResult>();
                
                foreach (var query in searchQueries)
                {
                    try
                    {
                        var results = await _webSearchPlugin.SearchAsync(query, maxResults);
                        allResults.AddRange(results);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Search query failed: {query}, Error: {ex.Message}");
                    }
                }

                var uniqueResults = allResults
                    .GroupBy(r => r.Url)
                    .Select(g => g.First())
                    .Take(maxResults)
                    .ToList();

                var resultBuilder = new StringBuilder();
                resultBuilder.AppendLine($"Found {uniqueResults.Count} academic papers:");

                for (int i = 0; i < uniqueResults.Count; i++)
                {
                    var paper = uniqueResults[i];
                    resultBuilder.AppendLine($"\n{i + 1}. {paper.Title}");
                    resultBuilder.AppendLine($"   URL: {paper.Url}");
                    resultBuilder.AppendLine($"   Summary: {paper.Snippet}");
                }

                return resultBuilder.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in optimized search for topic: {topic}");
                return $"Error searching papers: {ex.Message}";
            }
        }

        /// <summary>
        /// Lightweight paper analysis for synthesis - focuses on key insights only
        /// </summary>
        private async Task<string> AnalyzePaperLightweightAsync(string paperUrl, string focusArea)
        {
            try
            {
                // Extract basic content
                var paperContent = await ExtractPaperContentAsync(paperUrl);
                
                if (string.IsNullOrEmpty(paperContent))
                {
                    return "";
                }

                // Single AI call for lightweight analysis
                var prompt = $@"
Analyze this academic paper content and provide a concise summary focusing on '{focusArea}':

{paperContent.Substring(0, Math.Min(paperContent.Length, 2000))}

Provide a brief analysis with:
1. Main research objective
2. Key methodology/approach
3. Primary findings
4. Practical trading applications
5. Implementation considerations

Keep response concise (max 200 words).
Format output as PLAIN TEXT ONLY - no markdown, no asterisks, no hashtags, no code blocks.
";

                var response = await _kernel.InvokePromptAsync(prompt);
                var analysis = response.GetValue<string>() ?? "";
                
                return $"PAPER: {paperUrl}\n{analysis}\n{new string('-', 40)}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in lightweight analysis of paper: {paperUrl}");
                return "";
            }
        }

        /// <summary>
        /// Analyze a paper from URL and generate implementation blueprint
        /// </summary>
        public async Task<string> AnalyzePaperAndGenerateBlueprintAsync(string paperUrl, string focusArea = "")
        {
            try
            {
                var analysisBuilder = new StringBuilder();
                analysisBuilder.AppendLine(new string('=', 80));
                analysisBuilder.AppendLine("ACADEMIC PAPER ANALYSIS & IMPLEMENTATION BLUEPRINT");
                analysisBuilder.AppendLine(new string('=', 80));
                analysisBuilder.AppendLine($"Paper URL: {paperUrl}");
                analysisBuilder.AppendLine($"Analysis Date: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss UTC}");
                if (!string.IsNullOrEmpty(focusArea))
                {
                    analysisBuilder.AppendLine($"Focus Area: {focusArea}");
                }
                analysisBuilder.AppendLine();

                // Extract paper content
                var paperContent = await ExtractPaperContentAsync(paperUrl);
                
                if (string.IsNullOrEmpty(paperContent))
                {
                    return "Unable to extract content from the provided URL. Please ensure it's a valid academic paper link.";
                }

                // Analyze the paper using AI
                var paperAnalysis = await AnalyzePaperContentAsync(paperContent, focusArea);
                analysisBuilder.AppendLine("PAPER ANALYSIS");
                analysisBuilder.AppendLine(new string('-', 40));
                analysisBuilder.AppendLine(paperAnalysis);

                // Generate strategy blueprint
                var strategyBlueprint = await GenerateStrategyBlueprintAsync(paperContent, focusArea);
                analysisBuilder.AppendLine("\nIMPLEMENTATION BLUEPRINT");
                analysisBuilder.AppendLine(new string('-', 40));
                analysisBuilder.AppendLine(strategyBlueprint);

                // Generate pseudocode
                var pseudocode = await GeneratePseudocodeAsync(paperContent, focusArea);
                analysisBuilder.AppendLine("\nPSEUDOCODE IMPLEMENTATION");
                analysisBuilder.AppendLine(new string('-', 40));
                analysisBuilder.AppendLine(pseudocode);

                // Generate risk considerations
                var riskConsiderations = await GenerateRiskConsiderationsAsync(paperContent);
                analysisBuilder.AppendLine("\nIMPLEMENTATION RISKS & CONSIDERATIONS");
                analysisBuilder.AppendLine(new string('-', 40));
                analysisBuilder.AppendLine(riskConsiderations);

                // Generate backtesting framework
                var backtestingFramework = await GenerateBacktestingFrameworkAsync(paperContent);
                analysisBuilder.AppendLine("\nBACKTESTING FRAMEWORK");
                analysisBuilder.AppendLine(new string('-', 40));
                analysisBuilder.AppendLine(backtestingFramework);

                analysisBuilder.AppendLine("\n" + new string('=', 80));
                analysisBuilder.AppendLine("END OF ANALYSIS");
                analysisBuilder.AppendLine(new string('=', 80));

                return analysisBuilder.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error analyzing paper: {paperUrl}");
                return $"Error analyzing paper: {ex.Message}";
            }
        }

        /// <summary>
        /// Search and analyze multiple papers on a topic, then synthesize implementation strategies
        /// </summary>
        public async Task<string> ResearchTopicAndSynthesizeAsync(string topic, int maxPapers = 3)
        {
            try
            {
                var synthesisBuilder = new StringBuilder();
                synthesisBuilder.AppendLine(new string('=', 80));
                synthesisBuilder.AppendLine($"TOPIC RESEARCH SYNTHESIS: {topic.ToUpper()}");
                synthesisBuilder.AppendLine(new string('=', 80));
                Console.WriteLine("Phase 1: Searching for academic papers...");

                // Search for papers with optimized approach
                var searchResults = await SearchAcademicPapersOptimizedAsync(topic, maxPapers);
                synthesisBuilder.AppendLine(searchResults);

                // Extract URLs from search results
                var urls = ExtractUrlsFromSearchResults(searchResults);
                
                if (!urls.Any())
                {
                    synthesisBuilder.AppendLine("\nNo papers found to analyze.");
                    return synthesisBuilder.ToString();
                }

                Console.WriteLine($"Phase 2: Analyzing {Math.Min(urls.Count, maxPapers)} papers...");

                // Analyze papers with lightweight approach for synthesis
                var paperSummaries = new List<string>();
                var processedCount = 0;

                foreach (var url in urls.Take(maxPapers))
                {
                    try
                    {
                        processedCount++;
                        Console.WriteLine($"   Processing paper {processedCount}/{Math.Min(urls.Count, maxPapers)}...");
                        
                        // Use lightweight analysis for synthesis
                        var summary = await AnalyzePaperLightweightAsync(url, topic);
                        if (!string.IsNullOrEmpty(summary))
                        {
                            paperSummaries.Add(summary);
                        }
                        
                        // Shorter delay for faster processing
                        await Task.Delay(500);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Failed to analyze paper {url}: {ex.Message}");
                    }
                }

                Console.WriteLine("Phase 3: Synthesizing findings...");

                // Synthesize findings
                if (paperSummaries.Any())
                {
                    var synthesis = await SynthesizeMultiplePapersAsync(topic, paperSummaries);
                    synthesisBuilder.AppendLine("\nSYNTHESIZED RESEARCH FINDINGS");
                    synthesisBuilder.AppendLine(new string('-', 50));
                    synthesisBuilder.AppendLine(synthesis);
                }

                Console.WriteLine("SUCCESS: Research synthesis complete!");
                return synthesisBuilder.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error researching topic: {topic}");
                return $"Error researching topic: {ex.Message}";
            }
        }

        private async Task<string> ExtractPaperContentAsync(string url)
        {
            try
            {
                // For PDF URLs, we'll extract what we can from the abstract/metadata
                // In a production environment, you'd want to use a PDF parsing library
                
                if (url.Contains("arxiv.org"))
                {
                    // Extract ArXiv ID and get abstract
                    var arxivId = ExtractArxivId(url);
                    if (!string.IsNullOrEmpty(arxivId))
                    {
                        return await GetArxivAbstractAsync(arxivId);
                    }
                }

                // For other URLs, try to get the page content
                var response = await _httpClient.GetStringAsync(url);
                
                // Clean up HTML and extract meaningful content
                var cleanContent = Regex.Replace(response, @"<[^>]*>", " ");
                cleanContent = Regex.Replace(cleanContent, @"\s+", " ");
                
                // Take first 5000 characters as a reasonable sample
                return cleanContent.Length > 5000 ? cleanContent.Substring(0, 5000) : cleanContent;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error extracting content from URL: {url}");
                return "";
            }
        }

        private string ExtractArxivId(string url)
        {
            var match = Regex.Match(url, @"arxiv\.org/(?:abs/|pdf/)?(\d+\.\d+)");
            return match.Success ? match.Groups[1].Value : "";
        }

        private async Task<string> GetArxivAbstractAsync(string arxivId)
        {
            try
            {
                var apiUrl = $"http://export.arxiv.org/api/query?id_list={arxivId}";
                var response = await _httpClient.GetStringAsync(apiUrl);
                
                // Extract title and abstract from XML response
                var titleMatch = Regex.Match(response, @"<title>(.*?)</title>", RegexOptions.Singleline);
                var summaryMatch = Regex.Match(response, @"<summary>(.*?)</summary>", RegexOptions.Singleline);
                
                var content = new StringBuilder();
                if (titleMatch.Success)
                {
                    content.AppendLine($"Title: {titleMatch.Groups[1].Value.Trim()}");
                }
                if (summaryMatch.Success)
                {
                    content.AppendLine($"Abstract: {summaryMatch.Groups[1].Value.Trim()}");
                }
                
                return content.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting ArXiv abstract for ID: {arxivId}");
                return "";
            }
        }

        private async Task<string> AnalyzePaperContentAsync(string content, string focusArea)
        {
            var prompt = $@"
Analyze the following academic paper content and provide a comprehensive analysis focusing on {focusArea}:

{content}

Provide analysis covering:
1. Main research question and hypothesis
2. Methodology and approach used
3. Key findings and conclusions
4. Mathematical models or algorithms presented
5. Data requirements and sources
6. Practical applicability in quantitative finance
7. Strengths and limitations of the approach

Format your response clearly and concisely using PLAIN TEXT ONLY - no markdown, no asterisks, no hashtags, no code blocks.
";

            var response = await _kernel.InvokePromptAsync(prompt);
            return response.GetValue<string>() ?? "Unable to analyze paper content.";
        }

        private async Task<string> GenerateStrategyBlueprintAsync(string content, string focusArea)
        {
            var prompt = $@"
Based on the academic paper content below, create a detailed implementation blueprint for translating the research into a practical trading strategy:

{content}

Focus Area: {focusArea}

Create a blueprint that includes:
1. Strategy Overview and Objective
2. Required Data Sources and Preprocessing
3. Feature Engineering Requirements
4. Model/Algorithm Implementation Steps
5. Signal Generation Logic
6. Risk Management Framework
7. Portfolio Management Integration
8. Performance Monitoring and Maintenance
9. Technology Stack Recommendations
10. Implementation Timeline and Milestones

Provide specific, actionable guidance for each section using PLAIN TEXT ONLY - no markdown, no asterisks, no hashtags, no code blocks.
";

            var response = await _kernel.InvokePromptAsync(prompt);
            return response.GetValue<string>() ?? "Unable to generate strategy blueprint.";
        }

        private async Task<string> GeneratePseudocodeAsync(string content, string focusArea)
        {
            var prompt = $@"
Based on the academic paper content and focusing on {focusArea}, generate detailed pseudocode for implementing the main algorithm or strategy:

{content}

Generate pseudocode that includes:
1. Data initialization and preprocessing
2. Main algorithm/model implementation
3. Signal generation logic
4. Risk management checks
5. Portfolio allocation logic
6. Performance tracking
7. Error handling and edge cases

Use clear, structured pseudocode with proper comments and logical flow.
Format as PLAIN TEXT pseudocode - no markdown code blocks, use simple indentation and line breaks.
";

            var response = await _kernel.InvokePromptAsync(prompt);
            return response.GetValue<string>() ?? "Unable to generate pseudocode.";
        }

        private async Task<string> GenerateRiskConsiderationsAsync(string content)
        {
            var prompt = $@"
Based on the academic paper content below, identify and analyze potential risks and considerations for implementing this research in a live trading environment:

{content}

Analyze risks in these categories:
1. Model Risk (overfitting, parameter sensitivity, etc.)
2. Market Risk (regime changes, liquidity, etc.)
3. Implementation Risk (latency, execution, etc.)
4. Data Risk (quality, availability, survivorship bias, etc.)
5. Operational Risk (system failures, human error, etc.)
6. Regulatory and Compliance Risks
7. Capital and Liquidity Risks

Provide specific mitigation strategies for each identified risk using PLAIN TEXT ONLY - no markdown, no asterisks, no hashtags.
";

            var response = await _kernel.InvokePromptAsync(prompt);
            return response.GetValue<string>() ?? "Unable to generate risk considerations.";
        }

        private async Task<string> GenerateBacktestingFrameworkAsync(string content)
        {
            var prompt = $@"
Based on the academic paper content, design a comprehensive backtesting framework to validate the strategy before live implementation:

{content}

Design a framework that includes:
1. Historical Data Requirements
2. Backtesting Methodology
3. Performance Metrics to Track
4. Walk-Forward Analysis Design
5. Monte Carlo Simulation Framework
6. Stress Testing Scenarios
7. Transaction Cost Modeling
8. Slippage and Market Impact Estimation
9. Out-of-Sample Testing Protocol
10. Statistical Significance Testing

Provide specific implementation details for each component using PLAIN TEXT ONLY - no markdown, no asterisks, no hashtags.
";

            var response = await _kernel.InvokePromptAsync(prompt);
            return response.GetValue<string>() ?? "Unable to generate backtesting framework.";
        }

        private List<string> ExtractUrlsFromSearchResults(string searchResults)
        {
            var urls = new List<string>();
            var matches = Regex.Matches(searchResults, @"URL: (https?://[^\s]+)");
            
            foreach (Match match in matches)
            {
                urls.Add(match.Groups[1].Value);
            }
            
            return urls;
        }

        private async Task<string> SynthesizeMultiplePapersAsync(string topic, List<string> analyses)
        {
            var combinedAnalyses = string.Join("\n\n" + new string('=', 50) + "\n\n", analyses);
            
            var prompt = $@"
Based on the multiple paper analyses provided below for the topic '{topic}', synthesize the findings and create a comprehensive implementation strategy:

{combinedAnalyses}

Provide a synthesis that includes:
1. Common themes and approaches across papers
2. Conflicting findings and how to resolve them
3. Best practices identified from multiple sources
4. Comprehensive implementation roadmap
5. Risk factors identified across studies
6. Performance expectations based on research
7. Next steps for validation and implementation

Create a coherent, actionable synthesis that leverages insights from all analyzed papers using PLAIN TEXT ONLY - no markdown, no asterisks, no hashtags.
";

            var response = await _kernel.InvokePromptAsync(prompt);
            return response.GetValue<string>() ?? "Unable to synthesize multiple paper analyses.";
        }

        /// <summary>
        /// Quick research overview - faster alternative to full synthesis
        /// </summary>
        public async Task<string> GetQuickResearchOverviewAsync(string topic, int maxPapers = 3)
        {
            try
            {
                var overviewBuilder = new StringBuilder();
                overviewBuilder.AppendLine(new string('=', 60));
                overviewBuilder.AppendLine($"QUICK RESEARCH OVERVIEW: {topic.ToUpper()}");
                overviewBuilder.AppendLine(new string('=', 60));

                Console.WriteLine("Performing quick research scan...");

                // Fast search
                var searchResults = await SearchAcademicPapersOptimizedAsync(topic, maxPapers);
                overviewBuilder.AppendLine(searchResults);

                // Quick AI summary of the topic
                var quickSummary = await GenerateQuickTopicSummaryAsync(topic, searchResults);
                overviewBuilder.AppendLine("\nQUICK INSIGHTS");
                overviewBuilder.AppendLine(new string('-', 30));
                overviewBuilder.AppendLine(quickSummary);

                overviewBuilder.AppendLine($"\nFor detailed analysis, use: analyze-paper [url] {topic}");
                overviewBuilder.AppendLine($"For full synthesis, use: research-synthesis \"{topic}\" {maxPapers}");

                return overviewBuilder.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error generating quick overview for topic: {topic}");
                return $"Error generating quick overview: {ex.Message}";
            }
        }

        private async Task<string> GenerateQuickTopicSummaryAsync(string topic, string searchResults)
        {
            var prompt = $@"
Based on the search results below for '{topic}', provide a quick research overview:

{searchResults}

Provide a concise summary (max 150 words) covering:
1. Current research trends in this area
2. Key methodologies being used
3. Practical applications for trading
4. Implementation complexity level
5. Recommended next steps for deeper research

Be specific and actionable using PLAIN TEXT ONLY - no markdown, no asterisks, no hashtags.
";

            var response = await _kernel.InvokePromptAsync(prompt);
            return response.GetValue<string>() ?? "Unable to generate quick summary.";
        }
    }
}
