using DatabaseAccess;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PaymentProcessingService.Authentication;
using RabbitMQHelper;
using RabbitMQHelper.MessageTypes;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Threading.RateLimiting;
using PaymentProcessingService;

var builder = WebApplication.CreateBuilder(args);

// Configure options from appsettings
builder.Services.Configure<PaymentServiceOptions>(
    builder.Configuration.GetSection(PaymentServiceOptions.SectionName));
builder.Services.Configure<PaymentRateLimiterOptions>(
    builder.Configuration.GetSection(PaymentRateLimiterOptions.SectionName));

var rateLimiterConfig = builder.Configuration.GetSection("RateLimiterOptions:GlobalLimiter");

// Add rate limiting from configuration
builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                Window = rateLimiterConfig.GetValue<TimeSpan>("Window", TimeSpan.FromMinutes(1)),
                PermitLimit = rateLimiterConfig.GetValue<int>("PermitLimit", 60),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = rateLimiterConfig.GetValue<int>("QueueLimit", 10)
            }));
});

// Add authentication and authorization
builder.Services.AddAuthentication("ApiKey")
    .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>("ApiKey", null);
builder.Services.AddAuthorization();

// Add services
builder.Services.AddSingleton<IRmqHelper, RmqHelper>();

var app = builder.Build();

// Initialize RabbitMQ connection
var rmqHelper = app.Services.GetRequiredService<IRmqHelper>();
var logger = app.Services.GetRequiredService<ILogger<Program>>();

try
{
    var rmqConnected = await rmqHelper.Connect();
    if (!rmqConnected)
    {
        logger.LogWarning("RabbitMQ connection failed - notifications will be disabled");
    }
    else
    {
        logger.LogInformation("RabbitMQ connected successfully");
    }
}
catch (Exception ex)
{
    logger.LogError(ex, "Failed to initialize RabbitMQ connection");
}

// Validate required configuration
var connectionString = Environment.GetEnvironmentVariable("DATABASE_CONNECTION_STRING");
if (string.IsNullOrEmpty(connectionString))
{
    logger.LogCritical("DATABASE_CONNECTION_STRING environment variable is required");
    throw new InvalidOperationException("DATABASE_CONNECTION_STRING environment variable is required");
}

var apiKey = Environment.GetEnvironmentVariable("PAYMENT_API_KEY");
if (string.IsNullOrEmpty(apiKey))
{
    logger.LogCritical("PAYMENT_API_KEY environment variable is required");
    throw new InvalidOperationException("PAYMENT_API_KEY environment variable is required");
}

// Configure middleware pipeline
app.UseHttpsRedirection();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// Audit logging middleware
app.Use(async (context, next) =>
{
    var correlationId = Guid.NewGuid().ToString("N")[..8];
    context.Items["CorrelationId"] = correlationId;

    using var scope = logger.BeginScope(new Dictionary<string, object>
    {
        ["CorrelationId"] = correlationId,
        ["RequestPath"] = context.Request.Path,
        ["RemoteIP"] = context.Connection.RemoteIpAddress?.ToString()
    });

    await next();
});

