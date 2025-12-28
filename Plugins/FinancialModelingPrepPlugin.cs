using System.ComponentModel;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using QuantResearchAgent.Services;

namespace QuantResearchAgent.Plugins;

/// <summary>
/// Plugin for Financial Modeling Prep API integration
/// Provides access to comprehensive financial data and analysis
/// </summary>
public class FinancialModelingPrepPlugin
{
    private readonly FinancialModelingPrepService _fmpService;

    public FinancialModelingPrepPlugin(FinancialModelingPrepService fmpService)
    {
        _fmpService = fmpService;
    }

    [KernelFunction("get_company_profile")]
    [Description("Get detailed company profile information including industry, sector, CEO, employees, and contact details")]
    public async Task<string> GetCompanyProfileAsync(
        [Description("Stock symbol (e.g., AAPL, MSFT, GOOGL)")] string symbol)
    {
        var profile = await _fmpService.GetCompanyProfileAsync(symbol);
        if (profile == null)
        {
            return $"No company profile found for symbol: {symbol}";
        }

        return $@"Company Profile for {profile.Symbol} ({profile.CompanyName}):
Industry: {profile.Industry}
Sector: {profile.Sector}
CEO: {profile.CEO}
Employees: {profile.FullTimeEmployees:N0}
Website: {profile.Website}
Phone: {profile.Phone}
Address: {profile.Address}, {profile.City}, {profile.State} {profile.Zip}, {profile.Country}
Description: {profile.Description}
IPO Date: {profile.IpoDate}
Actively Trading: {profile.IsActivelyTrading}";
    }

    [KernelFunction("get_stock_quote")]
    [Description("Get real-time stock quote with price, volume, and market data")]
    public async Task<string> GetStockQuoteAsync(
        [Description("Stock symbol (e.g., AAPL, MSFT, GOOGL)")] string symbol)
    {
        var quote = await _fmpService.GetQuoteAsync(symbol);
        if (quote == null)
        {
            return $"No quote found for symbol: {symbol}";
        }

        return $@"Quote for {quote.Symbol} ({quote.Name}):
Price: ${quote.Price:F2}
Change: ${quote.Change:F2} ({quote.ChangesPercentage:F2}%)
Day Range: ${quote.DayLow:F2} - ${quote.DayHigh:F2}
52-Week Range: ${quote.YearLow:F2} - ${quote.YearHigh:F2}
Volume: {quote.Volume:N0}
Avg Volume: {quote.AvgVolume:N0}
Market Cap: ${quote.MarketCap:N0}
P/E Ratio: {quote.Pe:F2}
EPS: ${quote.Eps:F2}
Open: ${quote.Open:F2}
Previous Close: ${quote.PreviousClose:F2}";
    }

    [KernelFunction("get_income_statement")]
    [Description("Get income statement data for analysis of revenue, expenses, and profitability")]
    public async Task<string> GetIncomeStatementAsync(
        [Description("Stock symbol (e.g., AAPL, MSFT, GOOGL)")] string symbol,
        [Description("Number of periods to retrieve (default: 5)")] int limit = 5)
    {
        var statements = await _fmpService.GetIncomeStatementAsync(symbol, limit);
        if (statements == null || statements.Count == 0)
        {
            return $"No income statement data found for symbol: {symbol}";
        }

        var result = $"Income Statement for {symbol} (Last {statements.Count} periods):\n\n";
        foreach (var stmt in statements)
        {
            result += $@"Period: {stmt.Date} ({stmt.Period})
Revenue: ${stmt.Revenue:N0}
Cost of Revenue: ${stmt.CostOfRevenue:N0}
Gross Profit: ${stmt.GrossProfit:N0} ({stmt.GrossProfitRatio:P2})
Operating Expenses: ${stmt.OperatingExpenses:N0}
Operating Income: ${stmt.OperatingIncome:N0} ({stmt.OperatingIncomeRatio:P2})
Net Income: ${stmt.NetIncome:N0} ({stmt.NetIncomeRatio:P2})
EPS: ${stmt.Eps:F2}
EPS Diluted: ${stmt.Epsdiluted:F2}

";
        }

        return result.Trim();
    }

