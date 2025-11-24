using DatabaseAccess.Models;
using Microsoft.EntityFrameworkCore;

namespace DatabaseAccess.Helpers;

/// <summary>
///     Provides helper methods for accessing and modifying <see cref="MaterialPricePeriod" /> entities in the database.
/// </summary>
/// <remarks>
///     Initializes a new instance of the <see cref="MaterialPricePeriodHelper" /> class.
/// </remarks>
/// <param name="context">The database context to use for operations.</param>
public class MaterialPricePeriodHelper(OpenFarmContext context) : BaseHelper(context)
{
    private IQueryable<MaterialPricePeriod> MaterialPricePeriods => _context.MaterialPricePeriods.AsNoTracking();

    private IQueryable<MaterialPricePeriod> ActiveMaterialPricePeriods =>
        MaterialPricePeriods.Where(period => period.EndedAt == null);

    private IQueryable<MaterialPricePeriod> InactiveMaterialPricePeriods =>
        MaterialPricePeriods.Where(period => period.EndedAt != null);

    private IQueryable<Material> Materials => _context.Materials.AsNoTracking();

    /// <summary>
    ///     Asynchronously retrieves every <see cref="MaterialPricePeriod" />.
    /// </summary>
    /// <returns>A task that resolves to a list containing all material price periods.</returns>
    public async Task<List<MaterialPricePeriod>> GetMaterialPricePeriodsAsync()
    {
        return await MaterialPricePeriods.ToListAsync();
    }

    /// <summary>
    ///     Asynchronously retrieves every active <see cref="MaterialPricePeriod" />.
    /// </summary>
    /// <returns>A task that resolves to a list containing material price periods whose <c>EndedAt</c> value is <c>null</c>.</returns>
    public async Task<List<MaterialPricePeriod>> GetActiveMaterialPricePeriodsAsync()
    {
        return await ActiveMaterialPricePeriods.ToListAsync();
    }

    /// <summary>
    ///     Asynchronously retrieves every inactive <see cref="MaterialPricePeriod" />.
    /// </summary>
    /// <returns>
    ///     A task that resolves to a list containing material price periods whose <c>EndedAt</c> value is not <c>null</c>
    ///     .
    /// </returns>
    public async Task<List<MaterialPricePeriod>> GetInactiveMaterialPricePeriodsAsync()
    {
        return await InactiveMaterialPricePeriods.ToListAsync();
    }

    /// <summary>
    ///     Asynchronously retrieves the <see cref="MaterialPricePeriod" /> with the specified identifier.
    /// </summary>
    /// <param name="pricePeriodId">The identifier of the price period.</param>
    /// <returns>A task that resolves to the matching price period, or <c>null</c> when it cannot be found.</returns>
    public async Task<MaterialPricePeriod?> GetMaterialPricePeriodByIdAsync(int pricePeriodId)
    {
        return await MaterialPricePeriods.FirstOrDefaultAsync(period => period.Id == pricePeriodId);
    }

    /// <summary>
    ///     Asynchronously retrieves every price period for the supplied material identifier.
    /// </summary>
    /// <param name="materialId">The identifier of the material.</param>
    /// <returns>A task that resolves to a list containing price periods belonging to the specified material.</returns>
    public async Task<List<MaterialPricePeriod>> GetMaterialPricePeriodsAsync(int materialId)
    {
        return await MaterialPricePeriods
            .Where(period => period.MaterialId == materialId)
            .ToListAsync();
    }

    /// <summary>
    ///     Asynchronously retrieves every price period for the supplied material entity.
    /// </summary>
    /// <param name="material">The material whose price history is requested.</param>
    /// <returns>A task that resolves to a list containing price periods belonging to the specified material.</returns>
    public Task<List<MaterialPricePeriod>> GetMaterialPricePeriodsAsync(Material material)
    {
        return GetMaterialPricePeriodsAsync(material.Id);
    }

    /// <summary>
    ///     Asynchronously retrieves the active price period for the supplied material identifier.
    /// </summary>
    /// <param name="materialId">The identifier of the material.</param>
    /// <returns>A task that resolves to the active price period, or <c>null</c> if the material has no active pricing.</returns>
    public async Task<MaterialPricePeriod?> GetMaterialActivePricePeriodAsync(int materialId)
    {
        return await ActiveMaterialPricePeriods.FirstOrDefaultAsync(period => period.MaterialId == materialId);
    }

