using System.Linq;
using DatabaseAccess.Models;
using Microsoft.EntityFrameworkCore;

namespace DatabaseAccess.Helpers;

/// <summary>
/// Provides helper methods for accessing and modifying <see cref="Message"/> entities in the database.
/// </summary>
/// <param name="context">The database context to use for operations.</param>
public class MessageHelper(OpenFarmContext context) : BaseHelper(context)
{
    private const string StatusUnseen = "unseen";
    private const string StatusSeen = "seen";
    private const string StatusProcessed = "processed";

    private const string TypeEmail = "email";
    private const string TypeSystem = "system";

    private const string SenderUser = "user";
    private const string SenderSystem = "system";

    private static readonly HashSet<string> AllowedStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        StatusUnseen,
        StatusSeen,
        StatusProcessed
    };

    private static readonly HashSet<string> AllowedMessageTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        TypeEmail,
        TypeSystem
    };

    private static readonly HashSet<string> AllowedSenderTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        SenderUser,
        SenderSystem
    };

    private IQueryable<Message> MessagesAsNoTracking => _context.Messages.AsNoTracking();

    private IQueryable<Message> OrderedMessages => MessagesAsNoTracking
        .OrderBy(message => message.CreatedAt);

    /// <summary>
    /// Retrieves all messages for a specific thread ordered by creation time.
    /// </summary>
    /// <param name="threadId">The thread identifier to filter by.</param>
    /// <returns>A task that resolves to a list of messages in the specified thread.</returns>
    public async Task<List<Message>> GetMessagesByThreadAsync(long threadId) =>
        await OrderedMessages
            .Where(message => message.ThreadId == threadId)
            .ToListAsync();

    /// <summary>
    /// Retrieves the message with the specified identifier.
    /// </summary>
    /// <param name="messageId">The identifier of the message to retrieve.</param>
    /// <returns>A task that resolves to the message, or null if not found.</returns>
    public async Task<Message?> GetMessageAsync(long messageId) =>
        await MessagesAsNoTracking
            .Include(m => m.Thread)
            .FirstOrDefaultAsync(message => message.Id == messageId);

    /// <summary>
    /// Retrieves messages filtered by status.
    /// </summary>
    /// <param name="messageStatus">The message status to filter by.</param>
    /// <returns>A task that resolves to a list of messages with the specified status.</returns>
    public async Task<List<Message>> GetMessagesByStatusAsync(string messageStatus)
    {
        var normalizedStatus = NormalizeStatus(messageStatus);

        if (normalizedStatus == null)
            return [];

        return await OrderedMessages
            .Where(message => message.MessageStatus == normalizedStatus)
            .ToListAsync();
    }

    /// <summary>
    /// Retrieves messages filtered by type.
    /// </summary>
    /// <param name="messageType">The message type to filter by (email or system).</param>
    /// <returns>A task that resolves to a list of messages with the specified type.</returns>
    public async Task<List<Message>> GetMessagesByTypeAsync(string messageType)
    {
        var normalizedType = NormalizeMessageType(messageType);

        if (normalizedType == null)
            return [];

        return await OrderedMessages
            .Where(message => message.MessageType == normalizedType)
            .ToListAsync();
    }

    /// <summary>
    /// Retrieves messages from emails (not system messages).
    /// </summary>
    /// <param name="fromEmailAddress">The sender email address to filter by.</param>
    /// <returns>A task that resolves to a list of email messages from the specified sender.</returns>
    public async Task<List<Message>> GetEmailMessagesFromAsync(string fromEmailAddress)
    {
        if (string.IsNullOrWhiteSpace(fromEmailAddress))
            return [];

        var sanitizedAddress = TrimEmailAddress(fromEmailAddress);

        return await OrderedMessages
            .Where(message => message.MessageType == TypeEmail &&
                             message.FromEmailAddress == sanitizedAddress)
            .ToListAsync();
    }

    /// <summary>
    /// Retrieves all unseen messages.
    /// </summary>
    /// <returns>A task that resolves to a list of all unseen messages.</returns>
    public Task<List<Message>> GetUnseenMessagesAsync() =>
        GetMessagesByStatusAsync(StatusUnseen);

    /// <summary>
    /// Creates a new email message in a thread.
    /// </summary>
    /// <param name="threadId">The thread identifier.</param>
    /// <param name="fromEmailAddress">The sender email address.</param>
    /// <param name="messageSubject">The message subject.</param>
    /// <param name="messageContent">The message content.</param>
    /// <param name="senderType">The sender type (user or system).</param>
    /// <param name="messageStatus">The message status.</param>
    /// <param name="createdAtUtc">The UTC date/time when the message was created.</param>
    /// <returns>A task that resolves to a TransactionResult indicating the outcome of the operation.</returns>
    public async Task<(TransactionResult Result, Message? Message)> CreateEmailMessageAsync(
        long threadId,
        string fromEmailAddress,
        string messageSubject,
        string messageContent,
        string? senderType = null,
        string? messageStatus = null,
        DateTime? createdAtUtc = null)
    {
        if (string.IsNullOrWhiteSpace(fromEmailAddress) || string.IsNullOrWhiteSpace(messageContent))
            return (TransactionResult.Failed, null);

        try
        {
            // Validate thread exists
            var threadExists = await _context.Threads
                .AsNoTracking()
                .AnyAsync(thread => thread.Id == threadId);

            if (!threadExists)
                return (TransactionResult.Failed, null);

            var sanitizedAddress = TrimEmailAddress(fromEmailAddress);
            var sanitizedContent = messageContent.Trim();
            var sanitizedSubject = string.IsNullOrWhiteSpace(messageSubject)
                ? "(No subject)"
                : messageSubject.Trim();

            if (sanitizedContent.Length == 0)
                return (TransactionResult.Failed, null);

            var normalizedStatus = NormalizeStatus(messageStatus) ?? StatusUnseen;
            var normalizedSenderType = NormalizeSenderType(senderType) ?? SenderUser;

            var message = new Message
            {
                ThreadId = threadId,
                MessageContent = sanitizedContent,
                MessageSubject = sanitizedSubject,
                MessageType = TypeEmail,
                SenderType = normalizedSenderType,
                FromEmailAddress = sanitizedAddress,
                MessageStatus = normalizedStatus,
                CreatedAt = (createdAtUtc ?? DateTime.UtcNow).ToUniversalTime()
            };

            await _context.Messages.AddAsync(message);

            // Update thread's updated_at timestamp
            await UpdateThreadTimestampAsync(threadId);

            await _context.SaveChangesAsync();

            return (TransactionResult.Succeeded, message);
        }
        catch
        {
            _context.ChangeTracker.Clear();
            return (TransactionResult.Failed, null);
        }
    }

    /// <summary>
    /// Creates a new system message in a thread.
    /// </summary>
    /// <param name="threadId">The thread identifier.</param>
    /// <param name="messageContent">The message content.</param>
    /// <param name="messageSubject">The optional message subject.</param>
    /// <param name="messageStatus">The message status.</param>
    /// <param name="createdAtUtc">The UTC date/time when the message was created.</param>
    /// <returns>A task that resolves to a TransactionResult indicating the outcome of the operation.</returns>
    public async Task<(TransactionResult Result, Message? Message)> CreateSystemMessageAsync(
        long threadId,
        string messageContent,
        string? messageSubject = null,
        string? messageStatus = null,
        DateTime? createdAtUtc = null)
    {
        if (string.IsNullOrWhiteSpace(messageContent))
            return (TransactionResult.Failed, null);

        try
        {
            // Validate thread exists
            var threadExists = await _context.Threads
                .AsNoTracking()
                .AnyAsync(thread => thread.Id == threadId);

            if (!threadExists)
                return (TransactionResult.Failed, null);

            var sanitizedContent = messageContent.Trim();
            var sanitizedSubject = string.IsNullOrWhiteSpace(messageSubject)
                ? null
                : messageSubject.Trim();

            var normalizedStatus = NormalizeStatus(messageStatus) ?? StatusProcessed; // System messages are typically processed immediately

            var message = new Message
            {
                ThreadId = threadId,
                MessageContent = sanitizedContent,
                MessageSubject = sanitizedSubject,
                MessageType = TypeSystem,
                SenderType = SenderSystem,
                FromEmailAddress = null, // System messages don't have email addresses
                MessageStatus = normalizedStatus,
                CreatedAt = (createdAtUtc ?? DateTime.UtcNow).ToUniversalTime()
            };

            await _context.Messages.AddAsync(message);

            // Update thread's updated_at timestamp
            await UpdateThreadTimestampAsync(threadId);

            await _context.SaveChangesAsync();

            return (TransactionResult.Succeeded, message);
        }
        catch
        {
            _context.ChangeTracker.Clear();
            return (TransactionResult.Failed, null);
        }
    }

    /// <summary>
    /// Updates the status for a message.
    /// </summary>
    /// <param name="messageId">The identifier of the message to update.</param>
    /// <param name="messageStatus">The new message status.</param>
    /// <returns>A task that resolves to a TransactionResult indicating the outcome of the operation.</returns>
    public async Task<TransactionResult> UpdateMessageStatusAsync(long messageId, string messageStatus)
    {
        var normalizedStatus = NormalizeStatus(messageStatus);

        if (normalizedStatus == null)
            return TransactionResult.Failed;

        try
        {
            var message = await _context.Messages
                .FirstOrDefaultAsync(m => m.Id == messageId);

            if (message == null)
                return TransactionResult.NotFound;

            if (string.Equals(message.MessageStatus, normalizedStatus, StringComparison.OrdinalIgnoreCase))
                return TransactionResult.NoAction;

            message.MessageStatus = normalizedStatus;
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
    /// Marks the message as seen.
    /// </summary>
    /// <param name="messageId">The identifier of the message to mark as seen.</param>
    /// <returns>A task that resolves to a TransactionResult indicating the outcome of the operation.</returns>
    public Task<TransactionResult> MarkMessageSeenAsync(long messageId) =>
        UpdateMessageStatusAsync(messageId, StatusSeen);

    /// <summary>
    /// Marks the message as processed.
    /// </summary>
    /// <param name="messageId">The identifier of the message to mark as processed.</param>
    /// <returns>A task that resolves to a TransactionResult indicating the outcome of the operation.</returns>
    public Task<TransactionResult> MarkMessageProcessedAsync(long messageId) =>
        UpdateMessageStatusAsync(messageId, StatusProcessed);

    /// <summary>
    /// Marks the message as unseen.
    /// </summary>
    /// <param name="messageId">The identifier of the message to mark as unseen.</param>
    /// <returns>A task that resolves to a TransactionResult indicating the outcome of the operation.</returns>
    public Task<TransactionResult> MarkMessageUnseenAsync(long messageId) =>
        UpdateMessageStatusAsync(messageId, StatusUnseen);

    /// <summary>
    /// Deletes the specified message.
    /// </summary>
    /// <param name="messageId">The identifier of the message to delete.</param>
    /// <returns>A task that resolves to a TransactionResult indicating the outcome of the operation.</returns>
    public async Task<TransactionResult> DeleteMessageAsync(long messageId)
    {
        if (messageId <= 0)
            return TransactionResult.Failed;

        try
        {
            var message = await _context.Messages
                .FirstOrDefaultAsync(m => m.Id == messageId);

            if (message == null)
                return TransactionResult.NotFound;

            _context.Messages.Remove(message);
            await _context.SaveChangesAsync();
            return TransactionResult.Succeeded;
        }
        catch
        {
            _context.ChangeTracker.Clear();
            return TransactionResult.Failed;
        }
    }

    private async Task UpdateThreadTimestampAsync(long threadId)
    {
        var thread = await _context.Threads.FirstOrDefaultAsync(t => t.Id == threadId);
        if (thread != null)
        {
            thread.UpdatedAt = DateTime.UtcNow;
        }
    }

    private static string? NormalizeStatus(string? messageStatus)
    {
        if (string.IsNullOrWhiteSpace(messageStatus))
            return null;

        var normalized = messageStatus.Trim().ToLowerInvariant();
        return AllowedStatuses.Contains(normalized) ? normalized : null;
    }

    private static string? NormalizeMessageType(string? messageType)
    {
        if (string.IsNullOrWhiteSpace(messageType))
            return null;

        var normalized = messageType.Trim().ToLowerInvariant();
        return AllowedMessageTypes.Contains(normalized) ? normalized : null;
    }

    private static string? NormalizeSenderType(string? senderType)
    {
        if (string.IsNullOrWhiteSpace(senderType))
            return null;

        var normalized = senderType.Trim().ToLowerInvariant();
        return AllowedSenderTypes.Contains(normalized) ? normalized : null;
    }

    private static string TrimEmailAddress(string emailAddress) =>
        emailAddress.Trim();
}
