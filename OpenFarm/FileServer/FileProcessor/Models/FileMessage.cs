namespace FileProcessor.Models;

public enum SourceType
{
    GoogleDrive,
    Jira,
    Test,
    Unknown
}

public class FileMessage
{
    public long PrintJobId { get; set; } = -1;
    public string SourceUrl { get; set; } = string.Empty;
    public SourceType SourceType { get; set; } = SourceType.Unknown;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class UploadResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
}