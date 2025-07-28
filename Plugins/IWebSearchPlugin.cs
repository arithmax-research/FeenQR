using System.Threading.Tasks;
using System.Collections.Generic;

namespace QuantResearchAgent.Plugins
{
    /// <summary>
    /// Interface for web search plugins (Bing, Google, SerpAPI, etc.)
    /// </summary>
    public interface IWebSearchPlugin
    {
        /// <summary>
        /// Search the web for a given query and return a list of relevant results (title, snippet, url).
        /// </summary>
        Task<List<WebSearchResult>> SearchAsync(string query, int maxResults = 5);
    }

    public class WebSearchResult
    {
        public string Title { get; set; } = string.Empty;
        public string Snippet { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
    }
}
