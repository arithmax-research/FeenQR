#!/usr/bin/env python3
"""
Enhanced News Scraper for CNBC, Bloomberg, Reuters, and other financial news sites
Based on: 
- https://github.com/asepscareer/ycnbc
- https://github.com/weiwangchun/bbg_scraper  
- https://github.com/maximenc/Reuters-News-Scraper
"""

import requests
from bs4 import BeautifulSoup
import json
import sys
import argparse
from datetime import datetime
import re
from urllib.parse import urlparse

# Headers to mimic a real browser
HEADERS = {
    'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36',
    'Accept': 'text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8',
    'Accept-Language': 'en-US,en;q=0.5',
    'Accept-Encoding': 'gzip, deflate, br',
    'Connection': 'keep-alive',
    'Upgrade-Insecure-Requests': '1',
    'Sec-Fetch-Dest': 'document',
    'Sec-Fetch-Mode': 'navigate',
    'Sec-Fetch-Site': 'none',
    'Cache-Control': 'max-age=0'
}

def scrape_cnbc(url):
    """Scrape CNBC article"""
    try:
        print(f"Scraping CNBC article: {url}", file=sys.stderr)
        
        response = requests.get(url, headers=HEADERS, timeout=15)
        response.raise_for_status()
        
        soup = BeautifulSoup(response.content, 'html.parser')
        
        # Extract title
        title = None
        title_elem = soup.find('h1', class_='ArticleHeader-headline')
        if not title_elem:
            title_elem = soup.find('h1')
        if title_elem:
            title = title_elem.get_text().strip()
        
        # Extract author
        author = "CNBC"
        author_elem = soup.find('a', class_='Author-authorName')
        if author_elem:
            author = author_elem.get_text().strip()
        
        # Extract published date
        published_date = ""
        time_elem = soup.find('time')
        if time_elem:
            published_date = time_elem.get('datetime', time_elem.get_text().strip())
        
        # Extract content
        content = []
        
        # Try multiple selectors for content
        article_body = soup.find('div', class_='ArticleBody-articleBody')
        if not article_body:
            article_body = soup.find('div', class_='group')
        
        if article_body:
            paragraphs = article_body.find_all(['p', 'h2', 'h3'])
            for p in paragraphs:
                text = p.get_text().strip()
                if text and len(text) > 20:  # Filter out very short lines
                    content.append(text)
        
        # Extract image
        image_url = ""
        img_elem = soup.find('img', class_='ArticleHeader-image')
        if not img_elem:
            img_elem = soup.find('img')
        if img_elem:
            image_url = img_elem.get('src', '')
        
        return {
            'title': title or 'No title found',
            'content': '\n\n'.join(content) if content else 'No content found',
            'url': url,
            'source': 'CNBC',
            'author': author,
            'published_date': published_date,
            'image_url': image_url
        }
        
    except Exception as e:
        print(f"Error scraping CNBC: {e}", file=sys.stderr)
        return None

def scrape_bloomberg(url):
    """Scrape Bloomberg article"""
    try:
        print(f"Scraping Bloomberg article: {url}", file=sys.stderr)
        
        response = requests.get(url, headers=HEADERS, timeout=15)
        response.raise_for_status()
        
        soup = BeautifulSoup(response.content, 'html.parser')
        
        # Extract title
        title = None
        title_elem = soup.find('h1', attrs={'data-component': 'headline'})
        if not title_elem:
            title_elem = soup.find('h1')
        if title_elem:
            title = title_elem.get_text().strip()
        
        # Extract author
        author = "Bloomberg"
        author_elem = soup.find('a', attrs={'data-component': 'author-link'})
        if not author_elem:
            author_elem = soup.find('div', class_='author')
        if author_elem:
            author = author_elem.get_text().strip()
        
        # Extract published date
        published_date = ""
        time_elem = soup.find('time')
        if time_elem:
            published_date = time_elem.get('datetime', time_elem.get_text().strip())
        
        # Extract content
        content = []
        
        # Bloomberg uses various content containers
        article_body = soup.find('div', class_='body-content')
        if not article_body:
            article_body = soup.find('article')
        
        if article_body:
            paragraphs = article_body.find_all('p')
            for p in paragraphs:
                # Skip ads and promotional content
                if 'class' in p.attrs and any(cls in str(p['class']) for cls in ['ad', 'promo', 'subscribe']):
                    continue
                text = p.get_text().strip()
                if text and len(text) > 30:
                    content.append(text)
        
        # Extract image
        image_url = ""
        img_elem = soup.find('img', class_='hero-image')
        if not img_elem:
            img_elem = soup.find('img')
        if img_elem:
            image_url = img_elem.get('src', '')
        
        return {
            'title': title or 'No title found',
            'content': '\n\n'.join(content) if content else 'No content found',
            'url': url,
            'source': 'Bloomberg',
            'author': author,
            'published_date': published_date,
            'image_url': image_url
        }
        
    except Exception as e:
        print(f"Error scraping Bloomberg: {e}", file=sys.stderr)
        return None

