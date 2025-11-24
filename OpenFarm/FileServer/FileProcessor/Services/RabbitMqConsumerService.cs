using FileProcessor.Models;
using FileProcessor.Services.Interfaces;
using RabbitMQHelper;
using RabbitMQHelper.MessageTypes;

namespace FileProcessor.Services;

/// <summary>
///     Background service that consumes file messages from RabbitMQ and processes them.
///     Downloads files from URLs and stores them in MinIO based on file type.
/// </summary>
public class RabbitMqConsumerService : BackgroundService
{
    private readonly IFileDownloaderService _fileDownloader;
    private readonly ILogger<RabbitMqConsumerService> _logger;
    private readonly IMinioService _minioService;
    private readonly IRmqHelper _rmqHelper;

    /// <summary>
    ///     Initializes a new instance of the RabbitMqConsumerService class.
    /// </summary>
    /// <param name="logger">The logger instance for logging operations</param>
    /// <param name="fileDownloader">The file downloader service</param>
    /// <param name="minioService">The MinIO service for file storage</param>
    /// <param name="rmqHelper">The RabbitMQ helper for message operations</param>
    public RabbitMqConsumerService(
        ILogger<RabbitMqConsumerService> logger,
        IFileDownloaderService fileDownloader,
        IMinioService minioService,
        IRmqHelper rmqHelper)
    {
        _logger = logger;
        _fileDownloader = fileDownloader;
        _minioService = minioService;
        _rmqHelper = rmqHelper;
    }

    /// <summary>
    ///     Executes the background service to start consuming messages from RabbitMQ.
    /// </summary>
    /// <param name="stoppingToken">Cancellation token to stop the service</param>
    /// <returns>A completed task</returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        stoppingToken.Register(() => { _logger.LogInformation("FileServer RabbitMQ Consumer Service is stopping."); });

        await Task.Run(async () => await StartConsumer(stoppingToken), stoppingToken);
    }

    /// <summary>
    ///     Starts the RabbitMQ consumer and begins listening for file messages.
    /// </summary>
    /// <param name="stoppingToken">Cancellation token to stop the consumer</param>
    private async Task StartConsumer(CancellationToken stoppingToken)
    {
        try
        {
            await _rmqHelper.Connect();
            _rmqHelper.AddListener(QueueNames.JobAccepted, (AcceptMessage message) =>
            {
                try
                {
                    ProcessMessage(message, stoppingToken).GetAwaiter().GetResult();
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Error processing message for JobId {JobId}: {Error}. Service will continue running.",
                        message.JobId, ex.Message);
                    return false;
                }
            });

            _logger.LogInformation("RabbitMQ Consumer started. Waiting for messages...");

            while (!stoppingToken.IsCancellationRequested) await Task.Delay(1000, stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in RabbitMQ Consumer");
        }
    }

    /// <summary>
    ///     Processes a file message by downloading the file and uploading it to MinIO.
    /// </summary>
    /// <param name="message">The AcceptMessage containing file information</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    private async Task ProcessMessage(AcceptMessage message, CancellationToken cancellationToken)
    {
        try
        {
            var fileMessage = new FileMessage
            {
                SourceType = (SourceType)message.DownloadType,
                SourceUrl = message.DownloadUrl,
                PrintJobId = message.JobId
            };

            if (!ValidationService.ValidateFileMessage(fileMessage, out var validationError))
            {
                _logger.LogError("Invalid file message for JobId {JobId}: {Error}. Skipping message processing.",
                    message.JobId, validationError);
                return;
            }

            var fileName = FileNameService.GenerateGcodeFileName(fileMessage.PrintJobId);
            if (fileName == null)
            {
                _logger.LogError("Invalid print job ID {printJobId} for JobId {JobId}. Skipping message processing.",
                    fileMessage.PrintJobId, message.JobId);
                return;
            }

            _logger.LogInformation("Processing file download: {FileName} from {SourceURL}",
                fileName, fileMessage.SourceUrl);

            UploadResult uploadResult;
            try
            {
                if (message.DownloadType == DownloadType.GoogleDrive)
                    uploadResult =
                        await DownloadFileGoogleDriveAndUploadToMinioAsync(fileMessage.SourceUrl, cancellationToken,
                            fileName);
                else
                    uploadResult =
                        await DownloadFileAndUploadToMinioAsync(fileMessage.SourceUrl, cancellationToken, fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to download file from {SourceURL} for JobId {JobId} using {DownloadType}. Service continues running.",
                    fileMessage.SourceUrl, fileMessage.PrintJobId, message.DownloadType);
                return;
            }

            if (uploadResult.Success)
            {
                _logger.LogInformation("Successfully uploaded {FileName} to MinIO", fileName);

                try
                {
                    await SendUploadNotification(fileMessage, fileName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Failed to send upload notification for {FileName}. File upload was successful but notification failed.",
                        fileName);
                }
            }
            else
            {
                _logger.LogError("Failed to upload file {FileName} to MinIO for JobId {JobId}: {Error}",
                    fileName, fileMessage.PrintJobId, uploadResult.Error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error processing message for JobId {JobId}. Service continues running.",
                message.JobId);
        }
    }

    private async Task<UploadResult> DownloadFileGoogleDriveAndUploadToMinioAsync(string sourceUrl,
        CancellationToken cancellationToken, string fileName)
    {
        var stream = await _fileDownloader.DownloadGoogleDriveFileAsync(sourceUrl, cancellationToken);

        if (stream == null || stream.Length == 0)
            throw new Exception("Google drive file could not be downloaded.");

        return await _minioService.UploadStreamAsync(
            FileNameService.GcodeBucket,
            fileName,
            stream,
            stream.Length,
            FileNameService.GcodeContentType
        );
    }

    private async Task<UploadResult> DownloadFileAndUploadToMinioAsync(string sourceUrl,
        CancellationToken cancellationToken, string fileName)
    {
        var fileData = await _fileDownloader.DownloadFileAsync(sourceUrl, cancellationToken);

        if (fileData == null || fileData.Length == 0)
            throw new Exception("File could not be downloaded.");

        return await _minioService.UploadFileAsync(
            FileNameService.GcodeBucket,
            fileName,
            fileData,
            FileNameService.GcodeContentType
        );
    }

    /// <summary>
    ///     Sends a notification message to RabbitMQ after successful file upload.
    /// </summary>
    /// <param name="originalMessage">The original file message that was processed</param>
    /// <param name="fileName">The filename of the uploaded file</param>
    private async Task SendUploadNotification(FileMessage originalMessage, string fileName)
    {
        try
        {
            await _rmqHelper.QueueMessage(
                ExchangeNames.FileReady,
                new RabbitMQHelper.MessageTypes.Message
                {
                    JobId = originalMessage.PrintJobId
                });

            _logger.LogInformation("Sent upload notification for {FileName}", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send upload notification for {FileName}", fileName);
            throw; // Re-throw to be handled by caller
        }
    }
}