using System.ComponentModel;
using Microsoft.SemanticKernel;
using QuantResearchAgent.Services;

namespace QuantResearchAgent.Plugins
{
    public class EventDrivenTradingPlugin
    {
        private readonly EventDrivenTradingService _eventDrivenTradingService;

        public EventDrivenTradingPlugin(EventDrivenTradingService eventDrivenTradingService)
        {
            _eventDrivenTradingService = eventDrivenTradingService;
        }

        [KernelFunction, Description("Get list of active event-driven trading rules")]
        public async Task<string> GetActiveTradingRules()
        {
            try
            {
                var rules = await _eventDrivenTradingService.GetActiveRulesAsync();

                if (!rules.Any())
                {
                    return "No active trading rules found";
                }

                var result = "Active Trading Rules:\n";
                foreach (var rule in rules)
                {
                    result += $"- {rule.RuleId}: {rule.EventType} event for {rule.Symbol}, " +
                             $"{rule.Action} {rule.Quantity} shares when sentiment {rule.SentimentThreshold}\n";
                }

                return result;
            }
            catch (Exception ex)
            {
                return $"Error getting active trading rules: {ex.Message}";
            }
        }

        [KernelFunction, Description("Detect significant market events that could trigger trades")]
        public async Task<string> DetectMarketEvents()
        {
            try
            {
                var marketEvent = await _eventDrivenTradingService.DetectMarketEventsAsync();

                if (marketEvent == null)
                {
                    return "No significant market events detected";
                }

                return $"Market Event Detected:\n" +
                       $"- Type: {marketEvent.EventType}\n" +
                       $"- Symbol: {marketEvent.Symbol}\n" +
                       $"- Headline: {marketEvent.Headline}\n" +
                       $"- Sentiment Score: {marketEvent.SentimentScore:F2}\n" +
                       $"- Impact Score: {marketEvent.ImpactScore:F2}\n" +
                       $"- Time: {marketEvent.Timestamp}";
            }
            catch (Exception ex)
            {
                return $"Error detecting market events: {ex.Message}";
            }
        }

        [KernelFunction, Description("Execute automated trade based on market event and trading rule")]
        public async Task<string> ExecuteEventDrivenTrade(
            [Description("Market event details")] string eventDetails,
            [Description("Trading rule ID to apply")] string ruleId)
        {
            try
            {
                // Parse event details (simplified - in production would use structured data)
                var marketEvent = new EventDrivenTradingService.MarketEvent
                {
                    EventId = Guid.NewGuid().ToString(),
                    EventType = "news", // Would parse from eventDetails
                    Symbol = "AAPL", // Would extract from eventDetails
                    Headline = eventDetails,
                    SentimentScore = 0.8m, // Would analyze
                    ImpactScore = 0.6m,
                    Timestamp = DateTime.UtcNow
                };

                // Get the rule (simplified - would load by ID)
                var rules = await _eventDrivenTradingService.GetActiveRulesAsync();
                var rule = rules.FirstOrDefault(r => r.RuleId == ruleId);

                if (rule == null)
                {
                    return $"Trading rule {ruleId} not found";
                }

                var success = await _eventDrivenTradingService.ExecuteEventDrivenTradeAsync(marketEvent, rule);

                if (success)
                {
                    return $"Event-driven trade executed: {rule.Action} {rule.Quantity} {rule.Symbol} based on {marketEvent.Headline}";
                }
                else
                {
                    return "Event-driven trade was not executed (conditions not met or error occurred)";
                }
            }
            catch (Exception ex)
            {
                return $"Error executing event-driven trade: {ex.Message}";
            }
        }

        [KernelFunction, Description("Get recent market events that could affect trading decisions")]
        public async Task<string> GetRecentMarketEvents(
            [Description("Hours to look back")] int hours = 24)
        {
            try
            {
                var events = await _eventDrivenTradingService.GetRecentEventsAsync(hours);

                if (!events.Any())
                {
                    return $"No market events found in the last {hours} hours";
                }

                var result = $"Recent Market Events (last {hours} hours):\n";
                foreach (var evt in events)
                {
                    result += $"- {evt.Timestamp}: {evt.EventType} - {evt.Headline} " +
                             $"(Sentiment: {evt.SentimentScore:F2}, Impact: {evt.ImpactScore:F2})\n";
                }

                return result;
            }
            catch (Exception ex)
            {
                return $"Error getting recent market events: {ex.Message}";
            }
        }
    }
}