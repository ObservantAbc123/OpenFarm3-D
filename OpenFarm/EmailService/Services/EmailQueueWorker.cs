using DatabaseAccess;
using RabbitMQHelper;
using RabbitMQHelper.MessageTypes;

namespace EmailService.Services;

public class EmailQueueWorker : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EmailQueueWorker> _logger;
    private readonly IEmailTemplateRenderer _renderer;
    private readonly IRmqHelper _rmq;
    private readonly IEmailSender _sender;

    public EmailQueueWorker(
        ILogger<EmailQueueWorker> logger,
        IRmqHelper rmq,
        IServiceScopeFactory scopeFactory,
        DatabaseAccessHelper db, // Keeping this for now if needed, but ideally remove if fully scoped
        IEmailTemplateRenderer renderer,
        IEmailSender sender
    )
    {
        _logger = logger;
        _rmq = rmq;
        _scopeFactory = scopeFactory;
        _renderer = renderer;
        _sender = sender;

        _logger.LogInformation("EmailQueueWorker initialized");
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting EmailQueueWorker and connecting to RabbitMQ...");

        await _rmq.Connect();

        _rmq.AddListener(QueueNames.EmailJobAccepted, (AcceptMessage message) => OnJobReceived(message).GetAwaiter().GetResult());
        _rmq.AddListener(QueueNames.EmailPrintStarted, (PrintStartedMessage message) => OnPrintStarted(message).GetAwaiter().GetResult());
        _rmq.AddListener(QueueNames.EmailPrintCleared, (PrintClearedMessage message) => OnJobCompleted(message).GetAwaiter().GetResult());
        _rmq.AddListener(QueueNames.EmailJobPaid, (RabbitMQHelper.MessageTypes.Message message) => OnPaymentAccepted(message).GetAwaiter().GetResult());
        _rmq.AddListener(QueueNames.EmailJobApproved, (RabbitMQHelper.MessageTypes.Message message) => OnJobApproved(message).GetAwaiter().GetResult());
        _rmq.AddListener(QueueNames.EmailJobRejected, (RejectMessage message) => OnJobRejected(message).GetAwaiter().GetResult());
        _rmq.AddListener(QueueNames.EmailOperatorReply, (OperatorReplyMessage message) => OnOperatorReply(message).GetAwaiter().GetResult());

        _logger.LogInformation("EmailQueueWorker started and listeners registered.");
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping EmailQueueWorker...");
        return Task.CompletedTask;
    }

    private async Task<bool> OnJobReceived(AcceptMessage message)
    {
        _logger.LogInformation($"Received accepted for job {message.JobId}");

        var to = await GetPrimaryEmailForJob(message.JobId);
        if (to is null) return false;
        var html = _renderer.Render(
            "job_received.html",
            new Dictionary<string, string>
            {
                ["[JOB_ID]"] = message.JobId.ToString()
            }
        );
        _logger.LogInformation($"Sending received accepted email to {to} for job {message.JobId}");
        await _sender.SendAsync(to, $"Job Received #{message.JobId}", html);
        return true;
    }

    private async Task<bool> OnJobApproved(RabbitMQHelper.MessageTypes.Message message)
    {
        var to = await GetPrimaryEmailForJob(message.JobId);
        if (to is null) return false;
        var html = _renderer.Render(
            "job_verified.html",
            new Dictionary<string, string>
            {
                ["[JOB_ID]"] = message.JobId.ToString()
            }
        );
        await _sender.SendAsync(to, $"Job Verified #{message.JobId}", html);
        return true;
    }

    private async Task<bool> OnPaymentAccepted(RabbitMQHelper.MessageTypes.Message message)
    {
        _logger.LogInformation($"Processing payment accepted for job {message.JobId}");
        var to = await GetPrimaryEmailForJob(message.JobId);
        if (to is null) return false;
        var html = _renderer.Render(
            "payment_accepted.html",
            new Dictionary<string, string>
            {
                ["[JOB_ID]"] = message.JobId.ToString()
            }
        );
        _logger.LogInformation($"Sending payment accepted email to {to} for job {message.JobId}");
        await _sender.SendAsync(to, $"Payment Accepted #{message.JobId}", html);
        return true;
    }

    private async Task<bool> OnPrintStarted(PrintStartedMessage message)
    {
        var to = await GetPrimaryEmailForJob(message.JobId);
        if (to is null) return false;
        var html = _renderer.Render(
            "job_printing.html",
            new Dictionary<string, string>
            {
                ["[JOB_ID]"] = message.JobId.ToString()
            }
        );
        await _sender.SendAsync(to, $"Job Printing #{message.JobId}", html);
        return true;
    }

    private async Task<bool> OnJobRejected(RejectMessage message)
    {
        var to = await GetPrimaryEmailForJob(message.JobId);
        if (to is null) return false;
        var reason = GetRejectReasonText(message.RejectReason);
        var html = _renderer.Render(
            "job_rejected.html",
            new Dictionary<string, string>
            {
                ["[JOB_ID]"] = message.JobId.ToString(),
                ["[REJECTION_REASON]"] = reason
            }
        );
        await _sender.SendAsync(to, $"Job Rejected #{message.JobId}", html);
        return true;
    }

    private async Task<bool> OnJobCompleted(PrintClearedMessage message)
    {
        var jobId = message.JobId;
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DatabaseAccessHelper>();

        var job = await db.PrintJobs.GetPrintJobAsync(jobId);
        if (job?.UserId is null)
            throw new ArgumentException($"No data found for job {jobId}");
        var email = await db.Emails.GetUserPrimaryEmailAsync((int)job.UserId);
        if (email == null) return false;

        var html = _renderer.Render(
            "job_completed.html",
            new Dictionary<string, string>
            {
                ["[JOB_ID]"] = jobId.ToString()
            }
        );
        await _sender.SendAsync(email.EmailAddress, $"Job Completed #{jobId}", html);
        return true;
    }

    private async Task<string?> GetPrimaryEmailForJob(long jobId)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DatabaseAccessHelper>();

        var job = await db.PrintJobs.GetPrintJobAsync(jobId);
        if (job?.UserId is null) return null;
        return await db.Emails.GetUserPrimaryEmailAddressAsync((int)job.UserId);
    }

    private async Task<bool> OnOperatorReply(OperatorReplyMessage message)
    {
        _logger.LogInformation($"Received operator reply for Thread {message.ThreadId}, Message {message.MessageId}");

        // Convert plain text body to HTML paragraphs
        var bodyHtml = ConvertTextToHtmlParagraphs(message.Body);

        var html = _renderer.Render(
            "operator_reply.html",
            new Dictionary<string, string>
            {
                ["[SUBJECT]"] = message.Subject,
                ["[MESSAGE_BODY]"] = bodyHtml
            }
        );

        _logger.LogInformation($"Sending operator reply email to {message.CustomerEmail}");
        await _sender.SendAsync(message.CustomerEmail, message.Subject, html);
        return true;
    }

    private static string ConvertTextToHtmlParagraphs(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";

        // Split by line breaks and wrap each paragraph in <p> tags
        var lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        var paragraphs = lines.Select(line =>
        {
            if (string.IsNullOrWhiteSpace(line))
                return "<p style=\"margin:0 0 12px 0;\">&nbsp;</p>";
            return $"<p style=\"margin:0 0 12px 0;\">{System.Net.WebUtility.HtmlEncode(line)}</p>";
        });
        return string.Join("", paragraphs);
    }

    private static string GetRejectReasonText(RejectReason reason) => reason switch
    {
        RejectReason.CancelledByUser => "Cancelled by user",
        RejectReason.FailedValidation => "Failed validation",
        RejectReason.RejectedByApprover => "Rejected by approver",
        RejectReason.FailedToPrint => "Failed to print",
        RejectReason.FailedDownload => "Failed to download file",
        _ => "Rejected"
    };
}
