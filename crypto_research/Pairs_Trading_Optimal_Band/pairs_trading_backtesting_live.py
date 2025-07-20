import numpy as np
import pandas as pd
import time
import logging
import os
import asyncio
from datetime import datetime, timedelta
from binance.client import Client
import threading
import plotly.graph_objects as go
import plotly.express as px
from plotly.subplots import make_subplots
import dash
from dash import dcc, html
from dash.dependencies import Input, Output
import json
from dash import dash_table
import websockets
from dotenv import load_dotenv
import statsmodels.api as sm
from statsmodels.tsa.stattools import coint

load_dotenv()

logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(name)s - %(levelname)s - %(message)s',
    handlers=[
        logging.FileHandler("pairs_trading.log"),
        logging.StreamHandler()
    ]
)
logger = logging.getLogger(__name__)

ASSET1 = "ETHUSDT"  
ASSET2 = "BTCUSDT"
LOOKBACK_WINDOW = 20  # Number of periods to calculate the moving average and std dev
STD_DEV_THRESHOLD = 2.0  # Number of standard deviations to trigger a trade
TRADE_QUANTITY = 0.01  # Amount to trade in BTC or ETH
TRADING_FEE = 0.001  # 0.1% trading fee on Binance

BINANCE_API_KEY = os.environ.get('BINANCE_API_KEY', '')
BINANCE_API_SECRET = os.environ.get('BINANCE_API_SECRET', '')

# Constants for the optimal band strategy
OPTIMIZATION_WINDOW = 500  # Period to use for optimizing the bands
STD_DEV_MULTIPLES = [1.5, 2.0, 2.5, 3.0]  # Different thresholds to test
EXIT_THRESHOLD = 0.5  # Exit when z-score crosses this threshold

# Global variables for websocket data
websocket_data = {
    ASSET1: {'price': None, 'timestamp': None},
    ASSET2: {'price': None, 'timestamp': None}
}
websocket_lock = threading.Lock()

# Initialize Binance API
def initialize_binance_api():
    if not BINANCE_API_KEY or not BINANCE_API_SECRET:
        logger.error("Binance API key or secret not found. Please set environment variables.")
        return None
    
    try:
        client = Client(BINANCE_API_KEY, BINANCE_API_SECRET)
        client.ping()
        logger.info("Connected to Binance API successfully")
        return client
    except Exception as e:
        logger.error(f"Error connecting to Binance API: {e}")
        return None

# Function to fetch minimal historical data from Binance (just enough to initialize)
def fetch_historical_data_binance(client, symbol, interval='1m', limit=100):
    """Fetch minimal historical data - just enough to initialize the strategy"""
    try:
        # Get current time from Binance server
        server_time = client.get_server_time()
        current_time = datetime.fromtimestamp(server_time['serverTime']/1000)
        logger.info(f"Using Binance server time as reference: {current_time}")
        
        # Fetch just enough data points to initialize calculations
        # Need at least enough points to calculate initial z-scores plus some history
        klines = client.get_klines(
            symbol=symbol,
            interval=interval,
            limit=max(LOOKBACK_WINDOW * 3, 100)  # Ensure we have enough data for calculations
        )
        
        # Convert to dataframe
        df = pd.DataFrame(klines, columns=[
            'datetime', 'open', 'high', 'low', 'close', 'volume',
            'close_time', 'quote_asset_volume', 'number_of_trades',
            'taker_buy_base_asset_volume', 'taker_buy_quote_asset_volume', 'ignore'
        ])
        
        # Convert string values to numeric
        df['close'] = pd.to_numeric(df['close'])
        df['datetime'] = pd.to_datetime(df['datetime'], unit='ms')
        
        logger.info(f"Fetched minimal data to initialize: {len(df)} data points for {symbol}")
        return df[['datetime', 'close']]
    except Exception as e:
        logger.error(f"Error fetching minimal data from Binance for {symbol}: {e}")
        return None

# Websocket message processing function
def process_websocket_message(message):
    try:
        # Combined stream messages come in format: {"stream":"<streamName>","data":<rawPayload>}
        stream = message.get('stream', '')
        data = message.get('data', {})
        
        if not data:
            logger.error(f"Received invalid message: {message}")
            return
        
        # Extract symbol from stream name (format: "btcusdt@trade")
        if '@' in stream:
            symbol = stream.split('@')[0].upper()
            
            if data.get('e') == 'trade':
                price = float(data.get('p', 0))
                timestamp = pd.to_datetime(data.get('T', 0), unit='ms')
                
                with websocket_lock:
                    if symbol in websocket_data:
                        websocket_data[symbol]['price'] = price
                        websocket_data[symbol]['timestamp'] = timestamp
                        logger.debug(f"Updated price for {symbol}: {price}")
    except Exception as e:
        logger.error(f"Error processing websocket message: {e}")

# Periodic ping function to keep connection alive
async def send_periodic_pings(websocket):
    # Send a pong every 5 minutes (Binance sends ping every 3 minutes and expects pong within 10 minutes)
    while True:
        await asyncio.sleep(300)  # 5 minutes
        try:
            await websocket.pong()
            logger.debug("Sent pong frame to keep connection alive")
        except Exception as e:
            logger.error(f"Error sending pong: {e}")
            break

# Binance websocket handler
async def binance_websocket_handler():
    # Create URL for combined stream (using lowercase symbols and spot market instead of futures)
    url = f"wss://stream.binance.com:9443/stream?streams={ASSET1.lower()}@trade/{ASSET2.lower()}@trade"
    
    logger.info(f"Connecting to Binance websocket: {url}")
    
    while True:  # Reconnection loop
        try:
            async with websockets.connect(url) as websocket:
                logger.info(f"Connected to Binance websocket")
                
                # Start a task to send pings periodically to keep connection alive
                ping_task = asyncio.create_task(send_periodic_pings(websocket))
                
                try:
                    while True:
                        message = await websocket.recv()
                        process_websocket_message(json.loads(message))
                except Exception as e:
                    logger.error(f"Websocket connection error: {e}")
                finally:
                    ping_task.cancel()
                    
        except Exception as e:
            logger.error(f"Failed to connect to websocket: {e}")
            # Wait before reconnecting
            await asyncio.sleep(5)

# Start Binance websocket connection
def start_websocket():
    # Create a new event loop for the websocket
    loop = asyncio.new_event_loop()
    
    # Set the event loop for this thread
    asyncio.set_event_loop(loop)
    
    # Start the websocket handler in the event loop
    websocket_task = loop.create_task(binance_websocket_handler())
    
    # Create a thread to run the event loop
    websocket_thread = threading.Thread(
        target=lambda: loop.run_forever(),
        daemon=True
    )
    websocket_thread.start()
    
    logger.info(f"Started websocket connections for {ASSET1} and {ASSET2}")
    return websocket_thread, loop

