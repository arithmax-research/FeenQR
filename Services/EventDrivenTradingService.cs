using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using QuantResearchAgent.Core;

namespace QuantResearchAgent.Services
{
    public class EventDrivenTradingService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<EventDrivenTradingService> _logger;
        private readonly IConfiguration _configuration;
        private readonly AdvancedAlpacaService _alpacaService;
        private readonly NewsSentimentAnalysisService _newsService;
        private readonly FederalReserveService _fedService;
        private readonly GeopoliticalRiskService _geoRiskService;

        public EventDrivenTradingService(
            HttpClient httpClient,
            ILogger<EventDrivenTradingService> logger,
            IConfiguration configuration,
            AdvancedAlpacaService alpacaService,
            NewsSentimentAnalysisService newsService,
            FederalReserveService fedService,
            GeopoliticalRiskService geoRiskService)
        {
            _httpClient = httpClient;
            _logger = logger;
            _configuration = configuration;
            _alpacaService = alpacaService;
            _newsService = newsService;
            _fedService = fedService;
            _geoRiskService = geoRiskService;
        }

        public class TradingRule
        {
            public string RuleId { get; set; }
            public string EventType { get; set; } // "news", "economic", "geopolitical"
            public string Symbol { get; set; }
            public decimal SentimentThreshold { get; set; }
            public string Action { get; set; } // "buy", "sell", "hold"
            public int Quantity { get; set; }
            public bool IsActive { get; set; }
        }

        public class MarketEvent
        {
            public string EventId { get; set; }
            public string EventType { get; set; }
            public string Symbol { get; set; }
            public string Headline { get; set; }
            public decimal SentimentScore { get; set; }
            public decimal ImpactScore { get; set; }
            public DateTime Timestamp { get; set; }
            public Dictionary<string, object> Metadata { get; set; }
        }

        public async Task<List<TradingRule>> GetActiveRulesAsync()
        {
            // In a real implementation, this would load from database
            return new List<TradingRule>
            {
                new TradingRule
                {
                    RuleId = "news-positive-aapl",
                    EventType = "news",
                    Symbol = "AAPL",
                    SentimentThreshold = 0.7m,
                    Action = "buy",
                    Quantity = 100,
                    IsActive = true
                },
                new TradingRule
                {
                    RuleId = "fed-rate-hike",
                    EventType = "economic",
                    Symbol = "SPY",
                    SentimentThreshold = -0.3m,
                    Action = "sell",
                    Quantity = 50,
                    IsActive = true
                }
            };
        }

        public async Task<MarketEvent> DetectMarketEventsAsync()
        {
            try
            {
                _logger.LogInformation("Detecting market events...");

                var events = new List<MarketEvent>();

                // Check for news events
                var newsEvents = await DetectNewsEventsAsync();
                events.AddRange(newsEvents);

                // Check for economic events
                var economicEvents = await DetectEconomicEventsAsync();
                events.AddRange(economicEvents);

                // Check for geopolitical events
                var geoEvents = await DetectGeopoliticalEventsAsync();
                events.AddRange(geoEvents);

                // Return the most significant event
                var significantEvent = events
                    .OrderByDescending(e => Math.Abs(e.ImpactScore))
                    .FirstOrDefault();

                if (significantEvent != null)
                {
                    _logger.LogInformation($"Detected significant event: {significantEvent.Headline} (Impact: {significantEvent.ImpactScore})");
                }

                return significantEvent;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error detecting market events: {ex.Message}");
                return null;
            }
        }

