using FileProcessor.Services.Interfaces;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;

namespace FileProcessor.Services;

/// <summary>
///     Service for downloading files from remote URLs and Google Drive with validation and size limits.
/// </summary>
public class FileDownloaderService : IFileDownloaderService
{
    private readonly IConfiguration _configuration;
    private readonly DriveService _googleDriveService;
    private readonly HttpClient _httpClient;
    private readonly ILogger<FileDownloaderService> _logger;

    /// <summary>
    ///     Initializes a new instance of the FileDownloaderService class.
    /// </summary>
    /// <param name="httpClient">The HTTP client for downloading files</param>
    /// <param name="logger">The logger instance for logging operations</param>
    /// <param name="configuration">The configuration containing download settings</param>
    /// <param name="googleDriveService">The Google Drive service for handling Google Drive file IDs</param>
    public FileDownloaderService(
        HttpClient httpClient, ILogger<FileDownloaderService> logger,
        IConfiguration configuration)
    {
        _httpClient = httpClient;
        _logger = logger;
        _configuration = configuration;

        // Google drive credentials
        var credFilePath = Environment.GetEnvironmentVariable("GOOGLE_SERVICE_ACCOUNT_CRED_FILE");
        var credential = GoogleCredential.FromFile(credFilePath)
            .CreateScoped(DriveService.Scope.DriveReadonly);

        _googleDriveService = new DriveService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "OpenFarm FileServer"
        });


        var timeoutSecondsStr = configuration["DOWNLOAD_TIMEOUT_SECONDS"];
        var timeoutSeconds = int.TryParse(timeoutSecondsStr, out var parsed) ? parsed : 300;
        _httpClient.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
    }

    /// <summary>
    ///     Downloads a file from a Google Drive file ID.
    /// </summary>
    /// <param name="fileId">The Google Drive file ID</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>The file content as a byte array, or null if download fails or file is too large</returns>
    public async Task<Stream?> DownloadGoogleDriveFileAsync(string fileId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting Google Drive download for file ID: {FileId}", fileId);

            if (string.IsNullOrWhiteSpace(fileId))
            {
                _logger.LogError("Google Drive file ID is null or empty");
                return null;
            }

            var stream = new MemoryStream();
            var request = _googleDriveService.Files.Get(fileId);
            var result = await request.DownloadAsync(stream, cancellationToken);

            if (result.Exception is not null)
                throw new Exception(result.Exception.Message);

            // Reset stream position to the beginning so consumers can use it.
            if (stream.CanSeek)
                stream.Seek(0, SeekOrigin.Begin);

            return stream;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Unexpected error downloading Google Drive file with ID {FileId}. Service continues running.", fileId);
            return null;
        }
    }

    /// <summary>
    ///     Downloads a file from a regular HTTP/HTTPS URL.
    /// </summary>
    /// <param name="url">The URL to download the file from</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>The file content as a byte array, or null if download fails or file is too large</returns>
    public async Task<byte[]?> DownloadFileAsync(string url, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting regular download from URL: {Url}", url);

            if (string.IsNullOrWhiteSpace(url))
            {
                _logger.LogError("URL is null or empty");
                return null;
            }

            if (Uri.TryCreate(url, UriKind.Absolute, out _))
                return await DownloadFromUrlAsync(url, cancellationToken);

            _logger.LogError("Invalid URL format: {Url}", url);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error downloading file from URL {Url}. Service continues running.", url);
            return null;
        }
    }

    /// <summary>
    ///     Internal method to handle the actual HTTP download with size validation.
    /// </summary>
    /// <param name="url">The URL to download from</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>The file content as a byte array, or null if download fails or file is too large</returns>
    private async Task<byte[]?> DownloadFromUrlAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            if (response.Content.Headers.ContentLength.HasValue)
            {
                var maxSizeMbStr = _configuration["MAX_FILE_SIZE_MB"];
                var maxSizeMb = int.TryParse(maxSizeMbStr, out var parsedSize) ? parsedSize : 250;
                var maxSizeBytes = maxSizeMb * 1024 * 1024;

                if (response.Content.Headers.ContentLength.Value > maxSizeBytes)
                {
                    _logger.LogError("File too large: {Size} bytes exceeds limit of {MaxSize} bytes",
                        response.Content.Headers.ContentLength.Value, maxSizeBytes);
                    return null;
                }
            }

            var content = await response.Content.ReadAsByteArrayAsync(cancellationToken);

            _logger.LogInformation("Successfully downloaded {Size} bytes from {Url}", content.Length, url);

            return content;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error downloading file from {Url}. Service continues running.", url);
            return null;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Download timeout for {Url}. Service continues running.", url);
            return null;
        }
    }
}