def scrape_reuters(url):
    """Scrape Reuters article"""
    try:
        print(f"Scraping Reuters article: {url}", file=sys.stderr)
        
        response = requests.get(url, headers=HEADERS, timeout=15)
        response.raise_for_status()
        
        soup = BeautifulSoup(response.content, 'html.parser')
        
        # Extract title
        title = None
        title_elem = soup.find('h1', attrs={'data-testid': 'Heading'})
        if not title_elem:
            title_elem = soup.find('h1')
        if title_elem:
            title = title_elem.get_text().strip()
        
        # Extract author
        author = "Reuters"
        author_elem = soup.find('a', class_='author-name')
        if not author_elem:
            author_elem = soup.find('div', class_='ArticleHeader_author')
        if author_elem:
            author = author_elem.get_text().strip()
        
        # Extract published date
        published_date = ""
        time_elem = soup.find('time')
        if time_elem:
            published_date = time_elem.get('datetime', time_elem.get_text().strip())
        
        # Extract content
        content = []
        
        # Reuters article body
        article_body = soup.find('div', class_='article-body')
        if not article_body:
            article_body = soup.find('div', attrs={'data-testid': 'ArticleBody'})
        if not article_body:
            article_body = soup.find('article')
        
        if article_body:
            paragraphs = article_body.find_all('p')
            for p in paragraphs:
                text = p.get_text().strip()
                if text and len(text) > 30 and 'Register now for FREE' not in text:
                    content.append(text)
        
        # Extract image
        image_url = ""
        img_elem = soup.find('img', class_='Image_image')
        if not img_elem:
            img_elem = soup.find('img')
        if img_elem:
            image_url = img_elem.get('src', '')
        
        return {
            'title': title or 'No title found',
            'content': '\n\n'.join(content) if content else 'No content found',
            'url': url,
            'source': 'Reuters',
            'author': author,
            'published_date': published_date,
            'image_url': image_url
        }
        
    except Exception as e:
        print(f"Error scraping Reuters: {e}", file=sys.stderr)
        return None

def scrape_yahoo_finance(url):
    """Scrape Yahoo Finance article"""
    try:
        print(f"Scraping Yahoo Finance article: {url}", file=sys.stderr)
        
        response = requests.get(url, headers=HEADERS, timeout=15)
        response.raise_for_status()
        
        soup = BeautifulSoup(response.content, 'html.parser')
        
        # Extract title
        title = None
        title_elem = soup.find('h1')
        if title_elem:
            title = title_elem.get_text().strip()
        
        # Extract author
        author = "Yahoo Finance"
        author_elem = soup.find('div', class_='caas-author-byline-collapse')
        if author_elem:
            author = author_elem.get_text().strip()
        
        # Extract published date
        published_date = ""
        time_elem = soup.find('time')
        if time_elem:
            published_date = time_elem.get('datetime', time_elem.get_text().strip())
        
        # Extract content
        content = []
        article_body = soup.find('div', class_='caas-body')
        if article_body:
            paragraphs = article_body.find_all('p')
            for p in paragraphs:
                text = p.get_text().strip()
                if text and len(text) > 30:
                    content.append(text)
        
        # Extract image
        image_url = ""
        img_elem = soup.find('img')
        if img_elem:
            image_url = img_elem.get('src', '')
        
        return {
            'title': title or 'No title found',
            'content': '\n\n'.join(content) if content else 'No content found',
            'url': url,
            'source': 'Yahoo Finance',
            'author': author,
            'published_date': published_date,
            'image_url': image_url
        }
        
    except Exception as e:
        print(f"Error scraping Yahoo Finance: {e}", file=sys.stderr)
        return None