# Function to test for cointegration and get hedge ratio
def perform_cointegration_analysis(data1, data2):
    """
    Test for cointegration between two price series and return hedge ratio
    
    Parameters:
    data1 (pd.DataFrame): DataFrame with 'close' column for first asset
    data2 (pd.DataFrame): DataFrame with 'close' column for second asset
    
    Returns:
    tuple: (is_cointegrated, hedge_ratio, p_value)
    """
    # Get price data
    price1 = data1['close'].values
    price2 = data2['close'].values
    
    # Test for cointegration
    score, p_value, _ = coint(price1, price2)
    is_cointegrated = p_value < 0.05
    
    # Calculate hedge ratio using OLS regression
    model = sm.OLS(price1, sm.add_constant(price2)).fit()
    hedge_ratio = model.params[1]
    
    logger.info(f"Cointegration test: p-value={p_value:.4f}, Hedge ratio={hedge_ratio:.4f}")
    logger.info(f"Assets are {'cointegrated' if is_cointegrated else 'not cointegrated'}")
    
    return is_cointegrated, hedge_ratio, p_value

# Function to find optimal trading band
def find_optimal_trading_band(data1, data2, hedge_ratio, lookback_window=LOOKBACK_WINDOW):
    """
    Find the optimal standard deviation threshold for trading based on historical data
    
    Parameters:
    data1 (pd.DataFrame): DataFrame with 'close' column for first asset
    data2 (pd.DataFrame): DataFrame with 'close' column for second asset
    hedge_ratio (float): Hedge ratio from cointegration analysis
    lookback_window (int): Window for moving averages
    
    Returns:
    float: Optimal standard deviation multiple
    """
    # Calculate spread
    spread = data1['close'] - hedge_ratio * data2['close']
    
    # Calculate rolling mean and std of spread
    spread_mean = spread.rolling(window=lookback_window).mean()
    spread_std = spread.rolling(window=lookback_window).std()
    
    # Calculate z-score
    z_score = (spread - spread_mean) / spread_std
    
    # Test different thresholds
    results = []
    for threshold in STD_DEV_MULTIPLES:
        # Backtest with this threshold
        returns = backtest_threshold(z_score, threshold, data1, data2)
        
        # Calculate Sharpe ratio
        if len(returns) > 0 and returns.std() > 0:
            sharpe_ratio = returns.mean() / returns.std() * np.sqrt(252)
        else:
            sharpe_ratio = 0
            
        results.append({
            'threshold': threshold,
            'sharpe_ratio': sharpe_ratio
        })
        
        logger.info(f"Threshold {threshold}: Sharpe ratio = {sharpe_ratio:.4f}")
    
    # Find threshold with highest Sharpe ratio
    results_df = pd.DataFrame(results)
    if len(results_df) > 0:
        optimal_threshold = results_df.loc[results_df['sharpe_ratio'].idxmax()]['threshold']
    else:
        optimal_threshold = STD_DEV_THRESHOLD  # Default
    
    logger.info(f"Optimal threshold: {optimal_threshold}")
    return optimal_threshold

# Helper function for backtesting different thresholds
def backtest_threshold(z_score, threshold, data1, data2):
    """Simple backtest to calculate returns for a given threshold"""
    # Create position signals
    signals = pd.DataFrame(index=z_score.index)
    signals['z_score'] = z_score
    
    # Generate signals: 1 for long Asset1/short Asset2, -1 for short Asset1/long Asset2, 0 for no position
    signals['position'] = 0
    signals.loc[z_score < -threshold, 'position'] = 1  # Long Asset1, Short Asset2
    signals.loc[z_score > threshold, 'position'] = -1  # Short Asset1, Long Asset2
    
    # Exit when z-score crosses EXIT_THRESHOLD
    exit_condition = abs(z_score) < EXIT_THRESHOLD
    for i in range(1, len(signals)):
        if exit_condition.iloc[i] and signals['position'].iloc[i-1] != 0:
            signals.loc[signals.index[i], 'position'] = 0
    
    # Forward-fill positions (maintain position until a change)
    signals['position'] = signals['position'].replace(to_replace=0, method='ffill')
    
    # Calculate returns (assuming equal dollar amounts in each position)
    asset1_returns = data1['close'].pct_change()
    asset2_returns = data2['close'].pct_change()
    
    # Strategy returns = position in asset1 * returns of asset1 - position in asset2 * returns of asset2
    # We use -1 * position for asset2 because position 1 means long asset1, short asset2
    strategy_returns = signals['position'].shift(1) * asset1_returns - signals['position'].shift(1) * asset2_returns
    
    return strategy_returns.dropna()

# Function to calculate the spread and z-score using cointegration
def calculate_spread_and_zscore(data1, data2, hedge_ratio=None):
    """
    Calculate spread and z-score using cointegration-based hedge ratio
    
    Parameters:
    data1 (pd.DataFrame): DataFrame with 'close' column for first asset
    data2 (pd.DataFrame): DataFrame with 'close' column for second asset
    hedge_ratio (float): Optional pre-computed hedge ratio
    
    Returns:
    tuple: (spread, z_score, hedge_ratio)
    """
    # If hedge ratio not provided, calculate it
    if hedge_ratio is None:
        _, hedge_ratio, _ = perform_cointegration_analysis(data1, data2)
    
    # Calculate spread using hedge ratio: S(t) = P1(t) - h * P2(t)
    spread = data1['close'] - hedge_ratio * data2['close']
    
    # Calculate rolling mean and std of spread
    spread_mean = spread.rolling(window=LOOKBACK_WINDOW).mean()
    spread_std = spread.rolling(window=LOOKBACK_WINDOW).std()
    
    # Calculate z-score: z(t) = (S(t) - μ) / σ
    z_score = (spread - spread_mean) / spread_std
    
    return spread, z_score, hedge_ratio

