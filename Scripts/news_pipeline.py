#!/usr/bin/env python3
"""Dual news pipeline using NewsAPI, yfinance, and newspaper3k.

This module exposes a small CLI and importable helpers for:
- discovering article URLs from NewsAPI and yfinance
- filtering unwanted URLs
- scraping full article text with newspaper3k
- emitting a JSON payload for downstream consumers

It is intentionally Python-only so the pipeline can rely on the native
libraries that already exist in the Python ecosystem.
"""

from __future__ import annotations

import argparse
import json
import math
import os
import re
import sys
from dataclasses import dataclass, asdict
from datetime import datetime
from collections import Counter
from typing import Iterable, List, Optional
from dotenv import load_dotenv

import numpy as np
import yfinance as yf
from newsapi import NewsApiClient
from newspaper import Article as NewspaperArticle
from newspaper import ArticleException



# Load environment variables from .env file
load_dotenv()

# NewsAPI key
NEWSAPI_KEY = os.getenv("NEWSAPI_KEY")

POS_CENTROID_PATH = os.path.join(os.path.dirname(os.path.abspath(__file__)), "pos_centroid.npy")
NEG_CENTROID_PATH = os.path.join(os.path.dirname(os.path.abspath(__file__)), "neg_centroid.npy")
NEU_CENTROID_PATH = os.path.join(os.path.dirname(os.path.abspath(__file__)), "neu_centroid.npy")

_embedder = None
_centroids = None

_STOPWORDS = {
    "the", "and", "for", "with", "that", "this", "from", "have", "has", "had", "was", "were",
    "been", "will", "would", "could", "should", "about", "into", "over", "under", "than", "then",
    "after", "before", "while", "when", "where", "what", "which", "who", "whom", "their", "there",
    "they", "them", "these", "those", "also", "just", "like", "more", "most", "much", "very",
    "stock", "stocks", "company", "market", "news", "said", "says", "amid", "new", "all",
    "its", "our", "your", "his", "her", "out", "can", "may", "might",
}


def _get_embedder():
    global _embedder
    if _embedder is None:
        from fastembed import TextEmbedding

        _embedder = TextEmbedding(model_name="BAAI/bge-small-en-v1.5")
    return _embedder


def _normalize_vector(vector):
    norm = float(np.linalg.norm(vector))
    if norm == 0.0:
        return vector
    return vector / norm


def _cosine_similarity(left, right):
    left = _normalize_vector(np.asarray(left, dtype=np.float32))
    right = _normalize_vector(np.asarray(right, dtype=np.float32))
    return float(np.dot(left, right))


