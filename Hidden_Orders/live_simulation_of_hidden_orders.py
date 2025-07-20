import json
import time
import pandas as pd
import websocket
import threading
from datetime import datetime as dt, timedelta
import os
import traceback
from collections import deque, defaultdict
from tabulate import tabulate
import curses
import numpy as np

class HiddenOrderSimulator:
    def __init__(self, max_display_rows=15):
        self.orderbook_data = {}  # Current orderbook state
        self.hidden_orders = deque(maxlen=100)  # Store recent hidden orders
        self.combined_data = deque(maxlen=1000)  # Rolling buffer of recent data
        self.running = True
        self.max_display_rows = max_display_rows
        self.last_update = time.time()
        self.display_lock = threading.Lock()
        self.recent_trades = deque(maxlen=20)  # Track recent trades for pattern detection
        
        # New: Track repetitive anomalies
        self.anomaly_patterns = defaultdict(list)  # Pattern -> list of anomalies
        self.pattern_stats = defaultdict(lambda: {"count": 0, "total_volume": 0.0, "last_seen": None})
        self.time_window = 300  # Time window in seconds to consider repetitions (5 minutes)
        
        # Statistics
        self.stats = {
            'total_hidden_orders': 0,
            'below_bid_count': 0,
            'above_ask_count': 0,
            'between_spreads_count': 0,
            'volume_anomaly_count': 0
        }
        
        # Initialize display thread
        self.display_thread = threading.Thread(target=self._update_display)
        self.display_thread.daemon = True
        self.display_thread.start()

    def _ws_handler(self, message, data_type):
        try:
            data = json.loads(message)
            timestamp = dt.fromtimestamp(data['E']/1000)
            
            if data_type == 'depth':
                # Update orderbook
                if len(data.get('b', [])) > 0:
                    bid_price = float(data['b'][0][0])
                    bid_size = float(data['b'][0][1])
                    self.orderbook_data['bid_price'] = bid_price
                    self.orderbook_data['bid_size'] = bid_size
                
                if len(data.get('a', [])) > 0:
                    ask_price = float(data['a'][0][0])
                    ask_size = float(data['a'][0][1])
                    self.orderbook_data['ask_price'] = ask_price
                    self.orderbook_data['ask_size'] = ask_size
                    
                self.orderbook_data['timestamp'] = timestamp
                
            elif data_type == 'trade':
                # Process trade
                trade_price = float(data['p'])
                trade_size = float(data['q'])
                trade_volume = trade_price * trade_size
                
                # Store recent trade
                trade_record = {
                    'timestamp': timestamp,
                    'price': trade_price,
                    'size': trade_size,
                    'volume': trade_volume
                }
                self.recent_trades.append(trade_record)
                
                # Check for hidden order
                if 'bid_price' in self.orderbook_data and 'ask_price' in self.orderbook_data:
                    self._check_for_hidden_order(timestamp, trade_price, trade_size, trade_volume)
                
            # Store combined data for analysis
            record = {
                'timestamp': timestamp,
                'bid_price': self.orderbook_data.get('bid_price'),
                'ask_price': self.orderbook_data.get('ask_price'),
                'trade_price': trade_price if data_type == 'trade' else None,
                'trade_size': trade_size if data_type == 'trade' else None,
                'volume': trade_volume if data_type == 'trade' else None
            }
            self.combined_data.append(record)
            
        except Exception as e:
            print(f"Processing error: {str(e)[:200]}")
            traceback.print_exc()

    def _check_for_hidden_order(self, timestamp, trade_price, trade_size, trade_volume):
        bid_price = self.orderbook_data.get('bid_price')
        ask_price = self.orderbook_data.get('ask_price')
        
        # Skip if we don't have orderbook data
        if bid_price is None or ask_price is None:
            return
            
        reason = None
        is_hidden = False
        pattern_key = None
        
        # Check if trade is outside visible orders
        if trade_price < bid_price:
            reason = f"Trade price {trade_price} below best bid {bid_price}"
            is_hidden = True
            self.stats['below_bid_count'] += 1
            pattern_key = f"below_bid_{int(trade_price*100)//int(bid_price*100)}"
            
        elif trade_price > ask_price:
            reason = f"Trade price {trade_price} above best ask {ask_price}"
            is_hidden = True
            self.stats['above_ask_count'] += 1
            pattern_key = f"above_ask_{int(trade_price*100)//int(ask_price*100)}"
            
        # Check for unusual volume patterns
        elif len(self.recent_trades) > 5:
            avg_volume = np.mean([t['volume'] for t in list(self.recent_trades)[:-1]])
            if trade_volume > avg_volume * 3:  # Volume spike
                reason = f"Volume spike: {trade_volume:.2f} vs avg {avg_volume:.2f}"
                is_hidden = True
                self.stats['volume_anomaly_count'] += 1
                pattern_key = f"volume_spike_{int(trade_volume//avg_volume)}"
                
        # Check for trades exactly at mid price (could be dark pool)
        elif abs(trade_price - ((bid_price + ask_price) / 2)) < 0.00001:
            reason = f"Trade at exact mid-price: {trade_price}"
            is_hidden = True
            self.stats['between_spreads_count'] += 1
            pattern_key = "midprice_exact"
        
        if is_hidden:
            # Calculate repetition and add pattern
            repetition_count = 1
            if pattern_key:
                self._track_pattern(pattern_key, timestamp, trade_volume, trade_price, trade_size)
                repetition_count = self.pattern_stats[pattern_key]["count"]
            
            # Add to hidden orders list with repetition info
            self.hidden_orders.append({
                'timestamp': timestamp,
                'trade_price': trade_price,
                'trade_size': trade_size,
                'volume': trade_volume,
                'bid': bid_price,
                'ask': ask_price,
                'reason': reason,
                'pattern': pattern_key,
                'repetitions': repetition_count,
                'total_liquidity': self.pattern_stats[pattern_key]["total_volume"] if pattern_key else trade_volume
            })
            
            self.stats['total_hidden_orders'] += 1
            self.last_update = time.time()

    def _track_pattern(self, pattern_key, timestamp, volume, price, size):
        """Track repetitive anomaly patterns and update statistics"""
        current_time = timestamp
        
        # Clean up old patterns outside our time window
        now = dt.now()
        cutoff_time = now - timedelta(seconds=self.time_window)
        
        # Update pattern statistics
        self.pattern_stats[pattern_key]["count"] += 1
        self.pattern_stats[pattern_key]["total_volume"] += volume
        self.pattern_stats[pattern_key]["last_seen"] = current_time
        
        # Store this specific instance
        self.anomaly_patterns[pattern_key].append({
            'timestamp': current_time,
            'volume': volume,
            'price': price,
            'size': size
        })
        
        # Prune old entries outside time window
        self.anomaly_patterns[pattern_key] = [
            entry for entry in self.anomaly_patterns[pattern_key] 
            if entry['timestamp'] > cutoff_time
        ]

    def on_order_book_message(self, ws, message):
        self._ws_handler(message, 'depth')

    def on_trade_message(self, ws, message):
        self._ws_handler(message, 'trade')

    def _update_display(self):
        """Thread to update the terminal display with hidden orders"""
        while self.running:
            try:
                with self.display_lock:
                    self._render_table()
                time.sleep(0.5)  # Update every half second
            except Exception as e:
                print(f"Display error: {str(e)[:200]}")
                time.sleep(2)  # Longer pause on error

    def _render_table(self):
        """Render the table of hidden orders to the terminal"""
        os.system('clear')  # Clear terminal
        
        # Print header
        print("\n" + "="*100)
        print(f"  🕵️  HIDDEN ORDER DETECTOR - LIVE SIMULATION WITH REPETITION TRACKING  🕵️")
        print("="*100)
        
        # Print current market stats
        bid = self.orderbook_data.get('bid_price', 'N/A')
        ask = self.orderbook_data.get('ask_price', 'N/A')
        spread = ask - bid if isinstance(bid, float) and isinstance(ask, float) else 'N/A'
        
        print(f"\nCurrent Market: Bid: {bid} | Ask: {ask} | Spread: {spread}")
        print(f"Hidden Orders Detected: {self.stats['total_hidden_orders']} | Below Bid: {self.stats['below_bid_count']} | Above Ask: {self.stats['above_ask_count']} | Volume Anomalies: {self.stats['volume_anomaly_count']}")
        
        # Print repetitive pattern statistics
        if self.pattern_stats:
            print("\nRepetitive Anomaly Patterns (Last 5 minutes):")
            pattern_table = []
            for pattern, stats in sorted(self.pattern_stats.items(), key=lambda x: x[1]["count"], reverse=True):
                if stats["last_seen"] > dt.now() - timedelta(seconds=self.time_window):
                    pattern_table.append([
                        pattern,
                        stats["count"],
                        f"{stats['total_volume']:.8f}",
                        stats["last_seen"].strftime("%H:%M:%S") if stats["last_seen"] else "N/A"
                    ])
            
            if pattern_table:
                print(tabulate(pattern_table[:5], 
                              headers=["Pattern", "Repetitions", "Total Liquidity", "Last Seen"],
                              tablefmt="grid"))
        
        # Print most recent hidden orders
        if self.hidden_orders:
            print("\nRecent Hidden Orders:")
            
            # Convert deque to list and get latest entries
            display_orders = list(self.hidden_orders)[-self.max_display_rows:]
            
            table_data = []
            for order in display_orders:
                repetition_info = f" ({order.get('repetitions', 1)}x)" if order.get('repetitions', 1) > 1 else ""
                liquidity_info = f" Total: {order.get('total_liquidity', order['volume']):.5f}" if order.get('repetitions', 1) > 1 else ""
                
                table_data.append([
                    order['timestamp'].strftime("%H:%M:%S.%f")[:-3],
                    f"{order['trade_price']:.8f}",
                    f"{order['trade_size']:.5f}",
                    f"{order['bid']:.8f}",
                    f"{order['ask']:.8f}",
                    f"{order['reason']}{repetition_info}{liquidity_info}"
                ])
            
            headers = ["Timestamp", "Price", "Size", "Bid", "Ask", "Reason"]
            print(tabulate(table_data, headers=headers, tablefmt="grid"))
        else:
            print("\nNo hidden orders detected yet. Waiting for data...")
            
        print(f"\nLast updated: {dt.now().strftime('%H:%M:%S')}")
        print("Press Ctrl+C to exit")

    def _run_websocket(self, url, handler, stream_type):
        """Run a websocket connection with reconnection logic"""
        while self.running:
            try:
                ws = websocket.WebSocketApp(
                    url,
                    on_message=handler,
                    on_error=lambda ws, e: print(f"{stream_type} error: {str(e)[:200]}"),
                    on_close=lambda ws: print(f"{stream_type} closed"),
                    on_open=lambda ws: print(f"{stream_type} connected")
                )
                ws.run_forever(ping_interval=30, ping_timeout=10)
            except Exception as e:
                print(f"{stream_type} failure: {str(e)[:200]}")
            
            if self.running:
                print(f"Reconnecting {stream_type} in 5 seconds...")
                time.sleep(5)

    def start_simulation(self, symbol):
        """Start the hidden order simulation for a given symbol"""
        print(f"\nStarting hidden order simulation for {symbol}...")
        
        # Start websocket threads
        threads = [
            threading.Thread(target=self._run_websocket,
                args=(f"wss://stream.binance.com:9443/ws/{symbol}@depth@100ms", 
                    self.on_order_book_message, "OrderBook")),
            threading.Thread(target=self._run_websocket,
                args=(f"wss://stream.binance.com:9443/ws/{symbol}@trade", 
                    self.on_trade_message, "Trades"))
        ]

        for t in threads:
            t.daemon = True
            t.start()
        
        try:
            # Keep main thread alive
            while self.running:
                time.sleep(1)
        except KeyboardInterrupt:
            print("\nShutting down simulation...")
            self.running = False
            time.sleep(1)  # Allow threads to terminate
            print("Simulation ended")

if __name__ == "__main__":
    simulator = HiddenOrderSimulator()
    try:
        # Change to your preferred symbol
        simulator.start_simulation("btcusdt")
    except Exception as e:
        print(f"Fatal error: {str(e)[:200]}")
        traceback.print_exc()