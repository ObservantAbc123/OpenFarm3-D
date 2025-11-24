using DatabaseAccess.Models;
using Microsoft.EntityFrameworkCore;

namespace LlmAgentService;

/// <summary>
/// Generates dynamic knowledge entries from the database for RAG.
/// This ensures the AI always has up-to-date information about available materials, colors, etc.
/// </summary>
public class DynamicKnowledgeGenerator
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DynamicKnowledgeGenerator> _logger;

    public DynamicKnowledgeGenerator(IServiceProvider serviceProvider, ILogger<DynamicKnowledgeGenerator> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task<List<string>> GenerateKnowledgeEntriesAsync(CancellationToken cancellationToken)
    {
        var knowledge = new List<string>();

        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OpenFarmContext>();

        try
        {
            // Generate material types knowledge
            var materialTypes = await context.MaterialTypes
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            if (materialTypes.Any())
            {
                var typeNames = string.Join(", ", materialTypes.Select(m => m.Type));
                knowledge.Add($"OpenFarm offers the following 3D printing material types: {typeNames}.");
                _logger.LogInformation("Generated knowledge for {Count} material types.", materialTypes.Count);
            }

            // Generate colors knowledge
            var colors = await context.Colors
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            if (colors.Any())
            {
                var colorNames = string.Join(", ", colors.Select(c => c.Name));
                knowledge.Add($"OpenFarm offers the following filament colors: {colorNames}.");
                _logger.LogInformation("Generated knowledge for {Count} colors.", colors.Count);
            }

            // Generate in-stock materials knowledge
            var inStockMaterials = await context.Materials
                .AsNoTracking()
                .Include(m => m.MaterialType)
                .Include(m => m.MaterialColor)
                .Where(m => m.InStock)
                .ToListAsync(cancellationToken);

            if (inStockMaterials.Any())
            {
                var stockInfo = inStockMaterials
                    .GroupBy(m => m.MaterialType.Type)
                    .Select(g => $"{g.Key} ({string.Join(", ", g.Select(m => m.MaterialColor.Name))})");
                
                knowledge.Add($"Currently in stock: {string.Join("; ", stockInfo)}.");
                _logger.LogInformation("Generated knowledge for {Count} in-stock materials.", inStockMaterials.Count);
            }

            _logger.LogInformation("Generated {Count} dynamic knowledge entries from database.", knowledge.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating dynamic knowledge from database.");
        }

        return knowledge;
    }
}
