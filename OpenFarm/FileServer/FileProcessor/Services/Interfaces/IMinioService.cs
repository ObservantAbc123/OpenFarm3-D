using FileProcessor.Models;

namespace FileProcessor.Services.Interfaces;

/// <summary>
///     Interface for interacting with MinIO object storage.
///     Provides methods for uploading, downloading, and managing files in MinIO buckets.
/// </summary>
public interface IMinioService
{
    /// <summary>
    ///     Uploads a file to MinIO from a byte array.
    /// </summary>
    /// <param name="bucketName">The name of the bucket to upload to</param>
    /// <param name="objectName">The name of the object in the bucket</param>
    /// <param name="data">The file data as a byte array</param>
    /// <param name="contentType">The MIME content type of the file</param>
    /// <returns>An UploadResult indicating success or failure with details</returns>
    Task<UploadResult> UploadFileAsync(string bucketName, string objectName, byte[] data, string contentType);

    /// <summary>
    ///     Uploads a file to MinIO from a stream.
    /// </summary>
    /// <param name="bucketName">The name of the bucket to upload to</param>
    /// <param name="objectName">The name of the object in the bucket</param>
    /// <param name="stream">The stream containing the file data</param>
    /// <param name="size">The size of the file in bytes</param>
    /// <param name="contentType">The MIME content type of the file</param>
    /// <returns>An UploadResult indicating success or failure with details</returns>
    Task<UploadResult> UploadStreamAsync(string bucketName, string objectName, Stream stream, long size,
        string contentType);

    /// <summary>
    ///     Gets a file from MinIO as a stream.
    /// </summary>
    /// <param name="bucketName">The name of the bucket containing the file</param>
    /// <param name="objectName">The name of the object to retrieve</param>
    /// <returns>A stream containing the file data, or null if not found</returns>
    Task<Stream?> GetFileStreamAsync(string bucketName, string objectName);

    /// <summary>
    ///     Lists all files in a MinIO bucket.
    /// </summary>
    /// <param name="bucketName">The name of the bucket to list files from</param>
    /// <returns>A list of file names in the bucket</returns>
    Task<List<string>> ListFilesAsync(string bucketName);

    /// <summary>
    ///     Deletes a file from a MinIO bucket.
    /// </summary>
    /// <param name="bucketName">The name of the bucket containing the file</param>
    /// <param name="objectName">The name of the object to delete</param>
    /// <returns>True if deletion was successful, false otherwise</returns>
    Task<bool> DeleteFileAsync(string bucketName, string objectName);
}