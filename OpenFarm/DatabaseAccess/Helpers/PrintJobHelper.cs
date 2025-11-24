using System.Linq.Expressions;
using DatabaseAccess.Models;
using Microsoft.EntityFrameworkCore;

namespace DatabaseAccess.Helpers;

/// <summary>
///     Provides helper methods for accessing and modifying <see cref="PrintJob" /> entities in the database.
/// </summary>
/// <remarks>
///     Initializes a new instance of the <see cref="PrintJobHelper" /> class.
/// </remarks>
/// <param name="context">The database context to use for operations.</param>
public class PrintJobHelper(OpenFarmContext context) : BaseHelper(context)
{
    private IQueryable<PrintJob> PrintJobs => _context.PrintJobs.AsNoTracking();

    private IQueryable<PrinterModel> PrinterModels => _context.PrinterModels.AsNoTracking();

    private IQueryable<Material> Materials => _context.Materials.AsNoTracking();

    private IQueryable<Print> Prints => _context.Prints.AsNoTracking();

    #region Creation

    /// <summary>
    ///     Creates a new print job.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="responseId">The response identifier.</param>
    /// <param name="numCopies">The number of copies.</param>
    /// <param name="submittedAtUtc">The submission date (UTC).</param>
    /// <returns>A task that resolves to the created print job.</returns>
    public async Task<PrintJob> CreatePrintJobAsync(long userId, string responseId, int numCopies,
        DateTime submittedAtUtc)
    {
        var job = new PrintJob
        {
            UserId = userId,
            ResponseId = responseId,
            NumCopies = numCopies,
            SubmittedAt = submittedAtUtc.ToUniversalTime(),
            CreatedAt = DateTime.UtcNow,
            JobStatus = "received",
            Paid = false
        };

        await _context.PrintJobs.AddAsync(job);
        await _context.SaveChangesAsync();
        return job;
    }

    #endregion

    private async Task<TransactionResult> UpdatePrintJobAsync(
        long printJobId,
        Action<PrintJob> applyUpdates,
        params Expression<Func<PrintJob, object?>>[] modifiedProperties)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            // Use AsNoTracking to ensure we're not tracking the entity from the exists check
            var exists = await PrintJobs.AnyAsync(job => job.Id == printJobId);

            if (!exists)
            {
                await transaction.RollbackAsync();
                return TransactionResult.NotFound;
            }

            // Get the tracked entity if it exists, or null
            var trackedEntity = _context.ChangeTracker.Entries<PrintJob>()
                .FirstOrDefault(e => e.Entity.Id == printJobId);

            if (trackedEntity != null)
                // Detach the existing tracked entity
                trackedEntity.State = EntityState.Detached;

            // Create a new instance and attach it
            var trackedJob = new PrintJob { Id = printJobId };
            _context.PrintJobs.Attach(trackedJob);

            // Apply updates
            applyUpdates(trackedJob);

            // Mark specific properties as modified
            var entry = _context.Entry(trackedJob);
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

    #region Retrieval

    /// <summary>
    ///     Retrieves all print jobs.
    /// </summary>
    /// <returns>A task that resolves to a list of all print jobs.</returns>
    public async Task<List<PrintJob>> GetPrintJobsAsync()
    {
        return await PrintJobs.ToListAsync();
    }

    /// <summary>
    ///     Retrieves print jobs filtered by status.
    /// </summary>
    /// <param name="status">The status to filter by.</param>
    /// <returns>A task that resolves to a list of print jobs with the specified status.</returns>
    public async Task<List<PrintJob>> GetPrintJobsByStatusAsync(string status)
    {
        return await PrintJobs
            .Where(job => job.JobStatus == status)
            .ToListAsync();
    }

    /// <summary>
    ///     Retrieves queued print jobs.
    /// </summary>
    /// <returns>A task that resolves to a list of queued print jobs.</returns>
    public Task<List<PrintJob>> GetQueuedPrintJobsAsync()
    {
        return GetPrintJobsByStatusAsync("queued");
    }

