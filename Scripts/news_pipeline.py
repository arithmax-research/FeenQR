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
import os
import sys
from dataclasses import dataclass, asdict
from datetime import datetime
from typing import Iterable, List, Optional

import yfinance as yf
from newsapi import NewsApiClient
from newspaper import Article as NewspaperArticle
from newspaper import ArticleException


NEWSAPI_KEY = os.getenv("NEWSAPI_KEY")


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
    parser.add_argument("--pretty", action="store_true", help="Pretty-print JSON output")

    args = parser.parse_args()

    try:
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