# Update the pairs trading strategy to use cointegration and optimal bands
def pairs_trading_strategy(data1, data2, backtest=True):
    """
    Enhanced pairs trading strategy using cointegration and optimal band selection
    
    Parameters:
    data1 (pd.DataFrame): DataFrame with 'close' and 'datetime' columns for first asset
    data2 (pd.DataFrame): DataFrame with 'close' and 'datetime' columns for second asset
    backtest (bool): Whether to run in backtest mode
    
    Returns:
    dict: Results dictionary containing portfolio, signals, and metrics
    """
    # Check for cointegration and get hedge ratio
    is_cointegrated, hedge_ratio, p_value = perform_cointegration_analysis(data1, data2)
    
    # Find optimal trading band if we have enough data
    if len(data1) >= OPTIMIZATION_WINDOW:
        std_dev_threshold = find_optimal_trading_band(data1, data2, hedge_ratio)
    else:
        std_dev_threshold = STD_DEV_THRESHOLD
        logger.warning(f"Not enough data for optimization, using default threshold: {std_dev_threshold}")
    
    # Calculate spread and z-score using the hedge ratio
    spread, z_score, _ = calculate_spread_and_zscore(data1, data2, hedge_ratio)
    
    if backtest:
        signals = pd.DataFrame({
            'z_score': z_score,
            'datetime': data1['datetime'],
            'price1': data1['close'],
            'price2': data2['close'],
            'hedge_ratio': hedge_ratio  # Store hedge ratio for reference
        })
        
        # Enhanced signal generation with improved exit conditions
        signals['long_entry'] = (signals['z_score'] < -std_dev_threshold)
        signals['short_entry'] = (signals['z_score'] > std_dev_threshold)
        signals['exit'] = (abs(signals['z_score']) < EXIT_THRESHOLD)  # Exit when z-score crosses EXIT_THRESHOLD
        
        # Create a portfolio dataframe with explicit float dtypes to avoid warnings
        portfolio = pd.DataFrame(index=signals.index)
        portfolio['position_asset1'] = 0.0
        portfolio['position_asset2'] = 0.0
        portfolio['cash'] = 10000.0
        portfolio['asset1_value'] = 0.0
        portfolio['asset2_value'] = 0.0
        portfolio['total_value'] = portfolio['cash'].astype(float)
        portfolio['trade_count'] = 0
        portfolio['fees_paid'] = 0.0
        
        # Simulate trading with cointegration-based hedge ratios
        for i in range(1, len(signals)):
            # Default to previous values
            portfolio.loc[i, 'position_asset1'] = portfolio.loc[i-1, 'position_asset1']
            portfolio.loc[i, 'position_asset2'] = portfolio.loc[i-1, 'position_asset2']
            portfolio.loc[i, 'cash'] = portfolio.loc[i-1, 'cash']
            portfolio.loc[i, 'fees_paid'] = portfolio.loc[i-1, 'fees_paid']
            portfolio.loc[i, 'trade_count'] = portfolio.loc[i-1, 'trade_count']
            
            price1 = signals.loc[i, 'price1']
            price2 = signals.loc[i, 'price2']
            
            # Entry signals
            if signals.loc[i, 'long_entry'] and portfolio.loc[i-1, 'position_asset1'] == 0:
                # Buy ASSET1, Sell ASSET2 (using hedge ratio for proper scaling)
                # Hedge ratio tells us how much of asset2 to trade for each unit of asset1
                position_size1 = TRADE_QUANTITY  # Base position in asset1
                position_size2 = position_size1 * hedge_ratio  # Hedged position in asset2
                
                trade_value1 = position_size1 * price1
                trade_value2 = position_size2 * price2
                
                # Calculate fees
                fee1 = trade_value1 * TRADING_FEE
                fee2 = trade_value2 * TRADING_FEE
                total_fees = fee1 + fee2
                
                # Update positions
                portfolio.loc[i, 'position_asset1'] = position_size1
                portfolio.loc[i, 'position_asset2'] = -position_size2  # Short position
                portfolio.loc[i, 'cash'] = portfolio.loc[i-1, 'cash'] - trade_value1 + trade_value2 - total_fees
                portfolio.loc[i, 'fees_paid'] += total_fees
                portfolio.loc[i, 'trade_count'] += 1
                
            elif signals.loc[i, 'short_entry'] and portfolio.loc[i-1, 'position_asset1'] == 0:
                # Sell ASSET1, Buy ASSET2 (using hedge ratio for proper scaling)
                position_size1 = TRADE_QUANTITY  # Base position in asset1
                position_size2 = position_size1 * hedge_ratio  # Hedged position in asset2
                
                trade_value1 = position_size1 * price1
                trade_value2 = position_size2 * price2
                
                # Calculate fees
                fee1 = trade_value1 * TRADING_FEE
                fee2 = trade_value2 * TRADING_FEE
                total_fees = fee1 + fee2
                
                # Update positions
                portfolio.loc[i, 'position_asset1'] = -position_size1  # Short position
                portfolio.loc[i, 'position_asset2'] = position_size2
                portfolio.loc[i, 'cash'] = portfolio.loc[i-1, 'cash'] + trade_value1 - trade_value2 - total_fees
                portfolio.loc[i, 'fees_paid'] += total_fees
                portfolio.loc[i, 'trade_count'] += 1
                
            # Exit signals - using improved exit condition
            elif signals.loc[i, 'exit'] and (portfolio.loc[i-1, 'position_asset1'] != 0):
                # Close positions
                trade_value1 = abs(portfolio.loc[i-1, 'position_asset1']) * price1
                trade_value2 = abs(portfolio.loc[i-1, 'position_asset2']) * price2
                
                # Calculate fees
                fee1 = trade_value1 * TRADING_FEE
                fee2 = trade_value2 * TRADING_FEE
                total_fees = fee1 + fee2
                
                # Update cash based on current positions
                cash_change = 0
                if portfolio.loc[i-1, 'position_asset1'] > 0:  # Long ASSET1, Short ASSET2
                    cash_change = -trade_value1 + trade_value2
                else:  # Short ASSET1, Long ASSET2
                    cash_change = trade_value1 - trade_value2
                
                # Update positions
                portfolio.loc[i, 'position_asset1'] = 0
                portfolio.loc[i, 'position_asset2'] = 0
                portfolio.loc[i, 'cash'] = portfolio.loc[i-1, 'cash'] - cash_change - total_fees
                portfolio.loc[i, 'fees_paid'] += total_fees
                portfolio.loc[i, 'trade_count'] += 1
            
            # Update asset values
            portfolio.loc[i, 'asset1_value'] = portfolio.loc[i, 'position_asset1'] * price1
            portfolio.loc[i, 'asset2_value'] = portfolio.loc[i, 'position_asset2'] * price2
            portfolio.loc[i, 'total_value'] = (portfolio.loc[i, 'cash'] + 
                                              portfolio.loc[i, 'asset1_value'] + 
                                              portfolio.loc[i, 'asset2_value'])
        
        # Calculate daily returns
        portfolio['daily_return'] = portfolio['total_value'].pct_change()
        
        # Calculate performance metrics
        initial_value = portfolio['total_value'].iloc[0]
        final_value = portfolio['total_value'].iloc[-1]
        total_return = (final_value / initial_value) - 1
        
        # Annualized return - handle case where timeframe is very short
        time_diff = signals['datetime'].iloc[-1] - signals['datetime'].iloc[0]
        days = max(time_diff.total_seconds() / (60 * 60 * 24), 0.5)  # Minimum 0.5 days to avoid extreme annualization
        
        if days < 1:  # Very short timeframe
            annual_return = total_return * (365 / days) # Linear scaling for very short periods
            logger.warning(f"Short timeframe ({days:.2f} days) - annualized return may be exaggerated")
        else:
            annual_return = ((1 + total_return) ** (365 / days)) - 1
        
        # Sharpe ratio - handle very short timeframes
        daily_std = portfolio['daily_return'].std()
        if daily_std == 0 or days < 0.5:
            sharpe_ratio = 0.0 if total_return <= 0 else 1.0
            logger.warning("Cannot calculate meaningful Sharpe ratio for very short timeframe")
        else:
            risk_free_rate = 0.02  # Assume 2% risk-free rate
            sharpe_ratio = (annual_return - risk_free_rate) / (daily_std * np.sqrt(252))
            # Cap sharpe ratio to reasonable bounds
            sharpe_ratio = max(min(sharpe_ratio, 20), -20)
        
        # Maximum drawdown
        portfolio['cumulative_return'] = (1 + portfolio['daily_return']).cumprod()
        portfolio['cumulative_max'] = portfolio['cumulative_return'].cummax()
        portfolio['drawdown'] = (portfolio['cumulative_return'] / portfolio['cumulative_max']) - 1
        max_drawdown = portfolio['drawdown'].min()
        
        # Win rate - use the corrected calculation
        win_rate = calculate_win_rate(portfolio)
        
        # Get total number of trades
        total_trades = portfolio['trade_count'].iloc[-1]
        
        logger.info(f"Backtest Results:")
        logger.info(f"Cointegration p-value: {p_value:.4f}, Hedge Ratio: {hedge_ratio:.4f}")
        logger.info(f"Optimal threshold: {std_dev_threshold}")
        logger.info(f"Total Return: {total_return:.4f} ({total_return*100:.2f}%)")
        logger.info(f"Annual Return: {annual_return:.4f} ({annual_return*100:.2f}%)")
        logger.info(f"Sharpe Ratio: {sharpe_ratio:.4f}")
        logger.info(f"Maximum Drawdown: {max_drawdown:.4f} ({max_drawdown*100:.2f}%)")
        logger.info(f"Win Rate: {win_rate:.4f} ({win_rate*100:.2f}%)")
        logger.info(f"Total Trades: {total_trades}")
        logger.info(f"Total Fees Paid: ${portfolio['fees_paid'].iloc[-1]:.2f}")
        
        # Add additional information to portfolio for visualization
        portfolio['z_score'] = signals['z_score']
        portfolio['spread'] = spread
        portfolio['price1'] = signals['price1']
        portfolio['price2'] = signals['price2']
        portfolio['datetime'] = signals['datetime']
        portfolio['hedge_ratio'] = hedge_ratio
        portfolio['std_dev_threshold'] = std_dev_threshold
        
        results = {
            'portfolio': portfolio,
            'signals': signals,
            'metrics': {
                'total_return': total_return,
                'annual_return': annual_return,
                'sharpe_ratio': sharpe_ratio,
                'max_drawdown': max_drawdown,
                'win_rate': win_rate,
                'total_trades': total_trades,
                'total_fees': portfolio['fees_paid'].iloc[-1],
                'trading_period_days': days,
                'hedge_ratio': hedge_ratio,
                'std_dev_threshold': std_dev_threshold,
                'cointegration_p_value': p_value,
                'is_cointegrated': is_cointegrated
            }
        }
        
        return results
    
    else:  # Live trading
        # Implementation for live trading would go here
        # This would use the websocket data and execute trades with Binance API
        pass

