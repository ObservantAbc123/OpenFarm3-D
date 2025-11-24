using DatabaseAccess.Models;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace LlmAgentService;

public class RagService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly LlmClient _llmClient;
    private readonly ILogger<RagService> _logger;

    public RagService(IServiceProvider serviceProvider, LlmClient llmClient, ILogger<RagService> logger)
    {
        _serviceProvider = serviceProvider;
        _llmClient = llmClient;
        _logger = logger;
    }

    public async Task<string> GetRelevantContextAsync(string query, CancellationToken cancellationToken)
    {
        var embedding = await _llmClient.GenerateEmbeddingAsync(query, cancellationToken);
        if (embedding == null)
        {
            return "";
        }

        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OpenFarmContext>();

        var vector = new Vector(embedding);
        
        // Find top 3 most similar documents
        var documents = await context.RagDocuments
            .OrderBy(x => x.Embedding!.L2Distance(vector))
            .Take(3)
            .Select(x => x.Content)
            .ToListAsync(cancellationToken);

        return string.Join("\n\n", documents);
    }
}
