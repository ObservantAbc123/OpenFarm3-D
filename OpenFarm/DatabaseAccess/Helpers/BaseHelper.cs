using DatabaseAccess.Models;
using Microsoft.EntityFrameworkCore;

namespace DatabaseAccess.Helpers;

/// <summary>
///     Base class for all database helper classes, providing shared database context access.
/// </summary>
/// <remarks>
///     Initializes a new instance of the <see cref="BaseHelper" /> class.
/// </remarks>
/// <param name="context">The database context to use for operations.</param>
public abstract class BaseHelper(OpenFarmContext context)
{
    /// <summary>
    ///     The database context for accessing OpenFarm entities.
    /// </summary>
    protected readonly OpenFarmContext _context = context;

    /// <summary>
    ///     Determines if the exception is due to a unique constraint violation.
    /// </summary>
    /// <param name="ex">The database update exception to check.</param>
    /// <returns>True if the exception is due to a unique constraint violation, false otherwise.</returns>
    protected static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        // PostgreSQL unique constraint violation error codes and messages
        var message = ex.InnerException?.Message;
        return message != null && (
            message.Contains("duplicate key value violates unique constraint") ||
            message.Contains("23505") ||
            message.Contains("unique constraint"));
    }
}