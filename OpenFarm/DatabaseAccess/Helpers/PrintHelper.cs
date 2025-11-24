using System.Linq.Expressions;
using DatabaseAccess.Models;
using Microsoft.EntityFrameworkCore;

namespace DatabaseAccess.Helpers;

/// <summary>
///     Provides helper methods for accessing and modifying <see cref="Print" /> entities in the database.
/// </summary>
/// <remarks>
///     Initializes a new instance of the <see cref="PrintHelper" /> class.
/// </remarks>
/// <param name="context">The database context to use for operations.</param>
public class PrintHelper(OpenFarmContext context) : BaseHelper(context)
{
    private IQueryable<Print> Prints => _context.Prints.AsNoTracking();

    private IQueryable<PrintJob> PrintJobs => _context.PrintJobs.AsNoTracking();

    private IQueryable<Printer> Printers => _context.Printers.AsNoTracking();

    /// <summary>
    ///     Retrieves every print.
    /// </summary>
    /// <returns>A task that resolves to a list of all prints.</returns>
    public async Task<List<Print>> GetPrintsAsync()
    {
        return await Prints.ToListAsync();
    }

    /// <summary>
    ///     Retrieves the print with the specified identifier.
    /// </summary>
    /// <param name="printId">The print identifier to look up.</param>
    /// <returns>A task that resolves to the print, or null if not found.</returns>
    public async Task<Print?> GetPrintAsync(long printId)
    {
        return await Prints.FirstOrDefaultAsync(print => print.Id == printId);
    }

    /// <summary>
    ///     Retrieves prints for the specified print job identifier.
    /// </summary>
    /// <param name="printJobId">The print job identifier to filter by.</param>
    /// <returns>A task that resolves to a list of prints for the specified print job.</returns>
    public async Task<List<Print>> GetPrintsByPrintJobIdAsync(long printJobId)
    {
        return await Prints
            .Where(print => print.PrintJobId == printJobId)
            .Include(p => p.Printer)
            .ToListAsync();
    }

    /// <summary>
    ///     Retrieves prints for the specified printer identifier.
    /// </summary>
    /// <param name="printerId">The printer identifier to filter by.</param>
    /// <returns>A task that resolves to a list of prints for the specified printer.</returns>
    public async Task<List<Print>> GetPrintsByPrinterIdAsync(int printerId)
    {
        return await Prints
            .Where(print => print.PrinterId == printerId)
            .ToListAsync();
    }

    /// <summary>
    ///     Retrieves prints matching the supplied status.
    /// </summary>
    /// <param name="printStatus">The print status to filter by.</param>
    /// <returns>A task that resolves to a list of prints with the specified status.</returns>
    public async Task<List<Print>> GetPrintsByStatusAsync(string printStatus)
    {
        return await Prints
            .Where(print => print.PrintStatus == printStatus)
            .ToListAsync();
    }

    /// <summary>
    ///     Retrieves prints that have not been marked as finished.
    /// </summary>
    /// <returns>A task that resolves to a list of active prints.</returns>
    public async Task<List<Print>> GetActivePrintsAsync()
    {
        return await Prints
            .Where(print => print.FinishedAt == null)
            .ToListAsync();
    }

    /// <summary>
    ///     Retrieves prints that have been marked as finished.
    /// </summary>
    /// <returns>A task that resolves to a list of completed prints.</returns>
    public async Task<List<Print>> GetCompletedPrintsAsync()
    {
        return await Prints
            .Where(print => print.FinishedAt != null)
            .ToListAsync();
    }

    /// <summary>
    ///     Creates a print when the print job exists and an optional printer reference is valid.
    /// </summary>
    /// <param name="printJobId">The print job identifier.</param>
    /// <returns>A task that resolves to the created Print object, or null if the operation failed.</returns>
    public async Task<Print?> CreatePrintAsync(long printJobId)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            var jobExists = await PrintJobs.AnyAsync(job => job.Id == printJobId);

            if (!jobExists)
            {
                await transaction.RollbackAsync();
                return null;
            }

            var print = new Print
            {
                PrintJobId = printJobId,
                PrinterId = null,
                CreatedAt = DateTime.UtcNow,
                StartedAt = null,
                FinishedAt = null,
                PrintStatus = "pending"
            };

