#!/usr/bin/env python3
"""
Test script for Databento Futures Downloader
"""

import os
import sys
from datetime import datetime, timedelta

# Add the data_pipeline directory to Python path
sys.path.append(os.path.dirname(os.path.abspath(__file__)))

from databento_downloader import DatabentoFuturesDownloader
from utils import setup_logging

def test_databento_connection():
    """Test basic connection to Databento API"""
    print("=" * 60)
    print("Testing Databento Futures Downloader")
    print("=" * 60)
    
    try:
        # Initialize downloader
        print("1. Initializing Databento downloader...")
        downloader = DatabentoFuturesDownloader()
        print("   ✓ Downloader initialized successfully")
        
        # Test connection
        print("2. Testing API connection...")
        if downloader.test_connection():
            print("   ✓ Connection successful")
        else:
            print("   ✗ Connection failed")
            return False
        
        # Test single symbol download
        print("3. Testing single symbol download...")
        test_symbol = "ES.FUT"
        start_date = datetime.now() - timedelta(days=5)
        end_date = datetime.now() - timedelta(days=1)
        
        print(f"   Downloading {test_symbol} from {start_date.date()} to {end_date.date()}")
        
        data = downloader.get_futures_data(
            symbol=test_symbol,
            start_date=start_date,
            end_date=end_date,
            resolution='daily'
        )
        
        if not data.empty:
            print(f"   ✓ Successfully downloaded {len(data)} records")
            print("   Sample data:")
            print(data.head().to_string())
            
            # Test Lean formatting
            print("4. Testing Lean format conversion...")
            lean_data = downloader.format_for_lean(data, test_symbol)
            print(f"   ✓ Lean format conversion successful")
            print("   Lean formatted sample:")
            print(lean_data.head().to_string())
            
            return True
        else:
            print("   ✗ No data returned")
            return False
            
    except ImportError as e:
        print(f"   ✗ Import error: {e}")
        print("   Please install databento: pip install databento")
        return False
    except Exception as e:
        print(f"   ✗ Error: {e}")
        return False

def test_available_symbols():
    """Test getting available symbols"""
    try:
        print("5. Testing available symbols retrieval...")
        downloader = DatabentoFuturesDownloader()
        
        # This might not work with all accounts, so we'll make it optional
        try:
            symbols = downloader.get_available_symbols()
            if symbols:
                print(f"   ✓ Found {len(symbols)} available symbols")
                print(f"   Sample symbols: {symbols[:10]}")
            else:
                print("   ⚠ No symbols returned (might require higher tier account)")
        except Exception as e:
            print(f"   ⚠ Could not retrieve symbols: {e}")
            
    except Exception as e:
        print(f"   ✗ Error testing symbols: {e}")

def main():
    """Main test function"""
    logger = setup_logging()
    
    # Test basic functionality
    if test_databento_connection():
        print("\n" + "=" * 60)
        print("✓ Basic tests passed!")
        
        # Test additional functionality
        test_available_symbols()
        
        print("\n" + "=" * 60)
        print("Databento integration test completed successfully!")
        print("\nYou can now use the Databento downloader with:")
        print("python main.py --source databento --databento-symbols ES.FUT NQ.FUT --test")
        print("=" * 60)
        
    else:
        print("\n" + "=" * 60)
        print("✗ Tests failed!")
        print("Please check your Databento API credentials in the .env file")
        print("=" * 60)
        return 1
    
    return 0

if __name__ == "__main__":
    sys.exit(main())
