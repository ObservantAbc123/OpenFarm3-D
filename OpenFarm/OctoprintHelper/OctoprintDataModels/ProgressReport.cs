namespace OctoprintHelper;

/// <summary>
/// Represents progress information for an active print job from OctoPrint's job progress API.
/// This structure contains completion status, timing information, and file position data for monitoring print job progress.
/// All properties are deserialized from OctoPrint's /api/job endpoint response using the corresponding JSON property names.
/// </summary>
/// <param name="Completion">The completion percentage of the current print job (0-100).</param>
/// <param name="FilePos">The current position in the G-code file being processed.</param>
/// <param name="PrintTime">The elapsed time since the print job started, measured in seconds.</param>
/// <param name="PrintTimeLeft">The estimated remaining time for the print job to complete, measured in seconds.</param>
/// <param name="PrintTimeLeftOrigin">The source or method used to calculate the remaining print time estimate.</param>
public readonly record struct ProgressReport(
    /// <summary>
    /// Gets the completion percentage of the current print job.
    /// This value ranges from 0.0 (just started) to 100.0 (fully complete) and represents the progress through the G-code file.
    /// </summary>
    /// <value>A float value representing the completion percentage (0-100).</value>
    [property: System.Text.Json.Serialization.JsonPropertyName("completion")] float Completion,

    /// <summary>
    /// Gets the current position in the G-code file being processed.
    /// This value represents the byte offset from the beginning of the file, indicating how much of the file has been processed.
    /// </summary>
    /// <value>An integer representing the current byte position in the G-code file.</value>
    [property: System.Text.Json.Serialization.JsonPropertyName("filepos")] int FilePos,

    /// <summary>
    /// Gets the elapsed time since the print job started.
    /// This value is measured in seconds and represents the total time the printer has been actively working on this job.
    /// </summary>
    /// <value>An integer representing the elapsed print time in seconds.</value>
    [property: System.Text.Json.Serialization.JsonPropertyName("printTime")] int PrintTime,

    /// <summary>
    /// Gets the estimated remaining time for the print job to complete.
    /// This value is measured in seconds and represents OctoPrint's best estimate of how long the job will take to finish.
    /// The accuracy depends on the estimation method used (see PrintTimeLeftOrigin).
    /// </summary>
    /// <value>An integer representing the estimated remaining print time in seconds, or -1 if estimation is unavailable.</value>
    [property: System.Text.Json.Serialization.JsonPropertyName("printTimeLeft")] int PrintTimeLeft,

    /// <summary>
    /// Gets the source or method used to calculate the remaining print time estimate.
    /// Common values include "linear" (based on current progress rate), "analysis" (based on G-code analysis),
    /// or "estimate" (based on historical data and heuristics).
    /// </summary>
    /// <value>A string indicating the estimation method used for the print time calculation.</value>
    [property: System.Text.Json.Serialization.JsonPropertyName("printTimeLeftOrigin")] string PrintTimeLeftOrigin
    );
