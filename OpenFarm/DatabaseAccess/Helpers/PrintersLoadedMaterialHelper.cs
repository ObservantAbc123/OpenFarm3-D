using DatabaseAccess.Models;
using Microsoft.EntityFrameworkCore;

namespace DatabaseAccess.Helpers;

/// <summary>
///     Provides helper methods for accessing and modifying <see cref="PrintersLoadedMaterial" /> entities in the database.
/// </summary>
/// <remarks>
///     Initializes a new instance of the <see cref="PrintersLoadedMaterialHelper" /> class.
/// </remarks>
/// <param name="context">The database context to use for operations.</param>
public class PrintersLoadedMaterialHelper(OpenFarmContext context) : BaseHelper(context)
{
    private IQueryable<PrintersLoadedMaterial> LoadedMaterials => _context.PrintersLoadedMaterials.AsNoTracking();

    private IQueryable<Printer> Printers => _context.Printers.AsNoTracking();

    private IQueryable<Material> Materials => _context.Materials.AsNoTracking();

    /// <summary>
    ///     Retrieves every printer loaded material association.
    /// </summary>
    /// <returns>A task that resolves to a list of all printer loaded material associations.</returns>
    public async Task<List<PrintersLoadedMaterial>> GetPrintersLoadedMaterialsAsync()
    {
        return await LoadedMaterials.ToListAsync();
    }

    /// <summary>
    ///     Retrieves associations for the specified printer identifier.
    /// </summary>
    /// <param name="printerId">The printer identifier to filter by.</param>
    /// <returns>A task that resolves to a list of loaded material associations for the specified printer.</returns>
    public async Task<List<PrintersLoadedMaterial>> GetPrintersLoadedMaterialsByPrinterIdAsync(int printerId)
    {
        return await LoadedMaterials
            .Where(loaded => loaded.PrinterId == printerId)
            .ToListAsync();
    }

    /// <summary>
    ///     Retrieves associations for the specified material identifier.
    /// </summary>
    /// <param name="materialId">The material identifier to filter by.</param>
    /// <returns>A task that resolves to a list of loaded material associations for the specified material.</returns>
    public async Task<List<PrintersLoadedMaterial>> GetPrintersLoadedMaterialsByMaterialIdAsync(int materialId)
    {
        return await LoadedMaterials
            .Where(loaded => loaded.MaterialId == materialId)
            .ToListAsync();
    }

    /// <summary>
    ///     Retrieves the association with the specified identifier.
    /// </summary>
    /// <param name="id">The association identifier to look up.</param>
    /// <returns>A task that resolves to the loaded material association, or null if not found.</returns>
    public async Task<PrintersLoadedMaterial?> GetPrintersLoadedMaterialAsync(int id)
    {
        return await LoadedMaterials.FirstOrDefaultAsync(loaded => loaded.Id == id);
    }

    /// <summary>
    ///     Retrieves the association for a given printer/material combination.
    /// </summary>
    /// <param name="printerId">The printer identifier.</param>
    /// <param name="materialId">The material identifier.</param>
    /// <returns>A task that resolves to the loaded material association, or null if not found.</returns>
    public async Task<PrintersLoadedMaterial?> GetPrintersLoadedMaterialAsync(int printerId, int materialId)
    {
        return await LoadedMaterials
            .FirstOrDefaultAsync(loaded => loaded.PrinterId == printerId && loaded.MaterialId == materialId);
    }

    /// <summary>
    ///     Retrieves materials currently loaded on the specified printer.
    /// </summary>
    /// <param name="printerId">The printer identifier to look up.</param>
    /// <returns>A task that resolves to a list of materials loaded on the specified printer.</returns>
    public async Task<List<Material>> GetMaterialsLoadedOnPrinterAsync(int printerId)
    {
        return await LoadedMaterials
            .Where(loaded => loaded.PrinterId == printerId)
            .Include(loaded => loaded.Material)
            .Select(loaded => loaded.Material)
            .ToListAsync();
    }

