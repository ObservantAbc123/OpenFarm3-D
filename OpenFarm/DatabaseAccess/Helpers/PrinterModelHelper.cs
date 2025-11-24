using System.Linq.Expressions;
using DatabaseAccess.Models;
using Microsoft.EntityFrameworkCore;

namespace DatabaseAccess.Helpers;

/// <summary>
///     Provides helper methods for accessing and modifying PrinterModel entities in the database.
/// </summary>
/// <remarks>
///     Initializes a new instance of the <see cref="PrinterModelHelper" /> class.
/// </remarks>
/// <param name="context">The database context to use for operations.</param>
public class PrinterModelHelper(OpenFarmContext context) : BaseHelper(context)
{
    private IQueryable<PrinterModel> PrinterModels => _context.PrinterModels.AsNoTracking();

    private IQueryable<PrinterModelPricePeriod> PrinterModelPricePeriods =>
        _context.PrinterModelPricePeriods.AsNoTracking();

    private IQueryable<Printer> Printers => _context.Printers.AsNoTracking();

    private IQueryable<Print> Prints => _context.Prints.AsNoTracking();

    /// <summary>
    ///     Asynchronously retrieves all <see cref="PrinterModel" /> entities from the database.
    /// </summary>
    /// <returns>
    ///     A task that resolves to a list of all printer models.
    /// </returns>
    public async Task<List<PrinterModel>> GetPrinterModelsAsync()
    {
        return await PrinterModels.ToListAsync();
    }

    /// <summary>
    ///     Asynchronously retrieves a <see cref="PrinterModel" /> entity with the given identifier.
    /// </summary>
    /// <param name="printerModelId">The identifier of the printer model to look up.</param>
    /// <returns>
    ///     A task that resolves to the printer model, or <c>null</c> if not found.
    /// </returns>
    public async Task<PrinterModel?> GetPrinterModelAsync(int printerModelId)
    {
        return await PrinterModels.FirstOrDefaultAsync(model => model.Id == printerModelId);
    }

    /// <summary>
    ///     Asynchronously retrieves a <see cref="PrinterModel" /> entity with the given name.
    /// </summary>
    /// <param name="printerModelName">The name of the printer model to look up.</param>
    /// <returns>
    ///     A task that resolves to the printer model, or <c>null</c> if not found.
    /// </returns>
    public async Task<PrinterModel?> GetPrinterModelByNameAsync(string printerModelName)
    {
        return await PrinterModels.FirstOrDefaultAsync(model => model.Model == printerModelName);
    }

    /// <summary>
    ///     Asynchronously retrieves all <see cref="PrinterModel" /> entities with autostart enabled.
    /// </summary>
    /// <returns>
    ///     A task that resolves to a list of printer models with autostart enabled.
    /// </returns>
    public async Task<List<PrinterModel>> GetAutostartPrinterModelsAsync()
    {
        return await PrinterModels
            .Where(model => model.Autostart)
            .ToListAsync();
    }

    /// <summary>
    ///     Asynchronously retrieves all <see cref="PrinterModel" /> entities with autostart disabled.
    /// </summary>
    /// <returns>
    ///     A task that resolves to a list of printer models with autostart disabled.
    /// </returns>
    public async Task<List<PrinterModel>> GetNonAutostartPrinterModelsAsync()
    {
        return await PrinterModels
            .Where(model => !model.Autostart)
            .ToListAsync();
    }

