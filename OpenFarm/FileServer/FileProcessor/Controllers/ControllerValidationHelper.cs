using Microsoft.AspNetCore.Mvc;

namespace FileProcessor.Controllers;

/// <summary>
///     Shared validation helper methods for API controllers.
///     Provides common validation patterns used across multiple controllers.
/// </summary>
public static class ControllerValidationHelper
{
    /// <summary>
    ///     Validates that a print job ID is positive.
    /// </summary>
    /// <param name="printJobId">The print job ID to validate</param>
    /// <param name="errorResponse">The error response if validation fails</param>
    /// <returns>True if valid, false otherwise</returns>
    public static bool ValidatePrintJobId(long printJobId, out IActionResult? errorResponse)
    {
        errorResponse = null;
        if (printJobId > 0)
            return true;

        errorResponse = new BadRequestObjectResult(new { error = "printJobId is required to be positive" });
        return false;
    }

    /// <summary>
    ///     Validates that an uploaded file is not null or empty.
    /// </summary>
    /// <param name="file">The file to validate</param>
    /// <param name="errorResponse">The error response if validation fails</param>
    /// <returns>True if valid, false otherwise</returns>
    public static bool ValidateFileNotEmpty(IFormFile file, out IActionResult? errorResponse)
    {
        errorResponse = null;
        if (file.Length != 0)
            return true;

        errorResponse = new BadRequestObjectResult(new { error = "No file provided" });
        return false;
    }

    /// <summary>
    ///     Validates that a file has the correct extension.
    /// </summary>
    /// <param name="fileName">The filename to validate</param>
    /// <param name="allowedExtension">The allowed file extension (e.g., ".png", ".gcode")</param>
    /// <param name="errorResponse">The error response if validation fails</param>
    /// <returns>True if valid, false otherwise</returns>
    public static bool ValidateFileExtension(string fileName, string allowedExtension, out IActionResult? errorResponse)
    {
        errorResponse = null;
        if (fileName.EndsWith(allowedExtension, StringComparison.OrdinalIgnoreCase))
            return true;

        var fileType = allowedExtension.TrimStart('.').ToUpperInvariant();
        errorResponse =
            new BadRequestObjectResult(new { error = $"Invalid file type. Only {fileType} files are allowed." });
        return false;
    }

    /// <summary>
    /// Validates that a file has one of the allowed extensions.
    /// </summary>
    /// <param name="fileName">The filename to validate</param>
    /// <param name="allowedExtensions">The allowed file extensions (e.g., [".png", ".gif"])</param>
    /// <param name="errorResponse">The error response if validation fails</param>
    /// <returns>True if valid, false otherwise</returns>
    public static bool ValidateFileExtension(string fileName, string[] allowedExtensions, out IActionResult? errorResponse)
    {
        errorResponse = null;
        foreach (var extension in allowedExtensions)
        {
            if (fileName.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        var fileTypes = string.Join(" or ", allowedExtensions.Select(ext => ext.TrimStart('.').ToUpperInvariant()));
        errorResponse = new BadRequestObjectResult(new { error = $"Invalid file type. Only {fileTypes} files are allowed." });
        return false;
    }

    /// <summary>
    /// Validates that a file size is within limits.
    /// </summary>
    /// <param name="fileLength">The file size in bytes</param>
    /// <param name="maxSizeBytes">The maximum allowed size in bytes</param>
    /// <param name="errorResponse">The error response if validation fails</param>
    /// <returns>True if valid, false otherwise</returns>
    public static bool ValidateFileSize(long fileLength, int maxSizeBytes, out IActionResult? errorResponse)
    {
        errorResponse = null;
        if (fileLength <= maxSizeBytes)
            return true;

        var maxSizeMb = maxSizeBytes / (1024 * 1024);
        errorResponse = new BadRequestObjectResult(new { error = $"File too large. Maximum size is {maxSizeMb}MB." });
        return false;
    }

    /// <summary>
    ///     Validates that a file has the correct content type.
    /// </summary>
    /// <param name="contentType">The file's content type</param>
    /// <param name="allowedContentType">The allowed content type</param>
    /// <param name="errorResponse">The error response if validation fails</param>
    /// <returns>True if valid, false otherwise</returns>
    public static bool ValidateContentType(string contentType, string allowedContentType,
        out IActionResult? errorResponse)
    {
        errorResponse = null;
        if (string.Equals(contentType, allowedContentType, StringComparison.OrdinalIgnoreCase))
            return true;

        var fileType = GetFileTypeFromContentType(allowedContentType);
        errorResponse = new BadRequestObjectResult(new
            { error = $"Invalid content type. Only {fileType} files are allowed." });
        return false;
    }

    /// <summary>
    /// Validates that a file has one of the allowed content types.
    /// </summary>
    /// <param name="contentType">The file's content type</param>
    /// <param name="allowedContentTypes">The allowed content types (e.g., ["image/png", "image/gif"])</param>
    /// <param name="errorResponse">The error response if validation fails</param>
    /// <returns>True if valid, false otherwise</returns>
    public static bool ValidateContentType(string contentType, string[] allowedContentTypes, out IActionResult? errorResponse)
    {
        errorResponse = null;
        foreach (var allowedType in allowedContentTypes)
        {
            if (string.Equals(contentType, allowedType, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        var fileTypes = string.Join(" or ", allowedContentTypes.Select(GetFileTypeFromContentType));
        errorResponse = new BadRequestObjectResult(new { error = $"Invalid content type. Only {fileTypes} files are allowed." });
        return false;
    }

    /// <summary>
    /// Converts a content type to a user-friendly file type name.
    /// </summary>
    /// <param name="contentType">The content type (e.g., "image/png")</param>
    /// <returns>User-friendly file type name (e.g., "PNG")</returns>
    private static string GetFileTypeFromContentType(string contentType)
    {
        return contentType.ToLowerInvariant() switch
        {
            "image/png" => "PNG",
            "image/gif" => "GIF",
            "text/plain" => "GCode",
            _ => "Unknown"
        };
    }

    /// <summary>
    ///     Creates a standardized not found response.
    /// </summary>
    /// <param name="message">The error message</param>
    /// <returns>NotFound response with error details</returns>
    public static NotFoundObjectResult CreateNotFoundResponse(string message)
    {
        return new NotFoundObjectResult(new { error = message });
    }

    /// <summary>
    ///     Creates a standardized internal server error response.
    /// </summary>
    /// <returns>Internal server error response</returns>
    public static ObjectResult CreateInternalServerErrorResponse()
    {
        return new ObjectResult(new { error = "Internal server error" })
        {
            StatusCode = 500
        };
    }
}