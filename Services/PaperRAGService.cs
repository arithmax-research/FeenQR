using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Embeddings;
using Microsoft.SemanticKernel.Memory;
using System.Text;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;

namespace QuantResearchAgent.Services;

/// <summary>
/// RAG-based service for comprehensive academic paper analysis
/// Uses vector embeddings and semantic search to analyze entire papers
/// </summary>
public class PaperRAGService
{
    private readonly ILogger<PaperRAGService> _logger;
    private readonly Kernel _kernel;
    private readonly ISemanticTextMemory _memory;
    private readonly ITextEmbeddingGenerationService _embeddingService;
    private readonly HttpClient _httpClient;
    private const int CHUNK_SIZE = 1000; // tokens per chunk
    private const int CHUNK_OVERLAP = 200; // overlap between chunks

    public PaperRAGService(
        ILogger<PaperRAGService> logger,
        Kernel kernel,
        ISemanticTextMemory memory,
        ITextEmbeddingGenerationService embeddingService,
        HttpClient httpClient)
    {
        _logger = logger;
        _kernel = kernel;
        _memory = memory;
        _embeddingService = embeddingService;
        _httpClient = httpClient;
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
            Console.WriteLine($"      [RAG] Created {chunks.Count} chunks (1000 tokens each, 200 overlap)");
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
    /// Store chunks in vector memory with embeddings
    /// </summary>
    private async Task StoreChunksInMemoryAsync(List<PaperChunk> chunks, string collectionName, string paperUrl)
    {
        Console.WriteLine($"      [RAG] Storing {chunks.Count} chunks in collection: {collectionName}");
        
        foreach (var chunk in chunks)
        {
            var id = $"chunk_{chunk.Index}";
            Console.WriteLine($"      [RAG] Storing chunk {chunk.Index}: {chunk.Text.Length} chars, {chunk.TokenCount} tokens");
            Console.WriteLine($"      [RAG] Chunk preview: {chunk.Text.Substring(0, Math.Min(100, chunk.Text.Length))}...");
            
            try
            {
                await _memory.SaveInformationAsync(
                    collection: collectionName,
                    text: chunk.Text,
                    id: id,
                    description: $"Chunk {chunk.Index} from {paperUrl}",
                    additionalMetadata: $"url={paperUrl};index={chunk.Index};tokens={chunk.TokenCount}"
                );
                Console.WriteLine($"      [RAG] Successfully stored chunk {chunk.Index}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"      [RAG] ERROR storing chunk {chunk.Index}: {ex.Message}");
                _logger.LogError(ex, "Failed to store chunk {Index}", chunk.Index);
            }
        }

        _logger.LogInformation("Stored {Count} chunks in memory collection: {Collection}", 
            chunks.Count, collectionName);
        Console.WriteLine($"      [RAG] Storage complete. Collection: {collectionName}");
        
        // Verify storage by attempting to retrieve
        Console.WriteLine($"      [RAG] Verifying storage...");
        try
        {
            var testResults = await _memory.SearchAsync(collectionName, "test", limit: 10, minRelevanceScore: 0.0).ToListAsync();
            Console.WriteLine($"      [RAG] Verification: Found {testResults.Count} items in collection");
            if (testResults.Count == 0)
            {
                Console.WriteLine($"      [RAG] WARNING: No items found in verification search!");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"      [RAG] Verification failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Retrieve most relevant chunks using semantic search
    /// </summary>
    private async Task<List<string>> RetrieveRelevantChunksAsync(
        string collectionName, 
        string query, 
        int maxChunks = 10)
    {
        Console.WriteLine($"      [RAG] Searching collection: {collectionName}");
        Console.WriteLine($"      [RAG] Query: {query}");
        
        var relevantChunks = new List<string>();
        
        // Remove minRelevanceScore to see ALL results
        await foreach (var result in _memory.SearchAsync(
            collection: collectionName,
            query: query,
            limit: maxChunks,
            minRelevanceScore: 0.0))
        {
            Console.WriteLine($"      [RAG] Found chunk with score: {result.Relevance:F4} - Preview: {result.Metadata.Text.Substring(0, Math.Min(80, result.Metadata.Text.Length))}...");
            relevantChunks.Add(result.Metadata.Text);
            _logger.LogInformation("Chunk relevance score: {Score}", result.Relevance);
        }

        Console.WriteLine($"      [RAG] Total chunks found: {relevantChunks.Count}");
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
        return Regex.Split(text, @"(?<=[.!?])\s+")
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();
    }

    private int EstimateTokenCount(string text)
    {
        // Rough estimation: ~4 characters per token
        return text.Length / 4;
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
    /// Answer a question about the loaded paper
    /// </summary>
    public async Task<string> AskQuestionAsync(string collectionName, string question)
    {
        try
        {
            Console.WriteLine($"\n[RAG-CHAT] Searching for: \"{question}\"");
            
            // Retrieve relevant chunks (as strings)
            var relevantTexts = await RetrieveRelevantChunksAsync(collectionName, question, maxChunks: 10);
            Console.WriteLine($"[RAG-CHAT] Found {relevantTexts.Count} relevant chunks");
            
            if (!relevantTexts.Any())
            {
                return "No relevant information found in the paper for your question.";
            }

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
7. Structure your answer with clear sections if the question is complex
8. If information is partially available, explain what IS covered and what ISN'T
9. Use technical language appropriate for a quantitative researcher
10. If the excerpts don't fully answer the question, state what's missing

Provide a comprehensive, insightful answer:";

            var response = await _kernel.InvokePromptAsync(prompt);
            return response.GetValue<string>() ?? "Unable to generate answer";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to answer question");
            return $"Error: {ex.Message}";
        }
    }
}

public class PaperChunk
{
    public int Index { get; set; }
    public string Text { get; set; } = "";
    public int TokenCount { get; set; }
}