def _get_centroids():
    global _centroids
    if _centroids is not None:
        return _centroids

    if all(os.path.exists(path) for path in [POS_CENTROID_PATH, NEG_CENTROID_PATH, NEU_CENTROID_PATH]):
        _centroids = {
            "positive": np.load(POS_CENTROID_PATH),
            "negative": np.load(NEG_CENTROID_PATH),
            "neutral": np.load(NEU_CENTROID_PATH),
        }
        return _centroids

    positive_prototypes = [
        "Strong earnings beat with record revenue and expanding profit margins.",
        "Stock surging on positive analyst upgrades and strong demand outlook.",
        "Company launching innovative products driving massive adoption.",
        "Industry-leading performance with excellent free cash flow generation.",
        "Bullish outlook with raised guidance and accelerating market share.",
        "Breakthrough technology partnership expected to drive significant revenue.",
        "Exceptional quarter exceeds all expectations across every metric.",
        "Management executing flawlessly on growth strategy with momentum.",
        "Strong buy recommendations from analysts with high price targets.",
        "Record breaking sales figures and expanding customer base globally.",
        "Strategic acquisition expected to be immediately accretive to earnings.",
        "Company demonstrating resilient growth despite challenging environment.",
    ]
    negative_prototypes = [
        "Disappointing earnings miss with declining revenue and shrinking margins.",
        "Stock plummeting after catastrophic earnings report and lowered guidance.",
        "Company facing serious regulatory investigations and mounting lawsuits.",
        "Major product recall damaging brand reputation and consumer trust.",
        "Executive departures signal deep internal turmoil and leadership crisis.",
        "Debt downgrade amid concerns about ability to service obligations.",
        "Severe supply chain disruptions causing production halts and delays.",
        "Competitor gaining significant market share at company's expense.",
        "Fraud allegations triggering federal investigation and lawsuits.",
        "Layoffs and restructuring as company struggles with declining demand.",
        "Cash burn rate accelerating with no clear path to profitability.",
        "Analysts downgrade stock citing deteriorating fundamentals.",
    ]
    neutral_prototypes = [
        "Stock trading within expected range with no major catalyst today.",
        "Company announced routine quarterly dividend payment to shareholders.",
        "Industry conference scheduled for next week with panel discussions.",
        "Company filed standard regulatory paperwork with SEC on deadline.",
        "Trading volume in line with historical averages for the session.",
        "Board of directors announced regular annual meeting date.",
        "Company published updated corporate governance guidelines.",
        "Standard market making activity observed across the sector.",
        "Routine patent filing published by the patent office today.",
        "Company spokesperson declined to comment on market speculation.",
        "Analyst maintains hold rating with fair value estimate unchanged.",
        "Regular maintenance update released for company software products.",
    ]

    embedder = _get_embedder()
    pos_embs = np.asarray(list(embedder.embed(positive_prototypes)), dtype=np.float32)
    neg_embs = np.asarray(list(embedder.embed(negative_prototypes)), dtype=np.float32)
    neu_embs = np.asarray(list(embedder.embed(neutral_prototypes)), dtype=np.float32)

    _centroids = {
        "positive": np.mean(pos_embs, axis=0),
        "negative": np.mean(neg_embs, axis=0),
        "neutral": np.mean(neu_embs, axis=0),
    }

    np.save(POS_CENTROID_PATH, _centroids["positive"])
    np.save(NEG_CENTROID_PATH, _centroids["negative"])
    np.save(NEU_CENTROID_PATH, _centroids["neutral"])
    return _centroids


def _extract_key_topics(text: str, limit: int = 3) -> List[str]:
    tokens = re.findall(r"[A-Za-z][A-Za-z\-]{3,}", text.lower())
    filtered = [token for token in tokens if token not in _STOPWORDS and not token.isdigit()]
    if not filtered:
        return []

    counts = Counter(filtered)
    topics: List[str] = []
    for token, _ in counts.most_common():
        if token not in topics:
            topics.append(token)
        if len(topics) >= limit:
            break
    return topics


def _score_text(text: str) -> dict:
    if not text or not text.strip():
        return {
            "sentiment": "NEUTRAL",
            "polarity": 0.0,
            "pos_score": 0.3333,
            "neg_score": 0.3333,
            "neu_score": 0.3334,
            "confidence": 0.3334,
            "keyTopics": [],
            "impact": "Low",
        }

    embedder = _get_embedder()
    centroids = _get_centroids()
    embedding = np.asarray(next(embedder.embed([text])), dtype=np.float32)

    pos_sim = _cosine_similarity(embedding, centroids["positive"])
    neg_sim = _cosine_similarity(embedding, centroids["negative"])
    neu_sim = _cosine_similarity(embedding, centroids["neutral"])

    e_pos = math.exp(pos_sim)
    e_neg = math.exp(neg_sim)
    e_neu = math.exp(neu_sim)
    total = e_pos + e_neg + e_neu
    softmax_pos = e_pos / total
    softmax_neg = e_neg / total
    softmax_neu = e_neu / total

    raw_diff = pos_sim - neg_sim
    polarity = math.tanh(raw_diff * 10)

    if softmax_pos > softmax_neg and softmax_pos > softmax_neu:
        sentiment = "POSITIVE"
        confidence = softmax_pos
    elif softmax_neg > softmax_neu:
        sentiment = "NEGATIVE"
        confidence = softmax_neg
    else:
        sentiment = "NEUTRAL"
        confidence = softmax_neu

    if abs(polarity) > 0.35 and confidence > 0.45:
        impact = "High"
    elif abs(polarity) > 0.15:
        impact = "Medium"
    else:
        impact = "Low"

    return {
        "sentiment": sentiment,
        "polarity": round(polarity, 4),
        "pos_score": round(softmax_pos, 4),
        "neg_score": round(softmax_neg, 4),
        "neu_score": round(softmax_neu, 4),
        "confidence": round(confidence, 4),
        "keyTopics": _extract_key_topics(text),
        "impact": impact,
    }