# Helper function to convert DataFrames to JSON serializable format
def make_json_serializable(obj):
    """Convert DataFrame and other non-serializable objects to JSON serializable format"""
    if isinstance(obj, pd.DataFrame):
        return obj.to_dict('records')
    elif isinstance(obj, pd.Timestamp):
        return obj.isoformat()
    elif isinstance(obj, np.ndarray):
        return obj.tolist()
    elif isinstance(obj, np.integer):
        return int(obj)
    elif isinstance(obj, np.floating):
        return float(obj)
    elif isinstance(obj, dict):
        return {k: make_json_serializable(v) for k, v in obj.items()}
    elif isinstance(obj, list):
        return [make_json_serializable(item) for item in obj]
    else:
        return obj

# Create dashboard to visualize backtest results
def create_dashboard(backtest_results, update_interval=5000):
    app = dash.Dash(__name__, 
                    external_stylesheets=['https://stackpath.bootstrapcdn.com/bootstrap/4.5.2/css/bootstrap.min.css'])
    
    # Convert the backtest results to JSON serializable format
    serializable_results = make_json_serializable(backtest_results)
    
    # Keep a copy of original DataFrames for initial chart creation
    portfolio = backtest_results['portfolio']
    metrics = backtest_results['metrics']
    signals = backtest_results['signals']
    
    # Add Store component to share data between callbacks
    app.layout = html.Div([
        dcc.Store(id='backtest-data', data=serializable_results),
        dcc.Interval(id='interval-component', interval=update_interval, n_intervals=0),
        
        html.Div([
            html.H1('Crypto Pairs Trading Dashboard', className='text-center mt-3 mb-4'),
            html.H4(f'Strategy: {ASSET1}/{ASSET2} Statistical Arbitrage', className='text-center mb-4'),
            
            # Add cointegration information
            html.Div([
                html.Div([
                    html.Div([
                        html.H5('Cointegration Analysis', className='card-header bg-dark text-white'),
                        html.Div([
                            html.Div([
                                html.Div([
                                    html.H6('Hedge Ratio', className='card-subtitle mt-2 text-muted'),
                                    html.H3(id='hedge-ratio', children=f"{metrics.get('hedge_ratio', 0):.4f}", 
                                           className='card-title mb-1'),
                                ], className='col-md-4'),
                                html.Div([
                                    html.H6('P-Value', className='card-subtitle mt-2 text-muted'),
                                    html.H3(id='p-value', children=f"{metrics.get('cointegration_p_value', 1):.4f}", 
                                           className='card-title mb-1'),
                                    html.P(id='is-cointegrated', 
                                          children=f"Assets are {'cointegrated' if metrics.get('is_cointegrated', False) else 'not cointegrated'}", 
                                          className='card-text text-muted mb-0')
                                ], className='col-md-4'),
                                html.Div([
                                    html.H6('Trading Band', className='card-subtitle mt-2 text-muted'),
                                    html.H3(id='std-dev-threshold', children=f"{metrics.get('std_dev_threshold', STD_DEV_THRESHOLD):.2f}σ", 
                                           className='card-title mb-1'),
                                    html.P(id='exit-threshold', children=f"Exit: {EXIT_THRESHOLD}σ", 
                                          className='card-text text-muted mb-0')
                                ], className='col-md-4')
                            ], className='row')
                        ], className='card-body')
                    ], className='card shadow mb-4')
                ], className='col-md-12')
            ], className='row mb-4'),
            
            # Top row - metrics cards
            html.Div([
                # Performance metrics
                html.Div([
                    html.Div([
                        html.H5('Performance Metrics', className='card-header bg-primary text-white'),
                        html.Div([
                            html.Div([
                                html.Div([
                                    html.H6('Returns', className='card-subtitle mt-2 text-muted'),
                                    html.H3(id='total-return', children=f"{metrics['total_return']*100:.2f}%", 
                                           className='card-title mb-1'),
                                    html.P(id='annual-return', children=f"Ann: {metrics['annual_return']*100:.2f}%", 
                                          className='card-text text-muted mb-0')
                                ], className='col-md-4'),
                                html.Div([
                                    html.H6('Sharpe Ratio', className='card-subtitle mt-2 text-muted'),
                                    html.H3(id='sharpe-ratio', children=f"{metrics['sharpe_ratio']:.2f}", 
                                           className='card-title mb-1'),
                                    html.P(id='sortino-ratio', children=f"Sortino: {calculate_sortino_ratio(portfolio):.2f}", 
                                          className='card-text text-muted mb-0')
                                ], className='col-md-4'),
                                html.Div([
                                    html.H6('Max Drawdown', className='card-subtitle mt-2 text-muted'),
                                    html.H3(id='max-drawdown', children=f"{metrics['max_drawdown']*100:.2f}%", 
                                           className='card-title mb-1'),
                                    html.P(id='calmar-ratio', children=f"Calmar: {metrics['annual_return']/abs(metrics['max_drawdown']):.2f}", 
                                          className='card-text text-muted mb-0')
                                ], className='col-md-4')
                            ], className='row mb-2'),
                            html.Div([
                                html.Div([
                                    html.H6('Win Rate', className='card-subtitle mt-2 text-muted'),
                                    html.H3(id='win-rate', children=f"{metrics['win_rate']*100:.2f}%", 
                                           className='card-title mb-1'),
                                    html.P(id='profit-factor', children=f"Profit Factor: {calculate_profit_factor(portfolio):.2f}", 
                                          className='card-text text-muted mb-0')
                                ], className='col-md-4'),
                                html.Div([
                                    html.H6('Total Trades', className='card-subtitle mt-2 text-muted'),
                                    html.H3(id='total-trades', children=f"{metrics['total_trades']}", 
                                           className='card-title mb-1'),
                                    html.P(id='avg-trade', children=f"Avg P/L: ${calculate_avg_trade_pnl(portfolio):.2f}", 
                                          className='card-text text-muted mb-0')
                                ], className='col-md-4'),
                                html.Div([
                                    html.H6('Fees Paid', className='card-subtitle mt-2 text-muted'),
                                    html.H3(id='fees-paid', children=f"${metrics['total_fees']:.2f}", 
                                           className='card-title mb-1'),
                                    html.P(id='max-consecutive-losses', children=f"Max Cons. Losses: {calculate_max_consecutive_losses(portfolio)}", 
                                          className='card-text text-muted mb-0')
                                ], className='col-md-4')
                            ], className='row')
                        ], className='card-body')
                    ], className='card shadow mb-4')
                ], className='col-md-8'),
                
                # Trading parameters
                html.Div([
                    html.Div([
                        html.H5('Current Status', className='card-header bg-info text-white'),
                        html.Div([
                            html.Div([
                                html.Div([
                                    html.H6('Last Updated', className='card-subtitle mt-2 text-muted'),
                                    html.H4(id='last-updated', children=datetime.now().strftime("%Y-%m-%d %H:%M:%S"), 
                                           className='card-title')
                                ], className='col-md-12 mb-2'),
                                html.Div([
                                    html.H6('Current Z-Score', className='card-subtitle mt-2 text-muted'),
                                    html.H4(id='current-zscore', children=f"{signals['z_score'].iloc[-1]:.4f}", 
                                           className='card-title')
                                ], className='col-md-12 mb-2'),
                                html.Div([
                                    html.H6('Current Position', className='card-subtitle mt-2 text-muted'),
                                    html.H4(id='current-position', 
                                           children=get_position_text(portfolio['position_asset1'].iloc[-1], 
                                                                     portfolio['position_asset2'].iloc[-1]),
                                           className='card-title')
                                ], className='col-md-12')
                            ], className='row')
                        ], className='card-body')
                    ], className='card shadow mb-4')
                ], className='col-md-4')
            ], className='row mb-4'),
            
            # Second row - charts
            html.Div([
                # Price chart and Z-score
                html.Div([
                    html.Div([
                        html.H5('Asset Prices & Z-Score', className='card-header bg-secondary text-white'),
                        html.Div([
                            dcc.Graph(id='combined-chart', figure=create_combined_chart(portfolio))
                        ], className='card-body')
                    ], className='card shadow mb-4')
                ], className='col-md-12')
            ], className='row mb-4'),
            
            # Third row - portfolio and drawdown
            html.Div([
                # Portfolio value
                html.Div([
                    html.Div([
                        html.H5('Portfolio Performance', className='card-header bg-success text-white'),
                        html.Div([
                            dcc.Graph(id='portfolio-chart', figure=create_portfolio_chart(portfolio))
                        ], className='card-body')
                    ], className='card shadow mb-4')
                ], className='col-md-6'),
                
                # Drawdown
                html.Div([
                    html.Div([
                        html.H5('Drawdown Analysis', className='card-header bg-danger text-white'),
                        html.Div([
                            dcc.Graph(id='drawdown-chart', figure=create_drawdown_chart(portfolio))
                        ], className='card-body')
                    ], className='card shadow mb-4')
                ], className='col-md-6')
            ], className='row mb-4'),
            
            # Fourth row - trades table
            html.Div([
                html.Div([
                    html.Div([
                        html.H5('Recent Trades', className='card-header bg-warning text-dark'),
                        html.Div([
                            dash_table.DataTable(
                                id='trades-table',
                                columns=[
                                    {'name': 'Date', 'id': 'date'},
                                    {'name': 'Action', 'id': 'action'},
                                    {'name': f'{ASSET1} Qty', 'id': 'asset1_qty'},
                                    {'name': f'{ASSET2} Qty', 'id': 'asset2_qty'},
                                    {'name': 'P/L', 'id': 'pnl'},
                                    {'name': 'Fees', 'id': 'fees'}
                                ],
                                data=extract_trades_data(portfolio),
                                style_header={
                                    'backgroundColor': 'rgb(230, 230, 230)',
                                    'fontWeight': 'bold'
                                },
                                style_cell={
                                    'padding': '10px',
                                    'textAlign': 'center'
                                },
                                style_data_conditional=[
                                    {
                                        'if': {
                                            'filter_query': '{pnl} > 0',
                                            'column_id': 'pnl'
                                        },
                                        'color': 'green'
                                    },
                                    {
                                        'if': {
                                            'filter_query': '{pnl} < 0',
                                            'column_id': 'pnl'
                                        },
                                        'color': 'red'
                                    }
                                ],
                                page_size=10
                            )
                        ], className='card-body')
                    ], className='card shadow mb-4')
                ], className='col-md-12')
            ], className='row')
        ], className='container-fluid p-5')
    ])
    
    # Callback to update data periodically
    @app.callback(
        [Output('backtest-data', 'data'),
         Output('last-updated', 'children'),
         Output('total-return', 'children'),
         Output('annual-return', 'children'),
         Output('sharpe-ratio', 'children'),
         Output('sortino-ratio', 'children'),
         Output('max-drawdown', 'children'),
         Output('calmar-ratio', 'children'),
         Output('win-rate', 'children'),
         Output('profit-factor', 'children'),
         Output('total-trades', 'children'),
         Output('avg-trade', 'children'),
         Output('fees-paid', 'children'),
         Output('max-consecutive-losses', 'children'),
         Output('current-zscore', 'children'),
         Output('current-position', 'children'),
         Output('hedge-ratio', 'children'),  # New outputs
         Output('p-value', 'children'),
         Output('is-cointegrated', 'children'),
         Output('std-dev-threshold', 'children'),
         Output('combined-chart', 'figure'),
         Output('portfolio-chart', 'figure'),
         Output('drawdown-chart', 'figure'),
         Output('trades-table', 'data')],
        [Input('interval-component', 'n_intervals'),
         Input('backtest-data', 'data')]
    )
    def update_metrics(n_intervals, data):
        # Convert the stored data back to DataFrame format for processing
        portfolio_records = data.get('portfolio', [])
        portfolio = pd.DataFrame.from_records(portfolio_records) if portfolio_records else pd.DataFrame()
        
        metrics = data.get('metrics', {})
        
        # Calculate additional metrics
        sortino = calculate_sortino_ratio(portfolio)
        
        # Ensure calmar ratio doesn't produce extreme values
        if metrics['max_drawdown'] != 0 and abs(metrics['max_drawdown']) > 0.0001:
            calmar = metrics['annual_return']/abs(metrics['max_drawdown'])
            # Cap calmar to reasonable bounds
            calmar = max(min(calmar, 100), -100)
        else:
            calmar = 0
            
        profit_factor = calculate_profit_factor(portfolio)
        avg_trade_pnl = calculate_avg_trade_pnl(portfolio)
        max_cons_losses = calculate_max_consecutive_losses(portfolio)
        
        # Format outputs with more sanity checks
        trading_days = metrics.get('trading_period_days', 0)
        period_text = f" ({trading_days:.1f} days)" if trading_days < 30 else ""
        
        now = datetime.now().strftime("%Y-%m-%d %H:%M:%S")
        total_return = f"{metrics['total_return']*100:.2f}%"
        annual_return = f"Ann: {metrics['annual_return']*100:.2f}%"
        sharpe = f"{metrics['sharpe_ratio']:.2f}"
        sortino_text = f"Sortino: {sortino:.2f}"
        max_dd = f"{metrics['max_drawdown']*100:.2f}%"
        calmar_text = f"Calmar: {calmar:.2f}"
        win_rate = f"{min(metrics['win_rate']*100, 100):.2f}%"  # Cap at 100%
        profit_factor_text = f"Profit Factor: {profit_factor:.2f}"
        total_trades = f"{metrics['total_trades']}"
        avg_trade_text = f"Avg P/L: ${avg_trade_pnl:.2f}"
        fees_paid = f"${metrics['total_fees']:.2f}"
        max_cons_losses_text = f"Max Cons. Losses: {max_cons_losses}"
        
        # Current status
        current_zscore = f"{portfolio['z_score'].iloc[-1]:.4f}" if not portfolio.empty and 'z_score' in portfolio else "N/A"
        
        position_asset1 = portfolio['position_asset1'].iloc[-1] if not portfolio.empty and 'position_asset1' in portfolio else 0
        position_asset2 = portfolio['position_asset2'].iloc[-1] if not portfolio.empty and 'position_asset2' in portfolio else 0
        current_position = get_position_text(position_asset1, position_asset2)
        
        # Charts
        combined_chart = create_combined_chart(portfolio)
        portfolio_chart = create_portfolio_chart(portfolio)
        drawdown_chart = create_drawdown_chart(portfolio)
        
        # Trades table
        trades_data = extract_trades_data(portfolio)
        
        # Make sure to convert the updated data back to JSON serializable format
        updated_data = make_json_serializable(data)
        
        # Format additional outputs
        hedge_ratio = f"{metrics.get('hedge_ratio', 0):.4f}"
        p_value = f"{metrics.get('cointegration_p_value', 1):.4f}"
        is_cointegrated = f"Assets are {'cointegrated' if metrics.get('is_cointegrated', False) else 'not cointegrated'}"
        std_dev_threshold = f"{metrics.get('std_dev_threshold', STD_DEV_THRESHOLD):.2f}σ"
        
        return (updated_data, now, total_return, annual_return, sharpe, sortino_text, max_dd, calmar_text,
                win_rate, profit_factor_text, total_trades, avg_trade_text, fees_paid, 
                max_cons_losses_text, current_zscore, current_position, 
                hedge_ratio, p_value, is_cointegrated, std_dev_threshold,  # New outputs
                combined_chart, portfolio_chart, drawdown_chart, trades_data)
    
    return app