            await _context.Prints.AddAsync(print);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return print;
        }
        catch
        {
            await transaction.RollbackAsync();
            return null;
        }
    }

    /// <summary>
    ///     Updates the printer associated with the print when both identifiers are valid.
    /// </summary>
    /// <param name="printId">The print identifier to update.</param>
    /// <param name="printerId">The printer identifier to associate.</param>
    /// <returns>A task that resolves to a TransactionResult indicating the outcome of the operation.</returns>
    public async Task<TransactionResult> UpdatePrintPrinterAsync(long printId, int printerId)
    {
        var printerExists = await Printers.AnyAsync(printer => printer.Id == printerId);

        if (!printerExists)
            return TransactionResult.NotFound;

        return await UpdatePrintAsync(printId, print => print.PrinterId = printerId, p => p.PrinterId);
    }

    /// <summary>
    ///     Updates the print status.
    /// </summary>
    /// <param name="printId">The print identifier to update.</param>
    /// <param name="printStatus">The new print status.</param>
    /// <returns>A task that resolves to a TransactionResult indicating the outcome of the operation.</returns>
    public async Task<TransactionResult> UpdatePrintStatusAsync(long printId, string printStatus)
    {
        if (string.IsNullOrWhiteSpace(printStatus))
            return TransactionResult.Failed;

        return await UpdatePrintAsync(printId, print => print.PrintStatus = printStatus, p => p.PrintStatus);
    }

    /// <summary>
    ///     Marks the print as started and optionally updates the print status (defaults to "Printing").
    /// </summary>
    /// <param name="printId">The print identifier to update.</param>
    /// <param name="startedAtUtc">The UTC date/time when the print was started.</param>
    /// <returns>A task that resolves to a TransactionResult indicating the outcome of the operation.</returns>
    public async Task<TransactionResult> MarkPrintStartedAsync(long printId)
    {
        return await UpdatePrintAsync(
            printId,
            print =>
            {
                print.StartedAt = DateTime.UtcNow;
                print.PrintStatus = "printing";
            },
            print => print.StartedAt,
            print => print.PrintStatus);
    }

    /// <summary>
    ///     Marks the print as finished and optionally updates the print status (defaults to "Completed").
    /// </summary>
    /// <param name="printId">The print identifier to update.</param>
    /// <param name="finishedAtUtc">The UTC date/time when the print was finished.</param>
    /// <returns>A task that resolves to a TransactionResult indicating the outcome of the operation.</returns>
    public async Task<TransactionResult> MarkPrintFinishedAsync(long printId)
    {
        return await UpdatePrintAsync(
            printId,
            print =>
            {
                print.FinishedAt = DateTime.UtcNow;
                print.PrintStatus = "completed";
            },
            print => print.FinishedAt,
            print => print.PrintStatus);
    }

    /// <summary>
    ///     Deletes the specified print.
    /// </summary>
    /// <param name="printId">The print identifier to delete.</param>
    /// <returns>A task that resolves to a TransactionResult indicating the outcome of the operation.</returns>
    public async Task<TransactionResult> DeletePrintAsync(long printId)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            // Check if entity exists using AsNoTracking
            var print = await Prints.FirstOrDefaultAsync(p => p.Id == printId);

            if (print == null)
            {
                await transaction.RollbackAsync();
                return TransactionResult.NotFound;
            }

            // Get the tracked entity if it exists
            var trackedEntity = _context.ChangeTracker.Entries<Print>()
                .FirstOrDefault(e => e.Entity.Id == printId);

            if (trackedEntity != null)
            {
                // Entity is already tracked, remove it directly
                _context.Prints.Remove(trackedEntity.Entity);
            }
            else
            {
                // Entity is not tracked, attach and remove
                _context.Prints.Attach(print);
                _context.Prints.Remove(print);
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
    ///     Deletes every print associated with the specified print job.
    /// </summary>
    /// <param name="printJobId">The print job identifier.</param>
    /// <returns>A task that resolves to a TransactionResult indicating the outcome of the operation.</returns>
    public async Task<TransactionResult> DeletePrintsForJobAsync(long printJobId)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            var jobExists = await PrintJobs.AnyAsync(job => job.Id == printJobId);

            if (!jobExists)
            {
                await transaction.RollbackAsync();
                return TransactionResult.NotFound;
            }

            var prints = await Prints
                .Where(print => print.PrintJobId == printJobId)
                .ToListAsync();

            if (prints.Count == 0)
            {
                await transaction.RollbackAsync();
                return TransactionResult.NoAction;
            }

            _context.Prints.RemoveRange(prints);
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

    private async Task<TransactionResult> UpdatePrintAsync(
        long printId,
        Action<Print> applyUpdates,
        params Expression<Func<Print, object?>>[] modifiedProperties)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            // Use AsNoTracking to ensure we're not tracking the entity from the exists check
            var exists = await Prints.AnyAsync(print => print.Id == printId);

            if (!exists)
            {
                await transaction.RollbackAsync();
                return TransactionResult.NotFound;
            }

            // Get the tracked entity if it exists, or null
            var trackedEntity = _context.ChangeTracker.Entries<Print>()
                .FirstOrDefault(e => e.Entity.Id == printId);

            if (trackedEntity != null)
                // Detach the existing tracked entity
                trackedEntity.State = EntityState.Detached;

            // Create a new instance and attach it
            var print = new Print { Id = printId };
            _context.Prints.Attach(print);

            // Apply updates
            applyUpdates(print);

            // Mark specific properties as modified
            var entry = _context.Entry(print);
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
}