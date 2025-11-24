using System.Text.Json;
using DatabaseAccess.Models;
using Microsoft.EntityFrameworkCore;
using Pgvector;

namespace LlmAgentService;

public class KnowledgeSeeder
{
    private readonly IServiceProvider _serviceProvider;
    private readonly LlmClient _llmClient;
    private readonly DynamicKnowledgeGenerator _dynamicKnowledgeGenerator;
    private readonly ILogger<KnowledgeSeeder> _logger;
    private const string KnowledgeFilePath = "/app/knowledge/knowledge.json";

    public KnowledgeSeeder(
        IServiceProvider serviceProvider, 
        LlmClient llmClient, 
        DynamicKnowledgeGenerator dynamicKnowledgeGenerator,
        ILogger<KnowledgeSeeder> logger)
    {
        _serviceProvider = serviceProvider;
        _llmClient = llmClient;
        _dynamicKnowledgeGenerator = dynamicKnowledgeGenerator;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(KnowledgeFilePath))
        {
            _logger.LogInformation("No knowledge file found at {Path}. Skipping seeding.", KnowledgeFilePath);
            return;
        }

        try
        {
            _logger.LogInformation("Reading knowledge file from {Path}", KnowledgeFilePath);
            var json = await File.ReadAllTextAsync(KnowledgeFilePath, cancellationToken);
            _logger.LogInformation("Knowledge file content length: {Length}", json.Length);
            
            var items = JsonSerializer.Deserialize<List<KnowledgeItem>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            _logger.LogInformation("Deserialized {Count} items.", items?.Count ?? 0);

            if (items == null || !items.Any())
            {
                _logger.LogInformation("Knowledge file is empty.");
                return;
            }

            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<OpenFarmContext>();

            _logger.LogInformation("Ensuring LLM model is loaded...");
            await _llmClient.EnsureModelLoadedAsync(cancellationToken);
            _logger.LogInformation("LLM model loaded.");

            // Detect and validate embedding dimensions
            var detectedDimensions = await _llmClient.GetEmbeddingDimensionsAsync(cancellationToken);
            if (detectedDimensions == null)
            {
                _logger.LogError("Failed to detect embedding dimensions. Cannot proceed with seeding.");
                return;
            }

            const int EXPECTED_DIMENSIONS = 4096; // Should match RagDocument.cs TypeName
            if (detectedDimensions.Value != EXPECTED_DIMENSIONS)
            {
                _logger.LogError(
                    "CRITICAL: Model embedding dimensions ({Detected}) do not match database schema ({Expected}). " +
                    "Please update RagDocument.cs TypeName attribute to vector({Detected}) and rebuild the database with 'docker-compose down -v && docker-compose up -d --build'.",
                    detectedDimensions.Value,
                    EXPECTED_DIMENSIONS,
                    detectedDimensions.Value
                );
                return;
            }

            _logger.LogInformation("Embedding dimensions validated: {Dimensions}", detectedDimensions.Value);

            // Generate dynamic knowledge from database
            var dynamicKnowledge = await _dynamicKnowledgeGenerator.GenerateKnowledgeEntriesAsync(cancellationToken);
            _logger.LogInformation("Generated {Count} dynamic knowledge entries from database.", dynamicKnowledge.Count);

            // Combine static knowledge (from JSON) with dynamic knowledge (from database)
            var allKnowledgeContent = items.Select(i => i.Content).Concat(dynamicKnowledge).ToList();
            _logger.LogInformation("Total knowledge items to process: {Count} ({Static} static + {Dynamic} dynamic)", 
                allKnowledgeContent.Count, items.Count, dynamicKnowledge.Count);

            foreach (var content in allKnowledgeContent)
            {
                if (string.IsNullOrWhiteSpace(content)) continue;

                // Check if content already exists
                var exists = await context.RagDocuments.AnyAsync(d => d.Content == content, cancellationToken);
                if (exists)
                {
                    _logger.LogInformation("Content already exists: {Content}...", content[..Math.Min(content.Length, 30)]);
                    continue;
                }

                _logger.LogInformation("Seeding new knowledge item: {Content}...", content[..Math.Min(content.Length, 30)]);

                var embedding = await _llmClient.GenerateEmbeddingAsync(content, cancellationToken);
                if (embedding != null)
                {
                    var doc = new RagDocument
                    {
                        Content = content,
                        Embedding = new Vector(embedding),
                        CreatedAt = DateTime.UtcNow
                    };
                    context.RagDocuments.Add(doc);
                    _logger.LogInformation("Added document to context.");
                }
                else
                {
                    _logger.LogError("Failed to generate embedding for: {Content}", content);
                }
            }

            await context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Knowledge seeding completed.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error seeding knowledge.");
        }
    }

    private class KnowledgeItem
    {
        public string Content { get; set; } = "";
    }
}