        public async Task<bool> ExecuteEventDrivenTradeAsync(MarketEvent marketEvent, TradingRule rule)
        {
            try
            {
                _logger.LogInformation($"Executing event-driven trade for event: {marketEvent.EventId}, rule: {rule.RuleId}");

                // Validate the event meets the rule criteria
                if (!ShouldExecuteTrade(marketEvent, rule))
                {
                    _logger.LogInformation("Event does not meet rule criteria, skipping trade");
                    return false;
                }

                // Execute the trade
                var orderResponse = await _alpacaService.PlaceMarketOrderAsync(
                    rule.Symbol,
                    rule.Quantity,
                    rule.Action);

                if (orderResponse != null)
                {
                    _logger.LogInformation($"Event-driven trade executed successfully: {rule.Action} {rule.Quantity} {rule.Symbol}");
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error executing event-driven trade: {ex.Message}");
                return false;
            }
        }

        public async Task<List<MarketEvent>> GetRecentEventsAsync(int hours = 24)
        {
            try
            {
                var cutoffTime = DateTime.UtcNow.AddHours(-hours);

                // In a real implementation, this would query a database
                // For now, return mock recent events
                return new List<MarketEvent>
                {
                    new MarketEvent
                    {
                        EventId = Guid.NewGuid().ToString(),
                        EventType = "news",
                        Symbol = "AAPL",
                        Headline = "Apple Reports Strong Q4 Earnings",
                        SentimentScore = 0.8m,
                        ImpactScore = 0.6m,
                        Timestamp = DateTime.UtcNow.AddHours(-2),
                        Metadata = new Dictionary<string, object> { { "source", "Yahoo Finance" } }
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting recent events: {ex.Message}");
                return new List<MarketEvent>();
            }
        }

        private async Task<List<MarketEvent>> DetectNewsEventsAsync()
        {
            var events = new List<MarketEvent>();

            try
            {
                // Get recent news with sentiment analysis
                var newsItems = await _newsService.GetSentimentAnalyzedNewsAsync("market", 10);

                foreach (var news in newsItems.Where(n => n.SentimentScore > 0.5m || n.SentimentScore < -0.5m))
                {
                    // Extract symbols mentioned in the news
                    var symbols = ExtractSymbolsFromNews(news.Title + " " + news.Summary);

                    foreach (var symbol in symbols)
                    {
                        events.Add(new MarketEvent
                        {
                            EventId = Guid.NewGuid().ToString(),
                            EventType = "news",
                            Symbol = symbol,
                            Headline = news.Title,
                            SentimentScore = news.SentimentScore,
                            ImpactScore = Math.Abs(news.SentimentScore) * 0.8m,
                            Timestamp = news.PublishedAt,
                            Metadata = new Dictionary<string, object>
                            {
                                { "source", news.Source },
                                { "url", news.Url }
                            }
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Error detecting news events: {ex.Message}");
            }

            return events;
        }

        private async Task<List<MarketEvent>> DetectEconomicEventsAsync()
        {
            var events = new List<MarketEvent>();

            try
            {
                // Check for recent FOMC announcements
                var fomcData = await _fedService.GetLatestFOMCDataAsync();

                if (fomcData != null && fomcData.Timestamp > DateTime.UtcNow.AddHours(-24))
                {
                    // Analyze the impact of FOMC decisions
                    var impactScore = AnalyzeFOMCImpact(fomcData);

                    events.Add(new MarketEvent
                    {
                        EventId = Guid.NewGuid().ToString(),
                        EventType = "economic",
                        Symbol = "SPY", // Broad market impact
                        Headline = $"FOMC: {fomcData.Decision}",
                        SentimentScore = impactScore > 0 ? 0.3m : -0.3m,
                        ImpactScore = Math.Abs(impactScore),
                        Timestamp = fomcData.Timestamp,
                        Metadata = new Dictionary<string, object>
                        {
                            { "fed_funds_rate", fomcData.FedFundsRate },
                            { "decision", fomcData.Decision }
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Error detecting economic events: {ex.Message}");
            }

            return events;
        }

        private async Task<List<MarketEvent>> DetectGeopoliticalEventsAsync()
        {
            var events = new List<MarketEvent>();

            try
            {
                // Check for geopolitical risk events
                var riskEvents = await _geoRiskService.GetRecentRiskEventsAsync();

                foreach (var riskEvent in riskEvents.Where(r => r.RiskScore > 0.7m))
                {
                    events.Add(new MarketEvent
                    {
                        EventId = Guid.NewGuid().ToString(),
                        EventType = "geopolitical",
                        Symbol = "SPY", // Broad market impact
                        Headline = riskEvent.Description,
                        SentimentScore = -0.5m, // Geopolitical events typically negative
                        ImpactScore = riskEvent.RiskScore,
                        Timestamp = riskEvent.Timestamp,
                        Metadata = new Dictionary<string, object>
                        {
                            { "region", riskEvent.Region },
                            { "risk_score", riskEvent.RiskScore }
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Error detecting geopolitical events: {ex.Message}");
            }

            return events;
        }

        private bool ShouldExecuteTrade(MarketEvent marketEvent, TradingRule rule)
        {
            // Check if event type matches
            if (marketEvent.EventType != rule.EventType)
                return false;

            // Check sentiment threshold
            if (rule.EventType == "news" || rule.EventType == "economic")
            {
                if (Math.Abs(marketEvent.SentimentScore) < Math.Abs(rule.SentimentThreshold))
                    return false;
            }

            // Check symbol match
            if (!string.IsNullOrEmpty(rule.Symbol) && marketEvent.Symbol != rule.Symbol)
                return false;

            return true;
        }

        private List<string> ExtractSymbolsFromNews(string text)
        {
            // Simple symbol extraction - in production, use NLP
            var commonSymbols = new[] { "AAPL", "GOOGL", "MSFT", "AMZN", "TSLA", "NVDA", "SPY", "QQQ" };
            return commonSymbols.Where(symbol => text.Contains(symbol)).ToList();
        }

        private decimal AnalyzeFOMCImpact(object fomcData)
        {
            // Simplified FOMC impact analysis

        }
    }
}