# Helper functions for the dashboard
def calculate_sortino_ratio(portfolio, risk_free_rate=0.02, periods=252):
    """Calculate the Sortino ratio (only downside risk)"""
    returns = portfolio['daily_return'].dropna()
    
    if len(returns) == 0:
        return 0
    
    excess_returns = returns - (risk_free_rate / periods)
    downside_returns = excess_returns[excess_returns < 0]
    
    if len(downside_returns) == 0 or downside_returns.std() == 0:
        return float('inf')  # No downside risk
    
    downside_deviation = downside_returns.std() * np.sqrt(periods)
    return excess_returns.mean() * periods / downside_deviation

def calculate_profit_factor(portfolio):
    """Calculate profit factor (gross profits / gross losses)"""
    returns = portfolio['daily_return'].dropna()
    
    if len(returns) == 0:
        return 0
    
    gross_profits = returns[returns > 0].sum()
    gross_losses = abs(returns[returns < 0].sum())
    
    if gross_losses == 0:
        return float('inf')  # No losses
    
    return gross_profits / gross_losses

def calculate_avg_trade_pnl(portfolio):
    """Calculate average trade P/L"""
    if 'trade_count' not in portfolio.columns or portfolio['trade_count'].iloc[-1] == 0:
        return 0
    
    initial_value = portfolio['total_value'].iloc[0]
    final_value = portfolio['total_value'].iloc[-1]
    total_return = final_value - initial_value
    total_trades = portfolio['trade_count'].iloc[-1]
    
    return total_return / total_trades

