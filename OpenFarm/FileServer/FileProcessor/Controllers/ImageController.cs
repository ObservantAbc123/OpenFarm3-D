using FileProcessor.Services;
using FileProcessor.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace FileProcessor.Controllers;

/// <summary>
/// API controller for managing PNG images associated with print jobs.
/// Each print job has exactly one image associated with it.
/// Provides endpoints for uploading and retrieving PNG images using print job IDs.
/// </summary>
[ApiController]
[Route("api/images")]
public class ImageController : ControllerBase
{
    private readonly ILogger<ImageController> _logger;
    private readonly int _maxSizeBytes;
    private readonly IMinioService _minioService;


    /// <summary>
    ///     Initializes a new instance of the ImageController class.
    /// </summary>
    /// <param name="minioService">The MinIO service for file operations</param>
    /// <param name="logger">The logger instance for logging operations</param>
    /// <param name="configuration">The configuration containing upload settings</param>
    public ImageController(IMinioService minioService, ILogger<ImageController> logger, IConfiguration configuration)
    {
        _minioService = minioService;
        _logger = logger;
        var maxSizeMb = configuration.GetValue("MAX_FILE_SIZE_MB", 250);
        _maxSizeBytes = maxSizeMb * 1024 * 1024;
    }


    /// <summary>
    ///     Validates an uploaded image file for all requirements.
    /// </summary>
    /// <param name="file">The uploaded file to validate</param>
    /// <param name="errorResponse">The error response if validation fails</param>
    /// <returns>True if validation passes, false otherwise</returns>
    private bool ValidateImageFile(IFormFile file, out IActionResult? errorResponse)
    {
        if (!ControllerValidationHelper.ValidateFileNotEmpty(file, out errorResponse))
            return false;

        var allowedExtensions = new[] { FileNameService.ImageExtension };
        if (!ControllerValidationHelper.ValidateFileExtension(file.FileName, allowedExtensions, out errorResponse))
            return false;

        if (!ControllerValidationHelper.ValidateFileSize(file.Length, _maxSizeBytes, out errorResponse))
            return false;

        var allowedContentTypes = new[] { FileNameService.ImageContentType };
        if (!ControllerValidationHelper.ValidateContentType(file.ContentType, allowedContentTypes, out errorResponse))
            return false;

        return true;
    }

    /// <summary>
    /// Uploads a PNG image file to MinIO storage associated with a print job.
    /// Overwrites any existing image for the same print job.
    /// </summary>
    /// <param name="printJobId">The print job ID to associate the image with</param>
    /// <param name="file">PNG image file to upload</param>
    /// <returns>JSON object containing upload result with success status and print job ID</returns>
    /// <response code="200">Returns success status and print job ID</response>
    /// <response code="400">If the printJobId is invalid, no file provided, or file is invalid</response>
    /// <response code="500">If an internal error occurs during upload</response>
    [HttpPost("{printJobId:long}")]
    public async Task<IActionResult> UploadImage(long printJobId, IFormFile file)
    {
        try
        {
            if (!ControllerValidationHelper.ValidatePrintJobId(printJobId, out var errorResponse))
                return errorResponse!;

            if (!ValidateImageFile(file, out var validationError))
                return validationError!;

            // Use the filename from the uploaded file to preserve view-specific names
            // This allows multiple PNG files per job (e.g., job_1_view_0_north_west.png)
            var fileName = file.FileName;

            if (string.IsNullOrWhiteSpace(fileName))
                return new BadRequestObjectResult(new { error = "File name is required" });

            await using var stream = file.OpenReadStream();
            var uploadResult = await _minioService.UploadStreamAsync(
                FileNameService.ImageBucket,
                fileName,
                stream,
                file.Length,
                file.ContentType
            );

            if (!uploadResult.Success)
                return StatusCode(500, new { error = "Failed to upload image file", details = uploadResult.Error });

            _logger.LogInformation("Successfully uploaded image {FileName} for printJobId {printJobId}", fileName,
                printJobId);

            return Ok(new
            {
                success = true,
                printJobId
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading image for printJobId {printJobId}", printJobId);
            return ControllerValidationHelper.CreateInternalServerErrorResponse();
        }
    }

    /// <summary>
    /// Downloads all 8 PNG images associated with a specific print job.
    /// Returns a JSON response containing base64-encoded image data for all 8 views.
    /// </summary>
    /// <param name="printJobId">The positive integer print job ID to retrieve the images for</param>
    /// <returns>JSON object containing an array of base64-encoded image data</returns>
    /// <response code="200">Returns JSON object with array of base64-encoded images</response>
    /// <response code="400">If the print job ID is invalid (non-positive)</response>
    /// <response code="500">If an internal server error occurs during file retrieval</response>
    [HttpGet("{printJobId:long}")]
    public async Task<IActionResult> GetImage(long printJobId)
    {
        try
        {
            if (!ControllerValidationHelper.ValidatePrintJobId(printJobId, out var errorResponse))
                return errorResponse!;

            var views = new[] { "NORTH_WEST", "WEST", "SOUTH_WEST", "SOUTH", "SOUTH_EAST", "EAST", "NORTH_EAST", "NORTH" };
            var images = new List<string?>();

            for (int i = 0; i < views.Length; i++)
            {
                var fileName = $"job_{printJobId}_view_{i}_{views[i].ToLower()}.png";
                var stream = await _minioService.GetFileStreamAsync(FileNameService.ImageBucket, fileName);
                
                if (stream is MemoryStream memoryStream)
                {
                    using (memoryStream)
                    {
                        var bytes = memoryStream.ToArray();
                        var base64 = Convert.ToBase64String(bytes);
                        images.Add(base64);
                    }
                }
                else if (stream != null)
                {
                    await using (stream)
                    {
                        using var ms = new MemoryStream();
                        await stream.CopyToAsync(ms);
                        var bytes = ms.ToArray();
                        var base64 = Convert.ToBase64String(bytes);
                        images.Add(base64);
                    }
                }
                else
                {
                    images.Add(null);
                }
            }

            _logger.LogInformation("Retrieved {Count} images for printJobId {printJobId}", images.Count, printJobId);

            return Ok(new
            {
                printJobId,
                images
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving images for printJobId {printJobId}", printJobId);
            return ControllerValidationHelper.CreateInternalServerErrorResponse();
        }
    }

    /// <summary>
    ///     Lists all image files in storage.
    ///     This endpoint is for administrative purposes and debugging.
    /// </summary>
    /// <returns>JSON object containing all image files</returns>
    /// <response code="200">Returns the complete list of image files</response>
    /// <response code="500">If an internal server error occurs during file listing</response>
    [HttpGet("")]
    public async Task<IActionResult> ListImages()
    {
        try
        {
            var files = await _minioService.ListFilesAsync(FileNameService.ImageBucket);

            _logger.LogInformation("Listed {Count} image files", files.Count);

            return Ok(new
            {
                bucket = FileNameService.ImageBucket,
                files,
                count = files.Count
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing image files");
            return ControllerValidationHelper.CreateInternalServerErrorResponse();
        }
    }
}