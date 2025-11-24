using DatabaseAccess.Models;
using Microsoft.EntityFrameworkCore;

namespace DatabaseAccess.Helpers;

/// <summary>
///     Provides helper methods for accessing and modifying Material entities in the database.
/// </summary>
/// <remarks>
///     Initializes a new instance of the <see cref="MaterialHelper" /> class.
/// </remarks>
/// <param name="context">The database context to use for operations.</param>
public class MaterialHelper(OpenFarmContext context) : BaseHelper(context)
{
    private IQueryable<Material> Materials => _context.Materials.AsNoTracking();

    private IQueryable<MaterialPricePeriod> ActiveMaterialPricePeriods => _context.MaterialPricePeriods
        .AsNoTracking()
        .Where(period => period.EndedAt == null);

    private IQueryable<Color> Colors => _context.Colors.AsNoTracking();

    private IQueryable<MaterialType> MaterialTypes => _context.MaterialTypes.AsNoTracking();

    /// <summary>
    ///     Asynchronously retrieves all <see cref="Material" /> entities from the database.
    /// </summary>
    /// <returns>
    ///     A task that resolves to a list of all materials.
    /// </returns>
    public async Task<List<Material>> GetMaterialsAsync()
    {
        return await _context.Materials
            .Include(m => m.MaterialType)
            .Include(m => m.MaterialColor)
            .AsNoTracking()
            .ToListAsync();
    }


    /// <summary>
    ///     Asynchronously retrieves a <see cref="Material" /> entity with the given identifier.
    /// </summary>
    /// <param name="materialId">The identifier of the material to look up.</param>
    /// <returns>
    ///     A task that resolves to the material, or <c>null</c> if not found.
    /// </returns>
    public async Task<Material?> GetMaterialAsync(int materialId)
    {
        return await Materials.FirstOrDefaultAsync(material => material.Id == materialId);
    }

    /// <summary>
    ///     Asynchronously retrieves all <see cref="Material" /> entities with the specified color identifier.
    /// </summary>
    /// <param name="colorId">The identifier of the color to filter by.</param>
    /// <returns>
    ///     A task that resolves to a list of materials with the specified color.
    /// </returns>
    public async Task<List<Material>> GetMaterialsByColorIdAsync(int colorId)
    {
        return await Materials
            .Where(material => material.MaterialColorId == colorId)
            .ToListAsync();
    }

    /// <summary>
    ///     Asynchronously retrieves all <see cref="Material" /> entities with the specified material type identifier.
    /// </summary>
    /// <param name="typeId">The identifier of the material type to filter by.</param>
    /// <returns>
    ///     A task that resolves to a list of materials with the specified type.
    /// </returns>
    public async Task<List<Material>> GetMaterialsByTypeIdAsync(int typeId)
    {
        return await Materials
            .Where(material => material.MaterialTypeId == typeId)
            .ToListAsync();
    }

    /// <summary>
    ///     Asynchronously retrieves all <see cref="Material" /> entities with the specified color name.
    /// </summary>
    /// <param name="colorName">The name of the color to filter by.</param>
    /// <returns>
    ///     A task that resolves to a list of materials with the specified color name.
    /// </returns>
    public async Task<List<Material>> GetMaterialsByColorNameAsync(string colorName)
    {
        return await Materials
            .Include(material => material.MaterialColor)
            .Where(material => material.MaterialColor.Name == colorName)
            .ToListAsync();
    }

    /// <summary>
    ///     Asynchronously retrieves all <see cref="Material" /> entities with the specified material type name.
    /// </summary>
    /// <param name="type">The name of the material type to filter by.</param>
    /// <returns>
    ///     A task that resolves to a list of materials with the specified type name.
    /// </returns>
    public async Task<List<Material>> GetMaterialsByTypeNameAsync(string type)
    {
        return await Materials
            .Include(material => material.MaterialType)
            .Where(material => material.MaterialType.Type == type)
            .ToListAsync();
    }