    /// <summary>
    ///     Retrieves printers that currently have the specified material loaded.
    /// </summary>
    /// <param name="materialId">The material identifier to look up.</param>
    /// <returns>A task that resolves to a list of printers that have the specified material loaded.</returns>
    public async Task<List<Printer>> GetPrintersWithMaterialLoadedAsync(int materialId)
    {
        return await LoadedMaterials
            .Where(loaded => loaded.MaterialId == materialId)
            .Include(loaded => loaded.Printer)
            .Select(loaded => loaded.Printer)
            .ToListAsync();
    }

    /// <summary>
    ///     Indicates whether the specified material is loaded on the specified printer.
    /// </summary>
    /// <param name="printerId">The printer identifier.</param>
    /// <param name="materialId">The material identifier.</param>
    /// <returns>A task that resolves to true if the material is loaded on the printer, false otherwise.</returns>
    public async Task<bool> IsMaterialLoadedOnPrinterAsync(int printerId, int materialId)
    {
        return await LoadedMaterials.AnyAsync(loaded =>
            loaded.PrinterId == printerId && loaded.MaterialId == materialId);
    }

    /// <summary>
    ///     Returns the number of materials loaded on the specified printer.
    /// </summary>
    /// <param name="printerId">The printer identifier to count materials for.</param>
    /// <returns>A task that resolves to the number of materials loaded on the specified printer.</returns>
    public async Task<int> GetMaterialCountOnPrinterAsync(int printerId)
    {
        return await LoadedMaterials.CountAsync(loaded => loaded.PrinterId == printerId);
    }

    /// <summary>
    ///     Creates a printer/material association when it does not already exist.
    /// </summary>
    /// <param name="printerId">The printer identifier.</param>
    /// <param name="materialId">The material identifier.</param>
    /// <returns>A task that resolves to a TransactionResult indicating the outcome of the operation.</returns>
    public async Task<TransactionResult> LoadMaterialOnPrinterAsync(int printerId, int materialId)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            if (!await Printers.AnyAsync(printer => printer.Id == printerId) ||
                !await Materials.AnyAsync(material => material.Id == materialId))
            {
                await transaction.RollbackAsync();
                return TransactionResult.NotFound;
            }

            var exists = await LoadedMaterials
                .AnyAsync(loaded => loaded.PrinterId == printerId && loaded.MaterialId == materialId);

            if (exists)
            {
                await transaction.RollbackAsync();
                return TransactionResult.NoAction;
            }

