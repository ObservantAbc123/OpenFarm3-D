using System.Linq.Expressions;
using DatabaseAccess.Models;
using Microsoft.EntityFrameworkCore;

namespace DatabaseAccess.Helpers;

/// <summary>
///     Provides helper methods for accessing and modifying <see cref="User" /> entities in the database.
/// </summary>
/// <remarks>
///     Initializes a new instance of the <see cref="UserHelper" /> class.
/// </remarks>
/// <param name="context">The database context to use for operations.</param>
public class UserHelper(OpenFarmContext context) : BaseHelper(context)
{
    private IQueryable<User> Users => _context.Users.AsNoTracking();

    /// <summary>
    ///     Retrieves every user.
    /// </summary>
    /// <returns>A task that resolves to a list of all users.</returns>
    public async Task<List<User>> GetUsersAsync()
    {
        return await Users.ToListAsync();
    }

    /// <summary>
    ///     Retrieves the user with the specified identifier.
    /// </summary>
    /// <param name="userId">The user identifier to look up.</param>
    /// <returns>A task that resolves to the user, or null if not found.</returns>
    public async Task<User?> GetUserAsync(long userId)
    {
        return await Users.FirstOrDefaultAsync(user => user.Id == userId);
    }

    /// <summary>
    ///     Retrieves the user with the specified name.
    /// </summary>
    /// <param name="userName">The user name to look up.</param>
    /// <returns>A task that resolves to the user, or null if not found.</returns>
    public async Task<User?> GetUserByNameAsync(string userName)
    {
        return await Users.FirstOrDefaultAsync(user => user.Name == userName);
    }

    /// <summary>
    ///     Retrieves the user with the specified organization identifier.
    /// </summary>
    /// <param name="orgId">The organization identifier to look up.</param>
    /// <returns>A task that resolves to the user, or null if not found.</returns>
    public async Task<User?> GetUserByOrgIdAsync(string orgId)
    {
        return await Users.FirstOrDefaultAsync(user => user.OrgId == orgId);
    }

    /// <summary>
    ///     Retrieves users filtered by verification status.
    /// </summary>
    /// <param name="verified">The verification status to filter by.</param>
    /// <returns>A task that resolves to a list of users with the specified verification status.</returns>
    public async Task<List<User>> GetUsersByVerificationStatusAsync(bool verified) =>
        await Users.Where(user => (user.Verified ?? false) == verified).ToListAsync();

    /// <summary>
    ///     Retrieves verified users.
    /// </summary>
    /// <returns>A task that resolves to a list of verified users.</returns>
    public Task<List<User>> GetVerifiedUsersAsync()
    {
        return GetUsersByVerificationStatusAsync(true);
    }

    /// <summary>
    ///     Retrieves unverified users.
    /// </summary>
    /// <returns>A task that resolves to a list of unverified users.</returns>
    public Task<List<User>> GetUnverifiedUsersAsync()
    {
        return GetUsersByVerificationStatusAsync(false);
    }

    /// <summary>
    ///     Retrieves users filtered by suspension status.
    /// </summary>
    /// <param name="suspended">The suspension status to filter by.</param>
    /// <returns>A task that resolves to a list of users with the specified suspension status.</returns>
    public async Task<List<User>> GetUsersBySuspensionStatusAsync(bool suspended)
    {
        return await Users.Where(user => user.Suspended == suspended).ToListAsync();
    }

    /// <summary>
    ///     Retrieves suspended users.
    /// </summary>
    /// <returns>A task that resolves to a list of suspended users.</returns>
    public Task<List<User>> GetSuspendedUsersAsync()
    {
        return GetUsersBySuspensionStatusAsync(true);
    }

    /// <summary>
    ///     Retrieves active users (verified and not suspended).
    /// </summary>
    /// <returns>A task that resolves to a list of active users.</returns>
    public async Task<List<User>> GetActiveUsersAsync() =>
        await Users.Where(user => (user.Verified ?? false) && !user.Suspended).ToListAsync();

