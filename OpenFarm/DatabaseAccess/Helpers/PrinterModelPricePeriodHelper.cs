using System.Linq.Expressions;
using DatabaseAccess.Models;
using Microsoft.EntityFrameworkCore;

namespace DatabaseAccess.Helpers;

/// <summary>
///     Provides helper methods for accessing and modifying <see cref="PrinterModelPricePeriod" /> entities in the
///     database.
/// </summary>
/// <remarks>
///     Initializes a new instance of the <see cref="PrinterModelPricePeriodHelper" /> class.
/// </remarks>
/// <param name="context">The database context to use for operations.</param>
public class PrinterModelPricePeriodHelper(OpenFarmContext context) : BaseHelper(context)
{
    private IQueryable<PrinterModelPricePeriod> PrinterModelPricePeriods =>
        _context.PrinterModelPricePeriods.AsNoTracking();

    private IQueryable<PrinterModelPricePeriod> ActivePrinterModelPricePeriods => PrinterModelPricePeriods
        .Where(period => period.EndedAt == null);

    private IQueryable<PrinterModelPricePeriod> InactivePrinterModelPricePeriods => PrinterModelPricePeriods
        .Where(period => period.EndedAt != null);

    private IQueryable<PrinterModel> PrinterModels => _context.PrinterModels.AsNoTracking();

    /// <summary>
    ///     Retrieves every printer model price period.
    /// </summary>
    /// <returns>A task that resolves to a list of all printer model price periods.</returns>
    public async Task<List<PrinterModelPricePeriod>> GetPrinterModelPricePeriodsAsync()
    {
        return await PrinterModelPricePeriods.ToListAsync();
    }

    /// <summary>
    ///     Retrieves every active printer model price period.
    /// </summary>
    /// <returns>A task that resolves to a list of active printer model price periods.</returns>
    public async Task<List<PrinterModelPricePeriod>> GetActivePrinterModelPricePeriodsAsync()
    {
        return await ActivePrinterModelPricePeriods.ToListAsync();
    }

    /// <summary>
    ///     Retrieves every price period associated with the specified printer model identifier.
    /// </summary>
    /// <param name="printerModelId">The printer model identifier to filter by.</param>
    /// <returns>A task that resolves to a list of price periods for the specified printer model.</returns>
    public async Task<List<PrinterModelPricePeriod>> GetPrinterModelPricePeriodsAsync(int printerModelId)
    {
        return await PrinterModelPricePeriods
            .Where(period => period.PrinterModelId == printerModelId)
            .ToListAsync();
    }

    /// <summary>
    ///     Retrieves the active price period for the specified printer model identifier.
    /// </summary>
    /// <param name="printerModelId">The printer model identifier to look up.</param>
    /// <returns>A task that resolves to the active price period, or null if not found.</returns>
    public async Task<PrinterModelPricePeriod?> GetActivePrinterModelPricePeriodAsync(int printerModelId)
    {
        return await ActivePrinterModelPricePeriods
            .FirstOrDefaultAsync(period => period.PrinterModelId == printerModelId);
    }

    /// <summary>
    ///     Retrieves the price period with the specified identifier.
    /// </summary>
    /// <param name="pricePeriodId">The price period identifier to look up.</param>
    /// <returns>A task that resolves to the price period, or null if not found.</returns>
    public async Task<PrinterModelPricePeriod?> GetPrinterModelPricePeriodByIdAsync(int pricePeriodId)
    {
        return await PrinterModelPricePeriods
            .FirstOrDefaultAsync(period => period.Id == pricePeriodId);
    }

    /// <summary>
    ///     Retrieves every inactive printer model price period.
    /// </summary>
    /// <returns>A task that resolves to a list of inactive printer model price periods.</returns>
    public async Task<List<PrinterModelPricePeriod>> GetInactivePrinterModelPricePeriodsAsync()
    {
        return await InactivePrinterModelPricePeriods.ToListAsync();
    }

    /// <summary>
    ///     Retrieves the inactive price periods for the specified printer model identifier.
    /// </summary>
    /// <param name="printerModelId">The printer model identifier to filter by.</param>
    /// <returns>A task that resolves to a list of inactive price periods for the specified printer model.</returns>
    public async Task<List<PrinterModelPricePeriod>> GetInactivePrinterModelPricePeriodsAsync(int printerModelId)
    {
        return await InactivePrinterModelPricePeriods
            .Where(period => period.PrinterModelId == printerModelId)
            .ToListAsync();
    }