    /// <summary>
    ///     Asynchronously retrieves the <see cref="Color" /> associated with the specified material identifier.
    /// </summary>
    /// <param name="materialId">The identifier of the material to look up.</param>
    /// <returns>
    ///     A task that resolves to the color of the material, or <c>null</c> if not found.
    /// </returns>
    public async Task<Color?> GetMaterialsColorAsync(int materialId)
    {
        return await Materials
            .Where(material => material.Id == materialId)
            .Select(material => material.MaterialColor)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    ///     Asynchronously retrieves the <see cref="MaterialType" /> associated with the specified material identifier.
    /// </summary>
    /// <param name="materialId">The identifier of the material to look up.</param>
    /// <returns>
    ///     A task that resolves to the material type of the material, or <c>null</c> if not found.
    /// </returns>
    public async Task<MaterialType?> GetMaterialsTypeAsync(int materialId)
    {
        return await Materials
            .Where(material => material.Id == materialId)
            .Select(material => material.MaterialType)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    ///     Asynchronously retrieves the in-stock status of the specified material identifier.
    /// </summary>
    /// <param name="materialId">The identifier of the material to look up.</param>
    /// <returns>
    ///     A task that resolves to the in-stock status of the material, or <c>null</c> if not found.
    /// </returns>
    public async Task<bool?> GetMaterialInStockAsync(int materialId)
    {
        return await Materials
            .Where(material => material.Id == materialId)
            .Select(material => material.InStock)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    ///     Asynchronously retrieves the active <see cref="MaterialPricePeriod" /> for the specified material identifier.
    /// </summary>
    /// <param name="materialId">The identifier of the material to look up.</param>
    /// <returns>
    ///     A task that resolves to the active price period for the material, or <c>null</c> if not found.
    /// </returns>
    public async Task<MaterialPricePeriod?> GetActiveMaterialPricePeriodAsync(int materialId)
    {
        return await ActiveMaterialPricePeriods
            .FirstOrDefaultAsync(period => period.MaterialId == materialId);
    }

    /// <summary>
    ///     Asynchronously retrieves the current <see cref="MaterialPricePeriod" /> for the specified material identifier.
    /// </summary>
    /// <param name="materialId">The identifier of the material to look up.</param>
    /// <returns>
    ///     A task that resolves to the current price period for the material, or <c>null</c> if not found.
    /// </returns>
    public Task<MaterialPricePeriod?> GetMaterialPricePeriodAsync(int materialId)
    {
        return GetActiveMaterialPricePeriodAsync(materialId);
    }

    /// <summary>
    ///     Asynchronously retrieves the active price for the specified material identifier.
    /// </summary>
    /// <param name="materialId">The identifier of the material to look up.</param>
    /// <returns>
    ///     A task that resolves to the active price for the material, or <c>null</c> if not found.
    /// </returns>
    public async Task<decimal?> GetActivePriceAsync(int materialId)
    {
        return await ActiveMaterialPricePeriods
            .Where(period => period.MaterialId == materialId)
            .Select(period => period.Price)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    ///     Asynchronously creates a new <see cref="Material" /> that is not in stock.
    /// </summary>
    /// <param name="materialTypeId">The identifier of the material type.</param>
    /// <param name="materialColorId">The identifier of the material color.</param>
    /// <returns>
    ///     A task that resolves to a <see cref="TransactionResult" />.
    ///     Returns <see cref="TransactionResult.NoAction" /> if a material with the same type and color already exists,
    ///     <see cref="TransactionResult.Succeeded" /> if successful, or
    ///     <see cref="TransactionResult.Failed" /> if an error occurs.
    /// </returns>
    public Task<TransactionResult> CreateMaterialNotInStockAsync(int materialTypeId, int materialColorId)
    {
        return CreateMaterialAsync(materialTypeId, materialColorId, false);
    }

    /// <summary>
    ///     Asynchronously creates a new <see cref="Material" /> that is in stock.
    /// </summary>
    /// <param name="materialTypeId">The identifier of the material type.</param>
    /// <param name="materialColorId">The identifier of the material color.</param>
    /// <returns>
    ///     A task that resolves to a <see cref="TransactionResult" />.
    ///     Returns <see cref="TransactionResult.NoAction" /> if a material with the same type and color already exists,
    ///     <see cref="TransactionResult.Succeeded" /> if successful, or
    ///     <see cref="TransactionResult.Failed" /> if an error occurs.
    /// </returns>
    public Task<TransactionResult> CreateMaterialInStockAsync(int materialTypeId, int materialColorId)
    {
        return CreateMaterialAsync(materialTypeId, materialColorId, true);
    }

    /// <summary>
    ///     Creates a material with the supplied attributes when a duplicate does not already exist.
    /// </summary>
    /// <param name="materialTypeId">The identifier of the material type to associate.</param>
    /// <param name="materialColorId">The identifier of the material color to associate.</param>
    /// <param name="inStock">Indicates whether the material should be marked as in stock.</param>
    /// <returns>A <see cref="TransactionResult" /> representing the outcome of the creation.</returns>
    private async Task<TransactionResult> CreateMaterialAsync(int materialTypeId, int materialColorId, bool inStock)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            var exists = await Materials
                .AnyAsync(material =>
                    material.MaterialTypeId == materialTypeId && material.MaterialColorId == materialColorId);

            if (exists)
            {
                await transaction.RollbackAsync();
                return TransactionResult.NoAction;
            }

            await _context.Materials.AddAsync(new Material
            {
                MaterialTypeId = materialTypeId,
                MaterialColorId = materialColorId,
                InStock = inStock
            });

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return TransactionResult.Succeeded;
        }
        catch
        {
            await transaction.RollbackAsync();
            return TransactionResult.Failed;
        }
    }

    /// <summary>
    ///     Retrieves dependent print jobs and loaded materials for the specified material identifier.
    /// </summary>
    /// <param name="materialId">The identifier of the material to inspect.</param>
    /// <returns>A tuple containing print jobs and loaded materials that reference the material.</returns>
    private async Task<(List<PrintJob> PrintJobs, List<PrintersLoadedMaterial> LoadedMaterials)>
        GetMaterialDependenciesAsync(int materialId)
    {
        var printJobs = await _context.PrintJobs
            .AsNoTracking()
            .Where(job => job.MaterialId == materialId)
            .ToListAsync();

        var loadedMaterials = await _context.PrintersLoadedMaterials
            .AsNoTracking()
            .Where(loaded => loaded.MaterialId == materialId)
            .ToListAsync();

        return (printJobs, loadedMaterials);
    }

    /// <summary>
    ///     Asynchronously deletes a material with the specified identifier, cascading to delete all dependent entities.
    /// </summary>
    /// <param name="materialId">The identifier of the material to delete.</param>
    /// <returns>
    ///     A task that resolves to a <see cref="TransactionResult" />.
    ///     Returns <see cref="TransactionResult.NotFound" /> if the material doesn't exist,
    ///     <see cref="TransactionResult.Succeeded" /> if successful, or
    ///     <see cref="TransactionResult.Failed" /> if an error occurs.
    /// </returns>
    public async Task<TransactionResult> DeleteMaterialCascadingAsync(int materialId)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            var material = await _context.Materials
                .FirstOrDefaultAsync(m => m.Id == materialId);

            if (material == null)
                return TransactionResult.NotFound;

            _context.Materials.Remove(material);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return TransactionResult.Succeeded;
        }
        catch
        {
            await transaction.RollbackAsync();
            return TransactionResult.Failed;
        }
    }

