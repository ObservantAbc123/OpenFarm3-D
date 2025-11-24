using DatabaseAccess.Models;
using Microsoft.EntityFrameworkCore;

namespace DatabaseAccess.Helpers;

/// <summary>
///     Provides helper methods for accessing and modifying <see cref="Email" /> entities in the database.
/// </summary>
/// <param name="context">The database context to use for operations.</param>
public class EmailHelper(OpenFarmContext context) : BaseHelper(context)
{
    /// <summary>
    ///     Gets a queryable collection of emails that will not be tracked by the context.
    /// </summary>
    private IQueryable<Email> EmailsAsNoTracking => _context.Emails.AsNoTracking();

    /// <summary>
    ///     Gets the no-tracking collection of emails ordered alphabetically by address.
    /// </summary>
    private IQueryable<Email> OrderedEmails => EmailsAsNoTracking.OrderBy(email => email.EmailAddress);

    /// <summary>
    ///     Trims whitespace from the provided email address without altering the original casing.
    /// </summary>
    /// <param name="emailAddress">The email address to sanitize.</param>
    /// <returns>The sanitized email address.</returns>
    private static string SanitizeEmailAddress(string emailAddress)
    {
        return emailAddress.Trim();
    }

    /// <summary>
    ///     Retrieves all email entities ordered alphabetically.
    /// </summary>
    /// <returns>A list of <see cref="Email" /> entities.</returns>
    public async Task<List<Email>> GetEmailsAsync()
    {
        return await OrderedEmails.ToListAsync();
    }

    /// <summary>
    ///     Retrieves all email addresses ordered alphabetically.
    /// </summary>
    /// <returns>A list of email address strings.</returns>
    public async Task<List<string>> GetEmailAddressesAsync()
    {
        return await OrderedEmails.Select(email => email.EmailAddress).ToListAsync();
    }

    /// <summary>
    ///     Gets all email entities belonging to the specified user.
    /// </summary>
    /// <param name="userId">The identifier of the user.</param>
    /// <returns>A list of the user's <see cref="Email" /> entities, or an empty list when none exist.</returns>
    public async Task<List<Email>> GetUserEmailsAsync(long userId)
    {
        if (userId <= 0)
            return [];

        return await OrderedEmails
            .Where(email => email.UserId == userId)
            .ToListAsync();
    }

    /// <summary>
    ///     Gets the primary email entity for the specified user.
    /// </summary>
    /// <param name="userId">The identifier of the user.</param>
    /// <returns>The primary <see cref="Email" />, or <c>null</c> if none is set.</returns>
    public async Task<Email?> GetUserPrimaryEmailAsync(long userId)
    {
        if (userId <= 0)
            return null;

        return await EmailsAsNoTracking
            .FirstOrDefaultAsync(email => email.UserId == userId && email.IsPrimary);
    }