    /// <summary>
    ///     Retrieves printing print jobs.
    /// </summary>
    /// <returns>A task that resolves to a list of printing print jobs.</returns>
    public Task<List<PrintJob>> GetPrintingPrintJobsAsync()
    {
        return GetPrintJobsByStatusAsync("printing");
    }

    /// <summary>
    ///     Retrieves completed print jobs.
    /// </summary>
    /// <returns>A task that resolves to a list of completed print jobs.</returns>
    public Task<List<PrintJob>> GetCompletedPrintJobsAsync()
    {
        return GetPrintJobsByStatusAsync("completed");
    }

    /// <summary>
    ///     Retrieves failed print jobs.
    /// </summary>
    /// <returns>A task that resolves to a list of failed print jobs.</returns>
    public Task<List<PrintJob>> GetFailedPrintJobsAsync()
    {
        return GetPrintJobsByStatusAsync("failed");
    }

    /// <summary>
    ///     Retrieves cancelled print jobs.
    /// </summary>
    /// <returns>A task that resolves to a list of cancelled print jobs.</returns>
    public Task<List<PrintJob>> GetCancelledPrintJobsAsync()
    {
        return GetPrintJobsByStatusAsync("cancelled");
    }

    /// <summary>
    ///     Retrieves a print job by identifier.
    /// </summary>
    /// <param name="printJobId">The print job identifier to look up.</param>
    /// <returns>A task that resolves to the print job, or null if not found.</returns>
    public async Task<PrintJob?> GetPrintJobAsync(long printJobId)
    {
        return await PrintJobs.FirstOrDefaultAsync(job => job.Id == printJobId);
    }

    /// <summary>
    ///     Retrieves a print job by response identifier.
    /// </summary>
    /// <param name="responseId">The response identifier to look up.</param>
    /// <returns>A task that resolves to the print job, or null if not found.</returns>
    public async Task<PrintJob?> GetPrintJobByResponseIdAsync(string responseId)
    {
        return await PrintJobs.FirstOrDefaultAsync(job => job.ResponseId == responseId);
    }

    /// <summary>
    ///     Retrieves print jobs for a specific user.
    /// </summary>
    /// <param name="userId">The user identifier to filter by.</param>
    /// <returns>A task that resolves to a list of print jobs for the specified user.</returns>
    public async Task<List<PrintJob>> GetUserPrintJobsAsync(long userId)
    {
        return await PrintJobs.Where(job => job.UserId == userId).ToListAsync();
    }

    /// <summary>
    ///     Retrieves print jobs for a specific printer model.
    /// </summary>
    /// <param name="printerModelId">The printer model identifier to filter by.</param>
    /// <returns>A task that resolves to a list of print jobs for the specified printer model.</returns>
    public async Task<List<PrintJob>> GetPrinterModelPrintJobsAsync(int printerModelId)
    {
        return await PrintJobs.Where(job => job.PrinterModelId == printerModelId).ToListAsync();
    }

    /// <summary>
    ///     Retrieves print jobs for a specific material.
    /// </summary>
    /// <param name="materialId">The material identifier to filter by.</param>
    /// <returns>A task that resolves to a list of print jobs for the specified material.</returns>
    public async Task<List<PrintJob>> GetMaterialPrintJobsAsync(int materialId)
    {
        return await PrintJobs.Where(job => job.MaterialId == materialId).ToListAsync();
    }

    /// <summary>
    ///     Retrieves paid print jobs.
    /// </summary>
    /// <returns>A task that resolves to a list of paid print jobs.</returns>
    public async Task<List<PrintJob>> GetPaidPrintJobsAsync()
    {
        return await PrintJobs.Where(job => job.Paid).ToListAsync();
    }

    /// <summary>
    ///     Retrieves unpaid print jobs.
    /// </summary>
    /// <returns>A task that resolves to a list of unpaid print jobs.</returns>
    public async Task<List<PrintJob>> GetUnpaidPrintJobsAsync()
    {
        return await PrintJobs.Where(job => !job.Paid).ToListAsync();
    }