    /// <summary>
    ///     Retrieves price periods created within the specified date range.
    /// </summary>
    /// <param name="startDate">The start date of the range.</param>
    /// <param name="endDate">The end date of the range.</param>
    /// <returns>A task that resolves to a list of price periods created within the specified date range.</returns>
    public async Task<List<PrinterModelPricePeriod>> GetPrinterModelPricePeriodsInDateRangeAsync(DateTime startDate,
        DateTime endDate)
    {
        return await PrinterModelPricePeriods
            .Where(period => period.CreatedAt >= startDate && period.CreatedAt <= endDate)
            .ToListAsync();
    }

    /// <summary>
    ///     Creates a new price period when no active period exists for the printer model.
    /// </summary>
    /// <param name="printerModelId">The printer model identifier.</param>
    /// <param name="price">The price for the new period.</param>
    /// <returns>A task that resolves to a TransactionResult indicating the outcome of the operation.</returns>
    public async Task<TransactionResult> CreatePrinterModelPricePeriodRestrictedAsync(int printerModelId, decimal price)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            var modelExists = await PrinterModels
                .AnyAsync(model => model.Id == printerModelId);

            if (!modelExists)
            {
                await transaction.RollbackAsync();
                return TransactionResult.NotFound;
            }

            var hasActivePeriod = await ActivePrinterModelPricePeriods
                .AnyAsync(period => period.PrinterModelId == printerModelId);

            if (hasActivePeriod)
            {
                await transaction.RollbackAsync();
                return TransactionResult.NoAction;
            }

            await _context.PrinterModelPricePeriods.AddAsync(CreatePricePeriod(printerModelId, price));
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
    ///     Ends any active period for the model and then creates a new price period.
    /// </summary>
    /// <param name="printerModelId">The printer model identifier.</param>
    /// <param name="price">The price for the new period.</param>
    /// <returns>A task that resolves to a TransactionResult indicating the outcome of the operation.</returns>
    public async Task<TransactionResult> CreatePrinterModelPricePeriodCascadingAsync(int printerModelId, decimal price)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            var modelExists = await PrinterModels
                .AnyAsync(model => model.Id == printerModelId);

            if (!modelExists)
            {
                await transaction.RollbackAsync();
                return TransactionResult.NotFound;
            }

            var activePeriod = await ActivePrinterModelPricePeriods
                .FirstOrDefaultAsync(period => period.PrinterModelId == printerModelId);

            if (activePeriod != null)
            {
                activePeriod.EndedAt = DateTime.UtcNow;
                // Get the tracked entity if it exists, or null
                var trackedEntity = _context.ChangeTracker.Entries<PrinterModelPricePeriod>()
                    .FirstOrDefault(e => e.Entity.PrinterModelId == activePeriod.PrinterModelId);

                if (trackedEntity != null)
                    // Detach the existing tracked entity
                    trackedEntity.State = EntityState.Detached;

                _context.PrinterModelPricePeriods.Attach(activePeriod);
                _context.Entry(activePeriod).Property(period => period.EndedAt).IsModified = true;
            }

            await _context.PrinterModelPricePeriods.AddAsync(CreatePricePeriod(printerModelId, price));
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
    ///     Updates the price for the specified price period.
    /// </summary>
    /// <param name="pricePeriodId">The price period identifier to update.</param>
    /// <param name="newPrice">The new price value.</param>
    /// <returns>A task that resolves to a TransactionResult indicating the outcome of the operation.</returns>
    public async Task<TransactionResult> UpdatePrinterModelPricePeriodPriceAsync(int pricePeriodId, decimal newPrice)
    {
        return await UpdatePricePeriodAsync(pricePeriodId, period => period.Price = newPrice, p => p.Price);
    }

    /// <summary>
    ///     Ends the active price period for the specified printer model.
    /// </summary>
    /// <param name="printerModelId">The printer model identifier.</param>
    /// <returns>A task that resolves to a TransactionResult indicating the outcome of the operation.</returns>
    public async Task<TransactionResult> EndPrinterModelPricePeriodAsync(int printerModelId)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            var activePeriod = await ActivePrinterModelPricePeriods
                .FirstOrDefaultAsync(period => period.PrinterModelId == printerModelId);

            if (activePeriod == null)
            {
                await transaction.RollbackAsync();
                return TransactionResult.NotFound;
            }

