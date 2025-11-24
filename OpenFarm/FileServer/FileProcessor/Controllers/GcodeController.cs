using FileProcessor.Services;
using FileProcessor.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace FileProcessor.Controllers;

/// <summary>
///     API controller for retrieving gcode files.
///     Provides read-only endpoints for downloading and listing gcode files using print job IDs.
///     Gcode files are ingested through RabbitMQ message queue workflow, not direct uploads.
///     File names are automatically constructed from print job IDs using the pattern "job_{printJobId}.gcode".
/// </summary>
[ApiController]
[Route("api/gcode")]
public class GcodeController : ControllerBase
{
    private readonly ILogger<GcodeController> _logger;
    private readonly IMinioService _minioService;

    /// <summary>
    ///     Initializes a new instance of the GcodeController class with required dependencies.
    /// </summary>
    /// <param name="minioService">The MinIO service instance for performing file operations on storage buckets</param>
    /// <param name="logger">The logger instance for structured logging of file operations and errors</param>
    public GcodeController(IMinioService minioService, ILogger<GcodeController> logger)
    {
        _minioService = minioService;
        _logger = logger;
    }

    /// <summary>
    ///     Retrieves a gcode file as a raw byte array for the specified print job.
    /// </summary>
    /// <param name="printJobId">The print job ID to retrieve the gcode file for</param>
    /// <returns>Raw byte array content of the gcode file with application/octet-stream content type</returns>
    /// <response code="200">Returns the gcode file as raw binary data</response>
    /// <response code="400">If the print job ID is invalid (non-positive)</response>
    /// <response code="404">If the gcode file is not found in the MinIO storage bucket</response>
    /// <response code="500">If an internal server error occurs during file retrieval or processing</response>
    [HttpGet("{printJobId:long}/bytes")]
    public async Task<IActionResult> GetGcodeBytes(long printJobId)
    {
        try
        {
            if (!ControllerValidationHelper.ValidatePrintJobId(printJobId, out var errorResponse))
                return errorResponse!;

            var fileName = FileNameService.GenerateGcodeFileName(printJobId);

            if (fileName == null)
                return new BadRequestObjectResult(new { error = "Invalid print job ID" });

            var stream = await _minioService.GetFileStreamAsync(FileNameService.GcodeBucket, fileName);

            if (stream == null)
                return ControllerValidationHelper.CreateNotFoundResponse("File not found");

            await using (stream)
            {
                using var memoryStream = new MemoryStream();
                await stream.CopyToAsync(memoryStream);
                var bytes = memoryStream.ToArray();

                _logger.LogInformation(
                    "Retrieved gcode bytes for printJobId {printJobId}, FileName {FileName}, Size {Size}", printJobId,
                    fileName, bytes.Length);

                return File(bytes, "application/octet-stream", fileName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving gcode bytes for printJobId {printJobId}", printJobId);
            return ControllerValidationHelper.CreateInternalServerErrorResponse();
        }
    }

    /// <summary>
    ///     Downloads a gcode file directly as a file stream for the specified print job.
    /// </summary>
    /// <param name="printJobId">The positive integer print job ID to retrieve the gcode file for</param>
    /// <returns>File stream with text/plain content type and appropriate download headers</returns>
    /// <response code="200">Returns the gcode file as a downloadable stream with proper content disposition</response>
    /// <response code="400">If the print job ID is invalid (non-positive)</response>
    /// <response code="404">If the gcode file is not found in the MinIO storage bucket</response>
    /// <response code="500">If an internal server error occurs during file retrieval or streaming</response>
    [HttpGet("{printJobId:long}")]
    public async Task<IActionResult> GetGcode(long printJobId)
    {
        try
        {
            if (!ControllerValidationHelper.ValidatePrintJobId(printJobId, out var errorResponse))
                return errorResponse!;

            var fileName = FileNameService.GenerateGcodeFileName(printJobId);

            if (fileName == null)
                return new BadRequestObjectResult(new { error = "Invalid print job ID" });

            var stream = await _minioService.GetFileStreamAsync(FileNameService.GcodeBucket, fileName);

            if (stream == null)
                return ControllerValidationHelper.CreateNotFoundResponse("File not found");

            _logger.LogInformation("Retrieved gcode file for printJobId {printJobId}, FileName {FileName}", printJobId,
                fileName);

            return File(stream, "text/plain", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving gcode file for printJobId {printJobId}", printJobId);
            return ControllerValidationHelper.CreateInternalServerErrorResponse();
        }
    }

    /// <summary>
    ///     Retrieves a comprehensive list of all gcode files stored in the MinIO gcode-files bucket.
    /// </summary>
    /// <returns>JSON object containing bucket name, array of file objects with metadata, and total file count</returns>
    /// <response code="200">Returns the complete list of gcode files with bucket information and file count</response>
    /// <response code="500">If an internal server error occurs during bucket enumeration or file listing</response>
    [HttpGet("")]
    public async Task<IActionResult> ListGcodeFiles()
    {
        try
        {
            var files = await _minioService.ListFilesAsync(FileNameService.GcodeBucket);

            _logger.LogInformation("Listed {Count} gcode files", files.Count);

            return Ok(new
            {
                bucket = FileNameService.GcodeBucket,
                files,
                count = files.Count
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing gcode files");
            return ControllerValidationHelper.CreateInternalServerErrorResponse();
        }
    }
}