using FileProcessor.Models;
using Microsoft.AspNetCore.Mvc;
using RabbitMQHelper;
using RabbitMQHelper.MessageTypes;

namespace FileProcessor.Controllers;

/// <summary>
///     API controller for testing file processing functionality.
///     Provides endpoints to send test messages to RabbitMQ and test Google Drive downloads for development and testing.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class TestController : ControllerBase
{
    private readonly ILogger<TestController> _logger;
    private readonly IRmqHelper _rmqHelper;


    /// <summary>
    ///     Initializes a new instance of the TestController class.
    /// </summary>
    /// <param name="rmqHelper">The RabbitMQ helper for sending messages</param>
    /// <param name="logger">The logger instance for logging operations</param>
    public TestController(IRmqHelper rmqHelper, ILogger<TestController> logger)
    {
        _rmqHelper = rmqHelper;
        _logger = logger;
    }


    /// <summary>
    ///     Sends a test file message to RabbitMQ for processing.
    /// </summary>
    /// <param name="request">The test file request containing source URL and file type</param>
    /// <returns>JSON object indicating success and message details</returns>
    /// <response code="200">Returns success status with message details</response>
    /// <response code="500">If an error occurs sending the message</response>
    [HttpPost("send-file-message")]
    public async Task<IActionResult> SendFileMessage([FromBody] TestFileRequest request)
    {
        try
        {
            var printJobId = request.PrintJobId ?? Random.Shared.Next(1000, 9999);

            var testMessage = new FileMessage
            {
                PrintJobId = printJobId,
                SourceUrl = request.SourceUrl,
                SourceType = SourceType.Test,
                Timestamp = DateTime.UtcNow
            };

            await _rmqHelper.QueueMessage(
                ExchangeNames.JobAccepted,
                new AcceptMessage
                {
                    DownloadType = DownloadType.Test,
                    DownloadUrl = request.SourceUrl,
                    JobId = printJobId
                });

            _logger.LogInformation("Test message sent for file: {SourceUrl}", request.SourceUrl);

            return Ok(new
            {
                success = true,
                message = "File message sent to queue",
                details = testMessage
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send test message");
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    ///     Sends a test message to process a Google Drive file via RabbitMQ workflow.
    ///     This follows the standard RabbitMQ workflow for Google Drive files.
    /// </summary>
    /// <param name="request">The Google Drive test request containing file ID or URL</param>
    /// <returns>JSON object indicating success and message details</returns>
    /// <response code="200">Returns success status with message details</response>
    /// <response code="400">If the file ID or URL is invalid</response>
    /// <response code="500">If an error occurs sending the message</response>
    [HttpPost("google-drive-message")]
    public async Task<IActionResult> SendGoogleDriveMessage([FromBody] GoogleDriveTestRequest request)
    {
        try
        {
            var printJobId = request.PrintJobId ?? Random.Shared.Next(1000, 9999);

            await _rmqHelper.QueueMessage(
                ExchangeNames.JobAccepted,
                new AcceptMessage
                {
                    DownloadType = DownloadType.GoogleDrive,
                    DownloadUrl = request.FileId,
                    JobId = printJobId
                });

            _logger.LogInformation("Google Drive message sent for JobId: {JobId}", printJobId);

            return Ok(new
            {
                message = "Google Drive message sent successfully",
                details = new
                {
                    printJobId,
                    fileIdOrUrl = request.FileId,
                    downloadType = "GoogleDrive",
                    timestamp = DateTime.UtcNow
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send Google Drive message");
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }
}

/// <summary>
///     Request model for sending test file messages.
/// </summary>
public class TestFileRequest
{
    /// <summary>
    ///     The URL of the file to download and process.
    /// </summary>
    public string SourceUrl { get; set; } = string.Empty;

    /// <summary>
    ///     The print job ID for tracking this file. If not provided, a random ID will be generated.
    /// </summary>
    public int? PrintJobId { get; set; }
}

/// <summary>
///     Request model for testing Google Drive file processing via RabbitMQ.
/// </summary>
public class GoogleDriveTestRequest
{
    /// <summary>
    ///     The Google Drive file ID or URL to process via RabbitMQ workflow.
    ///     Supports various Google Drive URL formats or direct file IDs.
    /// </summary>
    public string FileId { get; set; } = string.Empty;

    /// <summary>
    ///     The print job ID for tracking this file. If not provided, a random ID will be generated.
    /// </summary>
    public int? PrintJobId { get; set; }
}