    /// <summary>
    ///     Retrieves print jobs created within a date range.
    /// </summary>
    /// <param name="startDateUtc">The start date of the range (UTC).</param>
    /// <param name="endDateUtc">The end date of the range (UTC).</param>
    /// <returns>A task that resolves to a list of print jobs created within the specified date range.</returns>
    public async Task<List<PrintJob>> GetPrintJobsByCreatedDateRangeAsync(DateTime startDateUtc, DateTime endDateUtc)
    {
        return await PrintJobs
            .Where(job => job.CreatedAt >= startDateUtc && job.CreatedAt <= endDateUtc)
            .ToListAsync();
    }

    /// <summary>
    ///     Retrieves print jobs submitted within a date range.
    /// </summary>
    /// <param name="startDateUtc">The start date of the range (UTC).</param>
    /// <param name="endDateUtc">The end date of the range (UTC).</param>
    /// <returns>A task that resolves to a list of print jobs submitted within the specified date range.</returns>
    public async Task<List<PrintJob>> GetPrintJobsBySubmittedDateRangeAsync(DateTime startDateUtc, DateTime endDateUtc)
    {
        return await PrintJobs
            .Where(job => job.SubmittedAt >= startDateUtc && job.SubmittedAt <= endDateUtc)
            .ToListAsync();
    }

    /// <summary>
    ///     Gets the count of print jobs for a specific user.
    /// </summary>
    /// <param name="userId">The user identifier to count jobs for.</param>
    /// <returns>A task that resolves to the number of print jobs for the specified user.</returns>
    public async Task<int> GetUserPrintJobCountAsync(long userId)
    {
        return await PrintJobs.CountAsync(job => job.UserId == userId);
    }

    /// <summary>
    ///     Gets the count of print jobs with a specific status.
    /// </summary>
    /// <param name="status">The status to count jobs for.</param>
    /// <returns>A task that resolves to the number of print jobs with the specified status.</returns>
    public async Task<int> GetPrintJobCountByStatusAsync(string status)
    {
        return await PrintJobs.CountAsync(job => job.JobStatus == status);
    }

    #endregion

    #region Updates

    /// <summary>
    ///     Updates the status of a print job.
    /// </summary>
    /// <param name="printJobId">The print job identifier to update.</param>
    /// <param name="newStatus">The new status value.</param>
    /// <returns>A task that resolves to a TransactionResult indicating the outcome of the operation.</returns>
    public async Task<TransactionResult> UpdatePrintJobStatusAsync(long printJobId, string newStatus)
    {
        if (string.IsNullOrWhiteSpace(newStatus))
            return TransactionResult.Failed;

        return await UpdatePrintJobAsync(printJobId, job => job.JobStatus = newStatus, job => job.JobStatus);
    }

    /// <summary>
    ///     Sets the payment status of a print job.
    /// </summary>
    /// <param name="printJobId">The print job identifier to update.</param>
    /// <param name="paid">The payment status to set.</param>
    /// <returns>A task that resolves to a TransactionResult indicating the outcome of the operation.</returns>
    public async Task<TransactionResult> SetPrintJobPaymentStatusAsync(long printJobId, bool paid)
    {
        return await UpdatePrintJobAsync(printJobId, job => job.Paid = paid, job => job.Paid);
    }

    /// <summary>
    ///     Marks a print job as paid.
    /// </summary>
    /// <param name="printJobId">The print job identifier to mark as paid.</param>
    /// <returns>A task that resolves to a TransactionResult indicating the outcome of the operation.</returns>
    public Task<TransactionResult> MarkPrintJobAsPaidAsync(long printJobId)
    {
        return SetPrintJobPaymentStatusAsync(printJobId, true);
    }

    /// <summary>
    ///     Marks a print job as unpaid.
    /// </summary>
    /// <param name="printJobId">The print job identifier to mark as unpaid.</param>
    /// <returns>A task that resolves to a TransactionResult indicating the outcome of the operation.</returns>
    public Task<TransactionResult> MarkPrintJobAsUnpaidAsync(long printJobId)
    {
        return SetPrintJobPaymentStatusAsync(printJobId, false);
    }