def analyze_sentiment(article_texts):
    single_mode = isinstance(article_texts, str)
    if single_mode:
        article_texts = [article_texts]

    results = [_score_text(text) for text in article_texts]

    polarities = [item["polarity"] for item in results]
    sentiments = [item["sentiment"] for item in results]
    avg_polarity = float(np.mean(polarities)) if polarities else 0.0
    bullish_pct = sentiments.count("POSITIVE") / len(sentiments) * 100 if sentiments else 0.0
    bearish_pct = sentiments.count("NEGATIVE") / len(sentiments) * 100 if sentiments else 0.0
    neutral_pct = sentiments.count("NEUTRAL") / len(sentiments) * 100 if sentiments else 0.0

    if avg_polarity > 0.15:
        agg_sentiment = "BULLISH"
    elif avg_polarity < -0.15:
        agg_sentiment = "BEARISH"
    else:
        agg_sentiment = "NEUTRAL"

    payload = {
        "articles": results,
        "aggregate": {
            "sentiment": agg_sentiment,
            "avg_polarity": round(avg_polarity, 4),
            "bullish_pct": round(bullish_pct, 1),
            "bearish_pct": round(bearish_pct, 1),
            "neutral_pct": round(neutral_pct, 1),
            "article_count": len(results),
        },
    }

    if single_mode:
        return {**results[0], "aggregate": payload["aggregate"]}

    return payload


def _load_text_payload(raw_payload: str):
    payload = json.loads(raw_payload)
    if isinstance(payload, list):
        return payload
    if isinstance(payload, dict):
        articles = payload.get("articles")
        if isinstance(articles, list):
            return articles
        texts = payload.get("texts")
        if isinstance(texts, list):
            return texts
        text = payload.get("text")
        if isinstance(text, str):
            return [text]
    raise ValueError("Sentiment payload must include an articles, texts, or text field")


def _sentiment_mode(args) -> int:
    raw_payload = args.texts_json or sys.stdin.read()
    if not raw_payload or not raw_payload.strip():
        raise ValueError("sentiment mode requires JSON payload on stdin or --texts-json")

    input_items = _load_text_payload(raw_payload)
    texts: List[str] = []

    for item in input_items:
        if isinstance(item, str):
            texts.append(item)
            continue

        if isinstance(item, dict):
            text = (
                item.get("content")
                or item.get("fullContent")
                or item.get("summary")
                or item.get("text")
                or ""
            )
            texts.append(text)
            continue

        texts.append(str(item))

    payload = analyze_sentiment(texts)
    json.dump(payload, sys.stdout, indent=2 if args.pretty else None)
    sys.stdout.write("\n")
    return 0


@dataclass
class NewsArticle:
    title: str
    content: str
    url: str
    source: str
    provider: str
    published_at: Optional[str] = None
    scraped_at: Optional[str] = None
    is_scraped: bool = False


def search_for_stock_news_urls(ticker: str, source: str, newsapi_page_size: int = 15, yfinance_limit: int = 10) -> List[str]:
    """Fetch recent news URLs for a ticker from NewsAPI and yfinance.

    The source filter matches provider/publication names. Use "All" to
    return everything.
    """
    print(f"[DEBUG] Searching for news: {source} -- {ticker}", file=sys.stderr)

    seen_urls = set()
    all_urls: List[str] = []

    newsapi_urls = _search_newsapi(ticker, source, page_size=newsapi_page_size)
    for url in newsapi_urls:
        if url not in seen_urls:
            seen_urls.add(url)
            all_urls.append(url)

    yf_urls = _search_yfinance(ticker, source, limit=yfinance_limit)
    for url in yf_urls:
        if url not in seen_urls:
            seen_urls.add(url)
            all_urls.append(url)

    print(f"[DEBUG] Combined total: {len(all_urls)} unique URLs", file=sys.stderr)
    return all_urls


