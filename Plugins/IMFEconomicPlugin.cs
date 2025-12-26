using System.ComponentModel;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using QuantResearchAgent.Services;

namespace QuantResearchAgent.Plugins
{
    public class IMFEconomicPlugin
    {
        private readonly IMFService _imfService;

        public IMFEconomicPlugin(IMFService imfService)
        {
            _imfService = imfService;
        }

        [KernelFunction("get_imf_economic_series")]
        [Description("Retrieves IMF economic data series for a specific indicator and country. Use this for getting international financial and economic data.")]
        public async Task<string> GetEconomicSeriesAsync(
            [Description("The IMF indicator code (e.g., NGDP_RPCH for GDP growth, PCPIPCH for inflation)")] string indicatorCode,
            [Description("ISO 3-letter country code (e.g., USA, GBR, DEU, FRA). Defaults to USA if not specified")] string countryCode = "USA",
            [Description("Start year for data retrieval (optional)")] int? startYear = null,
            [Description("End year for data retrieval (optional)")] int? endYear = null)
        {
            try
            {
                var dataPoints = await _imfService.GetSeriesDataAsync(indicatorCode, countryCode, startYear, endYear);

                if (dataPoints == null || dataPoints.Count == 0)
                {
                    return $"No data found for indicator {indicatorCode} in country {countryCode}";
                }

                var result = $"IMF Data for {indicatorCode} ({countryCode}):\n";
                result += $"Total data points: {dataPoints.Count}\n\n";

                // Show most recent 10 data points
                var recentData = dataPoints.Take(10);
                foreach (var point in recentData)
                {
                    result += $"{point.Year}: {point.Value:N2}\n";
                }

                if (dataPoints.Count > 10)
                {
                    result += $"\n... and {dataPoints.Count - 10} more data points available";
                }

                return result;
            }
            catch (System.Exception ex)
            {
                return $"Error retrieving IMF data: {ex.Message}";
            }
        }

        [KernelFunction("search_imf_indicators")]
        [Description("Searches for IMF economic indicators by name or code. Use this to find available IMF economic data series.")]
        public async Task<string> SearchEconomicIndicatorsAsync(
            [Description("Search query for economic indicators (e.g., 'GDP', 'inflation', 'debt')")] string query,
            [Description("Maximum number of results to return (default: 10)")] int maxResults = 10)
        {
            try
            {
                var indicators = await _imfService.SearchIndicatorsAsync(query, maxResults);

                if (indicators == null || indicators.Count == 0)
                {
                    return $"No IMF indicators found matching '{query}'";
                }

                var result = $"IMF Indicators matching '{query}':\n\n";
                foreach (var indicator in indicators)
                {
                    result += $"• {indicator.Code}: {indicator.Name}\n";
                    if (!string.IsNullOrEmpty(indicator.Description))
                    {
                        // Truncate long descriptions
                        var description = indicator.Description.Length > 100
                            ? indicator.Description.Substring(0, 100) + "..."
                            : indicator.Description;
                        result += $"  Description: {description}\n";
                    }
                    result += "\n";
                }

                return result;
            }
            catch (System.Exception ex)
            {
                return $"Error searching IMF indicators: {ex.Message}";
            }
        }

        [KernelFunction("get_imf_popular_indicators")]
        [Description("Retrieves a list of popular IMF economic indicators. Use this to get an overview of key international economic metrics.")]
        public async Task<string> GetPopularEconomicIndicatorsAsync()
        {
            try
            {
                var indicators = await _imfService.GetPopularIndicatorsAsync();

                if (indicators == null || indicators.Count == 0)
                {
                    return "No popular IMF indicators available";
                }

                var result = "Popular IMF Economic Indicators:\n\n";
                foreach (var indicator in indicators)
                {
                    result += $"• {indicator.Code}: {indicator.Name}\n";
                }

                result += "\nTo get data for any indicator, use the 'get_imf_economic_series' function with the indicator code.";

                return result;
            }
            catch (System.Exception ex)
            {
                return $"Error retrieving popular indicators: {ex.Message}";
            }
        }

        [KernelFunction("get_imf_indicator_info")]
        [Description("Gets detailed information about a specific IMF economic indicator.")]
        public async Task<string> GetIndicatorInfoAsync(
            [Description("The IMF indicator code (e.g., NGDP_RPCH)")] string indicatorCode)
        {
            try
            {
                var indicator = await _imfService.GetIndicatorInfoAsync(indicatorCode);

                if (indicator == null)
                {
                    return $"Indicator {indicatorCode} not found";
                }

                var result = $"IMF Indicator: {indicator.Code}\n";
                result += $"Name: {indicator.Name}\n";

                if (!string.IsNullOrEmpty(indicator.Description))
                {
                    result += $"Description: {indicator.Description}\n";
                }

                result += $"Source: {indicator.Source}\n";

                return result;
            }
            catch (System.Exception ex)
            {
                return $"Error retrieving indicator info: {ex.Message}";
            }
        }
    }
}