def calculate_max_consecutive_losses(portfolio):
    """Calculate maximum consecutive losing trades"""
    returns = portfolio['daily_return'].dropna()
    
    if len(returns) == 0:
        return 0
    
    # Count trade-based losses, not just daily returns
    trade_days = portfolio[portfolio['trade_count'] > portfolio['trade_count'].shift(1, fill_value=0)].index
    
    if len(trade_days) == 0:
        return 0
        
    # Only look at returns on trade days
    trade_returns = portfolio.loc[trade_days, 'daily_return']
    
    # Convert to binary wins/losses
    win_loss = (trade_returns > 0).astype(int)
    losses = 1 - win_loss
    
    # Find max consecutive losses
    max_cons = 0
    current_cons = 0
    
    for loss in losses:
        if loss == 1:
            current_cons += 1
            max_cons = max(max_cons, current_cons)
        else:
            current_cons = 0
    
    return max_cons

def calculate_win_rate(portfolio):
    """Calculate win rate correctly based on actual trades"""
    # Identify days when trades occurred
    trade_days = portfolio[portfolio['trade_count'] > portfolio['trade_count'].shift(1, fill_value=0)].index
    
    if len(trade_days) == 0:
        return 0
    
    # Get returns on trade days
    trade_returns = portfolio.loc[trade_days, 'daily_return']
    
    # Calculate winning trades
    winning_trades = (trade_returns > 0).sum()
    total_trades = len(trade_returns)
    
    # Return percentage (capped at 100%)
    return min(winning_trades / total_trades if total_trades > 0 else 0, 1.0)

def get_position_text(asset1_pos, asset2_pos):
    """Get text description of current position"""
    if asset1_pos > 0 and asset2_pos < 0:
        return f"Long {ASSET1} / Short {ASSET2}"
    elif asset1_pos < 0 and asset2_pos > 0:
        return f"Short {ASSET1} / Long {ASSET2}"
    else:
        return "No Position"