    /// <summary>
    ///     Asynchronously retrieves the active price period for the supplied material entity.
    /// </summary>
    /// <param name="material">The material whose active price period is requested.</param>
    /// <returns>A task that resolves to the active price period, or <c>null</c> if the material has no active pricing.</returns>
    public Task<MaterialPricePeriod?> GetMaterialActivePricePeriodAsync(Material material)
    {
        return GetMaterialActivePricePeriodAsync(material.Id);
    }

    /// <summary>
    ///     Asynchronously retrieves the current price period for the supplied material identifier.
    /// </summary>
    /// <param name="materialId">The identifier of the material.</param>
    /// <returns>A task that resolves to the current price period, or <c>null</c> when none exists.</returns>
    public Task<MaterialPricePeriod?> GetMaterialPricePeriodAsync(int materialId)
    {
        return GetMaterialActivePricePeriodAsync(materialId);
    }

    /// <summary>
    ///     Asynchronously retrieves the current price period for the supplied material entity.
    /// </summary>
    /// <param name="material">The material whose current price period is required.</param>
    /// <returns>A task that resolves to the current price period, or <c>null</c> when none exists.</returns>
    public Task<MaterialPricePeriod?> GetMaterialPricePeriodAsync(Material material)
    {
        return GetMaterialActivePricePeriodAsync(material.Id);
    }

