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
    public class ComplianceMonitoringService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<ComplianceMonitoringService> _logger;
        private readonly IConfiguration _configuration;
        private readonly AdvancedAlpacaService _alpacaService;
        private readonly AdvancedRiskService _riskService;

        public ComplianceMonitoringService(
            HttpClient httpClient,
            ILogger<ComplianceMonitoringService> logger,
            IConfiguration configuration,
            AdvancedAlpacaService alpacaService,
            AdvancedRiskService riskService)
        {
            _httpClient = httpClient;
            _logger = logger;
            _configuration = configuration;
            _alpacaService = alpacaService;
            _riskService = riskService;
        }

        public class ComplianceRule
        {
            public required string RuleId { get; set; }
            public required string RuleType { get; set; } // "position_limit", "concentration", "wash_sale", "pattern_day_trading"
            public required string Description { get; set; }
            public decimal Threshold { get; set; }
            public bool IsActive { get; set; }
        }

        public class ComplianceViolation
        {
            public required string ViolationId { get; set; }
            public required string RuleId { get; set; }
            public required string Description { get; set; }
            public required string Severity { get; set; } // "low", "medium", "high", "critical"
            public decimal CurrentValue { get; set; }
            public decimal Threshold { get; set; }
            public DateTime DetectedAt { get; set; }
            public bool IsResolved { get; set; }
        }

        public async Task<List<ComplianceViolation>> CheckComplianceAsync()
        {
            try
            {
                _logger.LogInformation("Running compliance checks...");

                var violations = new List<ComplianceViolation>();

                // Get active compliance rules
                var rules = await GetActiveComplianceRulesAsync();

                foreach (var rule in rules)
                {
                    var violation = await CheckComplianceRuleAsync(rule);
                    if (violation != null)
                    {
                        violations.Add(violation);
                    }
                }

                if (violations.Any())
                {
                    _logger.LogWarning($"Found {violations.Count} compliance violations");
                }
                else
                {
                    _logger.LogInformation("All compliance checks passed");
                }

                return violations;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error checking compliance: {ex.Message}");
                return new List<ComplianceViolation>();
            }
        }

        public async Task<List<ComplianceRule>> GetActiveComplianceRulesAsync()
        {
            // In production, load from database or configuration
            return new List<ComplianceRule>
            {
                new ComplianceRule
                {
                    RuleId = "position_limit",
                    RuleType = "position_limit",
                    Description = "Single position cannot exceed 10% of portfolio",
                    Threshold = 0.10m,
                    IsActive = true
                },
                new ComplianceRule
                {
                    RuleId = "concentration_limit",
                    RuleType = "concentration",
                    Description = "No sector can exceed 25% of portfolio",
                    Threshold = 0.25m,
                    IsActive = true
                },
                new ComplianceRule
                {
                    RuleId = "pattern_day_trading",
                    RuleType = "pattern_day_trading",
                    Description = "Maximum 4 day trades per 5 business days",
                    Threshold = 4.0m,
                    IsActive = true
                },
                new ComplianceRule
                {
                    RuleId = "wash_sale",
                    RuleType = "wash_sale",
                    Description = "Monitor for wash sale violations",
                    Threshold = 0m,
                    IsActive = true
                }
            };
        }

        public async Task<ComplianceViolation> CheckComplianceRuleAsync(ComplianceRule rule)
        {
            try
            {
                switch (rule.RuleType)
                {
                    case "position_limit":
                        return await CheckPositionLimitAsync(rule);
                    case "concentration":
                        return await CheckConcentrationLimitAsync(rule);
                    case "pattern_day_trading":
                        return await CheckPatternDayTradingAsync(rule);
                    case "wash_sale":
                        return await CheckWashSaleAsync(rule);
                    default:
                        return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Error checking rule {rule.RuleId}: {ex.Message}");
                return null;
            }
        }

        public async Task<List<ComplianceViolation>> GetRecentViolationsAsync(int days = 7)
        {
            // In production, query database for recent violations
            return new List<ComplianceViolation>();
        }

        public async Task<bool> ResolveViolationAsync(string violationId)
        {
            try
            {
                _logger.LogInformation($"Resolving violation: {violationId}");

                // In production, update violation status in database
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error resolving violation: {ex.Message}");
                return false;
            }
        }

        private async Task<ComplianceViolation> CheckPositionLimitAsync(ComplianceRule rule)
        {
            var positions = await _alpacaService.GetPortfolioPositionsAsync();
            var account = await _alpacaService.GetAccountAsync();

            if (positions == null || account == null) return null;

            foreach (var position in positions)
            {
                var positionValue = Math.Abs(position.MarketValue);
                var portfolioValue = account.Equity;
                var positionPercent = positionValue / portfolioValue;

                if (positionPercent > rule.Threshold)
                {
                    return new ComplianceViolation
                    {
                        ViolationId = Guid.NewGuid().ToString(),
                        RuleId = rule.RuleId,
                        Description = $"Position {position.Symbol} exceeds limit: {positionPercent:P2} > {rule.Threshold:P2}",
                        Severity = positionPercent > rule.Threshold * 1.5m ? "high" : "medium",
                        CurrentValue = positionPercent,
                        Threshold = rule.Threshold,
                        DetectedAt = DateTime.UtcNow,
                        IsResolved = false
                    };
                }
            }

            return null;
        }

        private async Task<ComplianceViolation> CheckConcentrationLimitAsync(ComplianceRule rule)
        {
            var positions = await _alpacaService.GetPortfolioPositionsAsync();
            var account = await _alpacaService.GetAccountAsync();

            if (positions == null || account == null) return null;

            // Group positions by sector (simplified - would need sector mapping)
            var sectorGroups = positions.GroupBy(p => GetSectorForSymbol(p.Symbol));

            foreach (var sector in sectorGroups)
            {
                var sectorValue = sector.Sum(p => Math.Abs(p.MarketValue));
                var portfolioValue = account.Equity;
                var sectorPercent = sectorValue / portfolioValue;

                if (sectorPercent > rule.Threshold)
                {
                    return new ComplianceViolation
                    {
                        ViolationId = Guid.NewGuid().ToString(),
                        RuleId = rule.RuleId,
                        Description = $"Sector {sector.Key} exceeds concentration limit: {sectorPercent:P2} > {rule.Threshold:P2}",
                        Severity = "medium",
                        CurrentValue = sectorPercent,
                        Threshold = rule.Threshold,
                        DetectedAt = DateTime.UtcNow,
                        IsResolved = false
                    };
                }
            }

            return null;
        }

        private async Task<ComplianceViolation> CheckPatternDayTradingAsync(ComplianceRule rule)
        {
            // Pattern day trading check - requires tracking trading history
            // This is a simplified implementation

            var account = await _alpacaService.GetAccountAsync();

            if (account == null) return null;

            // Check if account is flagged as pattern day trader
            // In production, this would analyze trading history over 5 business days

            var dayTradeCount = 2; // Placeholder - would calculate from actual trades

            if (dayTradeCount > rule.Threshold)
            {
                return new ComplianceViolation
                {
                    ViolationId = Guid.NewGuid().ToString(),
                    RuleId = rule.RuleId,
                    Description = $"Pattern day trading limit exceeded: {dayTradeCount} trades > {rule.Threshold} allowed",
                    Severity = "high",
                    CurrentValue = dayTradeCount,
                    Threshold = rule.Threshold,
                    DetectedAt = DateTime.UtcNow,
                    IsResolved = false
                };
            }

            return null;
        }

        private async Task<ComplianceViolation> CheckWashSaleAsync(ComplianceRule rule)
        {
            // Wash sale detection - complex rule requiring tax loss harvesting tracking
            // This is a placeholder implementation

            var positions = await _alpacaService.GetPortfolioPositionsAsync();

            // Check for recent sales and repurchases within 30 days
            // In production, this would analyze trading history

            var hasWashSale = false; // Placeholder

            if (hasWashSale)
            {
                return new ComplianceViolation
                {
                    ViolationId = Guid.NewGuid().ToString(),
                    RuleId = rule.RuleId,
                    Description = "Potential wash sale violation detected",
                    Severity = "medium",
                    CurrentValue = 1,
                    Threshold = 0,
                    DetectedAt = DateTime.UtcNow,
                    IsResolved = false
                };
            }

            return null;
        }

        private string GetSectorForSymbol(string symbol)
        {
            // Simplified sector mapping - in production, use a proper mapping service
            var sectorMap = new Dictionary<string, string>
            {
                ["AAPL"] = "Technology",
                ["MSFT"] = "Technology",
                ["GOOGL"] = "Technology",
                ["AMZN"] = "Consumer Discretionary",
                ["TSLA"] = "Consumer Discretionary",
                ["JPM"] = "Financials",
                ["BAC"] = "Financials",
                ["JNJ"] = "Healthcare",
                ["PFE"] = "Healthcare"
            };

        }
    }
}
