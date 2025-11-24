using System.Linq.Expressions;
using DatabaseAccess.Models;
using Microsoft.EntityFrameworkCore;

namespace DatabaseAccess.Helpers;

/// <summary>
///     Provides helper methods for accessing and modifying <see cref="Printer" /> entities in the database.
/// </summary>
/// <remarks>
///     Initializes a new instance of the <see cref="PrinterHelper" /> class.
/// </remarks>
/// <param name="context">The database context to use for operations.</param>
public class PrinterHelper(OpenFarmContext context) : BaseHelper(context)
{
    private IQueryable<Printer> Printers => _context.Printers.AsNoTracking();

    private IQueryable<PrinterModel> PrinterModels => _context.PrinterModels.AsNoTracking();

    private IQueryable<PrintersLoadedMaterial> PrinterLoadedMaterials =>
        _context.PrintersLoadedMaterials.AsNoTracking();

    private IQueryable<Print> Prints => _context.Prints.AsNoTracking();

    /// <summary>
    ///     Retrieves every printer.
    /// </summary>
    /// <returns>A task that resolves to a list of all printers.</returns>
    public async Task<List<Printer>> GetPrintersAsync()
    {
        return await Printers.ToListAsync();
    }

    /// <summary>
    ///     Retrieves printers filtered by enabled status.
    /// </summary>
    /// <param name="enabled">The enabled status to filter by.</param>
    /// <returns>A task that resolves to a list of printers with the specified enabled status.</returns>
    public async Task<List<Printer>> GetPrintersByEnabledStatusAsync(bool enabled)
    {
        return await Printers
            .Where(printer => printer.Enabled == enabled)
            .ToListAsync();
    }

    /// <summary>
    ///     Retrieves enabled printers.
    /// </summary>
    /// <returns>A task that resolves to a list of enabled printers.</returns>
    public Task<List<Printer>> GetEnabledPrintersAsync()
    {
        return GetPrintersByEnabledStatusAsync(true);
    }

    /// <summary>
    ///     Retrieves disabled printers.
    /// </summary>
    /// <returns>A task that resolves to a list of disabled printers.</returns>
    public Task<List<Printer>> GetDisabledPrintersAsync()
    {
        return GetPrintersByEnabledStatusAsync(false);
    }

    /// <summary>
    ///     Retrieves printers filtered by whether they are currently printing.
    /// </summary>
    /// <param name="currentlyPrinting">The printing status to filter by.</param>
    /// <returns>A task that resolves to a list of printers with the specified printing status.</returns>
    public async Task<List<Printer>> GetPrintersByPrintingStatusAsync(bool currentlyPrinting)
    {
        return await Printers
            .Where(printer => printer.CurrentlyPrinting == currentlyPrinting)
            .ToListAsync();
    }

    /// <summary>
    ///     Retrieves printers that are currently printing.
    /// </summary>
    /// <returns>A task that resolves to a list of printers that are currently printing.</returns>
    public Task<List<Printer>> GetCurrentlyPrintingPrintersAsync()
    {
        return GetPrintersByPrintingStatusAsync(true);
    }

    /// <summary>
    ///     Retrieves printers that are not currently printing.
    /// </summary>
    /// <returns>A task that resolves to a list of printers that are not currently printing.</returns>
    public Task<List<Printer>> GetIdlePrintersAsync()
    {
        return GetPrintersByPrintingStatusAsync(false);
    }

    /// <summary>
    ///     Retrieves printers for the specified printer model identifier.
    /// </summary>
    /// <param name="printerModelId">The printer model identifier to filter by.</param>
    /// <returns>A task that resolves to a list of printers with the specified printer model.</returns>
    public async Task<List<Printer>> GetPrintersByModelIdAsync(int printerModelId)
    {
        return await Printers
            .Where(printer => printer.PrinterModelId == printerModelId)
            .ToListAsync();
    }

    /// <summary>
    ///     Retrieves the printer with the specified identifier.
    /// </summary>
    /// <param name="printerId">The printer identifier to look up.</param>
    /// <returns>A task that resolves to the printer, or null if not found.</returns>
    public async Task<Printer?> GetPrinterAsync(int printerId)
    {
        return await Printers.FirstOrDefaultAsync(printer => printer.Id == printerId);
    }