def scrape_generic(url):
    """Generic scraper for other news sites"""
    try:
        print(f"Attempting generic scrape: {url}", file=sys.stderr)
        
        response = requests.get(url, headers=HEADERS, timeout=15)
        response.raise_for_status()
        
        soup = BeautifulSoup(response.content, 'html.parser')
        
        # Extract title
        title = None
        title_elem = soup.find('h1')
        if title_elem:
            title = title_elem.get_text().strip()
        
        # Extract author
        author_elem = soup.find('meta', attrs={'name': 'author'})
        author = author_elem.get('content', 'Unknown') if author_elem else urlparse(url).netloc
        
        # Extract published date
        published_date = ""
        time_elem = soup.find('time')
        if time_elem:
            published_date = time_elem.get('datetime', time_elem.get_text().strip())
        if not published_date:
            date_meta = soup.find('meta', attrs={'property': 'article:published_time'})
            if date_meta:
                published_date = date_meta.get('content', '')
        
        # Extract content - try to find article body
        content = []
        article = soup.find('article')
        if article:
            paragraphs = article.find_all('p')
        else:
            paragraphs = soup.find_all('p')
        
        for p in paragraphs:
            text = p.get_text().strip()
            if text and len(text) > 40:  # Only substantial paragraphs
                content.append(text)
        
        # Limit to reasonable content
        content = content[:50]  # Max 50 paragraphs
        
        # Extract image
        image_url = ""
        og_image = soup.find('meta', property='og:image')
        if og_image:
            image_url = og_image.get('content', '')
        elif soup.find('img'):
            img_elem = soup.find('img')
            image_url = img_elem.get('src', '')
        
        return {
            'title': title or 'No title found',
            'content': '\n\n'.join(content) if content else 'No content found',
            'url': url,
            'source': urlparse(url).netloc,
            'author': author,
            'published_date': published_date,
            'image_url': image_url
        }
        
    except Exception as e:
        print(f"Error with generic scraper: {e}", file=sys.stderr)
        return None

def scrape_url(url):
    """Main function to scrape URL based on source"""
    try:
        parsed_url = urlparse(url)
        domain = parsed_url.netloc.lower()
        
        print(f"Domain detected: {domain}", file=sys.stderr)
        
        # Route to appropriate scraper
        if 'cnbc.com' in domain:
            return scrape_cnbc(url)
        elif 'bloomberg.com' in domain:
            return scrape_bloomberg(url)
        elif 'reuters.com' in domain:
            return scrape_reuters(url)
        elif 'yahoo.com' in domain:
            return scrape_yahoo_finance(url)
        else:
            # Try generic scraper for other sites
            return scrape_generic(url)
            
    except Exception as e:
        print(f"Error scraping URL: {e}", file=sys.stderr)
        return None

def main():
    parser = argparse.ArgumentParser(description='Scrape news articles from various financial news sources')
    parser.add_argument('url', help='URL of the article to scrape')
    
    args = parser.parse_args()
    
    result = scrape_url(args.url)
    
    if result:
        # Output as JSON
        print(json.dumps(result, indent=2))
        sys.exit(0)
    else:
        print(json.dumps({'error': 'Failed to scrape article'}), file=sys.stderr)
        sys.exit(1)

if __name__ == '__main__':
    main()
