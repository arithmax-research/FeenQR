# LEAN Data Pipeline - Complete Setup Guide

## Overview

This data pipeline solves the problem of expensive data costs when using QuantConnect's LEAN engine by downloading free/cheaper data from Alpaca (US equities) and Binance (cryptocurrencies) and converting it to LEAN's format.

## What This Pipeline Does

1. **Downloads Market Data**: Gets OHLCV data from Alpaca and Binance
2. **Converts to LEAN Format**: Transforms data into LEAN's CSV format with proper compression
3. **Handles Multiple Assets**: Supports both equities and cryptocurrencies
4. **Multiple Resolutions**: Minute, hour, and daily data
5. **Data Validation**: Ensures data quality and integrity
6. **Rate Limiting**: Respects API limits to avoid getting blocked

## File Structure

```
data_pipeline/
├── README.md                 # Detailed documentation
├── SETUP_GUIDE.md           # This file
├── requirements.txt         # Python dependencies
├── setup.sh                 # Automated setup script
├── .env.example            # Environment variables template
├── config.py               # Configuration settings
├── env_loader.py          # Environment variable loader
├── utils.py               # Utility functions
├── alpaca_downloader.py   # Alpaca data downloader
├── binance_downloader.py  # Binance data downloader
├── data_validator.py      # Data validation tools
├── main.py               # Main pipeline script
└── test_setup.py         # Setup verification script
```

## Quick Start

### 1. Setup Environment

```bash
cd /Users/misango/codechest/Lean_cli_test/data_pipeline
./setup.sh
```

### 2. Configure API Keys

Copy the example environment file and add your API keys:

```bash
cp .env.example .env
# Edit .env with your actual API keys
```

For Alpaca (required for equity data):
- Go to https://alpaca.markets/
- Create free account
- Get API keys from dashboard
- Use paper trading keys for testing

For Binance (optional for crypto data):
- Go to https://www.binance.com/
- Create account
- Generate API keys (optional - public data works without keys)

### 3. Test Setup

```bash
python test_setup.py
```

### 4. Download Sample Data

```bash
# Test with small dataset
python main.py --test

# Download specific data
python main.py --source alpaca --resolution daily --equity-symbols AAPL GOOGL
python main.py --source binance --resolution minute --crypto-symbols BTCUSDT ETHUSDT
```

## Data Output Structure

The pipeline creates data in LEAN's expected format:

```
data/
├── equity/usa/
│   ├── daily/
│   │   └── aapl.zip                    # All daily data for AAPL
│   ├── minute/
│   │   └── aapl/
│   │       ├── 20240701_trade.zip      # Minute data for July 1, 2024
│   │       └── 20240702_trade.zip      # Minute data for July 2, 2024
│   └── hour/
│       └── aapl/
│           └── 20240701_trade.zip      # Hour data for July 1, 2024
└── crypto/binance/
    ├── daily/
    │   └── btcusdt.zip                 # All daily data for BTCUSDT
    ├── minute/
    │   └── btcusdt/
    │       ├── 20240701_trade.zip      # Minute data for July 1, 2024
    │       └── 20240702_trade.zip      # Minute data for July 2, 2024
    └── hour/
        └── btcusdt/
            └── 20240701_trade.zip      # Hour data for July 1, 2024
```

## CSV Format

Each ZIP file contains a CSV with the following format:

### Equity Data (Alpaca)
```csv
Time,Open,High,Low,Close,Volume
93000000,1500000,1520000,1495000,1510000,1000000
```
- Time: Milliseconds since midnight (NY timezone)
- OHLC: Price in deci-cents (price * 10000)
- Volume: Number of shares

### Crypto Data (Binance)
```csv
Time,Open,High,Low,Close,Volume
93000000,50000.50,50100.25,49950.00,50050.75,12.5
```
- Time: Milliseconds since midnight (UTC)
- OHLC: Actual price (no conversion)
- Volume: Base currency amount

## Command Line Usage

### Basic Commands

```bash
# Download both equity and crypto data
python main.py --source both --resolution daily

# Download only equity data
python main.py --source alpaca --resolution minute --equity-symbols AAPL GOOGL MSFT

# Download only crypto data
python main.py --source binance --resolution hour --crypto-symbols BTCUSDT ETHUSDT

# Custom date range
python main.py --start-date 2024-01-01 --end-date 2024-06-30 --resolution daily
```

### Advanced Options

```bash
# Test mode (limited data)
python main.py --test

# Help
python main.py --help

# Specific symbols and dates
python main.py --source both \
               --equity-symbols AAPL GOOGL MSFT TSLA NVDA \
               --crypto-symbols BTCUSDT ETHUSDT ADAUSDT SOLUSDT \
               --start-date 2024-01-01 \
               --end-date 2024-12-31 \
               --resolution minute
```

## Validation

After downloading data, validate it:

```bash
python data_validator.py
```

This will:
- Check file integrity
- Validate OHLC data
- Ensure proper timestamps
- Generate a validation report

## Integration with LEAN

Once data is downloaded, it will be automatically available in your LEAN backtests. Just ensure your `lean.json` points to the correct data folder:

```json
{
    "data-folder": "data",
    ...
}
```

## Troubleshooting

### Common Issues

1. **API Key Errors**
   - Check your .env file
   - Verify API keys are correct
   - Ensure proper permissions

2. **Rate Limiting**
   - Pipeline includes rate limiting
   - If you get errors, check API limits
   - Consider using smaller date ranges

3. **Data Gaps**
   - Some symbols may not have data for all periods
   - Check market hours and holidays
   - Validate data after download

4. **Storage Space**
   - High-resolution data can be large
   - Monitor disk space
   - Consider cleaning old data

### Getting Help

1. Check the logs: `data_pipeline.log`
2. Run validation: `python data_validator.py`
3. Test setup: `python test_setup.py`
4. Check configuration: Review `config.py`

## Cost Comparison

### Traditional LEAN Data
- QuantConnect: $50-200+ per month
- IEX Cloud: $100+ per month
- Polygon: $200+ per month

### This Pipeline
- Alpaca: FREE (with limitations)
- Binance: FREE (public data)
- Total: ~$0 per month

## Performance Optimization

### For Large Downloads
1. Use daily resolution when possible
2. Download in smaller date ranges
3. Use multiple API keys if available
4. Monitor rate limits

### For Storage
1. Clean old data regularly
2. Use compression (already implemented)
3. Consider using SSDs for better performance

## Next Steps

1. **Initial Setup**: Get basic data working
2. **Automation**: Set up cron jobs for regular updates
3. **Monitoring**: Add alerting for data quality issues
4. **Expansion**: Add more data sources (Yahoo Finance, etc.)
5. **Integration**: Connect with your LEAN strategies

## Data Sources Information

### Alpaca Markets
- **Type**: US Equities
- **Cost**: Free (with limitations)
- **Rate Limits**: 200 requests/minute
- **Data Quality**: Professional grade
- **Coverage**: All US stocks, ETFs

### Binance
- **Type**: Cryptocurrencies
- **Cost**: Free (public data)
- **Rate Limits**: 1200 requests/minute
- **Data Quality**: High
- **Coverage**: 500+ crypto pairs

This pipeline provides a cost-effective way to get quality financial data for your LEAN backtests while maintaining the flexibility to add more data sources as needed.