    /// <summary>
    ///     Updates the number of copies for a print job.
    /// </summary>
    /// <param name="printJobId">The print job identifier to update.</param>
    /// <param name="numCopies">The new number of copies.</param>
    /// <returns>A task that resolves to a TransactionResult indicating the outcome of the operation.</returns>
    public async Task<TransactionResult> UpdatePrintJobCopiesAsync(long printJobId, int numCopies)
    {
        if (numCopies < 0)
            return TransactionResult.Failed;

        return await UpdatePrintJobAsync(printJobId, job => job.NumCopies = numCopies, job => job.NumCopies);
    }

    /// <summary>
    ///     Marks a print job as completed and sets the CompletedAt timestamp.
    /// </summary>
    /// <param name="printJobId">The print job identifier to mark as completed.</param>
    /// <returns>A task that resolves to a TransactionResult indicating the outcome of the operation.</returns>
    public async Task<TransactionResult> MarkPrintJobCompletedAsync(long printJobId)
    {
        return await UpdatePrintJobAsync(
            printJobId,
            job =>
            {
                job.CompletedAt = DateTime.UtcNow;
                job.JobStatus = "completed";
            },
            job => job.CompletedAt, job => job.JobStatus);
    }

    /// <summary>
    ///     Ensures the database has a print record for each copy requested by the job.
    /// </summary>
    /// <param name="printJobId">The print job identifier.</param>
    /// <returns>A task that resolves to a TransactionResult indicating the outcome of the operation.</returns>
    public async Task<TransactionResult> CreatePrintCopiesAsync(long printJobId)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            var job = await _context.PrintJobs
                .FirstOrDefaultAsync(printJob => printJob.Id == printJobId);

            if (job == null)
            {
                await transaction.RollbackAsync();
                return TransactionResult.NotFound;
            }

            var completedCopies = await _context.Prints
                .CountAsync(print => print.PrintJobId == printJobId && print.PrintStatus == "completed");

            var desiredCopies = job.NumCopies - completedCopies;
            if (desiredCopies <= 0)
            {
                await transaction.RollbackAsync();
                return TransactionResult.NoAction;
            }

            for (var i = 0; i < desiredCopies; i++)
                await _context.Prints.AddAsync(new Print
                {
                    PrintJobId = job.Id,
                    PrinterId = null,
                    CreatedAt = DateTime.UtcNow,
                    PrintStatus = "pending"
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

    #endregion

    #region Deletion

    /// <summary>
    ///     Deletes a print job.
    /// </summary>
    /// <param name="printJobId">The print job identifier to delete.</param>
    /// <returns>A task that resolves to a TransactionResult indicating the outcome of the operation.</returns>
    public async Task<TransactionResult> DeletePrintJobAsync(long printJobId)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            // Check if entity exists using AsNoTracking
            var job = await PrintJobs.FirstOrDefaultAsync(printJob => printJob.Id == printJobId);

            if (job == null)
            {
                await transaction.RollbackAsync();
                return TransactionResult.NotFound;
            }

            // Get the tracked entity if it exists
            var trackedEntity = _context.ChangeTracker.Entries<PrintJob>()
                .FirstOrDefault(e => e.Entity.Id == printJobId);

            if (trackedEntity != null)
            {
                // Entity is already tracked, remove it directly
                _context.PrintJobs.Remove(trackedEntity.Entity);
            }
            else
            {
                // Entity is not tracked, attach and remove
                _context.PrintJobs.Attach(job);
                _context.PrintJobs.Remove(job);
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

    public async Task UpdatePrintJobMetaData(long printJobId, double printWeight, double printTime, int printModelId,
        int? materialId, long finishedBytePos)
    {
        await UpdatePrintJobAsync(
            printJobId,
            printJob =>
            {
                printJob.PrintWeight = printWeight;
                printJob.PrintTime = printTime;
                printJob.PrinterModelId = printModelId;
                printJob.MaterialId = materialId;
                printJob.FinishedBytePos = finishedBytePos;
            },
            printJob => printJob.PrintWeight,
            printJob => printJob.PrintTime,
            printJob => printJob.PrinterModelId,
            printJob => printJob.MaterialId,
            printJob => printJob.FinishedBytePos);
    }

    #endregion
}