            await _context.PrintersLoadedMaterials.AddAsync(new PrintersLoadedMaterial
            {
                PrinterId = printerId,
                MaterialId = materialId
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
    ///     Creates printer/material associations for the supplied printer when they do not already exist.
    /// </summary>
    /// <param name="printerId">The printer identifier.</param>
    /// <param name="materialIds">The list of material identifiers to load.</param>
    /// <returns>A task that resolves to a TransactionResult indicating the outcome of the operation.</returns>
    public async Task<TransactionResult> LoadMaterialsOnPrinterAsync(int printerId, List<int> materialIds)
    {
        if (materialIds == null || materialIds.Count == 0)
            return TransactionResult.Failed;

        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            if (!await Printers.AnyAsync(printer => printer.Id == printerId))
            {
                await transaction.RollbackAsync();
                return TransactionResult.NotFound;
            }

            var existingMaterialIds = await LoadedMaterials
                .Where(loaded => loaded.PrinterId == printerId)
                .Select(loaded => loaded.MaterialId)
                .ToListAsync();

            var sanitizedMaterialIds = materialIds
                .Distinct()
                .Where(id => id > 0)
                .ToList();

            var newMaterialIds = sanitizedMaterialIds
                .Except(existingMaterialIds)
                .ToList();

            if (newMaterialIds.Count == 0)
            {
                await transaction.RollbackAsync();
                return TransactionResult.NoAction;
            }

            var validMaterialIds = await Materials
                .Where(material => newMaterialIds.Contains(material.Id))
                .Select(material => material.Id)
                .ToListAsync();

            if (validMaterialIds.Count != newMaterialIds.Count)
            {
                await transaction.RollbackAsync();
                return TransactionResult.NotFound;
            }

            foreach (var materialId in validMaterialIds)
                await _context.PrintersLoadedMaterials.AddAsync(new PrintersLoadedMaterial
                {
                    PrinterId = printerId,
                    MaterialId = materialId
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
    ///     Removes a printer/material association when it exists.
    /// </summary>
    /// <param name="printerId">The printer identifier.</param>
    /// <param name="materialId">The material identifier.</param>
    /// <returns>A task that resolves to a TransactionResult indicating the outcome of the operation.</returns>
    public async Task<TransactionResult> UnloadMaterialFromPrinterAsync(int printerId, int materialId)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            var association = await LoadedMaterials
                .FirstOrDefaultAsync(loaded => loaded.PrinterId == printerId && loaded.MaterialId == materialId);

            if (association == null)
            {
                await transaction.RollbackAsync();
                return TransactionResult.NotFound;
            }

            // Get the tracked entity if it exists
            var trackedEntity = _context.ChangeTracker.Entries<PrintersLoadedMaterial>()
                .FirstOrDefault(e => e.Entity.PrinterId == printerId && e.Entity.MaterialId == materialId);

            if (trackedEntity != null)
            {
                // Entity is already tracked, remove it directly
                _context.PrintersLoadedMaterials.Remove(trackedEntity.Entity);
            }
            else
            {
                // Entity is not tracked, attach and remove
                _context.PrintersLoadedMaterials.Attach(association);
                _context.PrintersLoadedMaterials.Remove(association);
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
    ///     Removes every printer/material association for the specified printer.
    /// </summary>
    /// <param name="printerId">The printer identifier.</param>
    /// <returns>A task that resolves to a TransactionResult indicating the outcome of the operation.</returns>
    public async Task<TransactionResult> UnloadAllMaterialsFromPrinterAsync(int printerId)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            if (!await Printers.AnyAsync(printer => printer.Id == printerId))
            {
                await transaction.RollbackAsync();
                return TransactionResult.NotFound;
            }

            var associations = await LoadedMaterials
                .Where(loaded => loaded.PrinterId == printerId)
                .ToListAsync();

            if (associations.Count == 0)
            {
                await transaction.RollbackAsync();
                return TransactionResult.NoAction;
            }

            _context.PrintersLoadedMaterials.RemoveRange(associations);
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
    ///     Removes every printer/material association for the specified printer.
    /// </summary>
    /// <param name="printer">The printer entity.</param>
    /// <returns>A task that resolves to a TransactionResult indicating the outcome of the operation.</returns>
    public Task<TransactionResult> UnloadAllMaterialsFromPrinterAsync(Printer printer)
    {
        return UnloadAllMaterialsFromPrinterAsync(printer.Id);
    }

    /// <summary>
    ///     Removes every printer/material association for the specified material.
    /// </summary>
    /// <param name="materialId">The material identifier.</param>
    /// <returns>A task that resolves to a TransactionResult indicating the outcome of the operation.</returns>
    public async Task<TransactionResult> UnloadMaterialFromAllPrintersAsync(int materialId)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            if (!await Materials.AnyAsync(material => material.Id == materialId))
            {
                await transaction.RollbackAsync();
                return TransactionResult.NotFound;
            }

            var associations = await LoadedMaterials
                .Where(loaded => loaded.MaterialId == materialId)
                .ToListAsync();

            if (associations.Count == 0)
            {
                await transaction.RollbackAsync();
                return TransactionResult.NoAction;
            }

            _context.PrintersLoadedMaterials.RemoveRange(associations);
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
    ///     Removes every printer/material association for the specified material.
    /// </summary>
    /// <param name="material">The material entity.</param>
    /// <returns>A task that resolves to a TransactionResult indicating the outcome of the operation.</returns>
    public Task<TransactionResult> UnloadMaterialFromAllPrintersAsync(Material material)
    {
        return UnloadMaterialFromAllPrintersAsync(material.Id);
    }

    /// <summary>
    ///     Removes the specified association by identifier.
    /// </summary>
    /// <param name="id">The association identifier to delete.</param>
    /// <returns>A task that resolves to a TransactionResult indicating the outcome of the operation.</returns>
    public async Task<TransactionResult> DeletePrintersLoadedMaterialAsync(int id)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            var association = await LoadedMaterials
                .FirstOrDefaultAsync(loaded => loaded.Id == id);

            if (association == null)
            {
                await transaction.RollbackAsync();
                return TransactionResult.NotFound;
            }

            // Get the tracked entity if it exists
            var trackedEntity = _context.ChangeTracker.Entries<PrintersLoadedMaterial>()
                .FirstOrDefault(e => e.Entity.Id == id);

            if (trackedEntity != null)
            {
                // Entity is already tracked, remove it directly
                _context.PrintersLoadedMaterials.Remove(trackedEntity.Entity);
            }
            else
            {
                // Entity is not tracked, attach and remove
                _context.PrintersLoadedMaterials.Attach(association);
                _context.PrintersLoadedMaterials.Remove(association);
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
    ///     Replaces the set of materials loaded on the specified printer with the provided material identifiers.
    /// </summary>
    /// <param name="printerId">The printer identifier.</param>
    /// <param name="materialIds">The list of material identifiers to replace with.</param>
    /// <returns>A task that resolves to a TransactionResult indicating the outcome of the operation.</returns>
    public async Task<TransactionResult> ReplacePrinterMaterialsAsync(int printerId, List<int> materialIds)
    {
        if (materialIds == null)
            return TransactionResult.Failed;

        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            if (!await Printers.AnyAsync(printer => printer.Id == printerId))
            {
                await transaction.RollbackAsync();
                return TransactionResult.NotFound;
            }

            var existingAssociations = await LoadedMaterials
                .Where(loaded => loaded.PrinterId == printerId)
                .ToListAsync();

            var sanitizedMaterialIds = materialIds
                .Where(id => id > 0)
                .Distinct()
                .ToList();

            if (existingAssociations.Count == 0 && sanitizedMaterialIds.Count == 0)
            {
                await transaction.RollbackAsync();
                return TransactionResult.NoAction;
            }

            _context.PrintersLoadedMaterials.RemoveRange(existingAssociations);

            var validMaterialIds = await Materials
                .Where(material => sanitizedMaterialIds.Contains(material.Id))
                .Select(material => material.Id)
                .ToListAsync();

            if (validMaterialIds.Count != sanitizedMaterialIds.Count)
            {
                await transaction.RollbackAsync();
                return TransactionResult.NotFound;
            }

            foreach (var materialId in validMaterialIds)
                await _context.PrintersLoadedMaterials.AddAsync(new PrintersLoadedMaterial
                {
                    PrinterId = printerId,
                    MaterialId = materialId
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
    ///     Replaces the set of materials loaded on the specified printer with the provided materials.
    /// </summary>
    /// <param name="printer">The printer entity.</param>
    /// <param name="materials">The list of material entities to replace with.</param>
    /// <returns>A task that resolves to a TransactionResult indicating the outcome of the operation.</returns>
    public Task<TransactionResult> ReplacePrinterMaterialsAsync(Printer printer, List<Material> materials)
    {
        return ReplacePrinterMaterialsAsync(
            printer.Id,
            materials?.Select(material => material.Id).Where(id => id > 0).Distinct().ToList() ?? new List<int>());
    }
}