    [KernelFunction("get_balance_sheet")]
    [Description("Get balance sheet data showing assets, liabilities, and equity")]
    public async Task<string> GetBalanceSheetAsync(
        [Description("Stock symbol (e.g., AAPL, MSFT, GOOGL)")] string symbol,
        [Description("Number of periods to retrieve (default: 5)")] int limit = 5)
    {
        var sheets = await _fmpService.GetBalanceSheetAsync(symbol, limit);
        if (sheets == null || sheets.Count == 0)
        {
            return $"No balance sheet data found for symbol: {symbol}";
        }

        var result = $"Balance Sheet for {symbol} (Last {sheets.Count} periods):\n\n";
        foreach (var sheet in sheets)
        {
            result += $@"Period: {sheet.Date} ({sheet.Period})
Total Assets: ${sheet.TotalAssets:N0}
Total Liabilities: ${sheet.TotalLiabilities:N0}
Total Equity: ${sheet.TotalEquity:N0}
Cash & Equivalents: ${sheet.CashAndCashEquivalents:N0}
Total Current Assets: ${sheet.TotalCurrentAssets:N0}
Total Current Liabilities: ${sheet.TotalCurrentLiabilities:N0}
Long-term Debt: ${sheet.LongTermDebt:N0}
Total Debt: ${sheet.TotalDebt:N0}
Net Debt: ${sheet.NetDebt:N0}

";
        }

        return result.Trim();
    }

    [KernelFunction("get_cash_flow")]
    [Description("Get cash flow statement showing operating, investing, and financing activities")]
    public async Task<string> GetCashFlowAsync(
        [Description("Stock symbol (e.g., AAPL, MSFT, GOOGL)")] string symbol,
        [Description("Number of periods to retrieve (default: 5)")] int limit = 5)
    {
        var flows = await _fmpService.GetCashFlowAsync(symbol, limit);
        if (flows == null || flows.Count == 0)
        {
            return $"No cash flow data found for symbol: {symbol}";
        }

        var result = $"Cash Flow Statement for {symbol} (Last {flows.Count} periods):\n\n";
        foreach (var flow in flows)
        {
            result += $@"Period: {flow.Date} ({flow.Period})
Operating Cash Flow: ${flow.NetCashProvidedByOperatingActivities:N0}
Investing Cash Flow: ${flow.NetCashUsedForInvestingActivites:N0}
Financing Cash Flow: ${flow.NetCashUsedProvidedByFinancingActivities:N0}
Free Cash Flow: ${flow.FreeCashFlow:N0}
Capital Expenditure: ${flow.CapitalExpenditure:N0}
Net Change in Cash: ${flow.NetChangeInCash:N0}

";
        }

        return result.Trim();
    }

    [KernelFunction("get_key_metrics")]
    [Description("Get key financial metrics and ratios for valuation analysis")]
    public async Task<string> GetKeyMetricsAsync(
        [Description("Stock symbol (e.g., AAPL, MSFT, GOOGL)")] string symbol,
        [Description("Number of periods to retrieve (default: 5)")] int limit = 5)
    {
        var metrics = await _fmpService.GetKeyMetricsAsync(symbol, limit);
        if (metrics == null || metrics.Count == 0)
        {
            return $"No key metrics found for symbol: {symbol}";
        }

        var result = $"Key Metrics for {symbol} (Last {metrics.Count} periods):\n\n";
        foreach (var metric in metrics)
        {
            result += $@"Period: {metric.Date} ({metric.Period})
P/E Ratio: {metric.PeRatio:F2}
P/B Ratio: {metric.PbRatio:F2}
P/S Ratio: {metric.PriceToSalesRatio:F2}
EV/EBITDA: {metric.EnterpriseValueOverEBITDA:F2}
ROE: {metric.Roe:P2}
ROIC: {metric.Roic:P2}
Debt to Equity: {metric.DebtToEquity:F2}
Current Ratio: {metric.CurrentRatio:F2}
Dividend Yield: {metric.DividendYield:P2}
Free Cash Flow Yield: {metric.FreeCashFlowYield:P2}

";
        }

        return result.Trim();
    }

