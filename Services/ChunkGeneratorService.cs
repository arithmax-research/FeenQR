using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using QuantResearchAgent.Core;

namespace QuantResearchAgent.Services
{
    /// <summary>
    /// Service for generating semantic chunks from article content
    /// Splits articles into 500-1500 character chunks with 100-character overlap
    /// </summary>
    public class ChunkGeneratorService
    {
        private readonly ILogger<ChunkGeneratorService> _logger;
        private readonly int _minChunkSize;
        private readonly int _maxChunkSize;
        private readonly int _overlapSize;
        private readonly int _maxChunksPerArticle;

        public ChunkGeneratorService(
            IConfiguration configuration,
            ILogger<ChunkGeneratorService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Load configuration
            var config = new ChunkingConfig();
            configuration.GetSection("Chunking").Bind(config);

            _minChunkSize = config.MinChunkSize;
            _maxChunkSize = config.MaxChunkSize;
            _overlapSize = config.OverlapSize;
            _maxChunksPerArticle = config.MaxChunksPerArticle;

            _logger.LogInformation(
                "ChunkGeneratorService initialized - MinSize: {MinSize}, MaxSize: {MaxSize}, Overlap: {Overlap}",
                _minChunkSize, _maxChunkSize, _overlapSize);
        }

        /// <summary>
        /// Generate semantic chunks from article content
        /// </summary>
        /// <param name="article">Article content to chunk</param>
        /// <param name="metadata">News item metadata to attach to chunks</param>
        /// <returns>List of article chunks with metadata</returns>
        public List<ArticleChunk> GenerateChunks(ArticleContent article, NewsItem metadata)
        {
            if (article == null)
            {
                throw new ArgumentNullException(nameof(article));
            }

            if (metadata == null)
            {
                throw new ArgumentNullException(nameof(metadata));
            }

            var chunks = new List<ArticleChunk>();

            // Handle empty or failed article scraping
            if (!article.Success || string.IsNullOrWhiteSpace(article.Content))
            {
                _logger.LogWarning("Cannot generate chunks for article with no content: {Url}", article.Url);
                return chunks;
            }

            var content = article.Content;

            // Handle articles shorter than minimum chunk size as single chunk
            if (content.Length < _minChunkSize)
            {
                _logger.LogInformation(
                    "Article shorter than minimum chunk size ({Length} < {MinSize}), creating single chunk: {Url}",
                    content.Length, _minChunkSize, article.Url);

                chunks.Add(CreateChunk(content, article, metadata, 0));
                return chunks;
            }

            // Split into paragraphs
            var paragraphs = SplitIntoParagraphs(content);

            // Create chunks with overlap
            var chunkTexts = CreateChunksWithOverlap(paragraphs);

            // Limit to max chunks per article
            if (chunkTexts.Count > _maxChunksPerArticle)
            {
                _logger.LogWarning(
                    "Article has {Count} chunks, limiting to {Max}: {Url}",
                    chunkTexts.Count, _maxChunksPerArticle, article.Url);
                chunkTexts = chunkTexts.Take(_maxChunksPerArticle).ToList();
            }

            // Create ArticleChunk objects with metadata
            for (int i = 0; i < chunkTexts.Count; i++)
            {
                chunks.Add(CreateChunk(chunkTexts[i], article, metadata, i));
            }

            _logger.LogInformation(
                "Generated {Count} chunks for article: {Url}",
                chunks.Count, article.Url);

            return chunks;
        }

        /// <summary>
        /// Split content into paragraphs on paragraph boundaries
        /// </summary>
        /// <param name="content">Article content to split</param>
        /// <returns>List of paragraphs</returns>
        private List<string> SplitIntoParagraphs(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return new List<string>();
            }

            // Split on double newlines (paragraph boundaries)
            var paragraphs = content
                .Split(new[] { "\n\n", "\r\n\r\n" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim())
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .ToList();

            return paragraphs;
        }

