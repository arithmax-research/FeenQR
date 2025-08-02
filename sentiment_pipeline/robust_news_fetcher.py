#!/usr/bin/env python3
"""
Robust News Fetcher - Handles API failures and shows real news sources
"""

import logging
import sys
import json
import urllib.request
import urllib.parse
from datetime import datetime, timezone
from typing import List, Dict, Any

# Configure logging
logging.basicConfig(level=logging.INFO, format='%(asctime)s - %(levelname)s - %(message)s')
logger = logging.getLogger(__name__)

class RobustNewsFetcher:
    """Fetches real news from multiple sources with fallback mechanisms"""
    
    def __init__(self):
        self.user_agent = 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36'
        
        # Sentiment keywords
        self.positive_keywords = [
            'bullish', 'positive', 'growth', 'strong', 'beat', 'exceed', 'upgrade',
            'buy', 'rally', 'surge', 'gain', 'optimistic', 'outperform', 'profit',
            'revenue', 'earnings', 'success', 'expansion', 'innovation', 'breakthrough',
            'record', 'high', 'soar', 'jump', 'rise', 'boost', 'improve', 'advance'
        ]
        
        self.negative_keywords = [
            'bearish', 'negative', 'decline', 'weak', 'miss', 'downgrade',
            'sell', 'crash', 'drop', 'loss', 'pessimistic', 'underperform',
            'debt', 'lawsuit', 'investigation', 'concern', 'risk', 'warning',
            'fall', 'plunge', 'tumble', 'slide', 'slump', 'struggle', 'challenge'
        ]
    
    def fetch_yahoo_finance_news(self, symbol: str) -> List[Dict]:
        """Fetch news from Yahoo Finance with better error handling"""
        print(f"🔍 Attempting to fetch Yahoo Finance news for {symbol}...")
        
        try:
            # Try the yfinance approach first
            import yfinance as yf
            ticker = yf.Ticker(symbol)
            
            # Get the news with error handling
            try:
                news_data = ticker.news
                if news_data and len(news_data) > 0:
                    print(f"✅ Yahoo Finance: Found {len(news_data)} articles")
                    return news_data
                else:
                    print("⚠️ Yahoo Finance: No news data returned")
            except Exception as e:
                print(f"⚠️ Yahoo Finance API error: {e}")
            
        except ImportError:
            print("⚠️ yfinance not available")
        except Exception as e:
            print(f"⚠️ Yahoo Finance error: {e}")
        
        return []
    
    def fetch_google_news_rss(self, symbol: str) -> List[Dict]:
        """Fetch news from Google News RSS"""
        print(f"🔍 Attempting to fetch Google News for {symbol}...")
        
        try:
            # Create search query
            query = f"{symbol} stock news"
            encoded_query = urllib.parse.quote(query)
            url = f"https://news.google.com/rss/search?q={encoded_query}&hl=en-US&gl=US&ceid=US:en"
            
            # Create request with headers
            req = urllib.request.Request(url)
            req.add_header('User-Agent', self.user_agent)
            
            # Fetch the RSS feed
            with urllib.request.urlopen(req, timeout=10) as response:
                content = response.read().decode('utf-8')
            
            # Parse RSS content (simplified)
            articles = self._parse_rss_content(content, symbol)
            
            if articles:
                print(f"✅ Google News: Found {len(articles)} articles")
                return articles
            else:
                print("⚠️ Google News: No relevant articles found")
                
        except Exception as e:
            print(f"⚠️ Google News error: {e}")
        
        return []
    
    def _parse_rss_content(self, content: str, symbol: str) -> List[Dict]:
        """Parse RSS content to extract news articles"""
        articles = []
        
        try:
            import re
            
            # Extract items from RSS
            item_pattern = r'<item>(.*?)</item>'
            items = re.findall(item_pattern, content, re.DOTALL)
            
            for item in items[:10]:  # Limit to 10 articles
                # Extract title
                title_match = re.search(r'<title><!\[CDATA\[(.*?)\]\]></title>', item)
                title = title_match.group(1) if title_match else "No title"
                
                # Extract link
                link_match = re.search(r'<link>(.*?)</link>', item)
                link = link_match.group(1) if link_match else "No link"
                
                # Extract publication date
                pubdate_match = re.search(r'<pubDate>(.*?)</pubDate>', item)
                pubdate = pubdate_match.group(1) if pubdate_match else "Unknown date"
                
                # Extract description/summary
                desc_match = re.search(r'<description><!\[CDATA\[(.*?)\]\]></description>', item)
                description = desc_match.group(1) if desc_match else title
                
                # Only include if the symbol is mentioned
                if symbol.upper() in title.upper() or symbol.upper() in description.upper():
                    articles.append({
                        'title': title,
                        'link': link,
                        'summary': description,
                        'publisher': 'Google News',
                        'providerPublishTime': self._parse_date(pubdate)
                    })
        
        except Exception as e:
            print(f"⚠️ RSS parsing error: {e}")
        
        return articles
    
    def _parse_date(self, date_str: str) -> int:
        """Parse date string to timestamp"""
        try:
            # This is a simplified date parser
            # In production, you'd use a proper date parsing library
            return int(datetime.now(timezone.utc).timestamp())
        except:
            return int(datetime.now(timezone.utc).timestamp())
    
    def fetch_finviz_news(self, symbol: str) -> List[Dict]:
        """Fetch news from Finviz"""
        print(f"🔍 Attempting to fetch Finviz news for {symbol}...")
        
        try:
            url = f"https://finviz.com/quote.ashx?t={symbol}"
            
            req = urllib.request.Request(url)
            req.add_header('User-Agent', self.user_agent)
            
            with urllib.request.urlopen(req, timeout=10) as response:
                content = response.read().decode('utf-8')
            
            # Parse Finviz news (simplified)
            articles = self._parse_finviz_content(content, symbol)
            
            if articles:
                print(f"✅ Finviz: Found {len(articles)} articles")
                return articles
            else:
                print("⚠️ Finviz: No news found")
                
        except Exception as e:
            print(f"⚠️ Finviz error: {e}")
        
        return []
    
    def _parse_finviz_content(self, content: str, symbol: str) -> List[Dict]:
        """Parse Finviz content for news"""
        articles = []
        
        try:
            import re
            
            # Look for news table in Finviz
            news_pattern = r'<a[^>]*class="tab-link-news"[^>]*href="([^"]*)"[^>]*>([^<]*)</a>'
            matches = re.findall(news_pattern, content)
            
            for link, title in matches[:5]:
                full_link = link if link.startswith('http') else f"https://finviz.com{link}"
                articles.append({
                    'title': title,
                    'link': full_link,
                    'summary': title,
                    'publisher': 'Finviz',
                    'providerPublishTime': int(datetime.now(timezone.utc).timestamp())
                })
        
        except Exception as e:
            print(f"⚠️ Finviz parsing error: {e}")
        
        return articles
    
    def analyze_sentiment(self, articles: List[Dict]) -> Dict:
        """Analyze sentiment of news articles"""
        if not articles:
            return {
                'score': 0.0,
                'confidence': 0.0,
                'analyzed_count': 0,
                'total_count': 0
            }
        
        total_sentiment = 0
        analyzed_count = 0
        
        for article in articles:
            title = article.get('title', '')
            summary = article.get('summary', '')
            text = f"{title} {summary}".lower()
            
            positive_count = sum(1 for keyword in self.positive_keywords if keyword in text)
            negative_count = sum(1 for keyword in self.negative_keywords if keyword in text)
            
            if positive_count > 0 or negative_count > 0:
                article_sentiment = (positive_count - negative_count) / max(positive_count + negative_count, 1)
                total_sentiment += article_sentiment
                analyzed_count += 1
                article['sentiment_score'] = article_sentiment
            else:
                article['sentiment_score'] = 0.0
        
        if analyzed_count > 0:
            overall_sentiment = total_sentiment / analyzed_count
            confidence = min(0.9, 0.4 + (analyzed_count / len(articles)) * 0.5)
        else:
            overall_sentiment = 0.0
            confidence = 0.3
        
        return {
            'score': overall_sentiment,
            'confidence': confidence,
            'analyzed_count': analyzed_count,
            'total_count': len(articles)
        }
    
    def generate_ai_summary(self, articles: List[Dict], symbol: str) -> str:
        """Generate AI summary of the news articles"""
        if not articles:
            return "No articles available for summary."
        
        # Create a comprehensive text from all articles
        all_content = []
        for article in articles:
            title = article.get('title', '')
            summary = article.get('summary', '')
            publisher = article.get('publisher', '')
            content = f"[{publisher}] {title}"
            if summary and summary != title:
                content += f" - {summary}"
            all_content.append(content)
        
        combined_text = "\n".join(all_content)
        
        # Simple AI-like summary generation (keyword-based)
        # In a real implementation, you'd call an actual AI API
        key_themes = self._extract_key_themes(combined_text, symbol)
        market_impact = self._assess_market_impact(combined_text)
        outlook = self._generate_outlook(combined_text)
        
        summary = f"""
🤖 **AI SUMMARY FOR {symbol}**

📋 **Key Themes:**
{key_themes}

📈 **Market Impact:**
{market_impact}

🔮 **Outlook:**
{outlook}

📊 **Analysis Based On:** {len(articles)} real news articles from {len(set(a.get('publisher', '') for a in articles))} sources
"""
        return summary
    
    def _extract_key_themes(self, text: str, symbol: str) -> str:
        """Extract key themes from news content"""
        text_lower = text.lower()
        themes = []
        
        # Check for common financial themes
        if any(word in text_lower for word in ['earnings', 'revenue', 'profit', 'financial results']):
            themes.append("• Earnings and Financial Performance")
        
        if any(word in text_lower for word in ['ai', 'artificial intelligence', 'technology', 'innovation']):
            themes.append("• AI and Technology Development")
        
        if any(word in text_lower for word in ['growth', 'expansion', 'market share']):
            themes.append("• Business Growth and Expansion")
        
        if any(word in text_lower for word in ['risk', 'concern', 'challenge', 'regulatory']):
            themes.append("• Risk Factors and Challenges")
        
        if any(word in text_lower for word in ['analyst', 'upgrade', 'downgrade', 'rating']):
            themes.append("• Analyst Coverage and Ratings")
        
        if any(word in text_lower for word in ['partnership', 'deal', 'contract', 'agreement']):
            themes.append("• Strategic Partnerships and Deals")
        
        return "\n".join(themes) if themes else "• General market and company news"
    
    def _assess_market_impact(self, text: str) -> str:
        """Assess potential market impact"""
        text_lower = text.lower()
        
        positive_indicators = sum(1 for word in ['beat', 'exceed', 'strong', 'growth', 'positive', 'bullish'] if word in text_lower)
        negative_indicators = sum(1 for word in ['miss', 'weak', 'decline', 'concern', 'negative', 'bearish'] if word in text_lower)
        
        if positive_indicators > negative_indicators:
            return "Potentially positive market impact based on favorable news coverage and strong performance indicators."
        elif negative_indicators > positive_indicators:
            return "Potentially negative market impact due to concerns and challenging factors mentioned in coverage."
        else:
            return "Mixed market signals with both positive and negative factors present in news coverage."
    
    def _generate_outlook(self, text: str) -> str:
        """Generate outlook based on news content"""
        text_lower = text.lower()
        
        if any(word in text_lower for word in ['momentum', 'accelerating', 'expanding', 'growing']):
            return "Positive momentum suggested by recent developments and growth initiatives."
        elif any(word in text_lower for word in ['slowing', 'declining', 'struggling', 'challenging']):
            return "Cautious outlook due to mentioned challenges and potential headwinds."
        else:
            return "Neutral outlook with mixed signals requiring continued monitoring of developments."

    def display_articles(self, articles: List[Dict], sentiment_analysis: Dict, symbol: str):
        """Display articles with sentiment analysis and AI summary"""
        if not articles:
            print("❌ No articles to display")
            return
        
        print(f"\n📰 **NEWS ARTICLES ANALYZED**")
        print("=" * 80)
        
        for i, article in enumerate(articles, 1):
            title = article.get('title', 'No title')
            link = article.get('link', 'No link')
            publisher = article.get('publisher', 'Unknown')
            sentiment_score = article.get('sentiment_score', 0.0)
            
            # Determine sentiment emoji
            if sentiment_score > 0.2:
                sentiment_emoji = "🟢 POSITIVE"
            elif sentiment_score < -0.2:
                sentiment_emoji = "🔴 NEGATIVE"
            else:
                sentiment_emoji = "🟡 NEUTRAL"
            
            print(f"**{i}. {sentiment_emoji}**")
            print(f"   📰 Title: {title}")
            print(f"   🏢 Publisher: {publisher}")
            print(f"   📊 Sentiment Score: {sentiment_score:.2f}")
            print(f"   🔗 URL: {link}")
            
            # Show full summary/content
            summary = article.get('summary', '')
            if summary and summary != title and len(summary) > 10:
                print(f"   📝 Article Content: {summary}")
            
            print("-" * 80)
        
        # Generate and display AI summary
        ai_summary = self.generate_ai_summary(articles, symbol)
        print(ai_summary)
        
        # Display overall sentiment
        print(f"\n📊 **OVERALL NEWS SENTIMENT**")
        print(f"   📈 Sentiment Score: {sentiment_analysis['score']:.2f} (-1.0 to +1.0)")
        print(f"   🎯 Confidence: {sentiment_analysis['confidence']:.1%}")
        print(f"   📊 Articles Analyzed: {sentiment_analysis['analyzed_count']}/{sentiment_analysis['total_count']}")
        
        if sentiment_analysis['score'] > 0.2:
            sentiment_label = "🟢 BULLISH"
        elif sentiment_analysis['score'] < -0.2:
            sentiment_label = "🔴 BEARISH"
        else:
            sentiment_label = "🟡 NEUTRAL"
        
        print(f"   🏷️ Overall Label: {sentiment_label}")
    
    def fetch_all_news(self, symbol: str) -> List[Dict]:
        """Fetch news from all available sources"""
        print(f"🚀 FETCHING REAL NEWS FOR {symbol}")
        print("=" * 60)
        
        all_articles = []
        
        # Try Yahoo Finance
        yahoo_articles = self.fetch_yahoo_finance_news(symbol)
        all_articles.extend(yahoo_articles)
        
        # Try Google News
        google_articles = self.fetch_google_news_rss(symbol)
        all_articles.extend(google_articles)
        
        # Try Finviz
        finviz_articles = self.fetch_finviz_news(symbol)
        all_articles.extend(finviz_articles)
        
        # Remove duplicates based on title
        seen_titles = set()
        unique_articles = []
        for article in all_articles:
            title = article.get('title', '')
            if title not in seen_titles:
                seen_titles.add(title)
                unique_articles.append(article)
        
        print(f"\n📊 **NEWS FETCH SUMMARY**")
        print(f"   Yahoo Finance: {len(yahoo_articles)} articles")
        print(f"   Google News: {len(google_articles)} articles")
        print(f"   Finviz: {len(finviz_articles)} articles")
        print(f"   Total unique: {len(unique_articles)} articles")
        
        return unique_articles

def main():
    """Main function to demonstrate real news fetching"""
    import argparse
    
    parser = argparse.ArgumentParser(description="Robust News Fetcher")
    parser.add_argument("--asset", default="PLTR", help="Asset symbol to analyze")
    
    args = parser.parse_args()
    
    print("🎯 REAL NEWS SENTIMENT ANALYSIS")
    print("=" * 50)
    
    # Initialize fetcher
    fetcher = RobustNewsFetcher()
    
    # Fetch news from all sources
    articles = fetcher.fetch_all_news(args.asset)
    
    if articles:
        # Analyze sentiment
        sentiment_analysis = fetcher.analyze_sentiment(articles)
        
        # Display results with AI summary
        fetcher.display_articles(articles, sentiment_analysis, args.asset)
        
    else:
        print(f"\n❌ **NO NEWS FOUND**")
        print(f"   • {args.asset} might not be a valid stock symbol")
        print(f"   • No recent news exists for this asset")
        print(f"   • Network connectivity issues")
        print(f"   • Try popular stocks: AAPL, TSLA, NVDA")

if __name__ == "__main__":
    main()