def _search_newsapi(ticker: str, source: str, page_size: int = 15) -> List[str]:
    """Search news using NewsAPI and return article URLs."""
    print(f"[DEBUG] Using NewsAPI (ticker={ticker})", file=sys.stderr)

    query = f'"{ticker}" stock'
    base = ticker.split("-")[0]
    if base != ticker:
        query = f'"{base}" OR "{ticker}"'

    try:
        newsapi = NewsApiClient(api_key=NEWSAPI_KEY)
        results = newsapi.get_everything(
            q=query,
            language="en",
            sort_by="relevancy",
            page_size=page_size,
        )
    except Exception as exc:
        print(f"[ERROR] NewsAPI call failed: {exc}", file=sys.stderr)
        return []

    articles_data = results.get("articles", [])
    total = results.get("totalResults", "?")
    print(f"[DEBUG] NewsAPI returned {len(articles_data)} articles (total={total})", file=sys.stderr)

    if not articles_data:
        return []

    source_lower = source.lower().strip()
    return_all = source_lower in ("all", "all news", "general")
    raw_urls: List[str] = []

    for article in articles_data:
        source_name = article.get("source", {}).get("name", "") or "Unknown"
        title = article.get("title", "") or ""
        url = article.get("url", "") or ""

        if not url or "consent.yahoo.com" in url:
            continue

        if return_all or source_lower in source_name.lower():
            raw_urls.append(url)
            print(f"   [NewsAPI][{source_name}] {title}", file=sys.stderr)
            print(f"      -> {url}", file=sys.stderr)

    if not raw_urls:
        print("[DEBUG] NewsAPI: no source match. Returning all.", file=sys.stderr)
        for article in articles_data:
            source_name = article.get("source", {}).get("name", "") or "Unknown"
            title = article.get("title", "") or ""
            url = article.get("url", "") or ""
            if url and "consent.yahoo.com" not in url:
                raw_urls.append(url)
                print(f"   [NewsAPI][{source_name}] {title}", file=sys.stderr)
                print(f"      -> {url}", file=sys.stderr)

    print(f"[DEBUG] NewsAPI final URL count: {len(raw_urls)}", file=sys.stderr)
    return raw_urls


def _search_yfinance(ticker: str, source: str, limit: int = 10) -> List[str]:
    """Fetch news using yfinance and return canonical URLs."""
    print(f"[DEBUG] Searching yfinance for: {ticker}", file=sys.stderr)

    try:
        stock = yf.Ticker(ticker)
        news_items = stock.news
    except Exception as exc:
        print(f"[ERROR] yfinance failed: {exc}", file=sys.stderr)
        return []

    print(f"[DEBUG] yfinance returned {len(news_items)} news items", file=sys.stderr)

    source_lower = source.lower().strip()
    return_all = source_lower in ("all", "all news", "general")

    raw_urls: List[str] = []
    for item in news_items[:limit]:
        content = item.get("content", {})
        provider = content.get("provider", {})
        provider_name = provider.get("displayName", "")
        title = content.get("title", "")
        canonical_url = content.get("canonicalUrl", {}).get("url", "")

        if not canonical_url:
            continue

        if return_all or source_lower in provider_name.lower():
            raw_urls.append(canonical_url)
            print(f"   [yfinance][{provider_name}] {title}", file=sys.stderr)
            print(f"      -> {canonical_url}", file=sys.stderr)

    if not raw_urls:
        print("[DEBUG] yfinance: no source match. Returning all.", file=sys.stderr)
        for item in news_items[:limit]:
            content = item.get("content", {})
            provider = content.get("provider", {}).get("displayName", "Unknown")
            title = content.get("title", "")
            canonical_url = content.get("canonicalUrl", {}).get("url", "")
            if canonical_url:
                raw_urls.append(canonical_url)
                print(f"   [yfinance][{provider}] {title}", file=sys.stderr)
                print(f"      -> {canonical_url}", file=sys.stderr)

    print(f"[DEBUG] yfinance final URL count: {len(raw_urls)}", file=sys.stderr)
    return raw_urls


def strip_unwanted_urls(urls: Iterable[str], excluded_list: Iterable[str]) -> List[str]:
    print("[DEBUG] Filtering URLs...", file=sys.stderr)
    excluded = set(excluded_list)
    excluded.update({"google.com", "maps", "policies", "support"})

    clean: List[str] = []
    for url in urls:
        if any(fragment in url for fragment in excluded):
            print(f"   [SKIP] {url}", file=sys.stderr)
            continue
        print(f"   [KEEP] {url}", file=sys.stderr)
        clean.append(url)

    print(f"[DEBUG] Final count: {len(clean)} URLs", file=sys.stderr)
    return clean