    /// <summary>
    ///     Retrieves the printer with the specified name.
    /// </summary>
    /// <param name="printerName">The printer name to look up.</param>
    /// <returns>A task that resolves to the printer, or null if not found.</returns>
    public async Task<Printer?> GetPrinterByNameAsync(string printerName)
    {
        return await Printers.FirstOrDefaultAsync(printer => printer.Name == printerName);
    }

    /// <summary>
    ///     Retrieves the name of the specified printer.
    /// </summary>
    /// <param name="printerId">The printer identifier to look up.</param>
    /// <returns>A task that resolves to the printer name, or null if not found.</returns>
    public async Task<string?> GetPrinterNameAsync(int printerId)
    {
        return await Printers
            .Where(printer => printer.Id == printerId)
            .Select(printer => printer.Name)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    ///     Retrieves the identifier of a printer with the specified name.
    /// </summary>
    /// <param name="printerName">The printer name to look up.</param>
    /// <returns>A task that resolves to the printer identifier, or null if not found.</returns>
    public async Task<int?> GetPrinterIdAsync(string printerName)
    {
        return await Printers
            .Where(printer => printer.Name == printerName)
            .Select(printer => printer.Id)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    ///     Retrieves the IP address of the specified printer.
    /// </summary>
    /// <param name="printerId">The printer identifier to look up.</param>
    /// <returns>A task that resolves to the printer IP address, or null if not found.</returns>
    public async Task<string?> GetPrinterIpAddressAsync(int printerId)
    {
        return await Printers
            .Where(printer => printer.Id == printerId)
            .Select(printer => printer.IpAddress)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    ///     Retrieves the API key of the specified printer.
    /// </summary>
    /// <param name="printerId">The printer identifier to look up.</param>
    /// <returns>A task that resolves to the printer API key, or null if not found.</returns>
    public async Task<string?> GetPrinterApiKeyAsync(int printerId)
    {
        return await Printers
            .Where(printer => printer.Id == printerId)
            .Select(printer => printer.ApiKey)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    ///     Retrieves the enabled status for the specified printer.
    /// </summary>
    /// <param name="printerId">The printer identifier to look up.</param>
    /// <returns>A task that resolves to the printer enabled status, or null if not found.</returns>
    public async Task<bool?> GetPrinterEnabledStatusAsync(int printerId)
    {
        return await Printers
            .Where(printer => printer.Id == printerId)
            .Select(printer => printer.Enabled)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    ///     Retrieves the autostart status for the specified printer.
    /// </summary>
    /// <param name="printerId">The printer identifier to look up.</param>
    /// <returns>A task that resolves to the printer autostart status, or null if not found.</returns>
    public async Task<bool?> GetPrinterAutostartStatusAsync(int printerId)
    {
        var autostart = await Printers
            .Where(printer => printer.Id == printerId)
            .Select(printer => printer.Autostart)
            .FirstOrDefaultAsync();

        if (autostart != null)
            return autostart;

        var printer = await GetPrinterAsync(printerId);

        if (printer == null)
            return false;

        return await PrinterModels
            .Where(model => model.Id == printer.PrinterModelId)
            .Select(model => model.Autostart)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    ///     Retrieves the currently-printing status for the specified printer.
    /// </summary>
    /// <param name="printerId">The printer identifier to look up.</param>
    /// <returns>A task that resolves to the printer currently-printing status, or null if not found.</returns>
    public async Task<bool?> GetPrinterCurrentlyPrintingStatusAsync(int printerId)
    {
        return await Printers
            .Where(printer => printer.Id == printerId)
            .Select(printer => printer.CurrentlyPrinting)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    ///     Retrieves the printer-model identifier for the specified printer.
    /// </summary>
    /// <param name="printerId">The printer identifier to look up.</param>
    /// <returns>A task that resolves to the printer model identifier, or null if not found.</returns>
    public async Task<int?> GetPrinterModelIdAsync(int printerId)
    {
        return await Printers
            .Where(printer => printer.Id == printerId)
            .Select(printer => printer.PrinterModelId)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    ///     Retrieves the printer model for the specified printer.
    /// </summary>
    /// <param name="printerId">The printer identifier to look up.</param>
    /// <returns>A task that resolves to the printer model, or null if not found.</returns>
    public async Task<PrinterModel?> GetPrinterModelByPrinterIdAsync(int printerId)
    {
        return await Printers
            .Where(printer => printer.Id == printerId)
            .Include(printer => printer.PrinterModel)
            .Select(printer => printer.PrinterModel)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    ///     Creates a printer when a printer with the same name does not already exist.
    /// </summary>
    /// <param name="name">The name of the printer.</param>
    /// <param name="printerModelId">The printer model identifier.</param>
    /// <param name="enabled">Whether the printer is enabled.</param>
    /// <returns>A task that resolves to a TransactionResult indicating the outcome of the operation.</returns>
    public Task<TransactionResult> CreatePrinterAsync(string name, int printerModelId, bool enabled = true)
    {
        return CreatePrinterWithConfigAsync(name, printerModelId, null, null, enabled,
            null);
    }

    /// <summary>
    ///     Creates a printer with the provided configuration when the name is unique.
    /// </summary>
    /// <param name="name">The name of the printer.</param>
    /// <param name="printerModelId">The printer model identifier.</param>
    /// <param name="ipAddress">The IP address of the printer.</param>
    /// <param name="apiKey">The API key for the printer.</param>
    /// <param name="enabled">Whether the printer is enabled.</param>
    /// <param name="autostart">Whether autostart is enabled for the printer.</param>
    /// <returns>A task that resolves to a TransactionResult indicating the outcome of the operation.</returns>
    public async Task<TransactionResult> CreatePrinterWithConfigAsync(string name, int printerModelId,
        string? ipAddress, string? apiKey, bool enabled = true, bool? autostart = null)
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

            var nameExists = await Printers
                .AnyAsync(printer => printer.Name == name);

            if (nameExists)
            {
                await transaction.RollbackAsync();
                return TransactionResult.NoAction;
            }

            await _context.Printers.AddAsync(new Printer
            {
                Name = name,
                PrinterModelId = printerModelId,
                IpAddress = ipAddress,
                ApiKey = apiKey,
                Enabled = enabled,
                Autostart = autostart,
                CurrentlyPrinting = false
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
    ///     Updates the printer's name when the new name is unique.
    /// </summary>
    /// <param name="printerId">The printer identifier to update.</param>
    /// <param name="newName">The new name for the printer.</param>
    /// <returns>A task that resolves to a TransactionResult indicating the outcome of the operation.</returns>
    public async Task<TransactionResult> UpdatePrinterNameAsync(int printerId, string newName)
    {
        var nameExists = await Printers
            .AnyAsync(printer => printer.Name == newName && printer.Id != printerId);

        if (nameExists)
            return TransactionResult.NoAction;

        return await UpdatePrinterAsync(printerId, printer => printer.Name = newName, p => p.Name);
    }

    /// <summary>
    ///     Updates the printer's IP address.
    /// </summary>
    /// <param name="printerId">The printer identifier to update.</param>
    /// <param name="ipAddress">The new IP address for the printer.</param>
    /// <returns>A task that resolves to a TransactionResult indicating the outcome of the operation.</returns>
    public Task<TransactionResult> UpdatePrinterIpAddressAsync(int printerId, string? ipAddress)
    {
        return UpdatePrinterAsync(printerId, printer => printer.IpAddress = ipAddress, p => p.IpAddress);
    }

    /// <summary>
    ///     Updates the printer's API key.
    /// </summary>
    /// <param name="printerId">The printer identifier to update.</param>
    /// <param name="apiKey">The new API key for the printer.</param>
    /// <returns>A task that resolves to a TransactionResult indicating the outcome of the operation.</returns>
    public Task<TransactionResult> UpdatePrinterApiKeyAsync(int printerId, string? apiKey)
    {
        return UpdatePrinterAsync(printerId, printer => printer.ApiKey = apiKey, p => p.ApiKey);
    }

    /// <summary>
    ///     Updates the printer model associated with the printer.
    /// </summary>
    /// <param name="printerId">The printer identifier to update.</param>
    /// <param name="printerModelId">The new printer model identifier.</param>
    /// <returns>A task that resolves to a TransactionResult indicating the outcome of the operation.</returns>
    public async Task<TransactionResult> UpdatePrinterModelAsync(int printerId, int printerModelId)
    {
        var modelExists = await PrinterModels
            .AnyAsync(model => model.Id == printerModelId);

        if (!modelExists)
            return TransactionResult.NotFound;

        return await UpdatePrinterAsync(printerId, printer => printer.PrinterModelId = printerModelId,
            p => p.PrinterModelId);
    }

    /// <summary>
    ///     Sets the enabled state of the printer.
    /// </summary>
    /// <param name="printerId">The printer identifier to update.</param>
    /// <param name="enabled">The enabled status to set.</param>
    /// <returns>A task that resolves to a TransactionResult indicating the outcome of the operation.</returns>
    public Task<TransactionResult> SetPrinterEnabledStatusAsync(int printerId, bool enabled)
    {
        return UpdatePrinterAsync(printerId, printer => printer.Enabled = enabled, p => p.Enabled);
    }

    /// <summary>
    ///     Enables the printer.
    /// </summary>
    /// <param name="printerId">The printer identifier to enable.</param>
    /// <returns>A task that resolves to a TransactionResult indicating the outcome of the operation.</returns>
    public Task<TransactionResult> EnablePrinterAsync(int printerId)
    {
        return SetPrinterEnabledStatusAsync(printerId, true);
    }

    /// <summary>
    ///     Disables the printer.
    /// </summary>
    /// <param name="printerId">The printer identifier to disable.</param>
    /// <returns>A task that resolves to a TransactionResult indicating the outcome of the operation.</returns>
    public Task<TransactionResult> DisablePrinterAsync(int printerId)
    {
        return SetPrinterEnabledStatusAsync(printerId, false);
    }

    /// <summary>
    ///     Sets the autostart value for the printer.
    /// </summary>
    /// <param name="printerId">The printer identifier to update.</param>
    /// <param name="autostart">The autostart status to set.</param>
    /// <returns>A task that resolves to a TransactionResult indicating the outcome of the operation.</returns>
    public Task<TransactionResult> SetPrinterAutostartStatusAsync(int printerId, bool? autostart)
    {
        return UpdatePrinterAsync(printerId, printer => printer.Autostart = autostart, p => p.Autostart);
    }

    /// <summary>
    ///     Enables autostart on the printer.
    /// </summary>
    /// <param name="printerId">The printer identifier to update.</param>
    /// <returns>A task that resolves to a TransactionResult indicating the outcome of the operation.</returns>
    public Task<TransactionResult> EnablePrinterAutostartAsync(int printerId)
    {
        return SetPrinterAutostartStatusAsync(printerId, true);
    }

    /// <summary>
    ///     Disables autostart on the printer.
    /// </summary>
    /// <param name="printerId">The printer identifier to update.</param>
    /// <returns>A task that resolves to a TransactionResult indicating the outcome of the operation.</returns>
    public Task<TransactionResult> DisablePrinterAutostartAsync(int printerId)
    {
        return SetPrinterAutostartStatusAsync(printerId, false);
    }

    /// <summary>
    ///     Clears the autostart setting on the printer.
    /// </summary>
    /// <param name="printerId">The printer identifier to update.</param>
    /// <returns>A task that resolves to a TransactionResult indicating the outcome of the operation.</returns>
    public Task<TransactionResult> ClearPrinterAutostartAsync(int printerId)
    {
        return SetPrinterAutostartStatusAsync(printerId, null);
    }

    /// <summary>
    ///     Sets the currently-printing value for the printer.
    /// </summary>
    /// <param name="printerId">The printer identifier to update.</param>
    /// <param name="currentlyPrinting">The currently-printing status to set.</param>
    /// <returns>A task that resolves to a TransactionResult indicating the outcome of the operation.</returns>
    public Task<TransactionResult> SetPrinterCurrentlyPrintingStatusAsync(int printerId, bool currentlyPrinting)
    {
        return UpdatePrinterAsync(printerId, printer => printer.CurrentlyPrinting = currentlyPrinting,
            p => p.CurrentlyPrinting);
    }

    /// <summary>
    ///     Starts printing on the printer.
    /// </summary>
    /// <param name="printerId">The printer identifier to start printing on.</param>
    /// <returns>A task that resolves to a TransactionResult indicating the outcome of the operation.</returns>
    public Task<TransactionResult> StartPrinterPrintingAsync(int printerId)
    {
        return SetPrinterCurrentlyPrintingStatusAsync(printerId, true);
    }

    /// <summary>
    ///     Stops printing on the printer.
    /// </summary>
    /// <param name="printerId">The printer identifier to stop printing on.</param>
    /// <returns>A task that resolves to a TransactionResult indicating the outcome of the operation.</returns>
    public Task<TransactionResult> StopPrinterPrintingAsync(int printerId)
    {
        return SetPrinterCurrentlyPrintingStatusAsync(printerId, false);
    }

    /// <summary>
    ///     Deletes the printer, ensuring dependent prints or loaded materials do not exist.
    /// </summary>
    /// <param name="printerId">The printer identifier to delete.</param>
    /// <returns>A task that resolves to a tuple containing the TransactionResult and lists of dependent entities.</returns>
    public async Task<(TransactionResult, List<Print>, List<PrintersLoadedMaterial>)> DeletePrinterRestrictedAsync(
        int printerId)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            var printer = await Printers
                .FirstOrDefaultAsync(p => p.Id == printerId);

            if (printer == null)
            {
                await transaction.RollbackAsync();
                return (TransactionResult.NotFound, new List<Print>(), new List<PrintersLoadedMaterial>());
            }

            var (dependentPrints, dependentLoadedMaterials) = await GetPrinterDependenciesAsync(printerId);

            if (dependentPrints.Count > 0 || dependentLoadedMaterials.Count > 0)
            {
                await transaction.RollbackAsync();
                return (TransactionResult.Abandoned, dependentPrints, dependentLoadedMaterials);
            }

            // Get the tracked entity if it exists
            var trackedEntity = _context.ChangeTracker.Entries<Printer>()
                .FirstOrDefault(e => e.Entity.Id == printerId);

            if (trackedEntity != null)
            {
                // Entity is already tracked, remove it directly
                _context.Printers.Remove(trackedEntity.Entity);
            }
            else
            {
                // Entity is not tracked, attach and remove
                _context.Printers.Attach(printer);
                _context.Printers.Remove(printer);
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return (TransactionResult.Succeeded, new List<Print>(), new List<PrintersLoadedMaterial>());
        }
        catch
        {
            await transaction.RollbackAsync();
            return (TransactionResult.Failed, new List<Print>(), new List<PrintersLoadedMaterial>());
        }
    }

    /// <summary>
    ///     Deletes the specified printer, cascading dependents.
    /// </summary>
    /// <param name="printerId">The printer identifier to delete.</param>
    /// <returns>A task that resolves to a TransactionResult indicating the outcome of the operation.</returns>
    public async Task<TransactionResult> DeletePrinterCascadingAsync(int printerId)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            var printer = await Printers
                .FirstOrDefaultAsync(p => p.Id == printerId);

            if (printer == null)
            {
                await transaction.RollbackAsync();
                return TransactionResult.NotFound;
            }

            // Get the tracked entity if it exists
            var trackedEntity = _context.ChangeTracker.Entries<Printer>()
                .FirstOrDefault(e => e.Entity.Id == printerId);

            if (trackedEntity != null)
            {
                // Entity is already tracked, remove it directly
                _context.Printers.Remove(trackedEntity.Entity);
            }
            else
            {
                // Entity is not tracked, attach and remove
                _context.Printers.Attach(printer);
                _context.Printers.Remove(printer);
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

    private async Task<TransactionResult> UpdatePrinterAsync(
        int printerId,
        Action<Printer> applyUpdates,
        params Expression<Func<Printer, object?>>[] modifiedProperties)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            // Use AsNoTracking to ensure we're not tracking the entity from the exists check
            var exists = await Printers.AnyAsync(printer => printer.Id == printerId);

            if (!exists)
            {
                await transaction.RollbackAsync();
                return TransactionResult.NotFound;
            }

            // Get the tracked entity if it exists, or null
            var trackedEntity = _context.ChangeTracker.Entries<Printer>()
                .FirstOrDefault(e => e.Entity.Id == printerId);

            if (trackedEntity != null)
                // Detach the existing tracked entity
                trackedEntity.State = EntityState.Detached;

            // Create a new instance and attach it
            var printer = new Printer { Id = printerId };
            _context.Printers.Attach(printer);

            // Apply updates
            applyUpdates(printer);

            // Mark specific properties as modified
            var entry = _context.Entry(printer);
            foreach (var property in modifiedProperties)
                entry.Property(property).IsModified = true;

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return TransactionResult.Succeeded;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            await transaction.RollbackAsync();
            return TransactionResult.Failed;
        }
    }

    private async Task<(List<Print> Prints, List<PrintersLoadedMaterial> LoadedMaterials)>
        GetPrinterDependenciesAsync(int printerId)
    {
        var prints = await Prints
            .Where(print => print.PrinterId == printerId)
            .ToListAsync();

        var loadedMaterials = await PrinterLoadedMaterials
            .Where(material => material.PrinterId == printerId)
            .ToListAsync();

        return (prints, loadedMaterials);
    }
}