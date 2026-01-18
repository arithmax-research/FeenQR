using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Embeddings;
using Microsoft.SemanticKernel.Memory;
using System.Text;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace QuantResearchAgent.Services;

/// <summary>
/// Paper metadata for tracking loaded papers
/// </summary>
public class LoadedPaper
{
    public string CollectionName { get; set; } = "";
    public string PaperUrl { get; set; } = "";
    public string PaperTitle { get; set; } = "";
    public DateTime LoadedAt { get; set; }
    public int ChunkCount { get; set; }
    public string FilePath { get; set; } = ""; // For local PDFs
}

/// <summary>
/// RAG-based service for comprehensive academic paper analysis
/// Uses vector embeddings and semantic search to analyze entire papers
/// Stores embeddings in Qdrant for persistence
/// </summary>
public class PaperRAGService
{
    private readonly ILogger<PaperRAGService> _logger;
    private readonly Kernel _kernel;
    private readonly ISemanticTextMemory _memory;
    private readonly ITextEmbeddingGenerationService _embeddingService;
    private readonly HttpClient _httpClient;
    private readonly QdrantClient _qdrantClient;
    private const int CHUNK_SIZE = 500; // tokens per chunk (reduced to fit in 8192 model limit)
    private const int CHUNK_OVERLAP = 100; // overlap between chunks
    private const int VECTOR_SIZE = 1536; // text-embedding-3-small dimensions
    
    // Track loaded papers
    private readonly Dictionary<string, LoadedPaper> _loadedPapers = new();

    public PaperRAGService(
        ILogger<PaperRAGService> logger,
        Kernel kernel,
        ISemanticTextMemory memory,
        ITextEmbeddingGenerationService embeddingService,
        HttpClient httpClient,
        QdrantClient qdrantClient)
    {
        _logger = logger;
        _kernel = kernel;
        _memory = memory;
        _embeddingService = embeddingService;
        _httpClient = httpClient;
        _qdrantClient = qdrantClient;
    }

    /// <summary>
    /// Analyze an entire paper using RAG - chunks, embeds, and queries
    /// </summary>
    public async Task<string> AnalyzePaperFullRAGAsync(string paperUrl, string topic, string focusArea)
    {
        try
        {
            Console.WriteLine("      [RAG] Starting full paper analysis...");
            _logger.LogInformation("Starting RAG analysis for paper: {Url}", paperUrl);
            
            // Step 1: Download and extract full paper text
            var fullText = await DownloadFullPaperAsync(paperUrl);
            if (string.IsNullOrEmpty(fullText))
            {
                return "Failed to download paper content";
            }

            Console.WriteLine($"      [RAG] Downloaded full paper: {fullText.Length:N0} characters");
            _logger.LogInformation("Downloaded paper: {Length} characters", fullText.Length);

            // Step 2: Chunk the paper into manageable pieces
            var chunks = ChunkPaper(fullText, CHUNK_SIZE, CHUNK_OVERLAP);
            Console.WriteLine($"      [RAG] Created {chunks.Count} chunks ({CHUNK_SIZE} tokens each, {CHUNK_OVERLAP} overlap)");
            _logger.LogInformation("Chunked paper into {Count} segments", chunks.Count);

            // Step 3: Store chunks in vector memory with embeddings
            var collectionName = $"paper_{Guid.NewGuid():N}";
            await StoreChunksInMemoryAsync(chunks, collectionName, paperUrl);
            Console.WriteLine($"      [RAG] Stored {chunks.Count} chunks with embeddings in memory");

            // Step 4: Perform semantic searches for relevant sections
            var relevantChunks = await RetrieveRelevantChunksAsync(
                collectionName, 
                focusArea, 
                maxChunks: 15 // Get top 15 most relevant chunks
            );
            Console.WriteLine($"      [RAG] Retrieved {relevantChunks.Count} most relevant chunks for analysis");

            // Step 5: Generate comprehensive analysis using retrieved chunks
            var analysis = await GenerateAnalysisFromChunksAsync(
                paperUrl,
                topic,
                focusArea,
                relevantChunks
            );

            return analysis;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RAG analysis failed for paper: {Url}", paperUrl);
            return $"Error analyzing paper: {ex.Message}";
        }
    }