    /// <summary>
    ///     Asynchronously deletes a material with the specified identifier if no dependent entities exist.
    /// </summary>
    /// <param name="materialId">The identifier of the material to delete.</param>
    /// <returns>
    ///     A task that resolves to a tuple containing a <see cref="TransactionResult" /> and lists of dependent entities.
    ///     Returns <see cref="TransactionResult.NotFound" /> if the material doesn't exist,
    ///     <see cref="TransactionResult.Abandoned" /> with dependent entities if dependencies exist,
    ///     <see cref="TransactionResult.Succeeded" /> with empty lists if successful, or
    ///     <see cref="TransactionResult.Failed" /> with empty lists if an error occurs.
    /// </returns>
    public async Task<(TransactionResult, List<PrintJob>, List<PrintersLoadedMaterial>)> DeleteMaterialRestrictedAsync(
        int materialId)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            var material = await Materials
                .FirstOrDefaultAsync(m => m.Id == materialId);

            if (material == null)
            {
                await transaction.RollbackAsync();
                return (TransactionResult.NotFound, [], []);
            }

            var (dependentPrintJobs, dependentLoadedMaterials) = await GetMaterialDependenciesAsync(materialId);

            if (dependentPrintJobs.Count > 0 || dependentLoadedMaterials.Count > 0)
            {
                await transaction.RollbackAsync();
                return (TransactionResult.Abandoned, dependentPrintJobs, dependentLoadedMaterials);
            }

