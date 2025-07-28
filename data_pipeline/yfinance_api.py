from flask import Flask, request, jsonify
import yfinance as yf

app = Flask(__name__)

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
        news = yf.Ticker(ticker).news
        return jsonify(news)
    except Exception as e:
        return jsonify({'error': str(e)}), 500

if __name__ == '__main__':
    app.run(port=5001)
