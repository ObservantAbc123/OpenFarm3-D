namespace FileProcessor.Services.Interfaces;

/// <summary>
///     Interface for downloading files from remote URLs and Google Drive with validation and size limits.
/// </summary>
public interface IFileDownloaderService
{
    /// <summary>
    ///     Downloads a file from a Google Drive file ID.
    /// </summary>
    /// <param name="fileId">The Google Drive file ID</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>The file content as a byte array, or null if download fails or file is too large</returns>
    Task<Stream?> DownloadGoogleDriveFileAsync(string fileId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Downloads a file from a regular HTTP/HTTPS URL.
    /// </summary>
    /// <param name="url">The URL to download the file from</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>The file content as a byte array, or null if download fails or file is too large</returns>
    Task<byte[]?> DownloadFileAsync(string url, CancellationToken cancellationToken = default);
}