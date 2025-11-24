using System.Net;
using System.Text.Json;

namespace OctoprintHelper
{
        /// <summary>
        /// Interface defining operations for communicating with OctoPrint 3D printer management software.
        /// Provides methods for printer state management, job control, file operations, and connection handling.
        /// </summary>
        public interface IOctoprintHelper
        {
                #region GET Helpers
                /// <summary>
                /// Retrieves the current state of the printer, excluding temperature and SD card information.
                /// </summary>
                /// <param name="selectedInstance">The HTTP client configured for the specific printer instance.</param>
                /// <param name="ct">Cancellation token for the async operation.</param>
                /// <returns>HTTP response containing the printer state information.</returns>
                Task<HttpResponseMessage> GetPrinterState(HttpClient selectedInstance, CancellationToken ct = default);

                /// <summary>
                /// Retrieves a minimal printer state report containing only essential operational flags.
                /// </summary>
                /// <param name="selectedInstance">The HTTP client configured for the specific printer instance.</param>
                /// <param name="ct">Cancellation token for the async operation.</param>
                /// <returns>A MinimalStateReport containing operational, printing, error, and ready flags.</returns>
                Task<MinimalStateReport> GetPrinterStateMinimal(HttpClient selectedInstance, CancellationToken ct = default);

                /// <summary>
                /// Retrieves current temperature of the extruder nozzle. (by default, in Centigrade)
                /// </summary>
                /// <param name="selectedInstance">The HTTP client configured for the specific printer instance.</param>
                /// <param name="ct">Cancellation token for the async operation.</param>
                /// <returns></returns>
                Task<HttpResponseMessage> GetExtruderTemperature(HttpClient selectedInstance, CancellationToken ct = default);

                /// <summary>
                /// Retrieves the current progress information for an active print job.
                /// </summary>
                /// <param name="selectedInstance">The HTTP client configured for the specific printer instance.</param>
                /// <param name="ct">Cancellation token for the async operation.</param>
                /// <returns>A ProgressReport containing completion percentage, file position, and timing information.</returns>
                Task<ProgressReport> GetPrintProgress(HttpClient selectedInstance, CancellationToken ct = default);

                /// <summary>
                /// Retrieves the current connection state between OctoPrint and the printer.
                /// </summary>
                /// <param name="selectedInstance">The HTTP client configured for the specific printer instance.</param>
                /// <param name="ct">Cancellation token for the async operation.</param>
                /// <returns>HTTP response containing connection status information.</returns>
                Task<HttpResponseMessage> GetConnection(HttpClient selectedInstance, CancellationToken ct = default);

                /// <summary>
                /// Retrieves detailed information about the currently active print job.
                /// </summary>
                /// <param name="selectedInstance">The HTTP client configured for the specific printer instance.</param>
                /// <param name="ct">Cancellation token for the async operation.</param>
                /// <returns>HTTP response containing current job information including file details and progress.</returns>
                Task<HttpResponseMessage> GetCurrentJob(HttpClient selectedInstance, CancellationToken ct = default);

                /// <summary>
                /// Retrieves a list of all files previously uploaded to the printer's storage.
                /// </summary>
                /// <param name="selectedInstance">The HTTP client configured for the specific printer instance.</param>
                /// <param name="ct">Cancellation token for the async operation.</param>
                /// <returns>HTTP response containing the file listing with metadata.</returns>
                Task<HttpResponseMessage> GetUploadedFiles(HttpClient selectedInstance, CancellationToken ct = default);
                #endregion

                #region POST Helpers
                /// <summary>
                /// Initiates an active connection between OctoPrint and the printer using default settings.
                /// </summary>
                /// <param name="selectedInstance">The HTTP client configured for the specific printer instance.</param>
                /// <param name="ct">Cancellation token for the async operation.</param>
                /// <returns>HTTP response indicating the success or failure of the connection attempt.</returns>
                /// <exception cref="HttpRequestException">Thrown when the connection request fails.</exception>
                Task<HttpResponseMessage> Connect(HttpClient selectedInstance, CancellationToken ct = default);

