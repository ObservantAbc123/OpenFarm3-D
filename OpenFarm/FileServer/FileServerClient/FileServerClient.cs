using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FileServerClient;

/// <summary>
///     HTTP client for interacting with the FileServer REST API.
///     Wraps G-code endpoints for easy consumption by other services.
/// </summary>
public class FileServerClient : IDisposable
{
    private readonly HttpClient _http;

    /// <summary>
    ///     Constructs a new FileServerClient.
    /// </summary>
    /// <param name="baseAddress">Base URL of the FileServer service, e.g. "http://localhost:5001"</param>
    public FileServerClient(string baseAddress)
    {
        if (string.IsNullOrWhiteSpace(baseAddress))
            throw new ArgumentException("Base address is required", nameof(baseAddress));

        _http = new HttpClient
        {
            BaseAddress = new Uri(baseAddress.TrimEnd('/') + "/")
        };
    }

    public void Dispose()
    {
        _http.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Retrieves the gcode file for a print job as raw bytes.
    ///     Returns null on 404.
    /// </summary>
    public async Task<byte[]?> GetGcodeBytesAsync(long printJobId, CancellationToken ct = default)
    {
        if (!EnsureValidId(printJobId)) return null;
        var url = $"api/gcode/{printJobId}/bytes";
        var resp = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        try
        {
            if (resp.StatusCode == HttpStatusCode.NotFound) return null;
            resp.EnsureSuccessStatusCode();
            return await resp.Content.ReadAsByteArrayAsync(ct);
        }
        finally
        {
            resp.Dispose();
        }
    }

    /// <summary>
    ///     Retrieves the gcode file stream for a print job.
    ///     Caller owns the returned stream aka you dispose it. Returns null on 404.
    /// </summary>
    public async Task<Stream?> GetGcodeStreamAsync(long printJobId, CancellationToken ct = default)
    {
        if (!EnsureValidId(printJobId)) return null;
        var url = $"api/gcode/{printJobId}";
        var resp = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        if (resp.StatusCode == HttpStatusCode.NotFound)
        {
            resp.Dispose();
            return null;
        }

        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadAsStreamAsync(ct);
    }

    /// <summary>
    ///     Lists gcode files. Returns server's list payload.
    /// </summary>
    public async Task<BucketListResponse> ListGcodeFilesAsync(CancellationToken ct = default)
    {
        var resp = await _http.GetAsync("api/gcode", ct);
        resp.EnsureSuccessStatusCode();
        using var stream = await resp.Content.ReadAsStreamAsync(ct);
        var payload = await JsonSerializer.DeserializeAsync<BucketListResponse>(stream, cancellationToken: ct);
        return payload ?? new BucketListResponse();
    }

    private static bool EnsureValidId(long id)
    {
        if (id <= 0) return false;
        return true;
    }
}

/// <summary>
///     Response model for bucket listing endpoints.
/// </summary>
public record BucketListResponse
{
    [JsonPropertyName("bucket")] public string? Bucket { get; init; }

    [JsonPropertyName("files")] public List<string> Files { get; init; } = new();

    [JsonPropertyName("count")] public int Count { get; init; }
}