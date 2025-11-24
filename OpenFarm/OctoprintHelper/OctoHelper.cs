using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using static OctoprintHelper.IOctoprintHelper;

namespace OctoprintHelper;

/// <summary>
/// Implementation of IOctoprintHelper that provides concrete methods for communicating with OctoPrint 3D printer management software.
/// Handles HTTP requests to OctoPrint REST API endpoints for printer control, job management, and file operations.
/// </summary>
public class OctoHelper : IOctoprintHelper
{
    #region GET Helpers

    /// <summary>
    /// Retrieves the current state of the printer, excluding temperature and SD card information.
    /// Makes a GET request to the /api/printer endpoint with specific exclusions for performance.
    /// </summary>
    /// <param name="selectedInstance">The HTTP client configured for the specific printer instance.</param>
    /// <param name="ct">Cancellation token for the async operation.</param>
    /// <returns>HTTP response containing the printer state information in JSON format.</returns>
    /// <exception cref="HttpRequestException">Thrown when the API request fails or returns an error status code.</exception>
    public async Task<HttpResponseMessage> GetPrinterState(HttpClient selectedInstance, CancellationToken ct = default)
    {
        HttpResponseMessage response = await selectedInstance!.GetAsync("/api/printer?exclude=temperature,sd");
        return response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Retrieves current temperature of the extruder nozzle. (by default, in Centigrade)
    /// </summary>
    /// <param name="selectedInstance"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public async Task<HttpResponseMessage> GetExtruderTemperature(HttpClient selectedInstance, CancellationToken ct = default)
    {
        HttpResponseMessage response = await selectedInstance!.GetAsync("/api/printer/tool");
        return response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Retrieves a minimal printer state report containing only essential operational flags.
    /// Parses the JSON response to extract operational, printing, error, and ready status flags.
    /// </summary>
    /// <param name="selectedInstance">The HTTP client configured for the specific printer instance.</param>
    /// <param name="ct">Cancellation token for the async operation.</param>
    /// <returns>A MinimalStateReport containing operational, printing, error, and ready flags.</returns>
    /// <exception cref="HttpRequestException">Thrown when the API request fails or returns an error status code.</exception>
    /// <exception cref="JsonException">Thrown when the response JSON cannot be parsed.</exception>
    public async Task<MinimalStateReport> GetPrinterStateMinimal(HttpClient selectedInstance, CancellationToken ct = default)
    {
        HttpResponseMessage response = await selectedInstance!.GetAsync("/api/printer?exclude=temperature,sd");
        response.EnsureSuccessStatusCode();
        JsonDocument doc = JsonDocument.Parse(response.Content.ReadAsStream());
        MinimalStateReport state = doc.RootElement.GetProperty("state").GetProperty("flags").Deserialize<MinimalStateReport>();
        return state;
    }

    /// <summary>
    /// Retrieves the current connection state between OctoPrint and the printer.
    /// Makes a GET request to the /api/connection endpoint.
    /// </summary>
    /// <param name="selectedInstance">The HTTP client configured for the specific printer instance.</param>
    /// <param name="ct">Cancellation token for the async operation.</param>
    /// <returns>HTTP response containing connection status information.</returns>
    /// <exception cref="HttpRequestException">Thrown when the API request fails or returns an error status code.</exception>
    public async Task<HttpResponseMessage> GetConnection(HttpClient selectedInstance, CancellationToken ct = default)
    {
        HttpResponseMessage response = await selectedInstance!.GetAsync("/api/connection");
        return response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Retrieves the current progress information for an active print job.
    /// Parses the job API response to extract progress-specific data.
    /// </summary>
    /// <param name="selectedInstance">The HTTP client configured for the specific printer instance.</param>
    /// <param name="ct">Cancellation token for the async operation.</param>
    /// <returns>A ProgressReport containing completion percentage, file position, and timing information.</returns>
    /// <exception cref="HttpRequestException">Thrown when the API request fails or returns an error status code.</exception>
    /// <exception cref="JsonException">Thrown when the response JSON cannot be parsed.</exception>
    public async Task<ProgressReport> GetPrintProgress(HttpClient selectedInstance, CancellationToken ct = default)
    {
        HttpResponseMessage response = await selectedInstance!.GetAsync("/api/job");
        response.EnsureSuccessStatusCode();
        JsonDocument doc = JsonDocument.Parse(response.Content.ReadAsStream());
        ProgressReport progress = doc.RootElement.GetProperty("progress").Deserialize<ProgressReport>();
        return progress;
    }

    /// <summary>
    /// Retrieves detailed information about the currently active print job.
    /// Makes a GET request to the /api/job endpoint for complete job information.
    /// </summary>
    /// <param name="selectedInstance">The HTTP client configured for the specific printer instance.</param>
    /// <param name="ct">Cancellation token for the async operation.</param>
    /// <returns>HTTP response containing current job information including file details and progress.</returns>
    /// <exception cref="HttpRequestException">Thrown when the API request fails or returns an error status code.</exception>
    public async Task<HttpResponseMessage> GetCurrentJob(HttpClient selectedInstance, CancellationToken ct = default)
    {
        HttpResponseMessage response = await selectedInstance!.GetAsync("/api/job");
        return response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Retrieves a list of all files previously uploaded to the printer's storage.
    /// Makes a GET request to the /api/files endpoint to list available files.
    /// </summary>
    /// <param name="selectedInstance">The HTTP client configured for the specific printer instance.</param>
    /// <param name="ct">Cancellation token for the async operation.</param>
    /// <returns>HTTP response containing the file listing with metadata.</returns>
    /// <exception cref="HttpRequestException">Thrown when the API request fails or returns an error status code.</exception>
    public async Task<HttpResponseMessage> GetUploadedFiles(HttpClient selectedInstance, CancellationToken ct = default)
    {
        HttpResponseMessage response = await selectedInstance!.GetAsync("/api/files");
        return response.EnsureSuccessStatusCode();
    }
    #endregion

    #region POST Helpers
    /// <summary>
    /// Initiates an active connection between OctoPrint and the printer using default settings.
    /// Sends a connection request with AUTO port detection and standard baud rate.
    /// </summary>
    /// <param name="selectedInstance">The HTTP client configured for the specific printer instance.</param>
    /// <param name="ct">Cancellation token for the async operation.</param>
    /// <returns>HTTP response indicating the success or failure of the connection attempt.</returns>
    /// <exception cref="HttpRequestException">Thrown when the connection request fails or returns an error status code.</exception>
    public async Task<HttpResponseMessage> Connect(HttpClient selectedInstance, CancellationToken ct = default)
    {
        ConnectionRequestPacket body = new ConnectionRequestPacket("connect", "AUTO", 115200, "_default", true, true);
        HttpContent serializedBody = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        HttpResponseMessage connectionResponse = await selectedInstance.PostAsync("/api/connection", serializedBody);
        return connectionResponse.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Terminates the active connection between OctoPrint and the printer.
    /// Sends a disconnection request to safely disconnect from the printer.
    /// </summary>
    /// <param name="selectedInstance">The HTTP client configured for the specific printer instance.</param>
    /// <param name="ct">Cancellation token for the async operation.</param>
    /// <returns>HTTP response indicating the success or failure of the disconnection.</returns>
    /// <exception cref="HttpRequestException">Thrown when the disconnection request fails or returns an error status code.</exception>
    public async Task<HttpResponseMessage> Disconnect(HttpClient selectedInstance, CancellationToken ct = default)
    {
        ConnectionRequestPacket body = new ConnectionRequestPacket("disconnect", "AUTO", 115200, "_default", true, true);
        HttpContent serializedBody = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        var connectionResponse = await selectedInstance!.PostAsync("/api/connection", serializedBody);
        return connectionResponse.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Uploads a G-code file to the OctoPrint server and immediately starts printing it.
    /// Uses multipart form data to upload the file with select and print flags enabled.
    /// Handles file conflicts by attempting to overwrite existing files with the same name.
    /// </summary>
    /// <param name="selectedInstance">The HTTP client configured for the specific printer instance.</param>
    /// <param name="gcode">The G-code file content as a byte array.</param>
    /// <param name="fileName">The name to assign to the uploaded file.</param>
    /// <param name="ct">Cancellation token for the async operation.</param>
    /// <returns>HTTP response indicating the success or failure of the upload and print operation.</returns>
    /// <exception cref="HttpRequestException">Thrown when the upload request fails or returns an error status code.</exception>
    public async Task<HttpResponseMessage> UploadAndPrintFile(HttpClient selectedInstance, byte[] gcode, string fileName, CancellationToken ct = default)
    {
        if (gcode == null || gcode.Length == 0)
            return new HttpResponseMessage(HttpStatusCode.BadRequest);
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(gcode);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("text/plain");
        content.Add(fileContent, "file", fileName);
        content.Add(new StringContent("true"), "select");
        content.Add(new StringContent("true"), "print");
        var response = await selectedInstance!.PostAsync("/api/files/local", content);
        if (response.StatusCode == HttpStatusCode.Conflict) // issue print only if override trip occurs
        {
            response = await selectedInstance!.PostAsync($"/api/files/local/{fileName}", content);
        }
        return response.EnsureSuccessStatusCode();
    }



    /// <summary>
    /// Uploads a G-code file from a local file path to the OctoPrint server and immediately starts printing it.
    /// This method is intended for testing purposes only. Reads the file from disk and delegates to the byte array overload.
    /// </summary>
    /// <param name="selectedInstance">The HTTP client configured for the specific printer instance.</param>
    /// <param name="path">The local file system path to the G-code file.</param>
    /// <param name="ct">Cancellation token for the async operation.</param>
    /// <returns>HTTP response indicating the success or failure of the upload and print operation.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the specified file path does not exist.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when access to the file is denied.</exception>
    /// <exception cref="HttpRequestException">Thrown when the upload request fails or returns an error status code.</exception>
    public async Task<HttpResponseMessage> UploadAndPrintFile(HttpClient selectedInstance, string path, CancellationToken ct = default)
    {
        byte[] gcode = File.ReadAllBytes(path);
        string fileName = Path.GetFileName(path);
        return await UploadAndPrintFile(selectedInstance, gcode, fileName);
    }

    /// <summary>
    /// Sends a control command to manage the current print job (start, cancel, restart, etc.).
    /// Validates the command parameter and sends it as JSON to the job control endpoint.
    /// </summary>
    /// <param name="selectedInstance">The HTTP client configured for the specific printer instance.</param>
    /// <param name="command">The job control command to execute (e.g., "start", "cancel", "restart").</param>
    /// <param name="ct">Cancellation token for the async operation.</param>
    /// <returns>HTTP response indicating the success or failure of the command execution.</returns>
    /// <exception cref="HttpRequestException">Thrown when the command request fails or returns an error status code.</exception>
    public async Task<HttpResponseMessage> SendJobControlCommand(HttpClient selectedInstance, string command, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(command))
            return new HttpResponseMessage(HttpStatusCode.BadRequest);
        var content = new StringContent(JsonSerializer.Serialize(new { command = command }), Encoding.UTF8, "application/json");
        var response = await selectedInstance!.PostAsync("/api/job", content);
        return response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Pauses the currently active print job.
    /// Uses OctoPrint's command-action system to send a pause command with pause action.
    /// </summary>
    /// <param name="selectedInstance">The HTTP client configured for the specific printer instance.</param>
    /// <param name="ct">Cancellation token for the async operation.</param>
    /// <returns>HTTP response indicating the success or failure of the pause operation.</returns>
    /// <exception cref="HttpRequestException">Thrown when the pause request fails or returns an error status code.</exception>
    public async Task<HttpResponseMessage> PausePrintingJob(HttpClient selectedInstance, CancellationToken ct = default)
    {
        // octoprint API specifies the command-action system, read more there; yes it is clumsy and no i don't like it either
        var content = new StringContent(JsonSerializer.Serialize(new { command = "pause", action = "pause" }), Encoding.UTF8, "application/json");
        var response = await selectedInstance!.PostAsync("/api/job", content);
        return response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Resumes a previously paused print job.
    /// Uses OctoPrint's command-action system to send a pause command with resume action.
    /// </summary>
    /// <param name="selectedInstance">The HTTP client configured for the specific printer instance.</param>
    /// <param name="ct">Cancellation token for the async operation.</param>
    /// <returns>HTTP response indicating the success or failure of the resume operation.</returns>
    /// <exception cref="HttpRequestException">Thrown when the resume request fails or returns an error status code.</exception>
    public async Task<HttpResponseMessage> ResumePrintingJob(HttpClient selectedInstance, CancellationToken ct = default)
    {
        // octoprint API specifies the command-action system, read more there; yes it is clumsy and no i don't like it either
        var content = new StringContent(JsonSerializer.Serialize(new { command = "pause", action = "resume" }), Encoding.UTF8, "application/json");
        var response = await selectedInstance!.PostAsync("/api/job", content);
        return response.EnsureSuccessStatusCode();
    }
    #endregion



    #region Debug
    /// <summary>
    /// Converts an HTTP response message to its raw string representation for debugging purposes.
    /// Reads the response content stream as ASCII text for inspection and troubleshooting.
    /// </summary>
    /// <param name="response">The HTTP response message to convert to string.</param>
    /// <param name="ct">Cancellation token for the async operation.</param>
    /// <returns>The raw response content as a string, or null if the conversion fails.</returns>
    public string? PrintRawResponse(HttpResponseMessage response, CancellationToken ct = default)
    {
        return new StreamReader(response.Content.ReadAsStream(), Encoding.ASCII).ReadToEnd();
    }
    #endregion
}