    /// <summary>
    ///     Asynchronously retrieves the active price for the specified material identifier.
    /// </summary>
    /// <param name="materialId">The identifier of the material.</param>
    /// <returns>A task that resolves to the active price, or <c>null</c> if the material has no active price period.</returns>
    public async Task<decimal?> GetActivePriceAsync(int materialId)
    {
        return await ActiveMaterialPricePeriods
            .Where(period => period.MaterialId == materialId)
            .Select(period => period.Price)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    ///     Asynchronously creates a price period for the specified material when one does not already exist.
    /// </summary>
    /// <param name="materialId">The identifier of the material.</param>
    /// <param name="price">The price to associate with the material.</param>
    /// <returns>A task that resolves to a <see cref="TransactionResult" /> describing the outcome of the operation.</returns>
    public async Task<TransactionResult> CreateMaterialPricePeriodAsync(int materialId, decimal price)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            var materialExists = await Materials.AnyAsync(material => material.Id == materialId);

            if (!materialExists)
            {
                await transaction.RollbackAsync();
                return TransactionResult.NotFound;
            }

            await _context.MaterialPricePeriods.AddAsync(CreatePricePeriod(materialId, price));

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
    ///     Asynchronously ends the current price period for the material and creates a replacement.
    /// </summary>
    /// <param name="materialId">The identifier of the material.</param>
    /// <param name="newPrice">The new price for the material.</param>
    /// <returns>A task that resolves to a <see cref="TransactionResult" /> describing the outcome.</returns>
    public async Task<TransactionResult> UpdateMaterialPriceAsync(int materialId, decimal newPrice)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            var materialExists = await Materials.AnyAsync(material => material.Id == materialId);

            if (!materialExists)
            {
                await transaction.RollbackAsync();
                return TransactionResult.NotFound;
            }

            var activePeriod = await ActiveMaterialPricePeriods
                .FirstOrDefaultAsync(period => period.MaterialId == materialId);

            if (activePeriod != null)
            {
                activePeriod.EndedAt = DateTime.UtcNow;
                // Get the tracked entity if it exists, or null
                var trackedEntity = _context.ChangeTracker.Entries<MaterialPricePeriod>()
                    .FirstOrDefault(e => e.Entity.MaterialId == activePeriod.MaterialId);

                if (trackedEntity != null)
                    // Detach the existing tracked entity
                    trackedEntity.State = EntityState.Detached;

                _context.MaterialPricePeriods.Attach(activePeriod);
                _context.Entry(activePeriod).Property(period => period.EndedAt).IsModified = true;
            }

            await _context.MaterialPricePeriods.AddAsync(CreatePricePeriod(materialId, newPrice));

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
    ///     Asynchronously ends the active price period for the specified material.
    /// </summary>
    /// <param name="materialId">The identifier of the material.</param>
    /// <returns>A task that resolves to a <see cref="TransactionResult" /> describing the outcome.</returns>
    public async Task<TransactionResult> EndMaterialPricePeriodAsync(int materialId)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            var activePeriod = await ActiveMaterialPricePeriods
                .FirstOrDefaultAsync(period => period.MaterialId == materialId);

            if (activePeriod == null)
            {
                await transaction.RollbackAsync();
                return TransactionResult.NotFound;
            }

            activePeriod.EndedAt = DateTime.UtcNow;
            // Get the tracked entity if it exists, or null
            var trackedEntity = _context.ChangeTracker.Entries<MaterialPricePeriod>()
                .FirstOrDefault(e => e.Entity.MaterialId == activePeriod.MaterialId);

            if (trackedEntity != null)
                // Detach the existing tracked entity
                trackedEntity.State = EntityState.Detached;

            _context.MaterialPricePeriods.Attach(activePeriod);
            _context.Entry(activePeriod).Property(period => period.EndedAt).IsModified = true;

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
    ///     Asynchronously ends the specified price period.
    /// </summary>
    /// <param name="pricePeriodId">The identifier of the price period to end.</param>
    /// <returns>A task that resolves to a <see cref="TransactionResult" /> describing the outcome.</returns>
    public async Task<TransactionResult> EndPricePeriodAsync(int pricePeriodId)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            var pricePeriod = await MaterialPricePeriods
                .FirstOrDefaultAsync(period => period.Id == pricePeriodId);

            if (pricePeriod == null)
            {
                await transaction.RollbackAsync();
                return TransactionResult.NotFound;
            }

            pricePeriod.EndedAt = DateTime.UtcNow;
            // Get the tracked entity if it exists, or null
            var trackedEntity = _context.ChangeTracker.Entries<MaterialPricePeriod>()
                .FirstOrDefault(e => e.Entity.Id == pricePeriod.Id);

            if (trackedEntity != null)
                // Detach the existing tracked entity
                trackedEntity.State = EntityState.Detached;

            _context.MaterialPricePeriods.Attach(pricePeriod);
            _context.Entry(pricePeriod).Property(period => period.EndedAt).IsModified = true;

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
    ///     Asynchronously deletes the specified price period.
    /// </summary>
    /// <param name="pricePeriodId">The identifier of the price period to delete.</param>
    /// <returns>A task that resolves to a <see cref="TransactionResult" /> describing the outcome.</returns>
    public async Task<TransactionResult> DeleteMaterialPricePeriodAsync(int pricePeriodId)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            var pricePeriod = await MaterialPricePeriods
                .FirstOrDefaultAsync(period => period.Id == pricePeriodId);

            if (pricePeriod == null)
            {
                await transaction.RollbackAsync();
                return TransactionResult.NotFound;
            }

            // Get the tracked entity if it exists
            var trackedEntity = _context.ChangeTracker.Entries<MaterialPricePeriod>()
                .FirstOrDefault(e => e.Entity.Id == pricePeriodId);

            if (trackedEntity != null)
            {
                // Entity is already tracked, remove it directly
                _context.MaterialPricePeriods.Remove(trackedEntity.Entity);
            }
            else
            {
                // Entity is not tracked, attach and remove
                _context.MaterialPricePeriods.Attach(pricePeriod);
                _context.MaterialPricePeriods.Remove(pricePeriod);
            }

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
    ///     Asynchronously deletes every inactive price period associated with the specified material.
    /// </summary>
    /// <param name="materialId">The identifier of the material.</param>
    /// <returns>A task that resolves to a <see cref="TransactionResult" /> describing the outcome.</returns>
    public async Task<TransactionResult> DeleteInactiveMaterialPricePeriodsAsync(int materialId)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            var materialExists = await Materials.AnyAsync(material => material.Id == materialId);

            if (!materialExists)
            {
                await transaction.RollbackAsync();
                return TransactionResult.NotFound;
            }

            var inactivePeriods = await InactiveMaterialPricePeriods
                .Where(period => period.MaterialId == materialId)
                .ToListAsync();

            if (inactivePeriods.Count == 0)
            {
                await transaction.RollbackAsync();
                return TransactionResult.NoAction;
            }

            _context.MaterialPricePeriods.RemoveRange(inactivePeriods);
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
    ///     Creates a new price period instance with the supplied attributes.
    /// </summary>
    /// <param name="materialId">The identifier of the material to associate.</param>
    /// <param name="price">The price to store in the period.</param>
    /// <returns>A configured <see cref="MaterialPricePeriod" /> entity.</returns>
    private static MaterialPricePeriod CreatePricePeriod(int materialId, decimal price)
    {
        return new MaterialPricePeriod
        {
            MaterialId = materialId,
            Price = price,
            CreatedAt = DateTime.UtcNow,
            EndedAt = null
        };
    }
}