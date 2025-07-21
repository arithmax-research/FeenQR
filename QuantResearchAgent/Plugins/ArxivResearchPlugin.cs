using Microsoft.SemanticKernel;
using QuantResearchAgent.Services.ResearchAgents;
using System.ComponentModel;

namespace QuantResearchAgent.Plugins;

/// <summary>
/// ArXiv Research Plugin - Exposes academic paper analysis capabilities to Semantic Kernel
/// </summary>
public class ArxivResearchPlugin
{
    private readonly ArxivResearchAgentService _arxivService;

    public ArxivResearchPlugin(ArxivResearchAgentService arxivService)
    {
        _arxivService = arxivService;
    }

    [KernelFunction]
    [Description("Search for academic papers on ArXiv related to quantitative finance and trading")]
    public async Task<string> SearchFinancePapersAsync(
        [Description("Search query for finance/trading papers")] string query,
        [Description("Number of papers to retrieve")] int maxResults = 10)
    {
        try
        {
            var papers = await _arxivService.SearchPapersAsync(query, maxResults);
            
            if (!papers.Any())
            {
                return $"No papers found for query: {query}";
            }

            var summary = $"Found {papers.Count} papers related to '{query}':\n\n";
            
            foreach (var paper in papers.Take(5)) // Show top 5 in summary
            {
                summary += $"📄 **{paper.Title}**\n";
                summary += $"   Authors: {string.Join(", ", paper.Authors)}\n";
                summary += $"   Published: {paper.PublishedDate:yyyy-MM-dd}\n";
                summary += $"   Abstract: {paper.Abstract.Substring(0, Math.Min(200, paper.Abstract.Length))}...\n";
                summary += $"   ArXiv ID: {paper.ArxivId}\n\n";
            }

            if (papers.Count > 5)
            {
                summary += $"... and {papers.Count - 5} more papers available for detailed analysis.\n";
            }

            return summary;
        }
        catch (Exception ex)
        {
            return $"Error searching ArXiv papers: {ex.Message}";
        }
    }

    [KernelFunction]
    [Description("Analyze a specific ArXiv paper and extract trading strategies")]
    public async Task<string> AnalyzePaperForTradingStrategiesAsync(
        [Description("ArXiv paper ID (e.g., 2301.12345)")] string arxivId)
    {
        try
        {
            var analysis = await _arxivService.AnalyzePaperAsync(arxivId);
            
            if (analysis == null)
            {
                return $"Unable to analyze paper {arxivId}. Paper may not exist or be inaccessible.";
            }

            var result = $"## Analysis of ArXiv Paper: {arxivId}\n\n";
            result += $"**Title:** {analysis.Title}\n";
            result += $"**Authors:** {string.Join(", ", analysis.Authors)}\n";
            result += $"**Relevance Score:** {analysis.RelevanceScore:P0}\n\n";
            
            result += $"### Key Findings:\n{analysis.KeyFindings}\n\n";
            
            if (analysis.TradingStrategies.Any())
            {
                result += "### Identified Trading Strategies:\n";
                foreach (var strategy in analysis.TradingStrategies)
                {
                    result += $"- **{strategy.Name}** (Feasibility: {strategy.FeasibilityScore:P0})\n";
                    result += $"  {strategy.Description}\n";
                    result += $"  Implementation: {strategy.ImplementationComplexity}\n\n";
                }
            }
            else
            {
                result += "### No specific trading strategies identified in this paper.\n\n";
            }

            if (!string.IsNullOrEmpty(analysis.ImplementationRoadmap))
            {
                result += $"### Implementation Roadmap:\n{analysis.ImplementationRoadmap}\n";
            }

            return result;
        }
        catch (Exception ex)
        {
            return $"Error analyzing paper {arxivId}: {ex.Message}";
        }
    }

    [KernelFunction]
    [Description("Search and analyze multiple papers for trading strategies on a specific topic")]
    public async Task<string> ResearchTradingTopicAsync(
        [Description("Trading topic to research (e.g., 'mean reversion', 'momentum trading', 'portfolio optimization')")] string topic,
        [Description("Number of papers to analyze")] int paperCount = 5)
    {
        try
        {
            var allStrategies = await _arxivService.ExtractTradingStrategiesAsync(topic, paperCount);
            
            if (!allStrategies.Any())
            {
                return $"No trading strategies found for topic: {topic}";
            }

            var result = $"## Research Results for '{topic}'\n\n";
            result += $"Analyzed {paperCount} papers and found {allStrategies.Count} potential trading strategies:\n\n";

            var rankedStrategies = allStrategies.OrderByDescending(s => s.FeasibilityScore).Take(10);

            foreach (var strategy in rankedStrategies)
            {
                result += $"### {strategy.Name}\n";
                result += $"**Feasibility:** {strategy.FeasibilityScore:P0} | ";
                result += $"**Complexity:** {strategy.ImplementationComplexity}\n";
                result += $"**Description:** {strategy.Description}\n";
                result += $"**Source:** {strategy.Source}\n\n";
            }

            // Generate implementation recommendations
            var roadmap = await _arxivService.GenerateImplementationRoadmapAsync(rankedStrategies.ToList());
            result += $"## Implementation Recommendations:\n{roadmap}\n";

            return result;
        }
        catch (Exception ex)
        {
            return $"Error researching topic '{topic}': {ex.Message}";
        }
    }