def scrape_and_process(urls: Iterable[str], word_limit: Optional[int] = None) -> List[Optional[NewsArticle]]:
    """Scrape article text from URLs using newspaper3k."""
    print("[DEBUG] Scraping articles with newspaper3k...", file=sys.stderr)
    articles: List[Optional[NewsArticle]] = []

    for url in urls:
        print(f"[SCRAPE] {url}", file=sys.stderr)
        article = NewspaperArticle(url)
        try:
            article.download()
            article.parse()

            text = article.text or ""
            if not text:
                print("   [WARNING] No text extracted", file=sys.stderr)
                articles.append(None)
                continue

            if word_limit is not None:
                text = " ".join(text.split()[:word_limit])

            print(f"   Title: {article.title[:100]}", file=sys.stderr)
            print(f"   Extracted {len(text.split())} words", file=sys.stderr)

            articles.append(
                NewsArticle(
                    title=article.title or "",
                    content=text,
                    url=url,
                    source="newspaper3k",
                    provider="newspaper3k",
                    scraped_at=datetime.utcnow().isoformat(),
                    is_scraped=True,
                )
            )
        except ArticleException as exc:
            print(f"   [ERROR] newspaper3k failed: {exc}", file=sys.stderr)
            articles.append(None)
        except Exception as exc:
            print(f"   [ERROR] {exc}", file=sys.stderr)
            articles.append(None)

    return articles


def collect_pipeline_articles(
    ticker: str,
    source: str = "All",
    max_articles: int = 20,
    word_limit: Optional[int] = 3000,
) -> List[dict]:
    """Collect deduplicated, scraped articles as JSON-ready dictionaries."""
    raw_urls = search_for_stock_news_urls(ticker, source)
    filtered_urls = strip_unwanted_urls(raw_urls, [])
    scraped = scrape_and_process(filtered_urls[:max_articles], word_limit=word_limit)

    results: List[dict] = []
    for item in scraped:
        if item is None:
            continue
        results.append(asdict(item))

    return results


def scrape_single_url(url: str, word_limit: Optional[int] = 3000) -> dict:
    """Scrape a single URL and return a JSON-ready payload."""
    scraped = scrape_and_process([url], word_limit=word_limit)
    article = next((item for item in scraped if item is not None), None)
    if article is None:
        return {"url": url, "content": "", "scraped_at": datetime.utcnow().isoformat()}

    payload = asdict(article)
    payload["content"] = payload.pop("content", "")
    return payload


def main() -> int:
    parser = argparse.ArgumentParser(description="Dual news pipeline for stock/news scraping")
    parser.add_argument("ticker", nargs="?", help="Stock ticker symbol, e.g. TSLA")
    parser.add_argument("--url", help="Scrape a single article URL instead of searching by ticker")
    parser.add_argument("--source", default="All", help='Provider filter, e.g. "All", "Reuters", "CNBC"')
    parser.add_argument("--max-articles", type=int, default=20, help="Maximum articles to return")
    parser.add_argument("--word-limit", type=int, default=3000, help="Maximum words to keep per article")
    parser.add_argument("--sentiment", action="store_true", help="Analyze article sentiment using FastEmbed")
    parser.add_argument("--texts-json", help="JSON payload containing articles or texts for sentiment analysis")
    parser.add_argument("--pretty", action="store_true", help="Pretty-print JSON output")

    args = parser.parse_args()

    try:
        if args.sentiment:
            return _sentiment_mode(args)

        if args.url:
            payload = scrape_single_url(args.url, word_limit=args.word_limit)
            json.dump(payload, sys.stdout, indent=2 if args.pretty else None)
            sys.stdout.write("\n")
            return 0

        if not args.ticker:
            raise ValueError("ticker is required when --url is not provided")

        articles = collect_pipeline_articles(
            ticker=args.ticker,
            source=args.source,
            max_articles=args.max_articles,
            word_limit=args.word_limit,
        )
        payload = {
            "ticker": args.ticker,
            "source": args.source,
            "count": len(articles),
            "articles": articles,
            "generated_at": datetime.utcnow().isoformat(),
        }
        json.dump(payload, sys.stdout, indent=2 if args.pretty else None)
        sys.stdout.write("\n")
        return 0
    except Exception as exc:
        print(f"[ERROR] {exc}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
