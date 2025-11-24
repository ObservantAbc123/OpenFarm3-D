using FileProcessor.Models;
using FileProcessor.Services.Interfaces;
using Minio;
using Minio.DataModel.Args;

namespace FileProcessor.Services;

/// <summary>
///     Service for interacting with MinIO object storage.
///     Provides methods for uploading, downloading, and managing files in MinIO buckets.
/// </summary>
public class MinioService : IMinioService
{
    private readonly ILogger<MinioService> _logger;
    private readonly IMinioClient _minioClient;

    /// <summary>
    ///     Initializes a new instance of the MinioService class.
    /// </summary>
    /// <param name="logger">The logger instance for logging operations</param>
    /// <param name="configuration">The configuration containing MinIO connection settings</param>
    /// <exception cref="InvalidOperationException">Thrown when required MinIO configuration is missing</exception>
    public MinioService(ILogger<MinioService> logger, IConfiguration configuration)
    {
        _logger = logger;

        const string endpoint = "minio:9000";
        var accessKey = configuration["MINIO_ROOT_USER"]
                        ?? throw new InvalidOperationException("MINIO_ROOT_USER environment variable is required");
        var secretKey = configuration["MINIO_ROOT_PASSWORD"]
                        ?? throw new InvalidOperationException("MINIO_ROOT_PASSWORD environment variable is required");

        var useSsl = configuration["MINIO_USE_SSL"]?.ToLower() == "true";

        _minioClient = new MinioClient()
            .WithEndpoint(endpoint)
            .WithCredentials(accessKey, secretKey)
            .WithSSL(useSsl)
            .Build();
    }

    /// <summary>
    ///     Uploads a file to MinIO from a byte array.
    /// </summary>
    /// <param name="bucketName">The name of the bucket to upload to</param>
    /// <param name="objectName">The name of the object in the bucket</param>
    /// <param name="data">The file data as a byte array</param>
    /// <param name="contentType">The MIME content type of the file</param>
    /// <returns>An UploadResult indicating success or failure with details</returns>
    public async Task<UploadResult> UploadFileAsync(string bucketName, string objectName, byte[] data,
        string contentType)
    {
        using var stream = new MemoryStream(data);
        return await UploadStreamAsync(bucketName, objectName, stream, data.Length, contentType);
    }

    /// <summary>
    ///     Uploads a file to MinIO from a stream.
    /// </summary>
    /// <param name="bucketName">The name of the bucket to upload to</param>
    /// <param name="objectName">The name of the object in the bucket</param>
    /// <param name="stream">The stream containing the file data</param>
    /// <param name="size">The size of the file in bytes</param>
    /// <param name="contentType">The MIME content type of the file</param>
    /// <returns>An UploadResult indicating success or failure with details</returns>
    public async Task<UploadResult> UploadStreamAsync(string bucketName, string objectName, Stream stream, long size,
        string contentType)
    {
        try
        {
            await EnsureBucketExistsAsync(bucketName);

            var putObjectArgs = new PutObjectArgs()
                .WithBucket(bucketName)
                .WithObject(objectName)
                .WithStreamData(stream)
                .WithObjectSize(size)
                .WithContentType(contentType);

            await _minioClient.PutObjectAsync(putObjectArgs);

            _logger.LogInformation("Successfully uploaded {ObjectName} to bucket {BucketName}",
                objectName, bucketName);

            return new UploadResult
            {
                Success = true
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading {ObjectName} to bucket {BucketName}",
                objectName, bucketName);

            return new UploadResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    /// <summary>
    ///     Gets a file from MinIO as a stream.
    /// </summary>
    /// <param name="bucketName">The name of the bucket containing the file</param>
    /// <param name="objectName">The name of the object to retrieve</param>
    /// <returns>A stream containing the file data, or null if not found</returns>
    public async Task<Stream?> GetFileStreamAsync(string bucketName, string objectName)
    {
        try
        {
            var memoryStream = new MemoryStream();
            var getObjectArgs = new GetObjectArgs()
                .WithBucket(bucketName)
                .WithObject(objectName)
                .WithCallbackStream(stream => stream.CopyTo(memoryStream));

            await _minioClient.GetObjectAsync(getObjectArgs);
            memoryStream.Position = 0;

            _logger.LogInformation("Successfully retrieved stream for {ObjectName} from bucket {BucketName}",
                objectName, bucketName);

            return memoryStream;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving stream for {ObjectName} from bucket {BucketName}",
                objectName, bucketName);
            return null;
        }
    }

    /// <summary>
    ///     Lists all files in a MinIO bucket.
    /// </summary>
    /// <param name="bucketName">The name of the bucket to list files from</param>
    /// <returns>A list of file names in the bucket</returns>
    public async Task<List<string>> ListFilesAsync(string bucketName)
    {
        try
        {
            var listObjectsArgs = new ListObjectsArgs()
                .WithBucket(bucketName);

            var observable = _minioClient.ListObjectsAsync(listObjectsArgs);
            var items = await observable.ToListAsync();
            var fileNames = items.Select(item => item.Key).ToList();

            _logger.LogInformation("Listed {Count} files in bucket {BucketName}", fileNames.Count, bucketName);
            return fileNames;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing files in bucket {BucketName}", bucketName);
            return [];
        }
    }

    /// <summary>
    ///     Deletes a file from a MinIO bucket.
    /// </summary>
    /// <param name="bucketName">The name of the bucket containing the file</param>
    /// <param name="objectName">The name of the object to delete</param>
    /// <returns>True if deletion was successful, false otherwise</returns>
    public async Task<bool> DeleteFileAsync(string bucketName, string objectName)
    {
        try
        {
            var removeObjectArgs = new RemoveObjectArgs()
                .WithBucket(bucketName)
                .WithObject(objectName);

            await _minioClient.RemoveObjectAsync(removeObjectArgs);

            _logger.LogInformation("Successfully deleted {ObjectName} from bucket {BucketName}",
                objectName, bucketName);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting {ObjectName} from bucket {BucketName}",
                objectName, bucketName);
            return false;
        }
    }

    /// <summary>
    ///     Ensures a bucket exists in MinIO, creating it if necessary.
    /// </summary>
    /// <param name="bucketName">The name of the bucket to check/create</param>
    private async Task EnsureBucketExistsAsync(string bucketName)
    {
        var bucketExistsArgs = new BucketExistsArgs().WithBucket(bucketName);
        var exists = await _minioClient.BucketExistsAsync(bucketExistsArgs);

        if (!exists)
        {
            var makeBucketArgs = new MakeBucketArgs().WithBucket(bucketName);
            await _minioClient.MakeBucketAsync(makeBucketArgs);
            _logger.LogInformation("Created bucket: {BucketName}", bucketName);
        }
    }
}

/// <summary>
///     Extension methods for IObservable
/// </summary>
public static class ObservableExtensions
{
    /// <summary>
    ///     Converts an IObservable to a Task List
    /// </summary>
    public static Task<List<T>> ToListAsync<T>(this IObservable<T> observable)
    {
        var taskCompletionSource = new TaskCompletionSource<List<T>>();
        var items = new List<T>();

        observable.Subscribe(
            item => items.Add(item),
            exception => taskCompletionSource.TrySetException(exception),
            () => taskCompletionSource.TrySetResult(items)
        );

        return taskCompletionSource.Task;
    }
}