// Payment endpoints
app.MapPost("/api/payment/mark-paid",
    [Authorize] async ([FromBody] MarkPaidRequest request, IRmqHelper rmqHelper, ILogger<Program> logger, HttpContext context) =>
{
    var correlationId = context.Items["CorrelationId"]?.ToString();

    if (!IsValidRequest(request, out var validationError))
    {
        logger.LogWarning("Invalid payment request: {Error} [{CorrelationId}]", validationError, correlationId);
        return Results.BadRequest(new { error = "Invalid request parameters" });
    }

    try
    {
        using var dbHelper = new DatabaseAccessHelper(connectionString);
        var results = new List<PaymentResult>();

        foreach (var jobId in request.JobIds)
        {
            var result = await dbHelper.PrintJobs.MarkPrintJobAsPaidAsync(jobId);
            var success = result == TransactionResult.Succeeded;

            if (success)
            {
                results.Add(new PaymentResult(jobId, true, null));
                logger.LogInformation("Payment marked for job {JobId} [{CorrelationId}]", jobId, correlationId);

                // Send notification if RabbitMQ is available
                if (rmqHelper.IsConnected())
                {
                    try
                    {
                        var message = new Message { JobId = jobId };
                        await rmqHelper.QueueMessage(ExchangeNames.JobPaid, message);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Failed to send notification for job {JobId} [{CorrelationId}]", jobId, correlationId);
                    }
                }
            }
            else
            {
                results.Add(new PaymentResult(jobId, false, result.ToString()));
                logger.LogWarning("Failed to mark payment for job {JobId}: {Result} [{CorrelationId}]", jobId, result, correlationId);
            }
        }

        var successCount = results.Count(r => r.Success);
        logger.LogInformation("Payment operation completed: {SuccessCount}/{TotalCount} jobs processed [{CorrelationId}]",
            successCount, results.Count, correlationId);

        return Results.Ok(new
        {
            processedCount = results.Count,
            successCount = successCount,
            results = results.ToArray()
        });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Payment processing failed [{CorrelationId}]", correlationId);
        return Results.Problem("Payment processing failed", statusCode: 500);
    }
})
.RequireRateLimiting("default");

app.MapGet("/api/payment/unpaid",
    [Authorize] async (ILogger<Program> logger, HttpContext context) =>
{
    var correlationId = context.Items["CorrelationId"]?.ToString();

    try
    {
        using var dbHelper = new DatabaseAccessHelper(connectionString);
        var unpaidJobs = await dbHelper.PrintJobs.GetUnpaidPrintJobsAsync();
        var unpaidJobIds = unpaidJobs.Select(job => job.Id).ToArray();

        logger.LogInformation("Retrieved {Count} unpaid jobs [{CorrelationId}]", unpaidJobIds.Length, correlationId);

        return Results.Ok(new
        {
            unpaidJobIds = unpaidJobIds,
            count = unpaidJobIds.Length,
            retrievedAt = DateTime.UtcNow
        });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to retrieve unpaid jobs [{CorrelationId}]", correlationId);
        return Results.Problem("Failed to retrieve unpaid jobs", statusCode: 500);
    }
})
.RequireRateLimiting("default");

// Health check endpoint (no auth required)
app.MapGet("/health", () =>
{
    var health = new
    {
        status = "healthy",
        timestamp = DateTime.UtcNow,
        version = "1.0.0",
        dependencies = new
        {
            database = !string.IsNullOrEmpty(connectionString) ? "configured" : "missing",
            rabbitmq = rmqHelper.IsConnected() ? "connected" : "disconnected"
        }
    };

    return Results.Ok(health);
});

// Global error handler
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var correlationId = context.Items["CorrelationId"]?.ToString() ?? "unknown";
        logger.LogError("Unhandled exception in request [{CorrelationId}]", correlationId);

        context.Response.StatusCode = 500;
        await context.Response.WriteAsJsonAsync(new
        {
            error = "Internal server error",
            correlationId = correlationId
        });
    });
});

logger.LogInformation("PaymentProcessingService starting on {Urls}", string.Join(", ", builder.Configuration.GetSection("urls").Get<string[]>() ?? new[] { "https://0.0.0.0:443" }));

app.Run();

// Helper methods and records
static bool IsValidRequest(MarkPaidRequest? request, out string error)
{
    const int maxJobIds = 100; // From PaymentServiceOptions.MaxJobIdsPerRequest default
    error = string.Empty;

    if (request?.JobIds == null)
    {
        error = "JobIds cannot be null";
        return false;
    }

    if (request.JobIds.Length == 0)
    {
        error = "JobIds cannot be empty";
        return false;
    }

    if (request.JobIds.Length > maxJobIds)
    {
        error = $"Too many job IDs (max {maxJobIds})";
        return false;
    }

    if (request.JobIds.Any(id => id <= 0))
    {
        error = "Invalid job ID";
        return false;
    }

    return true;
}

public record MarkPaidRequest([Required] long[] JobIds);
public record PaymentResult(long JobId, bool Success, string? Error);