        /// <summary>
        /// Create chunks with overlap from paragraphs
        /// Combines paragraphs into chunks between minChunkSize and maxChunkSize
        /// with overlapSize characters of overlap between consecutive chunks
        /// </summary>
        /// <param name="paragraphs">List of paragraphs to chunk</param>
        /// <returns>List of chunk texts</returns>
        private List<string> CreateChunksWithOverlap(List<string> paragraphs)
        {
            var chunks = new List<string>();

            if (!paragraphs.Any())
            {
                return chunks;
            }

            var currentChunk = new StringBuilder();
            var paragraphsInChunk = new List<string>();

            for (int i = 0; i < paragraphs.Count; i++)
            {
                var paragraph = paragraphs[i];
                var potentialLength = currentChunk.Length + paragraph.Length + 2; // +2 for "\n\n"

                // If adding this paragraph would exceed max chunk size
                if (currentChunk.Length > 0 && potentialLength > _maxChunkSize)
                {
                    // Save current chunk if it meets minimum size
                    if (currentChunk.Length >= _minChunkSize)
                    {
                        chunks.Add(currentChunk.ToString().Trim());

                        // Start new chunk with overlap
                        currentChunk.Clear();
                        paragraphsInChunk.Clear();

                        // Add overlap from previous chunk
                        var previousChunk = chunks.Last();
                        var overlapText = GetOverlapText(previousChunk, _overlapSize);
                        if (!string.IsNullOrEmpty(overlapText))
                        {
                            currentChunk.Append(overlapText);
                            currentChunk.Append("\n\n");
                        }
                    }
                    else
                    {
                        // Current chunk is too small, keep adding
                        currentChunk.Append("\n\n");
                    }
                }
                else if (currentChunk.Length > 0)
                {
                    // Add paragraph separator
                    currentChunk.Append("\n\n");
                }

                // Add current paragraph
                currentChunk.Append(paragraph);
                paragraphsInChunk.Add(paragraph);

                // If we've reached a good chunk size, consider saving it
                if (currentChunk.Length >= _minChunkSize && currentChunk.Length <= _maxChunkSize)
                {
                    // Look ahead to see if next paragraph would fit
                    if (i + 1 < paragraphs.Count)
                    {
                        var nextParagraph = paragraphs[i + 1];
                        var nextLength = currentChunk.Length + nextParagraph.Length + 2;

                        // If next paragraph won't fit, save current chunk
                        if (nextLength > _maxChunkSize)
                        {
                            chunks.Add(currentChunk.ToString().Trim());

                            // Start new chunk with overlap
                            currentChunk.Clear();
                            paragraphsInChunk.Clear();

                            var previousChunk = chunks.Last();
                            var overlapText = GetOverlapText(previousChunk, _overlapSize);
                            if (!string.IsNullOrEmpty(overlapText))
                            {
                                currentChunk.Append(overlapText);
                                currentChunk.Append("\n\n");
                            }
                        }
                    }
                }
            }

            // Add remaining content as final chunk
            if (currentChunk.Length > 0)
            {
                chunks.Add(currentChunk.ToString().Trim());
            }

            return chunks;
        }

        /// <summary>
        /// Get overlap text from the end of a chunk
        /// </summary>
        /// <param name="text">Text to extract overlap from</param>
        /// <param name="overlapSize">Number of characters to overlap</param>
        /// <returns>Overlap text</returns>
        private string GetOverlapText(string text, int overlapSize)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= overlapSize)
            {
                return text;
            }

            // Get last overlapSize characters
            var overlap = text.Substring(text.Length - overlapSize);

            // Try to start at a word boundary for cleaner overlap
            var firstSpace = overlap.IndexOf(' ');
            if (firstSpace > 0 && firstSpace < overlap.Length / 2)
            {
                overlap = overlap.Substring(firstSpace + 1);
            }

            return overlap;
        }

        /// <summary>
        /// Create an ArticleChunk with metadata
        /// </summary>
        /// <param name="content">Chunk content</param>
        /// <param name="article">Article content</param>
        /// <param name="metadata">News item metadata</param>
        /// <param name="chunkIndex">Index of this chunk</param>
        /// <returns>ArticleChunk with all metadata</returns>
        private ArticleChunk CreateChunk(
            string content,
            ArticleContent article,
            NewsItem metadata,
            int chunkIndex)
        {
            // Generate unique chunk ID combining article URL and chunk index
            var chunkId = GenerateChunkId(article.Url, chunkIndex);

            return new ArticleChunk
            {
                ChunkId = chunkId,
                ArticleUrl = article.Url,
                Title = metadata.Title,
                Publisher = metadata.Publisher,
                PublishedDate = metadata.PublishedDate,
                Source = metadata.Source,
                ChunkIndex = chunkIndex,
                Content = content,
                ContentLength = content.Length
            };
        }

        /// <summary>
        /// Generate a unique chunk ID from article URL and chunk index
        /// </summary>
        /// <param name="articleUrl">Article URL</param>
        /// <param name="chunkIndex">Chunk index</param>
        /// <returns>Unique chunk ID</returns>
        private string GenerateChunkId(string articleUrl, int chunkIndex)
        {
            // Create a deterministic UUID from URL and index using MD5 hash
            var input = $"{articleUrl}|{chunkIndex}";
            using var md5 = System.Security.Cryptography.MD5.Create();
            var hashBytes = md5.ComputeHash(Encoding.UTF8.GetBytes(input));
            
            // Convert to UUID format (8-4-4-4-12)
            var guid = new Guid(hashBytes);
            return guid.ToString();
        }
    }

    /// <summary>
    /// Represents a semantic chunk of article content with metadata
    /// </summary>
    public class ArticleChunk
    {
        public string ChunkId { get; set; } = string.Empty;
        public string ArticleUrl { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Publisher { get; set; } = string.Empty;
        public DateTime PublishedDate { get; set; }
        public string Source { get; set; } = string.Empty;
        public int ChunkIndex { get; set; }
        public string Content { get; set; } = string.Empty;
        public int ContentLength { get; set; }
    }
}
