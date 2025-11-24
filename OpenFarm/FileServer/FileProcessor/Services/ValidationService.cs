using FileProcessor.Models;

namespace FileProcessor.Services;

/// <summary>
///     Provides validation methods for URLs and file messages.
/// </summary>
public static class ValidationService
{
    /// <summary>
    ///     Validates if a URL is a valid URL for downloading files from.
    /// </summary>
    /// <param name="url">The URL to validate</param>
    /// <returns>True if the URL is valid, false otherwise</returns>
    private static bool IsValidUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;

        return uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;
    }

    /// <summary>
    ///     Validates a complete file message for all required fields.
    /// </summary>
    /// <param name="message">The file message to validate</param>
    /// <param name="error">Output parameter containing validation error details if validation fails</param>
    /// <returns>True if the message is valid, false otherwise</returns>
    public static bool ValidateFileMessage(FileMessage message, out string? error)
    {
        error = null;

        if (string.IsNullOrWhiteSpace(message.SourceUrl))
        {
            error = "Source URL is required";
            return false;
        }

        if (message.PrintJobId > 0)
            return true;

        error = "Valid printJobId is required";
        return false;
    }
}