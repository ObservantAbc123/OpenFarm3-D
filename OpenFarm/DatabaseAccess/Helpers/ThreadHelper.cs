using System.Linq;
using DatabaseAccess.Models;
using Microsoft.EntityFrameworkCore;

namespace DatabaseAccess.Helpers;

/// <summary>
/// Provides helper methods for accessing and modifying <see cref="Thread"/> entities in the database.
/// </summary>
/// <param name="context">The database context to use for operations.</param>
public class ThreadHelper(OpenFarmContext context) : BaseHelper(context)
{
    private const string StatusActive = "active";
    private const string StatusArchived = "archived";
    private const string StatusClosed = "closed";

    private static readonly HashSet<string> AllowedStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        StatusActive,
        StatusArchived,
        StatusClosed
    };

    private IQueryable<Models.Thread> ThreadsAsNoTracking => _context.Threads.AsNoTracking();

    private IQueryable<Models.Thread> OrderedThreads => ThreadsAsNoTracking
        .OrderByDescending(thread => thread.UpdatedAt);

    /// <summary>
    /// Retrieves all threads ordered by most recently updated first.
    /// </summary>
    /// <returns>A task that resolves to a list of all threads ordered by most recently updated first.</returns>
    public async Task<List<Models.Thread>> GetAllThreadsAsync() =>
        await OrderedThreads.ToListAsync();

    /// <summary>
    /// Retrieves the thread with the specified identifier.
    /// </summary>
    /// <param name="threadId">The identifier of the thread to retrieve.</param>
    /// <returns>A task that resolves to the thread, or null if not found.</returns>
    /// <param name="threadId">The unique identifier of the thread.</param>
    /// <returns>A task that resolves to the thread with the specified ID, or null if not found.</returns>
    public async Task<Models.Thread?> GetThreadByIdAsync(long threadId) =>
        await ThreadsAsNoTracking
            .FirstOrDefaultAsync(thread => thread.Id == threadId);

    /// <summary>
    /// Retrieves threads associated with a specific user, ordered by most recently updated first.
    /// </summary>
    /// <param name="userId">The user identifier to filter by.</param>
    /// <returns>A task that resolves to a list of threads for the specified user.</returns>
    public async Task<List<Models.Thread>> GetThreadsByUserIdAsync(long userId) =>
        await OrderedThreads
            .Where(thread => thread.UserId == userId)
            .ToListAsync();

    /// <summary>
    /// Retrieves threads with a specific status, ordered by most recently updated first.
    /// </summary>
    /// <param name="status">The thread status to filter by.</param>
    /// <returns>A task that resolves to a list of threads with the specified status.</returns>
    public async Task<List<Models.Thread>> GetThreadsByStatusAsync(string status)
    {
        var normalizedStatus = NormalizeStatus(status);

        if (normalizedStatus == null)
            return [];

        return await OrderedThreads
            .Where(thread => thread.ThreadStatus == normalizedStatus)
            .ToListAsync();
    }

    /// <summary>
    /// Retrieves threads associated with a specific job ID.
    /// </summary>
    /// <param name="jobId">The job identifier to filter by.</param>
    /// <returns>A task that resolves to a list of threads associated with the specified job ID.</returns>
    public async Task<List<Models.Thread>> GetThreadsByJobIdAsync(long jobId) =>
        await OrderedThreads
            .Where(thread => thread.JobId == jobId)
            .ToListAsync();

    /// <summary>
    /// Retrieves threads that are not associated with any job.
    /// </summary>
    /// <returns>A task that resolves to a list of threads that are not associated with any job.</returns>
    public async Task<List<Models.Thread>> GetUnassociatedThreadsAsync() =>
        await OrderedThreads
            .Where(thread => thread.JobId == null)
            .ToListAsync();

    /// <summary>
    /// Retrieves all active threads.
    /// </summary>
    /// <returns>A task that resolves to a list of all active threads.</returns>
    public Task<List<Models.Thread>> GetActiveThreadsAsync() =>
        GetThreadsByStatusAsync(StatusActive);

    /// <summary>
    /// Creates a new thread for a user.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="jobId">The optional associated job identifier.</param>
    /// <param name="threadStatus">The thread status.</param>
    /// <returns>A task that resolves to a TransactionResult indicating the outcome of the operation.</returns>
    public async Task<(TransactionResult Result, Models.Thread? Thread)> CreateThreadAsync(
        long userId,
        long? jobId = null,
        string? threadStatus = null)
    {
        try
        {
            // Validate user exists
            var userExists = await _context.Users
                .AsNoTracking()
                .AnyAsync(user => user.Id == userId);

            if (!userExists)
                return (TransactionResult.Failed, null);

            // Validate job ID exists if provided
            long? validatedJobId = null;
            if (jobId.HasValue)
            {
                var jobExists = await _context.PrintJobs
                    .AsNoTracking()
                    .AnyAsync(job => job.Id == jobId.Value);

                if (jobExists)
                {
                    validatedJobId = jobId.Value;
                }
            }

            var normalizedStatus = NormalizeStatus(threadStatus) ?? StatusActive;
            var now = DateTime.UtcNow;

            var thread = new Models.Thread
            {
                UserId = userId,
                JobId = validatedJobId,
                ThreadStatus = normalizedStatus,
                CreatedAt = now,
                UpdatedAt = now
            };

            await _context.Threads.AddAsync(thread);
            await _context.SaveChangesAsync();

            return (TransactionResult.Succeeded, thread);
        }
        catch
        {
            _context.ChangeTracker.Clear();
            return (TransactionResult.Failed, null);
        }
    }

    /// <summary>
    /// Updates the status for a thread.
    /// </summary>
    /// <param name="threadId">The identifier of the thread to update.</param>
    /// <param name="threadStatus">The new thread status.</param>
    /// <returns>A task that resolves to a TransactionResult indicating the outcome of the operation.</returns>
    public async Task<TransactionResult> UpdateThreadStatusAsync(long threadId, string threadStatus)
    {
        var normalizedStatus = NormalizeStatus(threadStatus);

        if (normalizedStatus == null)
            return TransactionResult.Failed;

        try
        {
            var thread = await _context.Threads
                .FirstOrDefaultAsync(t => t.Id == threadId);

            if (thread == null)
                return TransactionResult.NotFound;

            if (string.Equals(thread.ThreadStatus, normalizedStatus, StringComparison.OrdinalIgnoreCase))
                return TransactionResult.NoAction;

            thread.ThreadStatus = normalizedStatus;
            thread.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return TransactionResult.Succeeded;
        }
        catch
        {
            _context.ChangeTracker.Clear();
            return TransactionResult.Failed;
        }
    }

    /// <summary>
    /// Associates a thread with a job.
    /// </summary>
    /// <param name="threadId">The identifier of the thread to update.</param>
    /// <param name="jobId">The job identifier to associate with the thread.</param>
    /// <returns>A task that resolves to a TransactionResult indicating the outcome of the operation.</returns>
    public async Task<TransactionResult> AssociateThreadWithJobAsync(long threadId, long jobId)
    {
        try
        {
            var thread = await _context.Threads
                .FirstOrDefaultAsync(t => t.Id == threadId);

            if (thread == null)
                return TransactionResult.NotFound;

            // Validate job exists
            var jobExists = await _context.PrintJobs
                .AsNoTracking()
                .AnyAsync(job => job.Id == jobId);

            if (!jobExists)
                return TransactionResult.Failed;

            if (thread.JobId == jobId)
                return TransactionResult.NoAction;

            thread.JobId = jobId;
            thread.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return TransactionResult.Succeeded;
        }
        catch
        {
            _context.ChangeTracker.Clear();
            return TransactionResult.Failed;
        }
    }

    /// <summary>
    /// Updates the thread's last updated timestamp.
    /// </summary>
    /// <param name="threadId">The identifier of the thread to update.</param>
    /// <returns>A task that resolves to a TransactionResult indicating the outcome of the operation.</returns>
    public async Task<TransactionResult> TouchThreadAsync(long threadId)
    {
        try
        {
            var thread = await _context.Threads
                .FirstOrDefaultAsync(t => t.Id == threadId);

            if (thread == null)
                return TransactionResult.NotFound;

            thread.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return TransactionResult.Succeeded;
        }
        catch
        {
            _context.ChangeTracker.Clear();
            return TransactionResult.Failed;
        }
    }

    /// <summary>
    /// Marks the thread as active.
    /// </summary>
    /// <param name="threadId">The identifier of the thread to mark as active.</param>
    /// <returns>A task that resolves to a TransactionResult indicating the outcome of the operation.</returns>
    public Task<TransactionResult> ActivateThreadAsync(long threadId) =>
        UpdateThreadStatusAsync(threadId, StatusActive);

    /// <summary>
    /// Marks the thread as archived.
    /// </summary>
    /// <param name="threadId">The identifier of the thread to mark as archived.</param>
    /// <returns>A task that resolves to a TransactionResult indicating the outcome of the operation.</returns>
    public Task<TransactionResult> ArchiveThreadAsync(long threadId) =>
        UpdateThreadStatusAsync(threadId, StatusArchived);

    /// <summary>
    /// Marks the thread as closed.
    /// </summary>
    /// <param name="threadId">The identifier of the thread to mark as closed.</param>
    /// <returns>A task that resolves to a TransactionResult indicating the outcome of the operation.</returns>
    public Task<TransactionResult> CloseThreadAsync(long threadId) =>
        UpdateThreadStatusAsync(threadId, StatusClosed);

    /// <summary>
    /// Deletes the specified thread and all its messages.
    /// </summary>
    /// <param name="threadId">The identifier of the thread to delete.</param>
    /// <returns>A task that resolves to a TransactionResult indicating the outcome of the operation.</returns>
    public async Task<TransactionResult> DeleteThreadAsync(long threadId)
    {
        if (threadId <= 0)
            return TransactionResult.Failed;

        try
        {
            var thread = await _context.Threads
                .Include(t => t.Messages)
                .FirstOrDefaultAsync(t => t.Id == threadId);

            if (thread == null)
                return TransactionResult.NotFound;

            _context.Threads.Remove(thread);
            await _context.SaveChangesAsync();
            return TransactionResult.Succeeded;
        }
        catch
        {
            _context.ChangeTracker.Clear();
            return TransactionResult.Failed;
        }
    }

    private static string? NormalizeStatus(string? threadStatus)
    {
        if (string.IsNullOrWhiteSpace(threadStatus))
            return null;

        var normalized = threadStatus.Trim().ToLowerInvariant();
        return AllowedStatuses.Contains(normalized) ? normalized : null;
    }
}