    [KernelFunction("get_financial_ratios")]
    [Description("Get comprehensive financial ratios for detailed analysis")]
    public async Task<string> GetFinancialRatiosAsync(
        [Description("Stock symbol (e.g., AAPL, MSFT, GOOGL)")] string symbol,
        [Description("Number of periods to retrieve (default: 5)")] int limit = 5)
    {
        var ratios = await _fmpService.GetFinancialRatiosAsync(symbol, limit);
        if (ratios == null || ratios.Count == 0)
        {
            return $"No financial ratios found for symbol: {symbol}";
        }

        var result = $"Financial Ratios for {symbol} (Last {ratios.Count} periods):\n\n";
        foreach (var ratio in ratios)
        {
            result += $@"Period: {ratio.Date} ({ratio.Period})
Profitability:
  Gross Margin: {ratio.GrossMargin:P2}
  Operating Margin: {ratio.OperatingMargin:P2}
  Net Profit Margin: {ratio.NetProfitMargin:P2}
  ROA: {ratio.ReturnOnAssets:P2}
  ROE: {ratio.ReturnOnEquity:P2}

Liquidity:
  Current Ratio: {ratio.CurrentRatio:F2}
  Quick Ratio: {ratio.QuickRatio:F2}
  Cash Ratio: {ratio.CashRatio:F2}

Leverage:
  Debt Ratio: {ratio.DebtRatio:F2}
  Debt to Equity: {ratio.DebtEquityRatio:F2}
  Interest Coverage: {ratio.InterestCoverage:F2}

Efficiency:
  Asset Turnover: {ratio.AssetTurnover:F2}
  Inventory Turnover: {ratio.InventoryTurnover:F2}
  Receivables Turnover: {ratio.ReceivablesTurnover:F2}

";
        }

        return result.Trim();
    }

    [KernelFunction("get_historical_prices")]
    [Description("Get historical price data for technical analysis")]
    public async Task<string> GetHistoricalPricesAsync(
        [Description("Stock symbol (e.g., AAPL, MSFT, GOOGL)")] string symbol,
        [Description("Start date in YYYY-MM-DD format")] string fromDate,
        [Description("End date in YYYY-MM-DD format")] string toDate)
    {
        var prices = await _fmpService.GetHistoricalPricesAsync(symbol, fromDate, toDate);
        if (prices == null || prices.Count == 0)
        {
            return $"No historical price data found for symbol: {symbol}";
        }

        var result = $"Historical Prices for {symbol} ({prices.Count} days):\n\n";
        result += "Date\t\tOpen\tHigh\tLow\tClose\tVolume\n";
        result += "----\t\t----\t----\t---\t-----\t------\n";

        foreach (var price in prices.OrderBy(p => p.Date))
        {
            result += $"{price.Date}\t${price.Open:F2}\t${price.High:F2}\t${price.Low:F2}\t${price.Close:F2}\t{price.Volume:N0}\n";
        }

        return result;
    }