    /// <summary>
    ///     Gets the verification status of a user.
    /// </summary>
    /// <param name="userId">The user identifier to look up.</param>
    /// <returns>A task that resolves to the user's verification status, or null if not found.</returns>
    public async Task<bool?> GetUserVerificationStatusAsync(long userId)
    {
        return await Users
            .Where(user => user.Id == userId)
            .Select(user => user.Verified)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    ///     Gets the suspension status of a user.
    /// </summary>
    /// <param name="userId">The user identifier to look up.</param>
    /// <returns>A task that resolves to the user's suspension status, or null if not found.</returns>
    public async Task<bool?> GetUserSuspensionStatusAsync(long userId)
    {
        return await Users
            .Where(user => user.Id == userId)
            .Select(user => user.Suspended)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    ///     Gets the creation date of a user.
    /// </summary>
    /// <param name="userId">The user identifier to look up.</param>
    /// <returns>A task that resolves to the user's creation date, or null if not found.</returns>
    public async Task<DateTime?> GetUserCreatedAtAsync(long userId)
    {
        return await Users
            .Where(user => user.Id == userId)
            .Select(user => user.CreatedAt)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    ///     Creates a user when the supplied name is unique.
    /// </summary>
    /// <param name="userName">The name of the user to create.</param>
    /// <returns>A task that resolves to a TransactionResult indicating the outcome of the operation.</returns>
    public async Task<TransactionResult> CreateUserAsync(string userName)
    {
        if (string.IsNullOrWhiteSpace(userName))
            return TransactionResult.Failed;

        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            var exists = await Users.AnyAsync(user => user.Name == userName);

            if (exists)
            {
                await transaction.RollbackAsync();
                return TransactionResult.NoAction;
            }

            await _context.Users.AddAsync(new User
            {
                Name = userName,
                Verified = false,
                Suspended = false,
                CreatedAt = DateTime.UtcNow
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
    ///     Ensures the supplied name exists, creating or returning the existing user.
    /// </summary>
    /// <param name="primaryEmail">The primary email address for the user.</param>
    /// <param name="userName">The name of the user.</param>
    /// <returns>A task that resolves to the created or existing user.</returns>
    public async Task<User> CreateOrGetUserAsync(string primaryEmail, string userName)
    {
        var existingUser = await Users.FirstOrDefaultAsync(user => user.Name == userName);
        if (existingUser != null)
            return existingUser;

        var user = new User
        {
            Name = userName,
            Verified = false,
            Suspended = false,
            CreatedAt = DateTime.UtcNow
        };

        var email = new Email
        {
            EmailAddress = primaryEmail,
            UserId = user.Id,
            IsPrimary = true
        };

        await _context.Users.AddAsync(user);
        await _context.Emails.AddAsync(email);
        await _context.SaveChangesAsync();
        return user;
    }

    /// <summary>
    ///     Updates the verification status.
    /// </summary>
    /// <param name="userId">The user identifier to update.</param>
    /// <param name="verified">The verification status to set.</param>
    /// <returns>A task that resolves to a TransactionResult indicating the outcome of the operation.</returns>
    public async Task<TransactionResult> SetUserVerificationStatusAsync(long userId, bool verified)
    {
        return await UpdateUserAsync(userId, user => user.Verified = verified, user => user.Verified);
    }

    /// <summary>
    ///     Verifies a user.
    /// </summary>
    /// <param name="userId">The user identifier to verify.</param>
    /// <returns>A task that resolves to a TransactionResult indicating the outcome of the operation.</returns>
    public Task<TransactionResult> VerifyUserAsync(long userId)
    {
        return SetUserVerificationStatusAsync(userId, true);
    }

    /// <summary>
    ///     Unverifies a user.
    /// </summary>
    /// <param name="userId">The user identifier to unverify.</param>
    /// <returns>A task that resolves to a TransactionResult indicating the outcome of the operation.</returns>
    public Task<TransactionResult> UnverifyUserAsync(long userId)
    {
        return SetUserVerificationStatusAsync(userId, false);
    }

    /// <summary>
    ///     Updates the suspension status.
    /// </summary>
    /// <param name="userId">The user identifier to update.</param>
    /// <param name="suspended">The suspension status to set.</param>
    /// <returns>A task that resolves to a TransactionResult indicating the outcome of the operation.</returns>
    public async Task<TransactionResult> SetUserSuspensionStatusAsync(long userId, bool suspended)
    {
        return await UpdateUserAsync(userId, user => user.Suspended = suspended, user => user.Suspended);
    }

    /// <summary>
    ///     Suspends a user.
    /// </summary>
    /// <param name="userId">The user identifier to suspend.</param>
    /// <returns>A task that resolves to a TransactionResult indicating the outcome of the operation.</returns>
    public Task<TransactionResult> SuspendUserAsync(long userId)
    {
        return SetUserSuspensionStatusAsync(userId, true);
    }

    /// <summary>
    ///     Unsuspends a user.
    /// </summary>
    /// <param name="userId">The user identifier to unsuspend.</param>
    /// <returns>A task that resolves to a TransactionResult indicating the outcome of the operation.</returns>
    public Task<TransactionResult> UnsuspendUserAsync(long userId)
    {
        return SetUserSuspensionStatusAsync(userId, false);
    }

    /// <summary>
    ///     Asynchronously creates a new <see cref="User" /> with the specified name. If the user exists already, returns that
    ///     user.
    /// </summary>
    /// <param name="email">The email of the new user.</param>
    /// <param name="userName">The name of the new user.</param>
    /// <returns>
    ///     A task that resolves to a <see cref="User" />. Returns the user if they existed already or if they were created.
    ///     Returns null in the case of an error.
    /// </returns>
    public async Task<User?> CreateOrGetUserByEmailAsync(string email, string userName)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            var existingUser = await _context.Emails
                .AsNoTracking()
                .Where(e => e.EmailAddress == email && e.IsPrimary)
                .Select(e => e.User)
                .FirstOrDefaultAsync();

            if (existingUser != null)
                return existingUser;

            var user = new User
            {
                Name = userName,
                Verified = false,
                Suspended = false
            };

            await _context.Users.AddAsync(user);
            await _context.Emails.AddAsync(new Email { IsPrimary = true, EmailAddress = email, User = user });
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return user;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    /// <summary>
    ///     Updates arbitrary fields on the user using the supplied action.
    /// </summary>
    /// <param name="userId">The user identifier to update.</param>
    /// <param name="applyUpdates">The action to apply updates to the user.</param>
    /// <param name="modifiedProperties">The properties that were modified.</param>
    /// <returns>A task that resolves to a TransactionResult indicating the outcome of the operation.</returns>
    public async Task<TransactionResult> UpdateUserAsync(long userId, Action<User> applyUpdates,
        params Expression<Func<User, object?>>[] modifiedProperties)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            var exists = await Users.AnyAsync(user => user.Id == userId);

            if (!exists)
            {
                await transaction.RollbackAsync();
                return TransactionResult.NotFound;
            }

            // Get the tracked entity if it exists, or null
            var trackedEntity = _context.ChangeTracker.Entries<User>()
                .FirstOrDefault(e => e.Entity.Id == userId);

            if (trackedEntity != null)
                // Detach the existing tracked entity
                trackedEntity.State = EntityState.Detached;

            var trackedUser = new User { Id = userId };
            _context.Users.Attach(trackedUser);

            applyUpdates(trackedUser);

            var entry = _context.Entry(trackedUser);
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

    /// <summary>
    ///     Deletes the user with the specified identifier.
    /// </summary>
    /// <param name="userId">The user identifier to delete.</param>
    /// <returns>A task that resolves to a TransactionResult indicating the outcome of the operation.</returns>
    public async Task<TransactionResult> DeleteUserAsync(long userId)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            var user = await Users.FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
            {
                await transaction.RollbackAsync();
                return TransactionResult.NotFound;
            }

            // Get the tracked entity if it exists
            var trackedEntity = _context.ChangeTracker.Entries<User>()
                .FirstOrDefault(e => e.Entity.Id == userId);

            if (trackedEntity != null)
            {
                // Entity is already tracked, remove it directly
                _context.Users.Remove(trackedEntity.Entity);
            }
            else
            {
                // Entity is not tracked, attach and remove
                _context.Users.Attach(user);
                _context.Users.Remove(user);
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
    ///     Deletes the supplied user.
    /// </summary>
    /// <param name="user">The user entity to delete.</param>
    /// <returns>A task that resolves to a TransactionResult indicating the outcome of the operation.</returns>
    public Task<TransactionResult> DeleteUserAsync(User user)
    {
        return user == null ? Task.FromResult(TransactionResult.NotFound) : DeleteUserAsync(user.Id);
    }
}