def extract_trades_data(portfolio):
    """Extract trades for the data table"""
    trades = []
    prev_pos_asset1 = 0
    
    for i in range(1, len(portfolio)):
        if portfolio['position_asset1'].iloc[i] != portfolio['position_asset1'].iloc[i-1]:
            # A trade happened
            date = portfolio['datetime'].iloc[i]
            if isinstance(date, pd.Timestamp):
                date = date.strftime('%Y-%m-%d %H:%M')
            
            # Determine action
            if portfolio['position_asset1'].iloc[i] > portfolio['position_asset1'].iloc[i-1]:
                action = f"Buy {ASSET1} / Sell {ASSET2}"
            elif portfolio['position_asset1'].iloc[i] < portfolio['position_asset1'].iloc[i-1]:
                action = f"Sell {ASSET1} / Buy {ASSET2}"
            else:
                action = "Close Position"
            
            # Calculate P/L if closing a position
            pnl = 0
            if portfolio['position_asset1'].iloc[i] == 0 and prev_pos_asset1 != 0:
                # Closed a position, calculate P/L
                pnl = portfolio['total_value'].iloc[i] - portfolio['total_value'].iloc[i-1]
                pnl = f"${pnl:.2f}"
            else:
                pnl = "N/A"
            
            # Get fees
            fees = portfolio['fees_paid'].iloc[i] - portfolio['fees_paid'].iloc[i-1]
            
            trades.append({
                'date': date,
                'action': action,
                'asset1_qty': f"{abs(portfolio['position_asset1'].iloc[i]):.4f}",
                'asset2_qty': f"{abs(portfolio['position_asset2'].iloc[i]):.4f}",
                'pnl': pnl,
                'fees': f"${fees:.2f}"
            })
            
            prev_pos_asset1 = portfolio['position_asset1'].iloc[i]
    
    # Return the most recent trades first
    return trades[::-1]

def create_combined_chart(portfolio):
    """Create a combined chart with price and z-score"""
    if len(portfolio) == 0:
        return go.Figure()
    
    # Create figure with secondary y-axis
    fig = make_subplots(rows=2, cols=1, 
                         shared_xaxes=True,
                         vertical_spacing=0.1,
                         row_heights=[0.7, 0.3],
                         subplot_titles=('Asset Prices', 'Z-Score'))
    
    # Add price traces
    fig.add_trace(
        go.Scatter(
            x=portfolio['datetime'], 
            y=portfolio['price1'], 
            name=ASSET1,
            line=dict(color='#1f77b4')
        ),
        row=1, col=1
    )
    
    fig.add_trace(
        go.Scatter(
            x=portfolio['datetime'], 
            y=portfolio['price2'], 
            name=ASSET2,
            line=dict(color='#ff7f0e')
        ),
        row=1, col=1
    )
    
    # Add z-score trace
    fig.add_trace(
        go.Scatter(
            x=portfolio['datetime'], 
            y=portfolio['z_score'], 
            name='Z-Score',
            line=dict(color='purple')
        ),
        row=2, col=1
    )
    
    # Add z-score threshold lines
    fig.add_shape(
        type="line", line=dict(dash='dash', color='red', width=1),
        y0=STD_DEV_THRESHOLD, y1=STD_DEV_THRESHOLD, 
        x0=portfolio['datetime'].iloc[0], x1=portfolio['datetime'].iloc[-1],
        row=2, col=1
    )
    
    fig.add_shape(
        type="line", line=dict(dash='dash', color='red', width=1),
        y0=-STD_DEV_THRESHOLD, y1=-STD_DEV_THRESHOLD, 
        x0=portfolio['datetime'].iloc[0], x1=portfolio['datetime'].iloc[-1],
        row=2, col=1
    )
    
    fig.add_shape(
        type="line", line=dict(dash='dash', color='green', width=1),
        y0=0, y1=0, 
        x0=portfolio['datetime'].iloc[0], x1=portfolio['datetime'].iloc[-1],
        row=2, col=1
    )
    
    # Add position markers
    for i in range(1, len(portfolio)):
        if portfolio['position_asset1'].iloc[i] != portfolio['position_asset1'].iloc[i-1]:
            # Position change
            marker_color = 'green' if portfolio['position_asset1'].iloc[i] > 0 else 'red'
            marker_symbol = 'triangle-up' if portfolio['position_asset1'].iloc[i] > 0 else 'triangle-down'
            
            fig.add_trace(
                go.Scatter(
                    x=[portfolio['datetime'].iloc[i]],
                    y=[portfolio['price1'].iloc[i]],
                    mode='markers',
                    marker=dict(
                        color=marker_color,
                        size=12,
                        symbol=marker_symbol
                    ),
                    showlegend=False
                ),
                row=1, col=1
            )
    
    # Update layout
    fig.update_layout(
        height=600,
        legend=dict(
            orientation="h",
            yanchor="bottom",
            y=1.02,
            xanchor="right",
            x=1
        ),
        margin=dict(l=40, r=40, t=60, b=40)
    )
    
    fig.update_xaxes(
        title_text="Date",
        row=2, col=1
    )
    
    fig.update_yaxes(
        title_text="Price",
        row=1, col=1
    )
    
    fig.update_yaxes(
        title_text="Z-Score",
        row=2, col=1
    )
    
    return fig

def create_portfolio_chart(portfolio):
    """Create portfolio value chart with annotations for trades"""
    if len(portfolio) == 0:
        return go.Figure()
    
    fig = go.Figure()
    
    # Add portfolio value trace
    fig.add_trace(
        go.Scatter(
            x=portfolio['datetime'],
            y=portfolio['total_value'],
            name='Portfolio Value',
            line=dict(color='#2ca02c', width=2)
        )
    )
    
    # Add markers for trades
    buy_dates = []
    buy_values = []
    sell_dates = []
    sell_values = []
    
    for i in range(1, len(portfolio)):
        if portfolio['position_asset1'].iloc[i] > portfolio['position_asset1'].iloc[i-1]:
            # Entry long
            buy_dates.append(portfolio['datetime'].iloc[i])
            buy_values.append(portfolio['total_value'].iloc[i])
        elif portfolio['position_asset1'].iloc[i] < portfolio['position_asset1'].iloc[i-1]:
            # Entry short or exit long
            sell_dates.append(portfolio['datetime'].iloc[i])
            sell_values.append(portfolio['total_value'].iloc[i])
    
    # Add buy markers
    if buy_dates:
        fig.add_trace(
            go.Scatter(
                x=buy_dates,
                y=buy_values,
                mode='markers',
                name='Buy',
                marker=dict(
                    color='green',
                    size=10,
                    symbol='triangle-up'
                )
            )
        )
    
    # Add sell markers
    if sell_dates:
        fig.add_trace(
            go.Scatter(
                x=sell_dates,
                y=sell_values,
                mode='markers',
                name='Sell',
                marker=dict(
                    color='red',
                    size=10,
                    symbol='triangle-down'
                )
            )
        )
    
    # Update layout
    fig.update_layout(
        title='Portfolio Value Over Time',
        xaxis_title='Date',
        yaxis_title='Value ($)',
        height=400,
        legend=dict(
            orientation="h",
            yanchor="bottom",
            y=1.02,
            xanchor="right",
            x=1
        ),
        margin=dict(l=40, r=40, t=60, b=40)
    )
    
    return fig

