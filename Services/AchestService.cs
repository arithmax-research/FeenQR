using Microsoft.Extensions.Logging;
using QuantResearchAgent.Core;

namespace QuantResearchAgent.Services;

/// <summary>
/// High-level façade over <see cref="AchestClient"/> that returns FeenQR's
/// native <see cref="MarketData"/> shapes. This is the single entry point the
/// rest of the app should use for market data so all provider access flows
/// through the Arithmax Chest API.
/// </summary>
public class AchestService
{
    private readonly AchestClient _client;
    private readonly ILogger<AchestService> _logger;

    public AchestService(AchestClient client, ILogger<AchestService> logger)
    {
        _client = client;
        _logger = logger;
    }

    /// <summary>True when the achest service is reachable.</summary>
    public Task<bool> IsHealthyAsync(CancellationToken ct = default) => _client.HealthAsync(ct);

    /// <summary>Resolves the provider achest would use for a symbol.</summary>
    public Task<string?> RouteProviderAsync(string symbol, string resolution = "daily", CancellationToken ct = default)
        => _client.RouteAsync(symbol, resolution, "auto", ct);

    /// <summary>
    /// Returns the latest quote for a symbol as a <see cref="MarketData"/> object,
    /// derived from the two most recent achest daily bars.
    /// </summary>
    public async Task<MarketData?> GetQuoteAsync(string symbol, string resolution = "daily", CancellationToken ct = default)
    {
        try
        {
            var bars = await _client.GetDataAsync(symbol, DateTime.UtcNow.Date.AddDays(-14), DateTime.UtcNow.Date.AddDays(1), resolution, "auto", ct).ConfigureAwait(false);
            if (bars.Count == 0)
            {
                return null;
            }

            var latest = bars[^1];
            var previous = bars.Count > 1 ? bars[^2] : null;
            var change = previous != null ? latest.Close - previous.Close : latest.Close - latest.Open;
            var changePct = previous != null && previous.Close != 0 ? (change / previous.Close) * 100 : 0;

            return new MarketData
            {
                Symbol = string.IsNullOrWhiteSpace(latest.Symbol) ? symbol : latest.Symbol,
                Price = latest.Close,
                Close = latest.Close,
                Volume = latest.Volume,
                High24h = latest.High,
                Low24h = latest.Low,
                Change24h = change,
                ChangePercent24h = changePct,
                Timestamp = latest.Timestamp,
                Source = "achest",
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "achest quote fetch failed for {Symbol}", symbol);
            return null;
        }
    }

