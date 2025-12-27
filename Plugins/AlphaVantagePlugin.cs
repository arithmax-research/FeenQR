using Microsoft.SemanticKernel;
using QuantResearchAgent.Services;
using System.ComponentModel;

namespace QuantResearchAgent.Plugins;

public class AlphaVantagePlugin
{
    private readonly AlphaVantageService _service;

    public AlphaVantagePlugin(AlphaVantageService service)
    {
        _service = service;
    }

    [KernelFunction, Description("Get comprehensive financial data from Alpha Vantage including quotes, fundamentals, and technical indicators")]
    public async Task<string> GetComprehensiveQuote(
        [Description("Stock symbol (e.g., AAPL, MSFT)")] string symbol)
    {
        try
        {
            var quote = await _service.GetQuoteAsync(symbol);
            var overview = await _service.GetCompanyOverviewAsync(symbol);
            var income = await _service.GetIncomeStatementAsync(symbol);
            var balance = await _service.GetBalanceSheetAsync(symbol);

            var result = $"Alpha Vantage Data for {symbol}:\n\n";

            if (quote != null)
            {
                result += $"Current Price: ${quote.Price:F2}\n";
                result += $"Change: ${quote.Change:F2} ({quote.ChangePercent})\n";
                result += $"Volume: {quote.Volume:N0}\n";
                result += $"Day Range: ${quote.Low:F2} - ${quote.High:F2}\n\n";
            }

            if (overview != null)
            {
                result += $"Company: {overview.Name}\n";
                result += $"Sector: {overview.Sector}\n";
                result += $"Industry: {overview.Industry}\n";
                result += $"Market Cap: ${overview.MarketCapitalization:N0}\n";
                result += $"P/E Ratio: {overview.PERatio}\n";
                result += $"EPS: ${overview.EPS}\n";
                result += $"Dividend Yield: {overview.DividendYield}%\n\n";
            }

            if (income?.Any() == true)
            {
                var latest = income.First();
                var fiscalYear = DateTime.TryParse(latest.FiscalDateEnding, out var date) ? date.Year.ToString() : latest.FiscalDateEnding;
                result += $"Latest Annual Income Statement ({fiscalYear}):\n";
                result += $"Revenue: ${latest.TotalRevenue:N0}\n";
                result += $"Net Income: ${latest.NetIncome:N0}\n";
                result += $"Operating Income: ${latest.OperatingIncome:N0}\n\n";
            }

            if (balance?.Any() == true)
            {
                var latest = balance.First();
                var fiscalYear = DateTime.TryParse(latest.FiscalDateEnding, out var date) ? date.Year.ToString() : latest.FiscalDateEnding;
                result += $"Latest Annual Balance Sheet ({fiscalYear}):\n";
                result += $"Total Assets: ${latest.TotalAssets:N0}\n";
                result += $"Total Liabilities: ${latest.TotalLiabilities:N0}\n";
                result += $"Total Shareholder Equity: ${latest.TotalShareholderEquity:N0}\n";
                result += $"Cash: ${latest.CashAndEquivalents:N0}\n";
            }

            return result;
        }
        catch (Exception ex)
        {
            return $"Error retrieving Alpha Vantage data: {ex.Message}";
        }
    }

    [KernelFunction, Description("Get technical indicators from Alpha Vantage")]
    public async Task<string> GetTechnicalIndicators(
        [Description("Stock symbol")] string symbol,
        [Description("Indicator type (SMA, EMA, RSI, MACD, etc.)")] string indicator = "SMA",
        [Description("Time interval")] string interval = "daily")
    {
        try
        {
            var result = $"Alpha Vantage {indicator} for {symbol}:\n\n";

            switch (indicator.ToUpper())
            {
                case "SMA":
                    var sma = await _service.GetSMAAsync(symbol, 20, interval);
                    if (sma?.Any() == true)
                    {
                        result += $"SMA(20): {sma.First().Indicators["SMA"]:F2}\n";
                    }
                    break;
                case "EMA":
                    var ema = await _service.GetEMAAsync(symbol, 20, interval);
                    if (ema?.Any() == true)
                    {
                        result += $"EMA(20): {ema.First().Indicators["EMA"]:F2}\n";
                    }
                    break;
                case "RSI":
                    var rsi = await _service.GetRSIAsync(symbol, 14, interval);
                    if (rsi?.Any() == true)
                    {
                        result += $"RSI(14): {rsi.First().Indicators["RSI"]:F2}\n";
                    }
                    break;
                case "MACD":
                    var macd = await _service.GetMACDAsync(symbol, interval);
                    if (macd?.Any() == true)
                    {
                        var latest = macd.First();
                        result += $"MACD: {latest.Indicators["MACD"]:F4}\n";
                    }
                    break;
                default:
                    result += $"Indicator {indicator} not supported. Try SMA, EMA, RSI, or MACD.";
                    break;
            }

            return result;
        }
        catch (Exception ex)
        {
            return $"Error retrieving technical indicators: {ex.Message}";
        }
    }

    [KernelFunction, Description("Get forex exchange rates from Alpha Vantage")]
    public async Task<string> GetForexRates(
        [Description("From currency")] string fromCurrency,
        [Description("To currency")] string toCurrency)
    {
        try
        {
            var forex = await _service.GetForexRateAsync(fromCurrency, toCurrency);

            if (forex != null)
            {
                return $"Forex Rate {fromCurrency}/{toCurrency}:\n" +
                       $"Exchange Rate: {forex.ExchangeRate:F4}\n" +
                       $"Last Refreshed: {forex.LastRefreshed}";
            }

            return $"No forex data available for {fromCurrency}/{toCurrency}";
        }
        catch (Exception ex)
        {
            return $"Error retrieving forex rates: {ex.Message}";
        }
    }
}