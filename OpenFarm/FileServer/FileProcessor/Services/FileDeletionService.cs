using FileProcessor.Services.Interfaces;
using RabbitMQHelper;
using RabbitMQHelper.MessageTypes;

namespace FileProcessor.Services;

/// <summary>
///     Background service that listens to RabbitMQ DeleteFile queue and deletes files from MinIO
///     when jobs are completed or rejected.
/// </summary>
public class FileDeletionService : BackgroundService
{
    private readonly ILogger<FileDeletionService> _logger;
    private readonly IMinioService _minioService;
    private readonly IRmqHelper _rmqHelper;

    public FileDeletionService(
        IRmqHelper rmqHelper,
        IMinioService minioService,
        ILogger<FileDeletionService> logger)
    {
        _rmqHelper = rmqHelper;
        _minioService = minioService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("FileDeletionService is starting");

        while (!stoppingToken.IsCancellationRequested)
            try
            {
                await _rmqHelper.Connect();
                _logger.LogInformation("Connected to RabbitMQ for file deletion processing");

                _rmqHelper.AddListener(QueueNames.DeleteFile, (RabbitMQHelper.MessageTypes.Message message) =>
                {
                    try
                    {
                        ProcessDeletionMessage(message, stoppingToken).GetAwaiter().GetResult();
                        return true;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing deletion message for JobId {JobId}", message.JobId);
                        return false;
                    }
                });

                _logger.LogInformation("Started listening to DeleteFile queue");

                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("FileDeletionService is stopping");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in FileDeletionService");
                await Task.Delay(5000, stoppingToken);
            }
    }

    private async Task ProcessDeletionMessage(RabbitMQHelper.MessageTypes.Message message, CancellationToken stoppingToken)
    {
        _logger.LogInformation("Processing file deletion for JobId {JobId}", message.JobId);

        var gcodeFileName = FileNameService.GenerateGcodeFileName(message.JobId);
        var imageFileName = FileNameService.GenerateImageFileName(message.JobId);

        if (gcodeFileName == null || imageFileName == null)
        {
            _logger.LogWarning("Invalid JobId {JobId} - cannot generate file names", message.JobId);
            return;
        }

        var deleteTasks = new List<Task<bool>>();

        deleteTasks.Add(DeleteFileWithLogging(FileNameService.GcodeBucket, gcodeFileName, "gcode"));
        deleteTasks.Add(DeleteFileWithLogging(FileNameService.ImageBucket, imageFileName, "image"));

        var results = await Task.WhenAll(deleteTasks);

        var gcodeDeleted = results[0];
        var imageDeleted = results[1];

        if (gcodeDeleted || imageDeleted)
            _logger.LogInformation(
                "Successfully deleted files for JobId {JobId} (gcode: {GcodeDeleted}, image: {ImageDeleted})",
                message.JobId, gcodeDeleted, imageDeleted);
        else
            _logger.LogWarning("No files were deleted for JobId {JobId}", message.JobId);
    }

    private async Task<bool> DeleteFileWithLogging(string bucketName, string objectName, string fileType)
    {
        try
        {
            var deleted = await _minioService.DeleteFileAsync(bucketName, objectName);
            if (deleted)
                _logger.LogInformation("Deleted {FileType} file {ObjectName} from bucket {BucketName}",
                    fileType, objectName, bucketName);
            else
                _logger.LogWarning("Failed to delete {FileType} file {ObjectName} from bucket {BucketName}",
                    fileType, objectName, bucketName);
            return deleted;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while deleting {FileType} file {ObjectName} from bucket {BucketName}",
                fileType, objectName, bucketName);
            return false;
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("FileDeletionService is stopping");
        await base.StopAsync(cancellationToken);
    }
}