    /// <summary>
    /// Returns historical OHLCV bars mapped to <see cref="MarketData"/> records,
    /// newest last, capped at <paramref name="limit"/> rows.
    /// </summary>
    public async Task<List<MarketData>?> GetHistoricalDataAsync(string symbol, int limit = 100, string resolution = "daily", CancellationToken ct = default)
    {
        try
        {
            var res = AchestClient.NormalizeResolution(resolution);
            var end = DateTime.UtcNow.Date.AddDays(1);
            var start = end.AddDays(-Math.Max(limit, 1) * 2);
            var bars = await _client.GetDataAsync(symbol, start, end, res, "auto", ct).ConfigureAwait(false);
            if (bars.Count == 0)
            {
                return null;
            }

            var mapped = bars.Select(b => new MarketData
            {
                Symbol = string.IsNullOrWhiteSpace(b.Symbol) ? symbol : b.Symbol,
                Price = b.Close,
                Close = b.Close,
                Volume = b.Volume,
                High24h = b.High,
                Low24h = b.Low,
                Timestamp = b.Timestamp,
                Source = "achest",
            }).ToList();

            return mapped.Count > limit ? mapped.TakeLast(limit).ToList() : mapped;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "achest historical fetch failed for {Symbol}", symbol);
            return null;
        }
    }

    /// <summary>Raw bars passthrough, when callers need OHLCV directly.</summary>
    public Task<List<AchestBar>> GetBarsAsync(string symbol, DateTime start, DateTime end, string resolution = "daily", CancellationToken ct = default)
        => _client.GetDataAsync(symbol, start, end, resolution, "auto", ct);

    /// <summary>
    /// Computes the market beta of a symbol versus a benchmark index (default S&amp;P 500,
    /// <c>^GSPC</c>) from daily OHLCV returned by achest (Yahoo-routed). Beta is the
    /// covariance of daily returns divided by the benchmark variance over
    /// <paramref name="lookbackDays"/> trading days. Returns <c>null</c> when there
    /// is insufficient overlapping data.
    /// </summary>
    public async Task<double?> GetBetaAsync(string symbol, string benchmark = "^GSPC", int lookbackDays = 365, CancellationToken ct = default)
    {
        try
        {
            var end = DateTime.UtcNow.Date.AddDays(1);
            // Pad the calendar range so we get ~lookbackDays trading days.
            var start = end.AddDays(-(int)(lookbackDays * 1.6));

            var symbolBars = await _client.GetDataAsync(symbol, start, end, "daily", "auto", ct).ConfigureAwait(false);
            if (symbolBars.Count < 30)
            {
                return null;
            }

            List<AchestBar> benchBars;
            if (string.Equals(symbol, benchmark, StringComparison.OrdinalIgnoreCase))
            {
                return 1.0;
            }
            benchBars = await _client.GetDataAsync(benchmark, start, end, "daily", "auto", ct).ConfigureAwait(false);
            if (benchBars.Count < 30)
            {
                return null;
            }

            // Align on date (daily close), compute simple returns.
            var benchByDate = benchBars
                .GroupBy(b => b.Timestamp.Date)
                .ToDictionary(g => g.Key, g => g.Last().Close);

            var symbolReturns = new List<double>();
            var benchReturns = new List<double>();
            double? prevSymbol = null;
            double? prevBench = null;

            foreach (var bar in symbolBars.OrderBy(b => b.Timestamp))
            {
                var date = bar.Timestamp.Date;
                if (!benchByDate.TryGetValue(date, out var benchClose))
                {
                    continue;
                }
                if (prevSymbol is > 0 && prevBench is > 0 && bar.Close > 0 && benchClose > 0)
                {
                    symbolReturns.Add(bar.Close / prevSymbol.Value - 1.0);
                    benchReturns.Add(benchClose / prevBench.Value - 1.0);
                }
                prevSymbol = bar.Close;
                prevBench = benchClose;
            }

            if (symbolReturns.Count < 20)
            {
                return null;
            }

            var n = symbolReturns.Count;
            var meanS = symbolReturns.Average();
            var meanB = benchReturns.Average();

            double cov = 0, varB = 0;
            for (var i = 0; i < n; i++)
            {
                var ds = symbolReturns[i] - meanS;
                var db = benchReturns[i] - meanB;
                cov += ds * db;
                varB += db * db;
            }

            if (varB == 0)
            {
                return null;
            }

            return cov / varB;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "achest beta computation failed for {Symbol}", symbol);
            return null;
        }
    }

    /// <summary>Access to the underlying low-level client for extended endpoints.</summary>
    public AchestClient Client => _client;

    // ── Generic Eulerpool passthrough with convenience accessors ───────

    /// <summary>
    /// Fetch any achest Eulerpool endpoint as a raw <see cref="JsonElement"/>.
    /// Returns <c>null</c> on failure (never throws), so callers can fall back.
    /// </summary>
    public async Task<System.Text.Json.JsonElement?> TryEulerpoolAsync(string path, CancellationToken ct = default)
    {
        try
        {
            return await _client.GetEulerpoolAsync(path, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "achest eulerpool fetch failed for {Path}", path);
            return null;
        }
    }

    /// <summary>Company profile + overview combined (Eulerpool fundamentals).</summary>
    public Task<System.Text.Json.JsonElement?> GetCompanyOverviewAsync(string symbol, CancellationToken ct = default)
        => TryEulerpoolAsync($"fundamentals/overview/{Uri.EscapeDataString(symbol)}", ct);

    /// <summary>40+ financial ratios (P/E, ROE, margins, growth, leverage).</summary>
    public Task<System.Text.Json.JsonElement?> GetFinancialMetricsAsync(string symbol, CancellationToken ct = default)
        => TryEulerpoolAsync($"fundamentals/metrics/{Uri.EscapeDataString(symbol)}", ct);

    /// <summary>Company profile: description, sector, employees, website.</summary>
    public Task<System.Text.Json.JsonElement?> GetCompanyProfileAsync(string symbol, CancellationToken ct = default)
        => TryEulerpoolAsync($"fundamentals/profile/{Uri.EscapeDataString(symbol)}", ct);

    /// <summary>Annual income statements.</summary>
    public Task<System.Text.Json.JsonElement?> GetIncomeStatementAsync(string symbol, CancellationToken ct = default)
        => TryEulerpoolAsync($"fundamentals/income/{Uri.EscapeDataString(symbol)}", ct);

    /// <summary>Annual balance sheets.</summary>
    public Task<System.Text.Json.JsonElement?> GetBalanceSheetAsync(string symbol, CancellationToken ct = default)
        => TryEulerpoolAsync($"fundamentals/balance/{Uri.EscapeDataString(symbol)}", ct);

    /// <summary>Annual cash-flow statements.</summary>
    public Task<System.Text.Json.JsonElement?> GetCashFlowAsync(string symbol, CancellationToken ct = default)
        => TryEulerpoolAsync($"fundamentals/cashflow/{Uri.EscapeDataString(symbol)}", ct);

    /// <summary>Analyst estimates (revenue / EPS / EBIT).</summary>
    public Task<System.Text.Json.JsonElement?> GetAnalystEstimatesAsync(string symbol, CancellationToken ct = default)
        => TryEulerpoolAsync($"analyst/estimates/{Uri.EscapeDataString(symbol)}", ct);

    /// <summary>Analyst price target consensus.</summary>
    public Task<System.Text.Json.JsonElement?> GetPriceTargetAsync(string symbol, CancellationToken ct = default)
        => TryEulerpoolAsync($"analyst/price-target/{Uri.EscapeDataString(symbol)}", ct);

    /// <summary>News and research for a ticker.</summary>
    public Task<System.Text.Json.JsonElement?> GetNewsAsync(string ticker, CancellationToken ct = default)
        => TryEulerpoolAsync($"news/{Uri.EscapeDataString(ticker)}", ct);

    /// <summary>Market-wide news.</summary>
    public Task<System.Text.Json.JsonElement?> GetMarketNewsAsync(CancellationToken ct = default)
        => TryEulerpoolAsync("news/market", ct);

    /// <summary>Latest quotes for the market.</summary>
    public Task<System.Text.Json.JsonElement?> GetLatestQuotesAsync(CancellationToken ct = default)
        => TryEulerpoolAsync("market/latest-quotes", ct);

    /// <summary>Macro economic calendar.</summary>
    public Task<System.Text.Json.JsonElement?> GetEconomicCalendarAsync(CancellationToken ct = default)
        => TryEulerpoolAsync("macro/calendar", ct);

    /// <summary>FRED series via achest macro endpoint.</summary>
    public Task<System.Text.Json.JsonElement?> GetFredSeriesAsync(string seriesId, CancellationToken ct = default)
        => TryEulerpoolAsync($"macro/fred/{Uri.EscapeDataString(seriesId)}", ct);

    /// <summary>Earnings calendar for a specific date (yyyy-MM-dd).</summary>
    public Task<System.Text.Json.JsonElement?> GetEarningsCalendarAsync(string date, CancellationToken ct = default)
        => TryEulerpoolAsync($"calendar/earnings/{Uri.EscapeDataString(date)}", ct);

    /// <summary>Crypto extended data by endpoint (top, fear-greed, ...).</summary>
    public Task<System.Text.Json.JsonElement?> GetCryptoAsync(string endpoint, CancellationToken ct = default)
        => TryEulerpoolAsync($"crypto/{endpoint.TrimStart('/')}", ct);

    /// <summary>Options &amp; derivatives data by endpoint (chain/AAPL, greeks, ...).</summary>
    public Task<System.Text.Json.JsonElement?> GetOptionsAsync(string endpoint, CancellationToken ct = default)
        => TryEulerpoolAsync($"options/{endpoint.TrimStart('/')}", ct);

    /// <summary>ETF data (profile, holdings, flows).</summary>
    public Task<System.Text.Json.JsonElement?> GetEtfAsync(string identifier, string dataType = "profile", CancellationToken ct = default)
        => TryEulerpoolAsync($"etf/{dataType}/{Uri.EscapeDataString(identifier)}", ct);
}