            _context.Materials.Remove(material);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return (TransactionResult.Succeeded, [], []);
        }
        catch
        {
            await transaction.RollbackAsync();
            return (TransactionResult.Failed, [], []);
        }
    }

    /// <summary>
    ///     Asynchronously creates a new <see cref="Material" /> with an associated price period.
    /// </summary>
    /// <param name="materialTypeId">The identifier of the material type.</param>
    /// <param name="materialColorId">The identifier of the material color.</param>
    /// <param name="price">The price for the material.</param>
    /// <param name="inStock">Whether the material is in stock.</param>
    /// <returns>
    ///     A task that resolves to a <see cref="TransactionResult" />.
    ///     Returns <see cref="TransactionResult.NoAction" /> if a material with the same type and color already exists,
    ///     <see cref="TransactionResult.Succeeded" /> if successful, or
    ///     <see cref="TransactionResult.Failed" /> if an error occurs.
    /// </returns>
    public async Task<TransactionResult> CreateMaterialWithPricePeriodAsync(int materialTypeId, int materialColorId,
        decimal price, bool inStock)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            var existingMaterial = await _context.Materials
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.MaterialTypeId == materialTypeId && m.MaterialColorId == materialColorId);

            if (existingMaterial != null)
                return TransactionResult.NoAction;

            var newMaterial = new Material
            {
                MaterialTypeId = materialTypeId,
                MaterialColorId = materialColorId,
                InStock = inStock
            };

            await _context.Materials.AddAsync(newMaterial);
            await _context.SaveChangesAsync();

            await _context.MaterialPricePeriods.AddAsync(new MaterialPricePeriod
            {
                MaterialId = newMaterial.Id,
                Price = price,
                CreatedAt = DateTime.UtcNow,
                EndedAt = null
            });

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return TransactionResult.Succeeded;
        }
        catch
        {
            await transaction.RollbackAsync();
            return TransactionResult.Failed;
        }
    }

    /// <summary>
    ///     Asynchronously sets the in-stock status of a material to true.
    /// </summary>
    /// <param name="materialId">The identifier of the material to update.</param>
    /// <returns>
    ///     A task that resolves to a <see cref="TransactionResult" />.
    ///     Returns <see cref="TransactionResult.NotFound" /> if the material doesn't exist,
    ///     <see cref="TransactionResult.Succeeded" /> if successful, or
    ///     <see cref="TransactionResult.Failed" /> if an error occurs.
    /// </returns>
    public async Task<TransactionResult> SetMaterialInStockAsync(int materialId)
    {
        var material = await _context.Materials
            .FirstOrDefaultAsync(m => m.Id == materialId);

        if (material == null)
            return TransactionResult.NotFound;

        return await SetMaterialStockStatusAsync(material, true);
    }

    /// <summary>
    ///     Asynchronously sets the in-stock status of a material to false.
    /// </summary>
    /// <param name="materialId">The identifier of the material to update.</param>
    /// <returns>
    ///     A task that resolves to a <see cref="TransactionResult" />.
    ///     Returns <see cref="TransactionResult.NotFound" /> if the material doesn't exist,
    ///     <see cref="TransactionResult.Succeeded" /> if successful, or
    ///     <see cref="TransactionResult.Failed" /> if an error occurs.
    /// </returns>
    public async Task<TransactionResult> SetMaterialOutOfStockAsync(int materialId)
    {
        var material = await _context.Materials
            .FirstOrDefaultAsync(m => m.Id == materialId);

        if (material == null)
            return TransactionResult.NotFound;

        return await SetMaterialStockStatusAsync(material, false);
    }

    /// <summary>
    ///     Updates the in-stock status of the supplied material entity.
    /// </summary>
    /// <param name="material">The material entity to update.</param>
    /// <param name="inStock">Indicates whether the material should be in stock.</param>
    /// <returns>A <see cref="TransactionResult" /> representing the outcome of the update.</returns>
    private async Task<TransactionResult> SetMaterialStockStatusAsync(Material material, bool inStock)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            material.InStock = inStock;
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return TransactionResult.Succeeded;
        }
        catch
        {
            await transaction.RollbackAsync();
            return TransactionResult.Failed;
        }
    }
}