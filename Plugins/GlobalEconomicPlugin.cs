using System.ComponentModel;
using Microsoft.SemanticKernel;
using QuantResearchAgent.Services;

namespace QuantResearchAgent.Plugins
{
    public class GlobalEconomicPlugin
    {
        private readonly GlobalEconomicService _globalEconomicService;

        public GlobalEconomicPlugin(GlobalEconomicService globalEconomicService)
        {
            _globalEconomicService = globalEconomicService;
        }

        [KernelFunction, Description("Get global economic indicators from IMF, OECD, and World Bank")]
        public async Task<string> GetGlobalEconomicIndicators(
            [Description("Start date for data retrieval (YYYY-MM-DD format)")] string? startDate = null,
            [Description("End date for data retrieval (YYYY-MM-DD format)")] string? endDate = null)
        {
            try
            {
                DateTime? start = string.IsNullOrEmpty(startDate) ? null : DateTime.Parse(startDate);
                DateTime? end = string.IsNullOrEmpty(endDate) ? null : DateTime.Parse(endDate);

                var indicators = await _globalEconomicService.GetGlobalEconomicIndicatorsAsync(start, end);
                return $"Retrieved {indicators.Count} global economic indicators";
            }
            catch (Exception ex)
            {
                return $"Error retrieving global economic indicators: {ex.Message}";
            }
        }

        [KernelFunction, Description("Monitor global supply chain disruptions")]
        public async Task<string> MonitorSupplyChainDisruptions()
        {
            try
            {
                var disruptions = await _globalEconomicService.MonitorSupplyChainDisruptionsAsync();
                return $"Identified {disruptions.Count} supply chain disruption indicators";
            }
            catch (Exception ex)
            {
                return $"Error monitoring supply chain disruptions: {ex.Message}";
            }
        }

        [KernelFunction, Description("Get global trade data and statistics")]
        public async Task<string> GetGlobalTradeData(
            [Description("Start date for data retrieval (YYYY-MM-DD format)")] string? startDate = null,
            [Description("End date for data retrieval (YYYY-MM-DD format)")] string? endDate = null)
        {
            try
            {
                DateTime? start = string.IsNullOrEmpty(startDate) ? null : DateTime.Parse(startDate);
                DateTime? end = string.IsNullOrEmpty(endDate) ? null : DateTime.Parse(endDate);

                var tradeData = await _globalEconomicService.GetGlobalTradeDataAsync(start, end);
                return $"Retrieved global trade data for {tradeData.Count} countries";
            }
            catch (Exception ex)
            {
                return $"Error retrieving global trade data: {ex.Message}";
            }
        }

        [KernelFunction, Description("Get global currency exchange rates")]
        public async Task<string> GetGlobalCurrencyData()
        {
            try
            {
                var currencyData = await _globalEconomicService.GetGlobalCurrencyDataAsync();
                return $"Retrieved currency data for {currencyData.Count} currency pairs";
            }
            catch (Exception ex)
            {
                return $"Error retrieving global currency data: {ex.Message}";
            }
        }

        [KernelFunction, Description("Get global commodity prices")]
        public async Task<string> GetGlobalCommodityPrices()
        {
            try
            {
                var commodityData = await _globalEconomicService.GetGlobalCommodityPricesAsync();
                return $"Retrieved commodity prices for {commodityData.Count} commodities";
            }
            catch (Exception ex)
            {
                return $"Error retrieving global commodity prices: {ex.Message}";
            }
        }
    }
}