    /// <summary>
    ///     Asynchronously retrieves the autostart status of a printer model with the given identifier.
    /// </summary>
    /// <param name="printerModelId">The identifier of the printer model to look up.</param>
    /// <returns>
    ///     A task that resolves to the autostart status, or <c>null</c> if not found.
    /// </returns>
    public async Task<bool?> GetPrinterModelAutostartStatusAsync(int printerModelId)
    {
        return await PrinterModels
            .Where(model => model.Id == printerModelId)
            .Select(model => model.Autostart)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    ///     Asynchronously retrieves the name of a printer model with the given identifier.
    /// </summary>
    /// <param name="printerModelId">The identifier of the printer model to look up.</param>
    /// <returns>
    ///     A task that resolves to the printer model name, or <c>null</c> if not found.
    /// </returns>
    public async Task<string?> GetPrinterModelNameAsync(int printerModelId)
    {
        return await PrinterModels
            .Where(model => model.Id == printerModelId)
            .Select(model => model.Model)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    ///     Asynchronously retrieves the identifier of a printer model with the given name.
    /// </summary>
    /// <param name="printerModelName">The name of the printer model to look up.</param>
    /// <returns>
    ///     A task that resolves to the printer model identifier, or <c>null</c> if not found.
    /// </returns>
    public async Task<int?> GetPrinterModelIdAsync(string printerModelName)
    {
        return await PrinterModels
            .Where(model => model.Model == printerModelName)
            .Select(model => model.Id)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    ///     Asynchronously retrieves the active <see cref="PrinterModelPricePeriod" /> for the specified printer model
    ///     identifier.
    /// </summary>
    /// <param name="printerModelId">The identifier of the printer model to look up.</param>
    /// <returns>
    ///     A task that resolves to the active price period for the printer model, or <c>null</c> if not found.
    /// </returns>
    public async Task<PrinterModelPricePeriod?> GetActivePrinterModelPricePeriodAsync(int printerModelId)
    {
        return await PrinterModelPricePeriods
            .FirstOrDefaultAsync(period => period.PrinterModelId == printerModelId && period.EndedAt == null);
    }

    /// <summary>
    ///     Asynchronously retrieves the current price for the specified printer model identifier.
    /// </summary>
    /// <param name="printerModelId">The identifier of the printer model to look up.</param>
    /// <returns>
    ///     A task that resolves to the current price for the printer model, or <c>null</c> if not found.
    /// </returns>
    public async Task<decimal?> GetActivePrinterModelPriceAsync(int printerModelId)
    {
        return await PrinterModelPricePeriods
            .Where(period => period.PrinterModelId == printerModelId && period.EndedAt == null)
            .Select(period => period.Price)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    ///     Asynchronously creates a new <see cref="PrinterModel" /> and commits it in a transaction.
    /// </summary>
    /// <param name="printerModelName">The name of the printer model.</param>
    /// <param name="autostart">Whether autostart is enabled for this printer model.</param>
    /// <returns>
    ///     A task that resolves to a <see cref="TransactionResult" />.
    ///     Returns <see cref="TransactionResult.NoAction" /> if a printer model with the same name already exists,
    ///     <see cref="TransactionResult.Succeeded" /> if successful, or
    ///     <see cref="TransactionResult.Failed" /> if an error occurs.
    /// </returns>
    public async Task<TransactionResult> CreatePrinterModelAsync(string printerModelName, bool autostart)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            var exists = await PrinterModels
                .AnyAsync(model => model.Model == printerModelName);

            if (exists)
            {
                await transaction.RollbackAsync();
                return TransactionResult.NoAction;
            }

            await _context.PrinterModels.AddAsync(new PrinterModel
            {
                Model = printerModelName,
                Autostart = autostart
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
    ///     Asynchronously sets the autostart status of a printer model.
    /// </summary>
    /// <param name="printerModelId">The identifier of the printer model to update.</param>
    /// <param name="autostart">The new autostart status.</param>
    /// <returns>
    ///     A task that resolves to a <see cref="TransactionResult" />.
    ///     Returns <see cref="TransactionResult.NotFound" /> if the printer model doesn't exist,
    ///     <see cref="TransactionResult.Succeeded" /> if successful, or
    ///     <see cref="TransactionResult.Failed" /> if an error occurs.
    /// </returns>
    public Task<TransactionResult> SetPrinterModelAutostartAsync(int printerModelId, bool autostart)
    {
        return UpdatePrinterModelAsync(printerModelId, model => model.Autostart = autostart, m => m.Autostart);
    }

    /// <summary>
    ///     Asynchronously sets the autostart status of a printer model.
    /// </summary>
    /// <param name="printerModel">The printer model entity to update.</param>
    /// <param name="autostart">The new autostart status.</param>
    /// <returns>
    ///     A task that resolves to a <see cref="TransactionResult" />.
    ///     Returns <see cref="TransactionResult.Succeeded" /> if successful, or
    ///     <see cref="TransactionResult.Failed" /> if an error occurs.
    /// </returns>
    public Task<TransactionResult> SetPrinterModelAutostartAsync(PrinterModel printerModel, bool autostart)
    {
        return SetPrinterModelAutostartAsync(printerModel.Id, autostart);
    }

    /// <summary>
    ///     Asynchronously enables autostart for a printer model.
    /// </summary>
    /// <param name="printerModelId">The identifier of the printer model to enable autostart for.</param>
    /// <returns>
    ///     A task that resolves to a <see cref="TransactionResult" />.
    ///     Returns <see cref="TransactionResult.NotFound" /> if the printer model doesn't exist,
    ///     <see cref="TransactionResult.Succeeded" /> if successful, or
    ///     <see cref="TransactionResult.Failed" /> if an error occurs.
    /// </returns>
    public Task<TransactionResult> EnablePrinterModelAutostartAsync(int printerModelId)
    {
        return SetPrinterModelAutostartAsync(printerModelId, true);
    }

    /// <summary>
    ///     Asynchronously disables autostart for a printer model.
    /// </summary>
    /// <param name="printerModelId">The identifier of the printer model to disable autostart for.</param>
    /// <returns>
    ///     A task that resolves to a <see cref="TransactionResult" />.
    ///     Returns <see cref="TransactionResult.NotFound" /> if the printer model doesn't exist,
    ///     <see cref="TransactionResult.Succeeded" /> if successful, or
    ///     <see cref="TransactionResult.Failed" /> if an error occurs.
    /// </returns>
    public Task<TransactionResult> DisablePrinterModelAutostartAsync(int printerModelId)
    {
        return SetPrinterModelAutostartAsync(printerModelId, false);
    }

    /// <summary>
    ///     Asynchronously updates the name of a printer model.
    /// </summary>
    /// <param name="printerModelId">The identifier of the printer model to update.</param>
    /// <param name="newName">The new name for the printer model.</param>
    /// <returns>
    ///     A task that resolves to a <see cref="TransactionResult" />.
    ///     Returns <see cref="TransactionResult.NotFound" /> if the printer model doesn't exist,
    ///     <see cref="TransactionResult.NoAction" /> if a printer model with the new name already exists,
    ///     <see cref="TransactionResult.Succeeded" /> if successful, or
    ///     <see cref="TransactionResult.Failed" /> if an error occurs.
    /// </returns>
    public async Task<TransactionResult> UpdatePrinterModelNameAsync(int printerModelId, string newName)
    {
        var nameExists = await PrinterModels
            .AnyAsync(model => model.Model == newName && model.Id != printerModelId);

        if (nameExists)
            return TransactionResult.NoAction;

        return await UpdatePrinterModelAsync(printerModelId, model => model.Model = newName, m => m.Model);
    }

    /// <summary>
    ///     Asynchronously deletes a printer model with the specified identifier if no dependent entities exist.
    /// </summary>
    /// <param name="printerModelId">The identifier of the printer model to delete.</param>
    /// <returns>
    ///     A task that resolves to a tuple containing a <see cref="TransactionResult" /> and lists of dependent entities.
    ///     Returns <see cref="TransactionResult.NotFound" /> if the printer model doesn't exist,
    ///     <see cref="TransactionResult.Abandoned" /> with dependent entities if dependencies exist,
    ///     <see cref="TransactionResult.Succeeded" /> with empty lists if successful, or
    ///     <see cref="TransactionResult.Failed" /> with empty lists if an error occurs.
    /// </returns>
    public async Task<(TransactionResult, List<Print>, List<Printer>)> DeletePrinterModelRestrictedAsync(
        int printerModelId)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            var printerModel = await PrinterModels
                .FirstOrDefaultAsync(model => model.Id == printerModelId);

            if (printerModel == null)
            {
                await transaction.RollbackAsync();
                return (TransactionResult.NotFound, [], []);
            }

            var (dependentPrints, dependentPrinters) = await GetPrinterModelDependenciesAsync(printerModelId);

            if (dependentPrints.Count > 0 || dependentPrinters.Count > 0)
            {
                await transaction.RollbackAsync();
                return (TransactionResult.Abandoned, dependentPrints, dependentPrinters);
            }

            // Get the tracked entity if it exists
            var trackedEntity = _context.ChangeTracker.Entries<PrinterModel>()
                .FirstOrDefault(e => e.Entity.Id == printerModelId);

            if (trackedEntity != null)
            {
                // Entity is already tracked, remove it directly
                _context.PrinterModels.Remove(trackedEntity.Entity);
            }
            else
            {
                // Entity is not tracked, attach and remove
                _context.PrinterModels.Attach(printerModel);
                _context.PrinterModels.Remove(printerModel);
            }

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
    ///     Asynchronously deletes a printer model with the specified identifier, cascading to delete all dependent entities.
    /// </summary>
    /// <param name="printerModelId">The identifier of the printer model to delete.</param>
    /// <returns>
    ///     A task that resolves to a <see cref="TransactionResult" />.
    ///     Returns <see cref="TransactionResult.NotFound" /> if the printer model doesn't exist,
    ///     <see cref="TransactionResult.Succeeded" /> if successful, or
    ///     <see cref="TransactionResult.Failed" /> if an error occurs.
    /// </returns>
    public async Task<TransactionResult> DeletePrinterModelCascadingAsync(int printerModelId)
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

            // Get the tracked entity if it exists
            var trackedEntity = _context.ChangeTracker.Entries<PrinterModel>()
                .FirstOrDefault(e => e.Entity.Id == printerModelId);

            if (trackedEntity != null)
            {
                // Entity is already tracked, remove it directly
                _context.PrinterModels.Remove(trackedEntity.Entity);
            }
            else
            {
                // Entity is not tracked, attach and remove
                _context.PrinterModels.Attach(printerModel);
                _context.PrinterModels.Remove(printerModel);
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
    ///     Asynchronously creates a new <see cref="PrinterModel" /> with an associated price period.
    /// </summary>
    /// <param name="printerModelName">The name of the printer model.</param>
    /// <param name="autostart">Whether autostart is enabled for this printer model.</param>
    /// <param name="price">The price for the printer model.</param>
    /// <returns>
    ///     A task that resolves to a <see cref="TransactionResult" />.
    ///     Returns <see cref="TransactionResult.NoAction" /> if a printer model with the same name already exists,
    ///     <see cref="TransactionResult.Succeeded" /> if successful, or
    ///     <see cref="TransactionResult.Failed" /> if an error occurs.
    /// </returns>
    public async Task<TransactionResult> CreatePrinterModelWithPricePeriodAsync(string printerModelName, bool autostart,
        decimal price)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            var exists = await PrinterModels
                .AnyAsync(model => model.Model == printerModelName);

            if (exists)
            {
                await transaction.RollbackAsync();
                return TransactionResult.NoAction;
            }

            var newModel = new PrinterModel
            {
                Model = printerModelName,
                Autostart = autostart
            };

            await _context.PrinterModels.AddAsync(newModel);
            await _context.SaveChangesAsync();

            await _context.PrinterModelPricePeriods.AddAsync(new PrinterModelPricePeriod
            {
                PrinterModelId = newModel.Id,
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

    private async Task<TransactionResult> UpdatePrinterModelAsync(
        int printerModelId,
        Action<PrinterModel> applyUpdates,
        params Expression<Func<PrinterModel, object?>>[] modifiedProperties)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            var exists = await PrinterModels.AnyAsync(model => model.Id == printerModelId);

            if (!exists)
            {
                await transaction.RollbackAsync();
                return TransactionResult.NotFound;
            }

            // Get the tracked entity if it exists, or null
            var trackedEntity = _context.ChangeTracker.Entries<PrinterModel>()
                .FirstOrDefault(e => e.Entity.Id == printerModelId);

            if (trackedEntity != null)
                // Detach the existing tracked entity
                trackedEntity.State = EntityState.Detached;

            var printerModel = new PrinterModel { Id = printerModelId };
            _context.PrinterModels.Attach(printerModel);

            applyUpdates(printerModel);

            var entry = _context.Entry(printerModel);
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

    private async Task<(List<Print> Prints, List<Printer> Printers)> GetPrinterModelDependenciesAsync(
        int printerModelId)
    {
        var prints = await Prints
            .Where(print => print.PrinterId != null)
            .Include(print => print.Printer)
            .Where(print => print.Printer!.PrinterModelId == printerModelId)
            .ToListAsync();

        var printers = await Printers
            .Where(printer => printer.PrinterModelId == printerModelId)
            .ToListAsync();

        return (prints, printers);
    }
}