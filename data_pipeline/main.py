"""
Main data pipeline script
"""

import argparse
import sys
from datetime import datetime, timedelta
from typing import List

from config import (
    DEFAULT_EQUITY_SYMBOLS, DEFAULT_CRYPTO_SYMBOLS,
    DEFAULT_START_DATE, DEFAULT_END_DATE,
    SUPPORTED_RESOLUTIONS
)
from alpaca_downloader import AlpacaDataDownloader
from binance_downloader import BinanceDataDownloader
from utils import setup_logging

logger = setup_logging()

def parse_date(date_str: str) -> datetime:
    """Parse date string to datetime object"""
    try:
        return datetime.strptime(date_str, '%Y-%m-%d')
    except ValueError:
        raise argparse.ArgumentTypeError(f"Invalid date format: {date_str}. Use YYYY-MM-DD")

def main():
    parser = argparse.ArgumentParser(description='Download financial data and convert to Lean format')
    
    # Data source arguments
    parser.add_argument('--source', choices=['alpaca', 'binance', 'both'], default='both',
                       help='Data source to download from')
    
    # Symbol arguments
    parser.add_argument('--equity-symbols', nargs='+', default=DEFAULT_EQUITY_SYMBOLS,
                       help='Equity symbols to download (for Alpaca)')
    parser.add_argument('--crypto-symbols', nargs='+', default=DEFAULT_CRYPTO_SYMBOLS,
                       help='Crypto symbols to download (for Binance)')
    
    # Date range arguments
    parser.add_argument('--start-date', type=parse_date, default=DEFAULT_START_DATE,
                       help='Start date (YYYY-MM-DD)')
    parser.add_argument('--end-date', type=parse_date, default=DEFAULT_END_DATE,
                       help='End date (YYYY-MM-DD)')
    
    # Resolution arguments
    parser.add_argument('--resolution', choices=SUPPORTED_RESOLUTIONS, default='minute',
                       help='Data resolution')
    
    # Other arguments
    parser.add_argument('--test', action='store_true',
                       help='Run in test mode with limited symbols and date range')
    
    args = parser.parse_args()
    
    # Test mode adjustments
    if args.test:
        args.equity_symbols = ['AAPL', 'GOOGL', 'MSFT'][:2]
        args.crypto_symbols = ['BTCUSDT', 'ETHUSDT'][:2]
        args.start_date = datetime.now() - timedelta(days=7)
        args.end_date = datetime.now()
        logger.info("Running in test mode with limited symbols and date range")
    
    # Validate date range
    if args.start_date >= args.end_date:
        logger.error("Start date must be before end date")
        sys.exit(1)
    
    logger.info(f"Starting data download from {args.start_date.strftime('%Y-%m-%d')} to {args.end_date.strftime('%Y-%m-%d')}")
    logger.info(f"Resolution: {args.resolution}")
    
    # Download equity data from Alpaca
    if args.source in ['alpaca', 'both']:
        try:
            logger.info("Starting Alpaca data download...")
            alpaca_downloader = AlpacaDataDownloader()
            alpaca_downloader.download_multiple_symbols(
                args.equity_symbols, 
                args.resolution, 
                args.start_date, 
                args.end_date
            )
            logger.info("Alpaca download completed")
        except Exception as e:
            logger.error(f"Error with Alpaca download: {str(e)}")
            if args.source == 'alpaca':
                sys.exit(1)
    
    # Download crypto data from Binance
    if args.source in ['binance', 'both']:
        try:
            logger.info("Starting Binance data download...")
            binance_downloader = BinanceDataDownloader()
            binance_downloader.download_multiple_symbols(
                args.crypto_symbols, 
                args.resolution, 
                args.start_date, 
                args.end_date
            )
            logger.info("Binance download completed")
        except Exception as e:
            logger.error(f"Error with Binance download: {str(e)}")
            if args.source == 'binance':
                sys.exit(1)
    
    logger.info("Data pipeline completed successfully!")

if __name__ == "__main__":
    main()