    /// <summary>
    /// Download full paper text (supports PDF and HTML)
    /// </summary>
    private async Task<string> DownloadFullPaperAsync(string url)
    {
        try
        {
            // Handle ArXiv papers - get full PDF
            if (url.Contains("arxiv.org"))
            {
                var arxivId = ExtractArxivId(url);
                if (!string.IsNullOrEmpty(arxivId))
                {
                    // Convert to PDF URL
                    url = $"https://arxiv.org/pdf/{arxivId}.pdf";
                }
            }

            // Download and extract from PDF
            if (url.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) || url.Contains(".pdf"))
            {
                return await ExtractFullPDFTextAsync(url);
            }

            // Extract from HTML
            var response = await _httpClient.GetStringAsync(url);
            var cleanText = Regex.Replace(response, @"<[^>]*>", " ");
            cleanText = Regex.Replace(cleanText, @"\s+", " ");
            return cleanText;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download paper: {Url}", url);
            return "";
        }
    }

    /// <summary>
    /// Extract all text from a PDF (not just first few pages)
    /// </summary>
    private async Task<string> ExtractFullPDFTextAsync(string pdfUrl)
    {
        try
        {
            using var stream = await _httpClient.GetStreamAsync(pdfUrl);
            using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream);
            memoryStream.Position = 0;

            // OPTIONAL: Save PDF to disk for verification
            var downloadsDir = Path.Combine(Directory.GetCurrentDirectory(), "downloaded_papers");
            Directory.CreateDirectory(downloadsDir);
            var fileName = $"paper_{DateTime.Now:yyyyMMdd_HHmmss}_{Path.GetFileName(pdfUrl.Split('?')[0])}";
            var filePath = Path.Combine(downloadsDir, fileName);
            
            // Save a copy to disk so user can verify downloads
            await File.WriteAllBytesAsync(filePath, memoryStream.ToArray());
            Console.WriteLine($"      [RAG] Saved PDF to: {filePath}");
            
            memoryStream.Position = 0; // Reset for reading

            var text = new StringBuilder();
            using var pdf = PdfDocument.Open(memoryStream);
            
            foreach (var page in pdf.GetPages())
            {
                text.AppendLine(page.Text);
                text.AppendLine("\n--- PAGE BREAK ---\n");
            }

            return text.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract PDF text from: {Url}", pdfUrl);
            return "";
        }
    }

    /// <summary>
    /// Chunk paper into overlapping segments for better context preservation
    /// </summary>
    private List<PaperChunk> ChunkPaper(string fullText, int chunkSize, int overlap)
    {
        var chunks = new List<PaperChunk>();
        var sentences = SplitIntoSentences(fullText);
        
        var currentChunk = new StringBuilder();
        var currentTokenCount = 0;
        var chunkIndex = 0;

        foreach (var sentence in sentences)
        {
            var sentenceTokens = EstimateTokenCount(sentence);
            
            if (currentTokenCount + sentenceTokens > chunkSize && currentTokenCount > 0)
            {
                // Save current chunk
                chunks.Add(new PaperChunk
                {
                    Index = chunkIndex++,
                    Text = currentChunk.ToString().Trim(),
                    TokenCount = currentTokenCount
                });

                // Start new chunk with overlap
                var overlapText = GetLastNTokens(currentChunk.ToString(), overlap);
                currentChunk.Clear();
                currentChunk.Append(overlapText);
                currentTokenCount = EstimateTokenCount(overlapText);
            }

            currentChunk.Append(sentence).Append(" ");
            currentTokenCount += sentenceTokens;
        }

        // Add final chunk
        if (currentTokenCount > 0)
        {
            chunks.Add(new PaperChunk
            {
                Index = chunkIndex,
                Text = currentChunk.ToString().Trim(),
                TokenCount = currentTokenCount
            });
        }

        return chunks;
    }

    /// <summary>
    /// Store chunks in Qdrant with embeddings for persistence
    /// </summary>
    private async Task StoreChunksInMemoryAsync(List<PaperChunk> chunks, string collectionName, string paperUrl)
    {
        Console.WriteLine($"      [RAG] Storing {chunks.Count} chunks in Qdrant collection: {collectionName}");
        
        // Ensure collection exists
        await EnsureCollectionExistsAsync(collectionName);
        
        var points = new List<PointStruct>();
        
        foreach (var chunk in chunks)
        {
            Console.WriteLine($"      [RAG] Processing chunk {chunk.Index}: {chunk.Text.Length} chars, {chunk.TokenCount} tokens");
            Console.WriteLine($"      [RAG] Chunk preview: {chunk.Text.Substring(0, Math.Min(100, chunk.Text.Length))}...");
            
            try
            {
                // Generate embedding
                var embedding = await _embeddingService.GenerateEmbeddingAsync(chunk.Text);
                var vector = embedding.ToArray();
                
                // Create point with metadata
                var point = new PointStruct
                {
                    Id = new PointId { Num = (ulong)chunk.Index + (ulong)(DateTime.UtcNow.Ticks / 10000000) },
                    Vectors = vector,
                    Payload =
                    {
                        ["text"] = chunk.Text,
                        ["url"] = paperUrl,
                        ["index"] = chunk.Index,
                        ["tokens"] = chunk.TokenCount
                    }
                };
                points.Add(point);
                Console.WriteLine($"      [RAG] Prepared chunk {chunk.Index} for storage");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"      [RAG] ERROR preparing chunk {chunk.Index}: {ex.Message}");
                _logger.LogError(ex, "Failed to prepare chunk {Index}", chunk.Index);
            }
        }
        
        // Batch upsert all points
        if (points.Any())
        {
            try
            {
                await _qdrantClient.UpsertAsync(collectionName, points);
                Console.WriteLine($"      [RAG] Successfully stored {points.Count} chunks in Qdrant");
                _logger.LogInformation("Stored {Count} chunks in Qdrant collection: {Collection}", 
                    points.Count, collectionName);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"      [RAG] ERROR upserting to Qdrant: {ex.Message}");
                _logger.LogError(ex, "Failed to upsert chunks to Qdrant");
            }
        }
        
        // Verify storage
        Console.WriteLine($"      [RAG] Verifying storage in Qdrant...");
        try
        {
            var collectionInfo = await _qdrantClient.GetCollectionInfoAsync(collectionName);
            Console.WriteLine($"      [RAG] Verification: Collection has {collectionInfo.PointsCount} points PERSISTED");
            if (collectionInfo.PointsCount == 0)
            {
                Console.WriteLine($"      [RAG] WARNING: No points found in collection!");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"      [RAG] Verification failed: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Ensure Qdrant collection exists with proper configuration
    /// </summary>
    private async Task EnsureCollectionExistsAsync(string collectionName)
    {
        try
        {
            var collections = await _qdrantClient.ListCollectionsAsync();
            if (!collections.Contains(collectionName))
            {
                Console.WriteLine($"      [RAG] Creating new Qdrant collection: {collectionName}");
                await _qdrantClient.CreateCollectionAsync(
                    collectionName,
                    new VectorParams { Size = VECTOR_SIZE, Distance = Distance.Cosine }
                );
                Console.WriteLine($"      [RAG] Collection created successfully with {VECTOR_SIZE} dimensions");
            }
            else
            {
                Console.WriteLine($"      [RAG] Collection {collectionName} already exists (PERSISTENT)");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ensure collection exists");
            throw;
        }
    }

    /// <summary>
    /// Retrieve most relevant chunks using semantic search from Qdrant
    /// </summary>
    private async Task<List<string>> RetrieveRelevantChunksAsync(
        string collectionName, 
        string query, 
        int maxChunks = 10)
    {
        var relevantChunks = new List<string>();
        
        try
        {
            // Generate query embedding
            var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(query);
            var queryVector = queryEmbedding.ToArray();
            
            // Search Qdrant directly
            var searchResult = await _qdrantClient.SearchAsync(
                collectionName: collectionName,
                vector: queryVector,
                limit: (ulong)maxChunks,
                scoreThreshold: 0.0f // Accept all results for now
            );
            
            foreach (var point in searchResult)
            {
                if (point.Payload.TryGetValue("text", out var textValue))
                {
                    var text = textValue.StringValue;
                    relevantChunks.Add(text);
                    _logger.LogInformation("Chunk relevance score: {Score}", point.Score);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search Qdrant collection");
        }

        _logger.LogInformation("Retrieved {Count} relevant chunks for query: {Query}", 
            relevantChunks.Count, query);

        return relevantChunks;
    }

    /// <summary>
    /// Generate comprehensive analysis from retrieved chunks
    /// </summary>
    private async Task<string> GenerateAnalysisFromChunksAsync(
        string paperUrl,
        string topic,
        string focusArea,
        List<string> relevantChunks)
    {
        var context = string.Join("\n\n--- SECTION ---\n\n", relevantChunks);

        var prompt = $@"
You are analyzing an academic paper about '{topic}' with focus on '{focusArea}'.

Below are the most relevant sections extracted from the full paper using semantic search:

{context}

Based on these sections, provide a comprehensive analysis including:

1. RESEARCH OBJECTIVE AND CONTRIBUTION
   - What novel contribution does this research make?
   - Why is it important?

2. METHODOLOGY IN DETAIL
   - Algorithms and mathematical formulations
   - Data sources and preprocessing
   - Feature engineering approaches
   - Model architecture and design choices

3. EXPERIMENTAL SETUP
   - Datasets used
   - Evaluation metrics
   - Baseline comparisons
   - Statistical validation methods

4. KEY RESULTS AND FINDINGS
   - Performance metrics with specific numbers
   - Comparison with baselines
   - Ablation studies and insights
   - Statistical significance

5. IMPLEMENTATION DETAILS FOR PRACTITIONERS
   - Specific parameters and hyperparameters
   - Computational requirements
   - Code/algorithm pseudocode if mentioned
   - Libraries and tools referenced

6. TRADING APPLICATIONS
   - How to apply in live trading
   - Market conditions and timeframes
   - Expected performance in practice
   - Risk considerations

7. LIMITATIONS AND FUTURE WORK
   - Acknowledged limitations
   - Edge cases
   - Suggested improvements
   - Open research questions

8. ACTIONABLE TAKEAWAYS
   - Top 5 insights for implementation
   - Recommended next steps
   - Resource requirements

Provide a detailed, technical analysis (1500-2000 words).
Format output as PLAIN TEXT ONLY - no markdown, no asterisks, no hashtags, no code blocks.

Paper URL: {paperUrl}
";

        var response = await _kernel.InvokePromptAsync(prompt);
        return response.GetValue<string>() ?? "Unable to generate analysis";
    }

    // Helper methods
    private List<string> SplitIntoSentences(string text)
    {
        // Split on multiple delimiters for better chunking of academic papers
        // Split on: sentence endings, new paragraphs, section breaks
        var sentences = new List<string>();
        
        // First split on double newlines (paragraphs)
        var paragraphs = text.Split(new[] { "\n\n", "\r\n\r\n" }, StringSplitOptions.RemoveEmptyEntries);
        
        foreach (var paragraph in paragraphs)
        {
            // Then split each paragraph into sentences
            var paragraphSentences = Regex.Split(paragraph, @"(?<=[.!?])\s+(?=[A-Z])")
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();
            
            sentences.AddRange(paragraphSentences);
        }
        
        // If no sentences found (malformed text), split by character count
        if (sentences.Count == 0 || sentences.All(s => s.Length > 5000))
        {
            sentences = SplitByCharacterCount(text, 2000);
        }
        
        return sentences;
    }
    
    private List<string> SplitByCharacterCount(string text, int maxChars)
    {
        var chunks = new List<string>();
        for (int i = 0; i < text.Length; i += maxChars)
        {
            var chunkSize = Math.Min(maxChars, text.Length - i);
            chunks.Add(text.Substring(i, chunkSize));
        }
        return chunks;
    }

    private int EstimateTokenCount(string text)
    {
        // More conservative estimation: ~3.5 characters per token for academic text
        // Academic papers have more specialized vocabulary = more tokens
        return (int)Math.Ceiling(text.Length / 3.5);
    }

    private string GetLastNTokens(string text, int tokenCount)
    {
        var estimatedChars = tokenCount * 4;
        if (text.Length <= estimatedChars)
            return text;
        
        return text.Substring(text.Length - estimatedChars);
    }

    private string ExtractArxivId(string url)
    {
        var match = Regex.Match(url, @"arxiv\.org/(?:abs/|pdf/)?(\d+\.\d+)");
        return match.Success ? match.Groups[1].Value : "";
    }

    /// <summary>
    /// Interactive RAG chat - load paper and answer questions about it
    /// Returns the collection name for continued queries
    /// </summary>
    public async Task<string> LoadPaperForChatAsync(string paperUrl)
    {
        try
        {
            Console.WriteLine($"\n[RAG-CHAT] Loading paper: {paperUrl}");
            Console.WriteLine("[RAG-CHAT] Using database: VolatileMemoryStore (in-memory)");
            
            // Step 1: Download full paper
            var fullText = await DownloadFullPaperAsync(paperUrl);
            if (string.IsNullOrEmpty(fullText))
            {
                Console.WriteLine("[RAG-CHAT] Failed to download paper");
                return "";
            }
            Console.WriteLine($"[RAG-CHAT] Downloaded: {fullText.Length:N0} characters");

            // Step 2: Chunk
            var chunks = ChunkPaper(fullText, CHUNK_SIZE, CHUNK_OVERLAP);
            Console.WriteLine($"[RAG-CHAT] Created {chunks.Count} chunks");

            // Step 3: Store with embeddings
            var collectionName = $"chat_{Guid.NewGuid():N}";
            await StoreChunksInMemoryAsync(chunks, collectionName, paperUrl);
            
            // Track loaded paper
            var title = ExtractPaperTitle(fullText, paperUrl);
            _loadedPapers[collectionName] = new LoadedPaper
            {
                CollectionName = collectionName,
                PaperUrl = paperUrl,
                PaperTitle = title,
                LoadedAt = DateTime.UtcNow,
                ChunkCount = chunks.Count
            };
            
            Console.WriteLine($"[RAG-CHAT] Stored in memory (collection: {collectionName[..8]}...)");
            Console.WriteLine($"[RAG-CHAT] Paper loaded! Ask me anything about it.\n");

            return collectionName;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load paper for chat");
            Console.WriteLine($"[RAG-CHAT] Error: {ex.Message}");
            return "";
        }
    }

    /// <summary>
    /// Load paper from local PDF file
    /// </summary>
    public async Task<string> LoadPaperFromFileAsync(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"[RAG-CHAT] File not found: {filePath}");
                return "";
            }

            Console.WriteLine($"\n[RAG-CHAT] Loading local paper: {Path.GetFileName(filePath)}");
            Console.WriteLine("[RAG-CHAT] Using database: Qdrant (persistent vector storage)");
            
            // Extract text from local PDF
            var fullText = await Task.Run(() => ExtractTextFromLocalPDF(filePath));
            if (string.IsNullOrEmpty(fullText))
            {
                Console.WriteLine("[RAG-CHAT] Failed to extract text from PDF");
                return "";
            }
            Console.WriteLine($"[RAG-CHAT] Extracted: {fullText.Length:N0} characters");

            // Step 2: Chunk
            var chunks = ChunkPaper(fullText, CHUNK_SIZE, CHUNK_OVERLAP);
            Console.WriteLine($"[RAG-CHAT] Created {chunks.Count} chunks");

            // Step 3: Store with embeddings
            var collectionName = $"chat_{Guid.NewGuid():N}";
            await StoreChunksInMemoryAsync(chunks, collectionName, filePath);
            
            // Track loaded paper
            var title = ExtractPaperTitle(fullText, Path.GetFileName(filePath));
            _loadedPapers[collectionName] = new LoadedPaper
            {
                CollectionName = collectionName,
                PaperUrl = filePath,
                PaperTitle = title,
                LoadedAt = DateTime.UtcNow,
                ChunkCount = chunks.Count,
                FilePath = filePath
            };
            
            Console.WriteLine($"[RAG-CHAT] Stored in memory (collection: {collectionName[..8]}...)");
            Console.WriteLine($"[RAG-CHAT] Paper loaded! Ask me anything about it.\n");

            return collectionName;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load paper from file");
            Console.WriteLine($"[RAG-CHAT] Error: {ex.Message}");
            return "";
        }
    }

    /// <summary>
    /// Extract text from local PDF file
    /// </summary>
    private string ExtractTextFromLocalPDF(string filePath)
    {
        try
        {
            var text = new StringBuilder();
            using var pdf = PdfDocument.Open(filePath);
            
            foreach (var page in pdf.GetPages())
            {
                text.AppendLine(page.Text);
                text.AppendLine("\n--- PAGE BREAK ---\n");
            }

            return text.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract text from local PDF: {FilePath}", filePath);
            return "";
        }
    }

    /// <summary>
    /// Extract paper title from content or filename
    /// </summary>
    private string ExtractPaperTitle(string fullText, string fallback)
    {
        // Try to extract first meaningful line as title
        var lines = fullText.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines.Take(10))
        {
            var trimmed = line.Trim();
            if (trimmed.Length > 10 && trimmed.Length < 200 && !trimmed.StartsWith("arXiv"))
            {
                return trimmed;
            }
        }
        return fallback;
    }

    /// <summary>
    /// Get all loaded papers
    /// </summary>
    public List<LoadedPaper> GetLoadedPapers()
    {
        return _loadedPapers.Values.OrderByDescending(p => p.LoadedAt).ToList();
    }

    /// <summary>
    /// Get paper by collection name
    /// </summary>
    public LoadedPaper? GetPaper(string collectionName)
    {
        return _loadedPapers.TryGetValue(collectionName, out var paper) ? paper : null;
    }

    /// <summary>
    /// Clear all collections from Qdrant (for starting fresh research)
    /// </summary>
    public async Task ClearAllPapersAsync()
    {
        try
        {
            Console.WriteLine("[RAG] Clearing all paper collections from Qdrant...");
            
            var collections = await _qdrantClient.ListCollectionsAsync();
            var paperCollections = collections.Where(c => c.StartsWith("paper_") || c.StartsWith("chat_")).ToList();
            
            Console.WriteLine($"[RAG] Found {paperCollections.Count} paper/chat collections to clear");
            
            foreach (var collection in paperCollections)
            {
                try
                {
                    await _qdrantClient.DeleteCollectionAsync(collection);
                    Console.WriteLine($"[RAG] Cleared collection: {collection}");
                    _logger.LogInformation("Cleared paper collection: {Collection}", collection);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[RAG] Error clearing collection {collection}: {ex.Message}");
                    _logger.LogError(ex, "Failed to clear collection {Collection}", collection);
                }
            }
            
            Console.WriteLine($"[RAG] Successfully cleared {paperCollections.Count} paper/chat collections");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[RAG] Error clearing papers: {ex.Message}");
            _logger.LogError(ex, "Failed to clear all papers");
            throw;
        }
    }
    
    /// <summary>
    /// Clear specific collection from Qdrant
    /// </summary>
    public async Task ClearPaperAsync(string collectionName)
    {
        try
        {
            Console.WriteLine($"[RAG] Clearing specific collection: {collectionName}");
            
            var collections = await _qdrantClient.ListCollectionsAsync();
            if (collections.Contains(collectionName))
            {
                await _qdrantClient.DeleteCollectionAsync(collectionName);
                Console.WriteLine($"[RAG] Successfully cleared collection: {collectionName}");
                _logger.LogInformation("Cleared specific paper collection: {Collection}", collectionName);
            }
            else
            {
                Console.WriteLine($"[RAG] Collection {collectionName} not found");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[RAG] Error clearing collection {collectionName}: {ex.Message}");
            _logger.LogError(ex, "Failed to clear collection {Collection}", collectionName);
            throw;
        }
    }

    /// <summary>
    /// Answer a question about the loaded paper
    /// </summary>
    public async Task<string> AskQuestionAsync(string collectionName, string question)
    {
        try
        {
            Console.Write($"[dim cyan]Searching paper...[/] ");
            
            // Retrieve relevant chunks (as strings)
            var relevantTexts = await RetrieveRelevantChunksAsync(collectionName, question, maxChunks: 10);
            Console.WriteLine($"[green]✓[/] Found {relevantTexts.Count} relevant sections");
            
            if (!relevantTexts.Any())
            {
                return "❌ No relevant information found in the paper for your question.";
            }

            Console.Write($"[dim cyan]Analyzing and generating answer...[/] ");

            // Generate answer with improved prompting
            var context = string.Join("\n\n--- NEXT SECTION ---\n\n", relevantTexts);
            var prompt = $@"You are an expert research analyst helping a quantitative researcher understand an academic paper in depth.

CONTEXT FROM PAPER:
{context}

USER QUESTION: {question}

INSTRUCTIONS:
1. Provide a thorough, well-reasoned answer based on the paper content above
2. Include specific details, metrics, equations, or methodologies mentioned in the paper
3. If the paper discusses implementation, explain the technical approach clearly
4. For methodological questions, describe the techniques and their rationale
5. For results questions, cite specific numbers, comparisons, or performance metrics
6. If applying concepts to trading/finance, explain practical implications
7. Structure your answer with clear sections and use markdown formatting (headers, lists, bold/italic)
8. If information is partially available, explain what IS covered and what ISN'T
9. Use technical language appropriate for a quantitative researcher
10. If the excerpts don't fully answer the question, state what's missing

Provide a comprehensive, well-formatted answer:";

            var response = await _kernel.InvokePromptAsync(prompt);
            Console.WriteLine($"[green]✓[/] Complete");
            return response.GetValue<string>() ?? "Unable to generate answer";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to answer question");
            return $"❌ Error: {ex.Message}";
        }
    }
}

public class PaperChunk
{
    public int Index { get; set; }
    public string Text { get; set; } = "";
    public int TokenCount { get; set; }
}
