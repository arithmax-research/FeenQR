using System.Threading.Tasks;
using System.Collections.Generic;

namespace QuantResearchAgent.Plugins
{
    /// <summary>
    /// Interface for financial data plugins (Alpaca, Yahoo Finance, Google Finance, etc.)
    /// </summary>
    public interface IFinancialDataPlugin
    {
        /// <summary>
        /// Get a list of global securities (stocks, ETFs, futures, etc.) by region or sector.
        /// </summary>
        Task<List<SecurityInfo>> GetSecuritiesAsync(string regionOrSector, int maxResults = 10);

        /// <summary>
        /// Get a list of securities for a specific set of tickers (used after AI analysis).
        /// </summary>
        Task<List<SecurityInfo>> GetSecuritiesForTickersAsync(IEnumerable<string> tickers, int maxResults = 10);

        /// <summary>
        /// Get recent financial news or case studies for a given topic or security.
        /// </summary>
        Task<List<FinancialNewsItem>> GetNewsAsync(string query, int maxResults = 5);
    }

    public class SecurityInfo
    {
        public string Symbol { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Exchange { get; set; } = string.Empty;
        public string Region { get; set; } = string.Empty;
    }

    public class FinancialNewsItem
    {
        public string Headline { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string PublishedAt { get; set; } = string.Empty;
    }
}
