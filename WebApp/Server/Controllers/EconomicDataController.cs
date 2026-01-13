using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System;
using System.Net.Http;
using System.Text.Json;

namespace QuantResearchAgent.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EconomicDataController : ControllerBase
    {
        private readonly ILogger<EconomicDataController> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private const string FRED_API_KEY = "YOUR_FRED_API_KEY"; // Replace with actual key
        private const string WORLD_BANK_BASE = "https://api.worldbank.org/v2";
        private const string OECD_BASE = "https://stats.oecd.org/sdmx-json";
        private const string IMF_BASE = "http://dataservices.imf.org/REST/SDMX_JSON.svc";

        public EconomicDataController(ILogger<EconomicDataController> logger, IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
        }

        #region FRED Endpoints

        [HttpGet("fred/series/{seriesId}")]
        public async Task<IActionResult> GetFredSeries(string seriesId, [FromQuery] DateTime? startDate = null, [FromQuery] DateTime? endDate = null)
        {
            try
            {
                _logger.LogInformation($"Fetching FRED series: {seriesId}");
                
                var client = _httpClientFactory.CreateClient();
                var url = $"https://api.stlouisfed.org/fred/series/observations?series_id={seriesId}&api_key={FRED_API_KEY}&file_type=json";
                
                if (startDate.HasValue)
                    url += $"&observation_start={startDate.Value:yyyy-MM-dd}";
                if (endDate.HasValue)
                    url += $"&observation_end={endDate.Value:yyyy-MM-dd}";

                var response = await client.GetStringAsync(url);
                
                return Ok(new
                {
                    seriesId,
                    source = "FRED",
                    data = JsonSerializer.Deserialize<JsonElement>(response),
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error fetching FRED series {seriesId}");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("fred/search")]
        public async Task<IActionResult> SearchFred([FromQuery] string query, [FromQuery] int limit = 10)
        {
            try
            {
                _logger.LogInformation($"Searching FRED for: {query}");
                
                var client = _httpClientFactory.CreateClient();
                var url = $"https://api.stlouisfed.org/fred/series/search?search_text={query}&api_key={FRED_API_KEY}&file_type=json&limit={limit}";
                
                var response = await client.GetStringAsync(url);
                
                return Ok(new
                {
                    query,
                    source = "FRED",
                    results = JsonSerializer.Deserialize<JsonElement>(response),
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error searching FRED for {query}");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("fred/popular")]
        public IActionResult GetFredPopular()
        {
            try
            {
                var popularSeries = new[]
                {
                    new { id = "GDP", name = "Gross Domestic Product", category = "National Accounts" },
                    new { id = "UNRATE", name = "Unemployment Rate", category = "Labor Market" },
                    new { id = "CPIAUCSL", name = "Consumer Price Index", category = "Prices" },
                    new { id = "DFF", name = "Federal Funds Rate", category = "Interest Rates" },
                    new { id = "T10Y2Y", name = "10-Year Treasury Constant Maturity Minus 2-Year", category = "Interest Rates" },
                    new { id = "MORTGAGE30US", name = "30-Year Fixed Rate Mortgage Average", category = "Housing" },
                    new { id = "PAYEMS", name = "All Employees: Total Nonfarm", category = "Labor Market" },
                    new { id = "INDPRO", name = "Industrial Production Index", category = "Production" },
                    new { id = "HOUST", name = "Housing Starts", category = "Housing" },
                    new { id = "DEXUSEU", name = "U.S. / Euro Foreign Exchange Rate", category = "Exchange Rates" },
                    new { id = "VIXCLS", name = "CBOE Volatility Index: VIX", category = "Financial Markets" },
                    new { id = "M2SL", name = "M2 Money Stock", category = "Money Supply" },
                    new { id = "PSAVERT", name = "Personal Saving Rate", category = "Personal Income" },
                    new { id = "CIVPART", name = "Labor Force Participation Rate", category = "Labor Market" },
                    new { id = "UMCSENT", name = "University of Michigan Consumer Sentiment", category = "Sentiment" }
                };

                return Ok(new
                {
                    source = "FRED",
                    popularSeries,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting FRED popular series");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        #endregion

        #region World Bank Endpoints

        [HttpGet("worldbank/series/{indicatorId}")]
        public async Task<IActionResult> GetWorldBankSeries(string indicatorId, [FromQuery] string country = "US", [FromQuery] int? year = null)
        {
            try
            {
                _logger.LogInformation($"Fetching World Bank series: {indicatorId} for {country}");
                
                var client = _httpClientFactory.CreateClient();
                var dateFilter = year.HasValue ? $"{year}" : "all";
                var url = $"{WORLD_BANK_BASE}/country/{country}/indicator/{indicatorId}?date={dateFilter}&format=json";
                
                var response = await client.GetStringAsync(url);
                
                return Ok(new
                {
                    indicatorId,
                    country,
                    source = "World Bank",
                    data = JsonSerializer.Deserialize<JsonElement>(response),
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error fetching World Bank series {indicatorId}");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("worldbank/search")]
        public async Task<IActionResult> SearchWorldBank([FromQuery] string query)
        {
            try
            {
                _logger.LogInformation($"Searching World Bank for: {query}");
                
                var client = _httpClientFactory.CreateClient();
                var url = $"{WORLD_BANK_BASE}/indicator?format=json&per_page=50";
                
                var response = await client.GetStringAsync(url);
                var allIndicators = JsonSerializer.Deserialize<JsonElement>(response);
                
                // Filter results based on query (simplified)
                return Ok(new
                {
                    query,
                    source = "World Bank",
                    results = allIndicators,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error searching World Bank for {query}");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("worldbank/popular")]
        public IActionResult GetWorldBankPopular()
        {
            try
            {
                var popularIndicators = new[]
                {
                    new { id = "NY.GDP.MKTP.CD", name = "GDP (current US$)", category = "Economy" },
                    new { id = "SP.POP.TOTL", name = "Population, total", category = "Demographics" },
                    new { id = "FP.CPI.TOTL.ZG", name = "Inflation, consumer prices (annual %)", category = "Prices" },
                    new { id = "SL.UEM.TOTL.ZS", name = "Unemployment, total (% of total labor force)", category = "Labor" },
                    new { id = "NY.GDP.PCAP.CD", name = "GDP per capita (current US$)", category = "Economy" },
                    new { id = "NE.EXP.GNFS.ZS", name = "Exports of goods and services (% of GDP)", category = "Trade" },
                    new { id = "NE.IMP.GNFS.ZS", name = "Imports of goods and services (% of GDP)", category = "Trade" },
                    new { id = "GC.DOD.TOTL.GD.ZS", name = "Central government debt, total (% of GDP)", category = "Fiscal" },
                    new { id = "BX.KLT.DINV.WD.GD.ZS", name = "Foreign direct investment, net inflows (% of GDP)", category = "Investment" },
                    new { id = "SE.XPD.TOTL.GD.ZS", name = "Government expenditure on education, total (% of GDP)", category = "Education" }
                };

                return Ok(new
                {
                    source = "World Bank",
                    popularIndicators,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting World Bank popular indicators");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("worldbank/indicator/{indicatorId}")]
        public async Task<IActionResult> GetWorldBankIndicator(string indicatorId)
        {
            try
            {
                _logger.LogInformation($"Fetching World Bank indicator metadata: {indicatorId}");
                
                var client = _httpClientFactory.CreateClient();
                var url = $"{WORLD_BANK_BASE}/indicator/{indicatorId}?format=json";
                
                var response = await client.GetStringAsync(url);
                
                return Ok(new
                {
                    indicatorId,
                    source = "World Bank",
                    metadata = JsonSerializer.Deserialize<JsonElement>(response),
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error fetching World Bank indicator {indicatorId}");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        #endregion

        #region OECD Endpoints

        [HttpGet("oecd/series/{dataset}/{filter}")]
        public async Task<IActionResult> GetOecdSeries(string dataset, string filter = "all")
        {
            try
            {
                _logger.LogInformation($"Fetching OECD series: {dataset}/{filter}");
                
                var client = _httpClientFactory.CreateClient();
                var url = $"{OECD_BASE}/data/{dataset}/{filter}/all?contentType=json";
                
                var response = await client.GetStringAsync(url);
                
                return Ok(new
                {
                    dataset,
                    filter,
                    source = "OECD",
                    data = JsonSerializer.Deserialize<JsonElement>(response),
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error fetching OECD series {dataset}");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("oecd/search")]
        public IActionResult SearchOecd([FromQuery] string query)
        {
            try
            {
                _logger.LogInformation($"Searching OECD for: {query}");
                
                // Return common OECD datasets matching the query
                var datasets = new[]
                {
                    new { id = "QNA", name = "Quarterly National Accounts", category = "National Accounts" },
                    new { id = "MEI", name = "Main Economic Indicators", category = "Economic Indicators" },
                    new { id = "DP_LIVE", name = "Data Portal Live", category = "Various" },
                    new { id = "SNA_TABLE1", name = "Gross domestic product (GDP)", category = "National Accounts" },
                    new { id = "KEI", name = "Key Short-Term Economic Indicators", category = "Economic Indicators" }
                };

                var filtered = datasets.Where(d => 
                    d.name.Contains(query, StringComparison.OrdinalIgnoreCase) || 
                    d.category.Contains(query, StringComparison.OrdinalIgnoreCase)).ToArray();

                return Ok(new
                {
                    query,
                    source = "OECD",
                    results = filtered.Any() ? filtered : datasets,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error searching OECD for {query}");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("oecd/popular")]
        public IActionResult GetOecdPopular()
        {
            try
            {
                var popularIndicators = new[]
                {
                    new { id = "GDP", dataset = "QNA", name = "Gross Domestic Product", category = "National Accounts" },
                    new { id = "CPI", dataset = "MEI", name = "Consumer Price Index", category = "Prices" },
                    new { id = "LF_UE", dataset = "MEI", name = "Unemployment Rate", category = "Labor Market" },
                    new { id = "IR_LT", dataset = "MEI", name = "Long-term Interest Rates", category = "Financial Markets" },
                    new { id = "PROD_IND", dataset = "MEI", name = "Industrial Production", category = "Production" },
                    new { id = "CLI", dataset = "MEI", name = "Composite Leading Indicator", category = "Economic Indicators" },
                    new { id = "TRADE_BALANCE", dataset = "MEI", name = "Trade Balance", category = "Trade" },
                    new { id = "EXCHANG", dataset = "MEI", name = "Exchange Rates", category = "Exchange Rates" },
                    new { id = "NAEXKP01", dataset = "QNA", name = "Exports of goods and services", category = "Trade" },
                    new { id = "NAMGP01", dataset = "QNA", name = "Imports of goods and services", category = "Trade" }
                };

                return Ok(new
                {
                    source = "OECD",
                    popularIndicators,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting OECD popular indicators");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("oecd/indicator/{indicatorId}")]
        public IActionResult GetOecdIndicator(string indicatorId, [FromQuery] string dataset = "MEI")
        {
            try
            {
                _logger.LogInformation($"Fetching OECD indicator metadata: {indicatorId}");
                
                return Ok(new
                {
                    indicatorId,
                    dataset,
                    source = "OECD",
                    metadata = new
                    {
                        description = $"OECD indicator {indicatorId} from {dataset} dataset",
                        units = "Various",
                        frequency = "Monthly/Quarterly",
                        lastUpdated = DateTime.UtcNow
                    },
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error fetching OECD indicator {indicatorId}");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        #endregion

        #region IMF Endpoints

        [HttpGet("imf/series/{database}/{indicator}")]
        public async Task<IActionResult> GetImfSeries(string database, string indicator, [FromQuery] string country = "US")
        {
            try
            {
                _logger.LogInformation($"Fetching IMF series: {database}/{indicator} for {country}");
                
                var client = _httpClientFactory.CreateClient();
                var url = $"{IMF_BASE}/CompactData/{database}/.{country}.{indicator}";
                
                var response = await client.GetStringAsync(url);
                
                return Ok(new
                {
                    database,
                    indicator,
                    country,
                    source = "IMF",
                    data = JsonSerializer.Deserialize<JsonElement>(response),
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error fetching IMF series {database}/{indicator}");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("imf/search")]
        public IActionResult SearchImf([FromQuery] string query)
        {
            try
            {
                _logger.LogInformation($"Searching IMF for: {query}");
                
                var databases = new[]
                {
                    new { id = "IFS", name = "International Financial Statistics", category = "Financial" },
                    new { id = "DOT", name = "Direction of Trade Statistics", category = "Trade" },
                    new { id = "BOP", name = "Balance of Payments", category = "External Sector" },
                    new { id = "GFSMAB", name = "Government Finance Statistics", category = "Fiscal" },
                    new { id = "WHDREO", name = "World Economic Outlook", category = "Economic Outlook" }
                };

                var filtered = databases.Where(d => 
                    d.name.Contains(query, StringComparison.OrdinalIgnoreCase) || 
                    d.category.Contains(query, StringComparison.OrdinalIgnoreCase)).ToArray();

                return Ok(new
                {
                    query,
                    source = "IMF",
                    results = filtered.Any() ? filtered : databases,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error searching IMF for {query}");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("imf/popular")]
        public IActionResult GetImfPopular()
        {
            try
            {
                var popularIndicators = new[]
                {
                    new { id = "NGDP_R", database = "WHDREO", name = "Real GDP growth", category = "National Accounts" },
                    new { id = "PCPI", database = "WHDREO", name = "Inflation, average consumer prices", category = "Prices" },
                    new { id = "LUR", database = "WHDREO", name = "Unemployment rate", category = "Labor Market" },
                    new { id = "BCA", database = "BOP", name = "Current Account Balance", category = "External Sector" },
                    new { id = "GGXWDG_NGDP", database = "GFSMAB", name = "General government gross debt (% of GDP)", category = "Fiscal" },
                    new { id = "GGR_NGDP", database = "GFSMAB", name = "General government revenue (% of GDP)", category = "Fiscal" },
                    new { id = "ENDA_XDC_USD_RATE", database = "IFS", name = "Exchange Rate (USD)", category = "Exchange Rates" },
                    new { id = "FITB_BP6_USD", database = "BOP", name = "Financial Account", category = "External Sector" },
                    new { id = "TMG_CIF_USD", database = "DOT", name = "Imports, goods, CIF, value (US Dollars)", category = "Trade" },
                    new { id = "TXG_FOB_USD", database = "DOT", name = "Exports, goods, FOB, value (US Dollars)", category = "Trade" }
                };

                return Ok(new
                {
                    source = "IMF",
                    popularIndicators,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting IMF popular indicators");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("imf/indicator/{indicatorId}")]
        public IActionResult GetImfIndicator(string indicatorId, [FromQuery] string database = "IFS")
        {
            try
            {
                _logger.LogInformation($"Fetching IMF indicator metadata: {indicatorId}");
                
                return Ok(new
                {
                    indicatorId,
                    database,
                    source = "IMF",
                    metadata = new
                    {
                        description = $"IMF indicator {indicatorId} from {database} database",
                        units = "Various",
                        frequency = "Monthly/Quarterly/Annual",
                        lastUpdated = DateTime.UtcNow
                    },
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error fetching IMF indicator {indicatorId}");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        #endregion

        #region Additional Data Sources (Futures, Options, etc.)

        [HttpGet("futures/{symbol}")]
        public IActionResult GetFuturesData(string symbol)
        {
            try
            {
                _logger.LogInformation($"Fetching futures data for: {symbol}");
                
                // Placeholder for futures data integration
                // This would connect to your futures data provider
                
                return Ok(new
                {
                    symbol,
                    dataType = "Futures",
                    message = "Futures data endpoint - integrate with your futures data provider",
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error fetching futures data for {symbol}");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("options/{symbol}")]
        public IActionResult GetOptionsData(string symbol)
        {
            try
            {
                _logger.LogInformation($"Fetching options data for: {symbol}");
                
                // Placeholder for options data integration
                // This would connect to your options data provider
                
                return Ok(new
                {
                    symbol,
                    dataType = "Options",
                    message = "Options data endpoint - integrate with your options data provider",
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error fetching options data for {symbol}");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("commodities/{commodity}")]
        public IActionResult GetCommodityData(string commodity)
        {
            try
            {
                _logger.LogInformation($"Fetching commodity data for: {commodity}");
                
                // Placeholder for commodity data integration
                
                return Ok(new
                {
                    commodity,
                    dataType = "Commodities",
                    message = "Commodity data endpoint - integrate with your commodity data provider",
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error fetching commodity data for {commodity}");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("forex/{pair}")]
        public IActionResult GetForexData(string pair)
        {
            try
            {
                _logger.LogInformation($"Fetching forex data for: {pair}");
                
                // Placeholder for forex data integration
                
                return Ok(new
                {
                    pair,
                    dataType = "Forex",
                    message = "Forex data endpoint - integrate with your forex data provider",
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error fetching forex data for {pair}");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        #endregion

        #region Utility Endpoints

        [HttpGet("sources")]
        public IActionResult GetAvailableSources()
        {
            var sources = new
            {
                economicData = new[]
                {
                    new { name = "FRED", description = "Federal Reserve Economic Data", categories = new[] { "GDP", "Unemployment", "Inflation", "Interest Rates" } },
                    new { name = "World Bank", description = "World Bank Development Indicators", categories = new[] { "Economic Growth", "Demographics", "Trade", "Development" } },
                    new { name = "OECD", description = "Organisation for Economic Co-operation and Development", categories = new[] { "Economic Indicators", "Labor", "Trade", "National Accounts" } },
                    new { name = "IMF", description = "International Monetary Fund", categories = new[] { "Financial Statistics", "Balance of Payments", "Government Finance", "Economic Outlook" } }
                },
                marketData = new[]
                {
                    new { name = "Futures", description = "Futures market data", categories = new[] { "Commodities", "Indices", "Currencies" } },
                    new { name = "Options", description = "Options market data", categories = new[] { "Equity Options", "Index Options", "Volatility" } },
                    new { name = "Forex", description = "Foreign exchange data", categories = new[] { "Currency Pairs", "Cross Rates" } },
                    new { name = "Commodities", description = "Commodity prices", categories = new[] { "Energy", "Metals", "Agriculture" } }
                },
                timestamp = DateTime.UtcNow
            };

            return Ok(sources);
        }

        #endregion
    }
}
