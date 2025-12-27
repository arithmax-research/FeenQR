using System.ComponentModel;
using Microsoft.SemanticKernel;
using QuantResearchAgent.Services;

namespace QuantResearchAgent.Plugins
{
    public class ComplianceMonitoringPlugin
    {
        private readonly ComplianceMonitoringService _complianceService;

        public ComplianceMonitoringPlugin(ComplianceMonitoringService complianceService)
        {
            _complianceService = complianceService;
        }

        [KernelFunction, Description("Run all compliance checks and report violations")]
        public async Task<string> CheckCompliance()
        {
            try
            {
                var violations = await _complianceService.CheckComplianceAsync();

                if (!violations.Any())
                {
                    return "✅ All compliance checks passed - no violations found";
                }

                var result = $"⚠️ Compliance Violations Found ({violations.Count}):\n\n";
                foreach (var violation in violations)
                {
                    var severityIcon = violation.Severity switch
                    {
                        "critical" => "🚨",
                        "high" => "⚠️",
                        "medium" => "🟡",
                        "low" => "ℹ️",
                        _ => "❓"
                    };

                    result += $"{severityIcon} {violation.Severity.ToUpper()} - {violation.Description}\n";
                    result += $"   Current: {violation.CurrentValue:F2}, Threshold: {violation.Threshold:F2}\n";
                    result += $"   Detected: {violation.DetectedAt}\n\n";
                }

                return result;
            }
            catch (Exception ex)
            {
                return $"Error checking compliance: {ex.Message}";
            }
        }

        [KernelFunction, Description("Get list of active compliance rules")]
        public async Task<string> GetComplianceRules()
        {
            try
            {
                var rules = await _complianceService.GetActiveComplianceRulesAsync();

                if (!rules.Any())
                {
                    return "No active compliance rules";
                }

                var result = $"Active Compliance Rules ({rules.Count}):\n";
                foreach (var rule in rules)
                {
                    result += $"- {rule.RuleId}: {rule.Description} (Threshold: {rule.Threshold})\n";
                }

                return result;
            }
            catch (Exception ex)
            {
                return $"Error getting compliance rules: {ex.Message}";
            }
        }

        [KernelFunction, Description("Get recent compliance violations")]
        public async Task<string> GetRecentViolations(
            [Description("Days to look back")] int days = 7)
        {
            try
            {
                var violations = await _complianceService.GetRecentViolationsAsync(days);

                if (!violations.Any())
                {
                    return $"No compliance violations in the last {days} days";
                }

                var result = $"Recent Compliance Violations (last {days} days):\n";
                foreach (var violation in violations.Where(v => !v.IsResolved))
                {
                    result += $"- {violation.DetectedAt}: {violation.Description} ({violation.Severity})\n";
                }

                return result;
            }
            catch (Exception ex)
            {
                return $"Error getting recent violations: {ex.Message}";
            }
        }

        [KernelFunction, Description("Resolve a compliance violation")]
        public async Task<string> ResolveViolation(
            [Description("Violation ID to resolve")] string violationId)
        {
            try
            {
                var success = await _complianceService.ResolveViolationAsync(violationId);

                if (success)
                {
                    return $"Compliance violation {violationId} marked as resolved";
                }
                else
                {
                    return "Failed to resolve compliance violation";
                }
            }
            catch (Exception ex)
            {
                return $"Error resolving violation: {ex.Message}";
            }
        }

        [KernelFunction, Description("Get compliance status summary")]
        public async Task<string> GetComplianceSummary()
        {
            try
            {
                var violations = await _complianceService.CheckComplianceAsync();
                var rules = await _complianceService.GetActiveComplianceRulesAsync();

                var result = "Compliance Status Summary:\n\n";
                result += $"📋 Active Rules: {rules.Count}\n";
                result += $"⚠️ Current Violations: {violations.Count}\n\n";

                if (violations.Any())
                {
                    var criticalCount = violations.Count(v => v.Severity == "critical");
                    var highCount = violations.Count(v => v.Severity == "high");
                    var mediumCount = violations.Count(v => v.Severity == "medium");
                    var lowCount = violations.Count(v => v.Severity == "low");

                    result += "Violation Breakdown:\n";
                    if (criticalCount > 0) result += $"- Critical: {criticalCount}\n";
                    if (highCount > 0) result += $"- High: {highCount}\n";
                    if (mediumCount > 0) result += $"- Medium: {mediumCount}\n";
                    if (lowCount > 0) result += $"- Low: {lowCount}\n";
                }
                else
                {
                    result += " All rules are compliant";
                }

                return result;
            }
            catch (Exception ex)
            {
                return $"Error getting compliance summary: {ex.Message}";
            }
        }
    }
}