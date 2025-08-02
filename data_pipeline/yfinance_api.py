from flask import Flask, request, jsonify
import yfinance as yf
import logging

app = Flask(__name__)
logging.basicConfig(level=logging.INFO)

@app.route('/securities', methods=['GET'])
def get_securities():
    tickers = request.args.get('tickers', 'AAPL,MSFT,GOOGL').split(',')
    data = []
    for t in tickers:
        try:
            ticker = yf.Ticker(t)
            fast = ticker.fast_info
            data.append({
                'symbol': t,
                'name': t,  # fast_info does not provide name
                'exchange': fast.get('exchange', ''),
                'region': '',  # not available in fast_info
                'lastPrice': fast.get('lastPrice', None)
            })
        except Exception as e:
            data.append({'symbol': t, 'error': str(e)})
    return jsonify(data)

@app.route('/news', methods=['GET'])
def get_news():
    ticker = request.args.get('ticker', 'AAPL')
    try:
        app.logger.info(f"Fetching news for ticker: {ticker}")
        ticker_obj = yf.Ticker(ticker)
        news = ticker_obj.news
        
        if not news:
            app.logger.warning(f"No news found for ticker: {ticker}")
            return jsonify([])
        
        # Transform the news data to match our expected format
        formatted_news = []
        for item in news:
            formatted_item = {
                'Title': item.get('title', ''),
                'Publisher': item.get('publisher', ''),
                'Link': item.get('link', ''),
                'ProviderPublishTime': item.get('providerPublishTime', 0),
                'Type': item.get('type', ''),
                'Thumbnail': item.get('thumbnail', {}).get('resolutions', [{}])[0].get('url', '') if item.get('thumbnail') else '',
                'Summary': item.get('title', '')  # Use title as summary since Yahoo doesn't provide summary
            }
            formatted_news.append(formatted_item)
        
        app.logger.info(f"Successfully fetched {len(formatted_news)} news items for {ticker}")
        return jsonify(formatted_news)
        
    except Exception as e:
        app.logger.error(f"Error fetching news for {ticker}: {str(e)}")
        return jsonify({'error': str(e)}), 500

@app.route('/health', methods=['GET'])
def health_check():
    return jsonify({'status': 'healthy', 'service': 'yfinance-api'})

if __name__ == '__main__':
    app.run(host='0.0.0.0', port=5001, debug=True)
