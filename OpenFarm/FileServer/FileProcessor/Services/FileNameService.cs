namespace FileProcessor.Services;

/// <summary>
///     Centralized service for managing file naming conventions across the FileServer.
///     Ensures consistent filename generation for all file types.
/// </summary>
public static class FileNameService
{
    // File extensions
    private const string GcodeExtension = ".gcode";
    public const string ImageExtension = ".png";

    // Bucket names
    public const string GcodeBucket = "gcode-files";
    public const string ImageBucket = "images";

    // Content types
    public const string GcodeContentType = "text/plain";
    public const string ImageContentType = "image/png";

    /// <summary>
    ///     Generates the filename for a gcode file based on print job ID.
    /// </summary>
    /// <param name="printJobId">The print job ID</param>
    /// <returns>Generated gcode filename, or null if printJobId is invalid (negative)</returns>
    public static string? GenerateGcodeFileName(long printJobId)
    {
        return printJobId < 0 ? null : $"job_{printJobId}{GcodeExtension}";
    }

    /// <summary>
    ///     Generates the filename for an image file based on print job ID.
    /// </summary>
    /// <param name="printJobId">The print job ID</param>
    /// <returns>Generated image filename, or null if printJobId is invalid (negative)</returns>
    public static string? GenerateImageFileName(long printJobId)
    {
        return printJobId < 0 ? null : $"job_{printJobId}_image{ImageExtension}";
    }
}