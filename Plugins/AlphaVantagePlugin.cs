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
                result += $"Current Price: ${quote.GlobalQuote.Price:F2}\n";
                result += $"Change: ${quote.GlobalQuote.Change:F2} ({quote.GlobalQuote.ChangePercent})\n";
                result += $"Volume: {quote.GlobalQuote.Volume:N0}\n";
                result += $"Day Range: ${quote.GlobalQuote.Low:F2} - ${quote.GlobalQuote.High:F2}\n\n";
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

            if (income?.AnnualReports?.Any() == true)
            {
                var latest = income.AnnualReports.First();
                result += $"Latest Annual Income Statement ({latest.FiscalDateEnding.Year}):\n";
                result += $"Revenue: ${latest.TotalRevenue:N0}\n";
                result += $"Net Income: ${latest.NetIncome:N0}\n";
                result += $"Operating Income: ${latest.OperatingIncome:N0}\n\n";
            }

            if (balance?.AnnualReports?.Any() == true)
            {
                var latest = balance.AnnualReports.First();
                result += $"Latest Annual Balance Sheet ({latest.FiscalDateEnding.Year}):\n";
                result += $"Total Assets: ${latest.TotalAssets:N0}\n";
                result += $"Total Liabilities: ${latest.TotalLiabilities:N0}\n";
                result += $"Total Shareholder Equity: ${latest.TotalShareholderEquity:N0}\n";
                result += $"Cash: ${latest.CashAndCashEquivalentsAtCarryingValue:N0}\n";
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
                    var sma = await _service.GetSMAAsync(symbol, interval, 20);
                    if (sma?.TechnicalAnalysis?.Any() == true)
                    {
                        result += $"SMA(20): {sma.TechnicalAnalysis.First().SMA:F2}\n";
                    }
                    break;
                case "EMA":
                    var ema = await _service.GetEMAAsync(symbol, interval, 20);
                    if (ema?.TechnicalAnalysis?.Any() == true)
                    {
                        result += $"EMA(20): {ema.TechnicalAnalysis.First().EMA:F2}\n";
                    }
                    break;
                case "RSI":
                    var rsi = await _service.GetRSIAsync(symbol, interval, 14);
                    if (rsi?.TechnicalAnalysis?.Any() == true)
                    {
                        result += $"RSI(14): {rsi.TechnicalAnalysis.First().RSI:F2}\n";
                    }
                    break;
                case "MACD":
                    var macd = await _service.GetMACDAsync(symbol, interval);
                    if (macd?.TechnicalAnalysis?.Any() == true)
                    {
                        var latest = macd.TechnicalAnalysis.First();
                        result += $"MACD: {latest.MACD:F4}\n";
                        result += $"Signal: {latest.MACD_Signal:F4}\n";
                        result += $"Histogram: {latest.MACD_Hist:F4}\n";
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

            if (forex?.RealtimeCurrencyExchangeRate != null)
            {
                var rate = forex.RealtimeCurrencyExchangeRate;
                return $"Forex Rate {fromCurrency}/{toCurrency}:\n" +
                       $"Exchange Rate: {rate.ExchangeRate:F4}\n" +
                       $"Bid Price: {rate.BidPrice:F4}\n" +
                       $"Ask Price: {rate.AskPrice:F4}\n" +
                       $"Last Refreshed: {rate.LastRefreshed}\n" +
                       $"Timezone: {rate.TimeZone}";
            }

            return $"No forex data available for {fromCurrency}/{toCurrency}";
        }
        catch (Exception ex)
        {
            return $"Error retrieving forex rates: {ex.Message}";
        }
    }
}