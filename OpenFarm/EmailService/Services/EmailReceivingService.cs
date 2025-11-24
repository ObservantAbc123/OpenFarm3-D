using System.Net;
using System.Text.RegularExpressions;
using DatabaseAccess;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MimeKit;
using MimeKit.Text;
using EmailService.Services;

namespace EmailService.Services;

/// <summary>
/// Background service responsible for pulling unread emails from Gmail via IMAP and
/// storing them in threads using the Thread and Message helpers.
/// </summary>
public partial class EmailReceivingService(
    ILogger<EmailReceivingService> logger,
    IServiceScopeFactory scopeFactory)
    : BackgroundService
{
    #region BackgroundService Implementation

    /// <summary>
    ///     Main execution loop that periodically checks for new emails
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "Email receiving service started. Poll interval: {IntervalMinutes} minutes",
            _pollInterval.TotalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessIncomingEmailsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                logger.LogDebug("Email processing cancelled due to service shutdown");
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error occurred while processing incoming emails");
            }

            try
            {
                await Task.Delay(_pollInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                logger.LogDebug("Polling delay cancelled due to service shutdown");
                break;
            }
        }

        logger.LogInformation("Email receiving service is shutting down");
    }

    #endregion

    #region Inner Types

    /// <summary>
    ///     Container for extracted email information
    /// </summary>
    private sealed class EmailInfo
    {
        public required string SenderAddress { get; init; }
        public required string Subject { get; init; }
        public required string Body { get; init; }
        public required DateTime ReceivedAt { get; init; }
        public long? JobId { get; init; }
    }

    #endregion

    #region Constants

    private const int DefaultPollIntervalMinutes = 1;
    private const string GmailSearchUrlPrefix = "https://mail.google.com/mail/u/0/#search/";
    private const string DefaultSubject = "(No subject)";
    private const string DefaultBodyContent = "(No body content)";
    private const string AttachmentNotePlain = "[Attachments detected - view original email in Gmail]";
    private const string AttachmentNoteWithLink = "[Attachments detected - view original email: {0}]";

    #endregion

    #region Configuration

    private readonly string _imapServer = Environment.GetEnvironmentVariable("IMAP_SERVER")
                                          ?? throw new InvalidOperationException(
                                              "IMAP_SERVER environment variable is required");

    private readonly int _imapPort = int.TryParse(Environment.GetEnvironmentVariable("IMAP_PORT"), out var port)
        ? port
        : throw new InvalidOperationException("IMAP_PORT environment variable must be a valid integer");

    private readonly string _emailAccount = Environment.GetEnvironmentVariable("GMAIL_EMAIL")
                                            ?? throw new InvalidOperationException(
                                                "GMAIL_EMAIL environment variable is required");

    private readonly string _emailPassword = Environment.GetEnvironmentVariable("GMAIL_APP_PASSWORD")
                                             ?? throw new InvalidOperationException(
                                                 "GMAIL_APP_PASSWORD environment variable is required");

    private readonly TimeSpan _pollInterval = TimeSpan.FromMinutes(
        int.TryParse(Environment.GetEnvironmentVariable("IMAP_POLL_INTERVAL_MINUTES"), out var interval)
            ? interval
            : DefaultPollIntervalMinutes);

    #endregion

    #region Regex Patterns

    private static readonly Regex JobNumberPattern = GenerateJobNumberRegex();

    private static readonly IReadOnlyList<Regex> ReplySeparatorPatterns = new List<Regex>
    {
        GenerateOnWrotePattern(),
        GenerateFromHeaderPattern(),
        GenerateOriginalMessagePattern(),
        GenerateUnderscoreSeparatorPattern()
    };

    #endregion

    #region Email Processing

    /// <summary>
    ///     Connects to IMAP server and processes all unread emails
    /// </summary>
    private async Task ProcessIncomingEmailsAsync(CancellationToken cancellationToken)
    {
        using var imapClient = new ImapClient();

        try
        {
            // Connect and authenticate with the mail server
            await imapClient.ConnectAsync(_imapServer, _imapPort, true, cancellationToken);
            await imapClient.AuthenticateAsync(_emailAccount, _emailPassword, cancellationToken);

            // Select the appropriate folder (All Mail for Gmail, otherwise Inbox)
            var mailFolder = GetMailFolder(imapClient);
            await mailFolder.OpenAsync(FolderAccess.ReadWrite, cancellationToken);

            // Search for unread messages
            var unreadIds = await mailFolder.SearchAsync(SearchQuery.NotSeen, cancellationToken);

            if (unreadIds.Count == 0)
            {
                logger.LogDebug("No unread emails found during this polling cycle");
                return;
            }

            // Process each unread message
            foreach (var messageUid in unreadIds)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await ProcessSingleMessageAsync(mailFolder, messageUid, cancellationToken);
            }

            logger.LogInformation("Completed processing all unread emails");
        }
        finally
        {
            await SafeDisconnectAsync(imapClient, cancellationToken);
        }
    }

    /// <summary>
    ///     Selects the appropriate mail folder
    /// </summary>
    private IMailFolder GetMailFolder(ImapClient client)
    {
        try
        {
            var allMail = client.GetFolder(SpecialFolder.All);
            if (allMail != null)
                return allMail;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Could not access Gmail 'All Mail' folder; falling back to Inbox");
        }

        return client.Inbox;
    }

    /// <summary>
    ///     Downloads and processes a single email message
    /// </summary>
    private async Task ProcessSingleMessageAsync(
        IMailFolder mailFolder,
        UniqueId messageUid,
        CancellationToken cancellationToken)
    {
        try
        {
            var message = await mailFolder.GetMessageAsync(messageUid, cancellationToken);
            await StoreMessageInDatabaseAsync(mailFolder, messageUid, message, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process email with UID {MessageId}", messageUid);
        }
    }

    /// <summary>
    ///     Extracts email data and stores it in the database
    /// </summary>
    private async Task StoreMessageInDatabaseAsync(
        IMailFolder mailFolder,
        UniqueId messageUid,
        MimeMessage message,
        CancellationToken cancellationToken)
    {
        try
        {
            var emailInfo = ExtractEmailInformation(message);

            // Skip emails without valid sender
            if (string.IsNullOrWhiteSpace(emailInfo.SenderAddress))
            {
                logger.LogWarning("Skipping email UID {MessageId} - no valid sender address", messageUid);
                await MarkAsReadAsync(mailFolder, messageUid, cancellationToken);
                return;
            }

            using var scope = scopeFactory.CreateScope();
            var databaseHelper = scope.ServiceProvider.GetRequiredService<DatabaseAccessHelper>();
            var autoReply = scope.ServiceProvider.GetRequiredService<EmailAutoReplyService>();

            // Find or create a thread for this user and job
            var threadResult = await FindOrCreateThreadAsync(databaseHelper, emailInfo.SenderAddress, emailInfo.JobId);
            if (threadResult.Result != TransactionResult.Succeeded || threadResult.Thread == null)
            {
                logger.LogWarning("Failed to find or create thread for email from {SenderAddress}", emailInfo.SenderAddress);
                return;
            }

            // Add the email as a message to the thread
            var (messageResult, createdMessage) = await databaseHelper.Message.CreateEmailMessageAsync(
                threadId: threadResult.Thread.Id,
                fromEmailAddress: emailInfo.SenderAddress,
                messageSubject: emailInfo.Subject,
                messageContent: emailInfo.Body,
                senderType: "user",
                messageStatus: "unseen",
                createdAtUtc: emailInfo.ReceivedAt);

            var result = messageResult;

            if (result is TransactionResult.Succeeded or TransactionResult.NoAction)
            {
                if (ShouldAttemptAutoReply(emailInfo.SenderAddress))
                {
                    try
                    {
                        await autoReply.SendAutoReplyIfNeededAsync(
                            emailInfo.SenderAddress,
                            cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        // We don't want auto-reply issues to block normal processing
                        logger.LogError(
                            ex,
                            "Failed to send auto-reply to {SenderAddress} for email UID {MessageId}",
                            emailInfo.SenderAddress,
                            messageUid);
                    }
                }

                await MarkAsReadAsync(mailFolder, messageUid, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to store email UID {MessageId}", messageUid);
            // Leave unread for retry
        }
    }
    /// <summary>
    ///     Decides whether we should even *try* to auto-reply
    ///     (skips no-reply and our own address to prevent loops).
    /// </summary>
    private bool ShouldAttemptAutoReply(string senderAddress)
    {
        if (string.IsNullOrWhiteSpace(senderAddress))
            return false;

        var normalized = senderAddress.Trim().ToLowerInvariant();

        // Don't auto-reply to ourselves
        if (string.Equals(normalized, _emailAccount.Trim().ToLowerInvariant(), StringComparison.Ordinal))
            return false;

        // Don't auto-reply to typical no-reply addresses
        if (normalized.Contains("no-reply") || normalized.Contains("noreply"))
            return false;

        return true;
    }

    /// <summary>
    /// Finds an existing thread for the user and job, or creates a new one if none exists.
    /// </summary>
    /// <param name="databaseHelper">The database access helper.</param>
    /// <param name="fromEmailAddress">The email address of the sender.</param>
    /// <param name="jobId">The optional job ID associated with the email.</param>
    /// <returns>A tuple containing the transaction result and the thread.</returns>
    private async Task<(TransactionResult Result, DatabaseAccess.Models.Thread? Thread)> FindOrCreateThreadAsync(DatabaseAccessHelper databaseHelper, string fromEmailAddress, long? jobId)
    {
        try
        {
            // First, find the user by email address
            var userIds = await databaseHelper.Emails.GetEmailsUserIdsAsync(fromEmailAddress);
            DatabaseAccess.Models.User? user = null;

            if (userIds.Count > 0)
            {
                // Take the first user if multiple users have the same email
                user = await databaseHelper.Users.GetUserAsync(userIds[0]);
            }

            if (user == null)
            {
                // Create a new user if none exists for this email
                user = await databaseHelper.Users.CreateOrGetUserByEmailAsync(fromEmailAddress, ExtractNameFromEmail(fromEmailAddress));

                if (user == null)
                {
                    logger.LogError("Failed to create user for email address {EmailAddress}", fromEmailAddress);
                    return (TransactionResult.Failed, null);
                }
            }

            // Look for an existing active thread for this user and job combination
            var existingThreads = jobId.HasValue
                ? await databaseHelper.Thread.GetThreadsByJobIdAsync(jobId.Value)
                : await databaseHelper.Thread.GetThreadsByUserIdAsync(user.Id);

            var activeThread = existingThreads
                .Where(t => t.UserId == user.Id &&
                           t.JobId == jobId &&
                           t.ThreadStatus == "active")
                .FirstOrDefault();

            if (activeThread != null)
            {
                return (TransactionResult.Succeeded, activeThread);
            }

            // Create a new thread
            return await databaseHelper.Thread.CreateThreadAsync(
                userId: user.Id,
                jobId: jobId,
                threadStatus: "active");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error finding or creating thread for email from {EmailAddress}", fromEmailAddress);
            return (TransactionResult.Failed, null);
        }
    }

    /// <summary>
    /// Extracts a name from an email address for user creation.
    /// </summary>
    /// <param name="emailAddress">The email address.</param>
    /// <returns>A name derived from the email address.</returns>
    private static string ExtractNameFromEmail(string emailAddress)
    {
        var localPart = emailAddress.Split('@')[0];
        return char.ToUpper(localPart[0]) + localPart.Substring(1);
    }


    #endregion

    #region Email Content Extraction

    /// <summary>
    ///     Extracts all relevant information from the email message
    /// </summary>
    private static EmailInfo ExtractEmailInformation(MimeMessage message)
    {
        var sender = message.From.Mailboxes.FirstOrDefault()?.Address ?? string.Empty;
        var subject = string.IsNullOrWhiteSpace(message.Subject) ? DefaultSubject : message.Subject.Trim();
        var receivedAt = message.Date.UtcDateTime == DateTime.MinValue ? DateTime.UtcNow : message.Date.UtcDateTime;

        var body = GetCleanBodyText(message);

        // Add attachment notification if needed
        if (message.Attachments?.Any() ?? false)
        {
            var attachmentNote = BuildAttachmentNote(message.MessageId);
            body = string.IsNullOrWhiteSpace(body)
                ? attachmentNote
                : $"{body}{Environment.NewLine}{Environment.NewLine}{attachmentNote}";
        }

        if (string.IsNullOrWhiteSpace(body)) body = DefaultBodyContent;

        long? jobId = null;
        var jobNumberMatch = JobNumberPattern.Match(subject);
        if (jobNumberMatch.Success && long.TryParse(jobNumberMatch.Groups["digits"].Value, out var parsedId))
            jobId = parsedId;

        return new EmailInfo
        {
            SenderAddress = sender,
            Subject = subject,
            Body = body,
            ReceivedAt = receivedAt,
            JobId = jobId
        };
    }

    /// <summary>
    ///     Extracts and cleans the email body, removing quoted replies
    /// </summary>
    private static string GetCleanBodyText(MimeMessage message)
    {
        // Try plain text first
        var textBody = message.GetTextBody(TextFormat.Plain) ?? message.TextBody;
        if (!string.IsNullOrWhiteSpace(textBody))
        {
            var cleaned = RemoveQuotedContent(textBody);
            return string.IsNullOrWhiteSpace(cleaned) ? textBody.Trim() : cleaned;
        }

        // Fall back to HTML
        var htmlBody = message.HtmlBody;
        if (string.IsNullOrWhiteSpace(htmlBody))
            return string.Empty;

        var plainText = StripHtmlTags(htmlBody);
        var cleanedHtml = RemoveQuotedContent(plainText);
        return string.IsNullOrWhiteSpace(cleanedHtml) ? plainText : cleanedHtml;
    }

    /// <summary>
    ///     Creates an attachment notification with optional Gmail link
    /// </summary>
    private static string BuildAttachmentNote(string? messageId)
    {
        if (string.IsNullOrWhiteSpace(messageId))
            return AttachmentNotePlain;

        var gmailUrl = $"{GmailSearchUrlPrefix}rfc822msgid:{Uri.EscapeDataString(messageId)}";
        return string.Format(AttachmentNoteWithLink, gmailUrl);
    }

    #endregion

    #region Text Processing

    /// <summary>
    ///     Removes quoted reply content from email text
    /// </summary>
    private static string RemoveQuotedContent(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var lines = text.Split(["\r\n", "\n"], StringSplitOptions.None);
        var endIndex = FindQuoteStartIndex(lines);

        if (endIndex == 0)
            return string.Empty;

        // Take only non-quoted lines and trim trailing empty lines
        var relevantLines = lines.Take(endIndex).ToList();
        while (relevantLines.Count > 0 && string.IsNullOrWhiteSpace(relevantLines[^1]))
            relevantLines.RemoveAt(relevantLines.Count - 1);

        return string.Join(Environment.NewLine, relevantLines).Trim();
    }

    /// <summary>
    ///     Finds where quoted content begins in the email
    /// </summary>
    private static int FindQuoteStartIndex(string[] lines)
    {
        for (var i = 0; i < lines.Length; i++)
        {
            var trimmed = lines[i].Trim();

            if (string.IsNullOrEmpty(trimmed))
                continue;
            if (trimmed.StartsWith('>') ||
                ReplySeparatorPatterns.Any(pattern => pattern.IsMatch(trimmed)))
                return i;
        }

        return lines.Length;
    }

    /// <summary>
    ///     Converts HTML to plain text by removing tags and decoding entities
    /// </summary>
    private static string StripHtmlTags(string html)
    {
        // Remove script and style blocks with their content
        var withoutScripts = HtmlScriptRegex().Replace(html, string.Empty);
        var withoutStyles = HtmlStyleRegex().Replace(withoutScripts, string.Empty);
        var withoutTags = HtmlTagRegex().Replace(withoutStyles, string.Empty);

        // Decode HTML entities (e.g., &amp; to &)
        return WebUtility.HtmlDecode(withoutTags).Trim();
    }

    #endregion

    #region IMAP Operations

    /// <summary>
    ///     Marks an email as read in the mail folder
    /// </summary>
    private async Task MarkAsReadAsync(
        IMailFolder folder,
        UniqueId messageUid,
        CancellationToken cancellationToken)
    {
        try
        {
            await folder.AddFlagsAsync(messageUid, MessageFlags.Seen, true, cancellationToken);
        }
        catch (Exception ex)
        {
            // Log but don't throw
            logger.LogDebug(ex, "Failed to mark message {MessageUid} as read", messageUid);
        }
    }

    /// <summary>
    ///     Safely disconnects from the IMAP server
    /// </summary>
    private async Task SafeDisconnectAsync(ImapClient client, CancellationToken cancellationToken)
    {
        if (!client.IsConnected)
            return;

        try
        {
            await client.DisconnectAsync(true, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to cleanly disconnect from IMAP server");
        }
    }

    #endregion

    #region Regex Pattern Generators

    [GeneratedRegex("#(?<digits>\\d+)", RegexOptions.Compiled)]
    private static partial Regex GenerateJobNumberRegex();

    [GeneratedRegex(@"^On .+ wrote:$", RegexOptions.IgnoreCase | RegexOptions.Compiled, "en-US")]
    private static partial Regex GenerateOnWrotePattern();

    [GeneratedRegex(@"^From:\s", RegexOptions.IgnoreCase | RegexOptions.Compiled, "en-US")]
    private static partial Regex GenerateFromHeaderPattern();

    [GeneratedRegex(@"^-+\s*Original Message\s*-+$", RegexOptions.IgnoreCase | RegexOptions.Compiled, "en-US")]
    private static partial Regex GenerateOriginalMessagePattern();

    [GeneratedRegex("^_{2,}$", RegexOptions.Compiled)]
    private static partial Regex GenerateUnderscoreSeparatorPattern();

    [GeneratedRegex(@"<script[\s\S]*?</script>", RegexOptions.IgnoreCase)]
    private static partial Regex HtmlScriptRegex();

    [GeneratedRegex(@"<style[\s\S]*?</style>", RegexOptions.IgnoreCase)]
    private static partial Regex HtmlStyleRegex();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex HtmlTagRegex();

    #endregion
}