            activePeriod.EndedAt = DateTime.UtcNow;
            // Get the tracked entity if it exists, or null
            var trackedEntity = _context.ChangeTracker.Entries<PrinterModelPricePeriod>()
                .FirstOrDefault(e => e.Entity.PrinterModelId == activePeriod.PrinterModelId);

            if (trackedEntity != null)
                // Detach the existing tracked entity
                trackedEntity.State = EntityState.Detached;

            _context.PrinterModelPricePeriods.Attach(activePeriod);
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
    ///     Ends the specified price period by identifier.
    /// </summary>
    /// <param name="pricePeriodId">The price period identifier to end.</param>
    /// <returns>A task that resolves to a TransactionResult indicating the outcome of the operation.</returns>
    public async Task<TransactionResult> EndPricePeriodAsync(int pricePeriodId)
    {
        return await UpdatePricePeriodAsync(pricePeriodId, period => period.EndedAt = DateTime.UtcNow, p => p.EndedAt);
    }

    /// <summary>
    ///     Deletes the price period with the specified identifier.
    /// </summary>
    /// <param name="pricePeriodId">The price period identifier to delete.</param>
    /// <returns>A task that resolves to a TransactionResult indicating the outcome of the operation.</returns>
    public async Task<TransactionResult> DeletePrinterModelPricePeriodAsync(int pricePeriodId)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            var pricePeriod = await PrinterModelPricePeriods
                .FirstOrDefaultAsync(period => period.Id == pricePeriodId);

            if (pricePeriod == null)
            {
                await transaction.RollbackAsync();
                return TransactionResult.NotFound;
            }

            // Get the tracked entity if it exists
            var trackedEntity = _context.ChangeTracker.Entries<PrinterModelPricePeriod>()
                .FirstOrDefault(e => e.Entity.Id == pricePeriodId);

            if (trackedEntity != null)
            {
                // Entity is already tracked, remove it directly
                _context.PrinterModelPricePeriods.Remove(trackedEntity.Entity);
            }
            else
            {
                // Entity is not tracked, attach and remove
                _context.PrinterModelPricePeriods.Attach(pricePeriod);
                _context.PrinterModelPricePeriods.Remove(pricePeriod);
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
    ///     Deletes every inactive price period associated with the specified printer model.
    /// </summary>
    /// <param name="printerModelId">The printer model identifier.</param>
    /// <returns>A task that resolves to a TransactionResult indicating the outcome of the operation.</returns>
    public async Task<TransactionResult> DeleteInactivePrinterModelPricePeriodsAsync(int printerModelId)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            var printerModel = await PrinterModels
                .FirstOrDefaultAsync(model => model.Id == printerModelId);

            if (printerModel == null)
            {
                await transaction.RollbackAsync();
                return TransactionResult.NotFound;
            }

            var inactivePeriods = await InactivePrinterModelPricePeriods
                .Where(period => period.PrinterModelId == printerModelId)
                .ToListAsync();

            _context.PrinterModelPricePeriods.RemoveRange(inactivePeriods);
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

    private async Task<TransactionResult> UpdatePricePeriodAsync(
        int pricePeriodId,
        Action<PrinterModelPricePeriod> applyUpdates,
        params Expression<Func<PrinterModelPricePeriod, object?>>[] modifiedProperties)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            var exists = await PrinterModelPricePeriods
                .AnyAsync(period => period.Id == pricePeriodId);

            if (!exists)
            {
                await transaction.RollbackAsync();
                return TransactionResult.NotFound;
            }

            // Get the tracked entity if it exists, or null
            var trackedEntity = _context.ChangeTracker.Entries<PrinterModelPricePeriod>()
                .FirstOrDefault(e => e.Entity.Id == pricePeriodId);

            if (trackedEntity != null)
                // Detach the existing tracked entity
                trackedEntity.State = EntityState.Detached;

            var pricePeriod = new PrinterModelPricePeriod { Id = pricePeriodId };
            _context.PrinterModelPricePeriods.Attach(pricePeriod);

            applyUpdates(pricePeriod);

            var entry = _context.Entry(pricePeriod);
            foreach (var property in modifiedProperties)
                entry.Property(property).IsModified = true;

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

    private static PrinterModelPricePeriod CreatePricePeriod(int printerModelId, decimal price)
    {
        return new PrinterModelPricePeriod
        {
            PrinterModelId = printerModelId,
            Price = price,
            CreatedAt = DateTime.UtcNow,
            EndedAt = null
        };
    }
}