                /// <summary>
                /// Terminates the active connection between OctoPrint and the printer.
                /// </summary>
                /// <param name="selectedInstance">The HTTP client configured for the specific printer instance.</param>
                /// <param name="ct">Cancellation token for the async operation.</param>
                /// <returns>HTTP response indicating the success or failure of the disconnection.</returns>
                /// <exception cref="HttpRequestException">Thrown when the disconnection request fails.</exception>
                Task<HttpResponseMessage> Disconnect(HttpClient selectedInstance, CancellationToken ct = default);

                /// <summary>
                /// Uploads a G-code file to the OctoPrint server and immediately starts printing it.
                /// </summary>
                /// <param name="selectedInstance">The HTTP client configured for the specific printer instance.</param>
                /// <param name="gcode">The G-code file content as a byte array.</param>
                /// <param name="fileName">The name to assign to the uploaded file.</param>
                /// <param name="ct">Cancellation token for the async operation.</param>
                /// <returns>HTTP response indicating the success or failure of the upload and print operation.</returns>
                Task<HttpResponseMessage> UploadAndPrintFile(HttpClient selectedInstance, byte[] gcode, string fileName, CancellationToken ct = default);

                /// <summary>
                /// Uploads a G-code file from a local file path to the OctoPrint server and immediately starts printing it.
                /// This method is intended for testing purposes only.
                /// </summary>
                /// <param name="selectedInstance">The HTTP client configured for the specific printer instance.</param>
                /// <param name="path">The local file system path to the G-code file.</param>
                /// <param name="ct">Cancellation token for the async operation.</param>
                /// <returns>HTTP response indicating the success or failure of the upload and print operation.</returns>
                Task<HttpResponseMessage> UploadAndPrintFile(HttpClient selectedInstance, string path, CancellationToken ct = default);

                /// <summary>
                /// Sends a control command to manage the current print job (start, cancel, restart, etc.).
                /// </summary>
                /// <param name="selectedInstance">The HTTP client configured for the specific printer instance.</param>
                /// <param name="command">The job control command to execute (e.g., "start", "cancel", "restart").</param>
                /// <param name="ct">Cancellation token for the async operation.</param>
                /// <returns>HTTP response indicating the success or failure of the command execution.</returns>
                Task<HttpResponseMessage> SendJobControlCommand(HttpClient selectedInstance, string command, CancellationToken ct = default);

                /// <summary>
                /// Pauses the currently active print job.
                /// </summary>
                /// <param name="selectedInstance">The HTTP client configured for the specific printer instance.</param>
                /// <param name="ct">Cancellation token for the async operation.</param>
                /// <returns>HTTP response indicating the success or failure of the pause operation.</returns>
                Task<HttpResponseMessage> PausePrintingJob(HttpClient selectedInstance, CancellationToken ct = default);

                /// <summary>
                /// Resumes a previously paused print job.
                /// </summary>
                /// <param name="selectedInstance">The HTTP client configured for the specific printer instance.</param>
                /// <param name="ct">Cancellation token for the async operation.</param>
                /// <returns>HTTP response indicating the success or failure of the resume operation.</returns>
                Task<HttpResponseMessage> ResumePrintingJob(HttpClient selectedInstance, CancellationToken ct = default);

                #endregion

                #region Miscellaneous Helpers
                /// <summary>
                /// Converts an HTTP response message to its raw string representation for debugging purposes.
                /// </summary>
                /// <param name="response">The HTTP response message to convert to string.</param>
                /// <param name="ct">Cancellation token for the async operation.</param>
                /// <returns>The raw response content as a string, or null if the conversion fails.</returns>
                string? PrintRawResponse(HttpResponseMessage response, CancellationToken ct = default);
                #endregion

        }
}