    [KernelFunction("get_analyst_estimates")]
    [Description("Get analyst estimates for revenue, earnings, and growth projections")]
    public async Task<string> GetAnalystEstimatesAsync(
        [Description("Stock symbol (e.g., AAPL, MSFT, GOOGL)")] string symbol,
        [Description("Number of periods to retrieve (default: 5)")] int limit = 5)
    {
        var estimates = await _fmpService.GetAnalystEstimatesAsync(symbol, limit);
        if (estimates == null || estimates.Count == 0)
        {
            return $"No analyst estimates found for symbol: {symbol}";
        }

        var result = $"Analyst Estimates for {symbol} (Last {estimates.Count} periods):\n\n";
        foreach (var estimate in estimates)
        {
            result += $@"Period: {estimate.Date} ({estimate.Period})
Revenue Estimate: ${estimate.EstimatedRevenueAvg:N0}M (Low: ${estimate.EstimatedRevenueLow:N0}M, High: ${estimate.EstimatedRevenueHigh:N0}M)
EBITDA Estimate: ${estimate.EstimatedEbitdaAvg:N0}M (Low: ${estimate.EstimatedEbitdaLow:N0}M, High: ${estimate.EstimatedEbitdaHigh:N0}M)
Net Income Estimate: ${estimate.EstimatedNetIncomeAvg:N0}M (Low: ${estimate.EstimatedNetIncomeLow:N0}M, High: ${estimate.EstimatedNetIncomeHigh:N0}M)
EPS Estimate: ${estimate.EstimatedEpsAvg:F2} (Low: ${estimate.EstimatedEpsLow:F2}, High: ${estimate.EstimatedEpsHigh:F2})
Number of Analysts: Revenue: {estimate.NumberAnalystEstimatedRevenue}, EPS: {estimate.NumberAnalystsEstimatedEps}

";
        }

        return result.Trim();
    }

    [KernelFunction("screen_stocks")]
    [Description("Screen stocks based on criteria like market cap, P/E ratio, sector, etc.")]
    public async Task<string> ScreenStocksAsync(
        [Description("Minimum market capitalization")] long marketCapMin = 0,
        [Description("Maximum market capitalization")] long marketCapMax = 0,
        [Description("Minimum P/E ratio")] decimal peRatioMin = 0,
        [Description("Maximum P/E ratio")] decimal peRatioMax = 0,
        [Description("Sector filter")] string? sector = null,
        [Description("Industry filter")] string? industry = null,
        [Description("Country filter")] string? country = null)
    {
        var criteria = new FMPStockScreenerCriteria
        {
            MarketCapMin = marketCapMin,
            MarketCapMax = marketCapMax,
            PERatioMin = peRatioMin,
            PERatioMax = peRatioMax,
            Sector = sector,
            Industry = industry,
            Country = country
        };

        var results = await _fmpService.GetStockScreenerAsync(criteria);
        if (results == null || results.Count == 0)
        {
            return "No stocks match the screening criteria.";
        }

        var result = $"Stock Screener Results ({results.Count} matches):\n\n";
        result += "Symbol\tCompany\t\tSector\t\tMarket Cap\tPrice\n";
        result += "------\t-------\t\t------\t\t----------\t-----\n";

        foreach (var stock in results.Take(20)) // Limit to first 20 results
        {
            result += $"{stock.Symbol}\t{stock.CompanyName?.Substring(0, Math.Min(15, stock.CompanyName.Length))}\t{stock.Sector?.Substring(0, Math.Min(12, stock.Sector.Length))}\t{stock.MarketCap}\t{stock.Price}\n";
        }

        if (results.Count > 20)
        {
            result += $"\n... and {results.Count - 20} more results.";
        }

        return result;
    }

    [KernelFunction("get_market_indices")]
    [Description("Get current market index quotes (S&P 500, NASDAQ, Dow Jones, etc.)")]
    public async Task<string> GetMarketIndicesAsync()
    {
        var indices = await _fmpService.GetMarketIndicesAsync();
        if (indices == null || indices.Count == 0)
        {
            return "No market index data available.";
        }

        var result = "Market Indices:\n\n";
        result += "Index\t\t\tPrice\t\tChange\t\t% Change\n";
        result += "-----\t\t\t-----\t\t------\t\t--------\n";

        foreach (var index in indices)
        {
            var name = index.Name.Length > 20 ? index.Name.Substring(0, 17) + "..." : index.Name;
            result += $"{name}\t${index.Price:F2}\t${index.Change:F2}\t{index.ChangesPercentage:F2}%\n";
        }

        return result;
    }
}