    /// <summary>
    ///     Resolves the user identifier associated with the provided primary email address.
    /// </summary>
    /// <param name="primaryEmail">The primary email address.</param>
    /// <returns>The user identifier, or <c>null</c> when a match is not found.</returns>
    public async Task<long?> GetUserIdByPrimaryEmailAsync(string primaryEmail)
    {
        if (string.IsNullOrWhiteSpace(primaryEmail))
            return null;

        return await EmailsAsNoTracking
            .Where(email => email.EmailAddress == SanitizeEmailAddress(primaryEmail) && email.IsPrimary)
            .Select(email => (long?)email.UserId)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    ///     Gets all non-primary email entities for the specified user.
    /// </summary>
    /// <param name="userId">The identifier of the user.</param>
    /// <returns>A list of non-primary <see cref="Email" /> entities, or an empty list when none exist.</returns>
    public async Task<List<Email>> GetUserNonPrimaryEmailsAsync(long userId)
    {
        if (userId <= 0)
            return [];

        return await OrderedEmails
            .Where(email => email.UserId == userId && !email.IsPrimary)
            .ToListAsync();
    }

    /// <summary>
    ///     Gets all email addresses belonging to the specified user.
    /// </summary>
    /// <param name="userId">The identifier of the user.</param>
    /// <returns>A list of email address strings, or an empty list when none exist.</returns>
    public async Task<List<string>> GetUserEmailAddressesAsync(long userId)
    {
        if (userId <= 0)
            return [];

        return await OrderedEmails
            .Where(email => email.UserId == userId)
            .Select(email => email.EmailAddress)
            .ToListAsync();
    }

    /// <summary>
    ///     Gets the primary email address for the specified user.
    /// </summary>
    /// <param name="userId">The identifier of the user.</param>
    /// <returns>The primary email address, or <c>null</c> if none is set.</returns>
    public async Task<string?> GetUserPrimaryEmailAddressAsync(long userId)
    {
        if (userId <= 0)
            return null;

        return await EmailsAsNoTracking
            .Where(email => email.UserId == userId && email.IsPrimary)
            .Select(email => email.EmailAddress)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    ///     Gets all non-primary email addresses for the specified user.
    /// </summary>
    /// <param name="userId">The identifier of the user.</param>
    /// <returns>A list of non-primary email address strings, or an empty list when none exist.</returns>
    public async Task<List<string>> GetUserNonPrimaryEmailAddressesAsync(long userId)
    {
        if (userId <= 0)
            return [];

        return await OrderedEmails
            .Where(email => email.UserId == userId && !email.IsPrimary)
            .Select(email => email.EmailAddress)
            .ToListAsync();
    }

    /// <summary>
    ///     Gets the user identifiers that are associated with the specified email address.
    /// </summary>
    /// <param name="emailAddress">The email address to search.</param>
    /// <returns>A list of user identifiers, or an empty list when no users are linked.</returns>
    public async Task<List<long>> GetEmailsUserIdsAsync(string emailAddress)
    {
        if (string.IsNullOrWhiteSpace(emailAddress))
            return [];

        return await EmailsAsNoTracking
            .Where(email => email.EmailAddress == SanitizeEmailAddress(emailAddress))
            .Select(email => email.UserId)
            .ToListAsync();
    }

    /// <summary>
    ///     Creates a non-primary email for the specified user when it does not already exist.
    /// </summary>
    /// <param name="userId">The identifier of the user.</param>
    /// <param name="emailAddress">The email address to associate with the user.</param>
    /// <returns>A <see cref="TransactionResult" /> indicating the outcome of the operation.</returns>
    public async Task<TransactionResult> CreateNonPrimaryEmailAsync(long userId, string emailAddress)
    {
        if (userId <= 0 || string.IsNullOrWhiteSpace(emailAddress))
            return TransactionResult.Failed;

        try
        {
            var userExists = await _context.Users
                .AsNoTracking()
                .AnyAsync(user => user.Id == userId);

            if (!userExists)
                return TransactionResult.NotFound;

            var sanitizedEmail = SanitizeEmailAddress(emailAddress);

            var exists = await EmailsAsNoTracking
                .AnyAsync(email => email.UserId == userId && email.EmailAddress == sanitizedEmail);

            if (exists)
                return TransactionResult.NoAction;

            _context.Emails.Add(new Email
            {
                UserId = userId,
                EmailAddress = sanitizedEmail,
                IsPrimary = false
            });

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
    ///     Creates non-primary emails for the specified user, skipping addresses that already exist.
    /// </summary>
    /// <param name="userId">The identifier of the user.</param>
    /// <param name="emailAddresses">The collection of email addresses to associate with the user.</param>
    /// <returns>A <see cref="TransactionResult" /> indicating the outcome of the operation.</returns>
    public async Task<TransactionResult> CreateNonPrimaryEmailsAsync(long userId, List<string> emailAddresses)
    {
        if (userId <= 0 || emailAddresses == null || emailAddresses.Count == 0)
            return TransactionResult.Failed;

        try
        {
            var userExists = await _context.Users
                .AsNoTracking()
                .AnyAsync(user => user.Id == userId);

            if (!userExists)
                return TransactionResult.NotFound;

            var sanitizedAddresses = emailAddresses
                .Select(address => address.Trim())
                .Where(address => !string.IsNullOrWhiteSpace(address))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (sanitizedAddresses.Count == 0)
                return TransactionResult.NoAction;

            var existingAddresses = await EmailsAsNoTracking
                .Where(email => email.UserId == userId)
                .Select(email => email.EmailAddress)
                .ToListAsync();

            var newEmails = sanitizedAddresses
                .Except(existingAddresses, StringComparer.OrdinalIgnoreCase)
                .Select(address => new Email
                {
                    UserId = userId,
                    EmailAddress = address,
                    IsPrimary = false
                })
                .ToList();

            if (newEmails.Count == 0)
                return TransactionResult.NoAction;

            await _context.Emails.AddRangeAsync(newEmails);
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
    ///     Creates a primary email for the specified user when the user currently has no primary email.
    /// </summary>
    /// <param name="userId">The identifier of the user.</param>
    /// <param name="emailAddress">The email address to set as primary.</param>
    /// <returns>A <see cref="TransactionResult" /> indicating the outcome of the operation.</returns>
    public async Task<TransactionResult> CreatePrimaryEmailRestrictedAsync(long userId, string emailAddress)
    {
        if (userId <= 0 || string.IsNullOrWhiteSpace(emailAddress))
            return TransactionResult.Failed;

        try
        {
            var userExists = await _context.Users
                .AsNoTracking()
                .AnyAsync(user => user.Id == userId);

            if (!userExists)
                return TransactionResult.NotFound;

            var sanitizedEmail = SanitizeEmailAddress(emailAddress);

            var existingEmail = await EmailsAsNoTracking
                .AnyAsync(email => email.UserId == userId && email.EmailAddress == sanitizedEmail);

            if (existingEmail)
                return TransactionResult.NoAction;

            var hasPrimary = await EmailsAsNoTracking
                .AnyAsync(email => email.UserId == userId && email.IsPrimary);

            if (hasPrimary)
                return TransactionResult.NoAction;

            _context.Emails.Add(new Email
            {
                UserId = userId,
                EmailAddress = sanitizedEmail,
                IsPrimary = true
            });

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
    ///     Creates or promotes a primary email for the user, demoting any existing primary.
    /// </summary>
    /// <param name="userId">The identifier of the user.</param>
    /// <param name="emailAddress">The email address to set as primary.</param>
    /// <returns>A <see cref="TransactionResult" /> indicating the outcome of the operation.</returns>
    public async Task<TransactionResult> CreatePrimaryCascadingEmailAsync(long userId, string emailAddress)
    {
        if (userId <= 0 || string.IsNullOrWhiteSpace(emailAddress))
            return TransactionResult.Failed;

        try
        {
            var userExists = await _context.Users
                .AsNoTracking()
                .AnyAsync(user => user.Id == userId);

            if (!userExists)
                return TransactionResult.NotFound;

            var sanitizedEmail = SanitizeEmailAddress(emailAddress);

            var existingEmail = await EmailsAsNoTracking
                .FirstOrDefaultAsync(email => email.UserId == userId && email.EmailAddress == sanitizedEmail);

            if (existingEmail != null && existingEmail.IsPrimary)
                return TransactionResult.NoAction;

            var currentPrimary = await EmailsAsNoTracking
                .FirstOrDefaultAsync(email => email.UserId == userId && email.IsPrimary);

            if (currentPrimary != null)
                currentPrimary.IsPrimary = false;

            if (existingEmail != null)
                existingEmail.IsPrimary = true;
            else
                _context.Emails.Add(new Email
                {
                    UserId = userId,
                    EmailAddress = sanitizedEmail,
                    IsPrimary = true
                });

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
    ///     Deletes the specified non-primary email address when it exists.
    /// </summary>
    /// <param name="emailAddress">The email address to delete.</param>
    /// <returns>A <see cref="TransactionResult" /> indicating the outcome of the operation.</returns>
    public async Task<TransactionResult> DeleteEmailAsync(string emailAddress)
    {
        if (string.IsNullOrWhiteSpace(emailAddress))
            return TransactionResult.Failed;

        try
        {
            var sanitizedEmail = SanitizeEmailAddress(emailAddress);

            var email = await EmailsAsNoTracking
                .FirstOrDefaultAsync(entity => entity.EmailAddress == sanitizedEmail);

            if (email == null)
                return TransactionResult.NotFound;

            if (email.IsPrimary)
                return TransactionResult.Abandoned;

            _context.Emails.Remove(email);
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
    ///     Deletes all non-primary email addresses for the specified user.
    /// </summary>
    /// <param name="userId">The identifier of the user.</param>
    /// <returns>A <see cref="TransactionResult" /> indicating the outcome of the operation.</returns>
    public async Task<TransactionResult> DeleteAllNonPrimaryEmailsAsync(long userId)
    {
        if (userId <= 0)
            return TransactionResult.Failed;

        try
        {
            var userExists = await _context.Users
                .AsNoTracking()
                .AnyAsync(user => user.Id == userId);

            if (!userExists)
                return TransactionResult.NotFound;

            var nonPrimaryEmails = await EmailsAsNoTracking
                .Where(email => email.UserId == userId && !email.IsPrimary)
                .ToListAsync();

            if (nonPrimaryEmails.Count == 0)
                return TransactionResult.NoAction;

            _context.Emails.RemoveRange(nonPrimaryEmails);
            await _context.SaveChangesAsync();
            return TransactionResult.Succeeded;
        }
        catch
        {
            _context.ChangeTracker.Clear();
            return TransactionResult.Failed;
        }
    }
}