    [KernelFunction]
    [Description("Get recent academic research trends in quantitative finance")]
    public async Task<string> GetResearchTrendsAsync(
        [Description("Time period to analyze (e.g., 'last 6 months', 'last year')")] string period = "last 6 months")
    {
        try
        {
            // Define trending topics in quantitative finance
            var trendingTopics = new[]
            {
                "machine learning trading",
                "reinforcement learning finance", 
                "neural networks portfolio",
                "crypto quantitative",
                "ESG investing strategies",
                "alternative data trading"
            };

            var result = $"## Recent Research Trends in Quantitative Finance ({period})\n\n";

            foreach (var topic in trendingTopics)
            {
                var papers = await _arxivService.SearchPapersAsync(topic, 3);
                if (papers.Any())
                {
                    result += $"### {topic.ToTitleCase()}\n";
                    result += $"Recent papers ({papers.Count} found):\n";
                    
                    foreach (var paper in papers)
                    {
                        result += $"- **{paper.Title}** ({paper.PublishedDate:MMM yyyy})\n";
                        result += $"  {paper.Abstract.Substring(0, Math.Min(150, paper.Abstract.Length))}...\n\n";
                    }
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            return $"Error getting research trends: {ex.Message}";
        }
    }

    [KernelFunction]
    [Description("Validate the feasibility of implementing a trading strategy from academic research")]
    public async Task<string> ValidateStrategyFeasibilityAsync(
        [Description("Description of the trading strategy to validate")] string strategyDescription,
        [Description("Available infrastructure (e.g., 'basic', 'advanced', 'institutional')")] string infrastructure = "basic")
    {
        try
        {
            var validation = await _arxivService.ValidateStrategyFeasibilityAsync(strategyDescription, infrastructure);
            
            var result = $"## Strategy Feasibility Analysis\n\n";
            result += $"**Strategy:** {strategyDescription}\n";
            result += $"**Infrastructure Level:** {infrastructure}\n\n";
            
            result += $"### Feasibility Assessment\n";
            result += $"**Overall Score:** {validation.FeasibilityScore:P0}\n";
            result += $"**Implementation Complexity:** {validation.ImplementationComplexity}\n\n";
            
            result += $"### Required Components:\n";
            foreach (var requirement in validation.Requirements)
            {
                result += $"- {requirement}\n";
            }
            
            result += $"\n### Risk Assessment:\n";
            foreach (var risk in validation.Risks)
            {
                result += $"- {risk}\n";
            }
            
            if (validation.EstimatedTimeframe > 0)
            {
                result += $"\n**Estimated Implementation Time:** {validation.EstimatedTimeframe} weeks\n";
            }
            
            result += $"\n### Recommendations:\n{validation.Recommendations}\n";

            return result;
        }
        catch (Exception ex)
        {
            return $"Error validating strategy feasibility: {ex.Message}";
        }
    }

    [KernelFunction]
    [Description("Get history of ArXiv research analysis performed")]
    public string GetResearchHistoryAsync()
    {
        try
        {
            var history = _arxivService.GetResearchHistory();
            
            if (!history.Any())
            {
                return "No research history available.";
            }

            var result = $"## ArXiv Research History\n\n";
            result += $"Total research sessions: {history.Count}\n\n";

            var recentHistory = history.OrderByDescending(h => h.Timestamp).Take(10);
            
            foreach (var entry in recentHistory)
            {
                result += $"### {entry.Timestamp:yyyy-MM-dd HH:mm}\n";
                result += $"**Topic:** {entry.SearchQuery}\n";
                result += $"**Papers Found:** {entry.PapersAnalyzed}\n";
                result += $"**Strategies Extracted:** {entry.StrategiesFound}\n\n";
            }

            return result;
        }
        catch (Exception ex)
        {
            return $"Error retrieving research history: {ex.Message}";
        }
    }
}

// Extension method for string formatting
public static class StringExtensions
{
    public static string ToTitleCase(this string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        
        return string.Join(" ", input.Split(' ')
            .Select(word => char.ToUpper(word[0]) + word.Substring(1).ToLower()));
    }
}