def create_drawdown_chart(portfolio):
    """Create drawdown chart with annotations for worst periods"""
    if len(portfolio) == 0 or 'drawdown' not in portfolio:
        return go.Figure()
    
    fig = go.Figure()
    
    # Add drawdown trace
    fig.add_trace(
        go.Scatter(
            x=portfolio['datetime'],
            y=portfolio['drawdown'] * 100,  # Convert to percentage
            name='Drawdown',
            fill='tozeroy',
            line=dict(color='#d62728')
        )
    )
    
    # Find worst drawdown period
    worst_dd_idx = portfolio['drawdown'].idxmin()
    worst_dd = portfolio['drawdown'].iloc[worst_dd_idx]
    worst_dd_date = portfolio['datetime'].iloc[worst_dd_idx]
    
    # Add annotation for worst drawdown
    fig.add_annotation(
        x=worst_dd_date,
        y=worst_dd * 100,
        text=f"Max DD: {worst_dd*100:.2f}%",
        showarrow=True,
        arrowhead=1,
        ax=0,
        ay=40
    )
    
    # Update layout
    fig.update_layout(
        title='Drawdown Analysis',
        xaxis_title='Date',
        yaxis_title='Drawdown (%)',
        height=400,
        margin=dict(l=40, r=40, t=60, b=40)
    )
    
    # Update y-axis to be negative (drawdowns are negative)
    fig.update_yaxes(autorange="reversed")
    
    return fig

def run_semi_live_backtesting():
    """Run the pairs trading strategy focusing primarily on live data"""
    # Initialize Binance API
    client = initialize_binance_api()
    if client is None:
        logger.error("Failed to initialize Binance API. Exiting.")
        return
    
    # Get the current time from Binance server
    try:
        server_time = client.get_server_time()
        current_time = datetime.fromtimestamp(server_time['serverTime']/1000)
        logger.info(f"Current Binance server time: {current_time}")
    except Exception as e:
        logger.error(f"Error getting Binance server time: {e}")
        current_time = datetime.now()
    
    logger.info(f"Starting LIVE trading for {ASSET1} and {ASSET2} as of {current_time}")
    
    # Fetch minimal data just to initialize calculations
    data1 = fetch_historical_data_binance(client, ASSET1, interval='1m')
    data2 = fetch_historical_data_binance(client, ASSET2, interval='1m')
    
    if data1 is None or data2 is None:
        logger.error("Could not retrieve initialization data. Exiting.")
        return
    
    # Make sure both dataframes have the same length
    min_date = max(data1['datetime'].min(), data2['datetime'].min())
    max_date = min(data1['datetime'].max(), data2['datetime'].max())
    
    data1 = data1[(data1['datetime'] >= min_date) & (data1['datetime'] <= max_date)]
    data2 = data2[(data2['datetime'] >= min_date) & (data2['datetime'] <= max_date)]
    
    # Run initial calculation with minimal historical data
    logger.info(f"Initializing with minimal data. Primary focus will be on LIVE data.")
    
    backtest_results = pairs_trading_strategy(data1, data2, backtest=True)
    
    # Start websocket connection to get live data
    websocket_thread, websocket_loop = start_websocket()
    
    # Create and start the dashboard with faster updates (500ms = 0.5 seconds)
    app = create_dashboard(backtest_results, update_interval=500)  # Update very frequently
    
    # Create a thread to update with live data
    update_thread = threading.Thread(
        target=update_backtest_with_live_data,
        args=(client, data1, data2, backtest_results),
        daemon=True
    )
    update_thread.start()
    
    # Run the dashboard
    logger.info("Starting dashboard. Open http://127.0.0.1:8050/ in your browser")
    app.run(debug=False, host='0.0.0.0')
    
    # Cleanup when the dashboard is closed
    logger.info("Stopping websocket connection...")
    websocket_loop.call_soon_threadsafe(websocket_loop.stop)
    websocket_thread.join(timeout=5)
    logger.info("Websocket connection closed")

def update_backtest_with_live_data(client, data1, data2, backtest_results):
    """Continuously update the strategy with live data from websockets"""
    # Initialize update timestamp
    last_update_time = datetime.now()
    
    # Set an even more aggressive update frequency for real-time focus
    update_frequency = 0.2  # Update every 0.2 seconds for more real-time response
    min_price_change_pct = 0.00001  # Even more sensitive to price changes
    
    # Store last prices to check for changes
    last_price1 = None
    last_price2 = None
    
    logger.info(f"Starting LIVE data updates with {update_frequency}s frequency")
    
    while True:
        try:
            # Sleep briefly between checks
            time.sleep(update_frequency)
            
            # Get the latest prices from websocket data
            with websocket_lock:
                price1 = websocket_data[ASSET1]['price']
                price2 = websocket_data[ASSET2]['price']
                timestamp1 = websocket_data[ASSET1]['timestamp']
                timestamp2 = websocket_data[ASSET2]['timestamp']
            
            # Skip if we don't have price data yet
            if not (price1 and price2):
                continue
                
            # Use websocket timestamps for more accurate timing
            current_time = timestamp1 if timestamp1 else datetime.now()
            
            # Check if prices have changed enough to warrant an update
            price_changed = (
                last_price1 is None or 
                last_price2 is None or 
                abs(price1 - last_price1) / last_price1 > min_price_change_pct or
                abs(price2 - last_price2) / last_price2 > min_price_change_pct
            )
            
            # Always update at least every 2 seconds regardless of price change
            time_elapsed = (datetime.now() - last_update_time).total_seconds()
            force_update = time_elapsed >= 2  # Force update more frequently
            
            if price_changed or force_update:
                # Add new live data points
                new_row1 = pd.DataFrame({'datetime': [current_time], 'close': [price1]})
                new_row2 = pd.DataFrame({'datetime': [current_time], 'close': [price2]})
                
                # Concatenate new data with existing
                data1 = pd.concat([data1, new_row1], ignore_index=True)
                data2 = pd.concat([data2, new_row2], ignore_index=True)
                
                # Optionally limit the data size to prevent memory issues
                max_data_points = 10000  # Keep last 10000 data points
                if len(data1) > max_data_points:
                    data1 = data1.iloc[-max_data_points:]
                    data2 = data2.iloc[-max_data_points:]
                
                # Re-run the strategy with updated data
                updated_results = pairs_trading_strategy(data1, data2, backtest=True)
                
                # Update the global backtest results (to be picked up by the dashboard)
                serializable_updated_results = make_json_serializable(updated_results)
                backtest_results.update(serializable_updated_results)
                
                # Update tracking variables
                last_update_time = datetime.now()
                last_price1 = price1
                last_price2 = price2
                
                logger.info(f"Updated backtest with new data: {ASSET1}={price1}, {ASSET2}={price2}")
                
        except Exception as e:
            logger.error(f"Error updating backtest with live data: {e}")
            # Don't crash on error, continue the loop

# Ensure the strategy starts immediately when run
if __name__ == "__main__":
    # Run the semi-live backtesting immediately
    run_semi_live_backtesting()