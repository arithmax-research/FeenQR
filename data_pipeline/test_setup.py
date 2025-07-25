#!/usr/bin/env python3
"""
Test script to verify data pipeline setup
"""

import sys
import os

# Add current directory to path
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

def test_imports():
    """Test that all required modules can be imported"""
    try:
        import pandas as pd
        print("✓ pandas imported successfully")
    except ImportError as e:
        print(f"✗ pandas import failed: {e}")
        return False
    
    try:
        import numpy as np
        print("✓ numpy imported successfully")
    except ImportError as e:
        print(f"✗ numpy import failed: {e}")
        return False
    
    try:
        import requests
        print("✓ requests imported successfully")
    except ImportError as e:
        print(f"✗ requests import failed: {e}")
        return False
    
    try:
        import alpaca_trade_api as tradeapi
        print("✓ alpaca_trade_api imported successfully")
    except ImportError as e:
        print(f"✗ alpaca_trade_api import failed: {e}")
        print("  Install with: pip install alpaca-trade-api")
        return False
    
    try:
        from binance.client import Client
        print("✓ binance imported successfully")
    except ImportError as e:
        print(f"✗ binance import failed: {e}")
        print("  Install with: pip install python-binance")
        return False
    
    return True

def test_config():
    """Test configuration loading"""
    try:
        from config import (
            ALPACA_API_KEY, ALPACA_SECRET_KEY,
            BINANCE_API_KEY, BINANCE_SECRET_KEY,
            DATA_ROOT, EQUITY_DATA_PATH, CRYPTO_DATA_PATH
        )
        print("✓ Configuration loaded successfully")
        
        # Check API keys
        if ALPACA_API_KEY and ALPACA_SECRET_KEY:
            print("✓ Alpaca API keys configured")
        else:
            print("⚠ Alpaca API keys not configured")
        
        if BINANCE_API_KEY and BINANCE_SECRET_KEY:
            print("✓ Binance API keys configured")
        else:
            print("⚠ Binance API keys not configured (optional for public data)")
        
        # Check paths
        print(f"✓ Data root: {DATA_ROOT}")
        print(f"✓ Equity data path: {EQUITY_DATA_PATH}")
        print(f"✓ Crypto data path: {CRYPTO_DATA_PATH}")
        
        return True
        
    except ImportError as e:
        print(f"✗ Configuration import failed: {e}")
        return False

def test_utils():
    """Test utility functions"""
    try:
        from utils import setup_logging, ensure_directory_exists
        logger = setup_logging()
        logger.info("Testing logging system")
        print("✓ Utility functions working")
        return True
    except Exception as e:
        print(f"✗ Utility functions failed: {e}")
        return False

def test_api_connections():
    """Test API connections"""
    try:
        from config import ALPACA_API_KEY, ALPACA_SECRET_KEY
        
        if ALPACA_API_KEY and ALPACA_SECRET_KEY:
            try:
                import alpaca_trade_api as tradeapi
                api = tradeapi.REST(
                    ALPACA_API_KEY,
                    ALPACA_SECRET_KEY,
                    'https://paper-api.alpaca.markets',
                    api_version='v2'
                )
                account = api.get_account()
                print(f"✓ Alpaca connection successful (Account: {account.status})")
            except Exception as e:
                print(f"⚠ Alpaca connection failed: {e}")
        else:
            print("⚠ Alpaca API keys not configured - skipping connection test")
        
        # Test Binance connection (public data)
        try:
            from binance.client import Client
            client = Client()
            server_time = client.get_server_time()
            print("✓ Binance connection successful")
        except Exception as e:
            print(f"⚠ Binance connection failed: {e}")
        
        return True
    except Exception as e:
        print(f"✗ API connection test failed: {e}")
        return False

def main():
    print("LEAN Data Pipeline Setup Test")
    print("=" * 40)
    
    tests = [
        ("Import Test", test_imports),
        ("Configuration Test", test_config),
        ("Utilities Test", test_utils),
        ("API Connection Test", test_api_connections),
    ]
    
    passed = 0
    total = len(tests)
    
    for test_name, test_func in tests:
        print(f"\n{test_name}:")
        if test_func():
            passed += 1
        else:
            print(f"  {test_name} failed")
    
    print(f"\n{'='*40}")
    print(f"Test Results: {passed}/{total} passed")
    
    if passed == total:
        print("✓ All tests passed! Data pipeline is ready to use.")
        print("\nNext steps:")
        print("1. Set your API keys in .env file")
        print("2. Run: python main.py --test")
    else:
        print("✗ Some tests failed. Please check the requirements and configuration.")
        sys.exit(1)

if __name__ == "__main__":
    main()
