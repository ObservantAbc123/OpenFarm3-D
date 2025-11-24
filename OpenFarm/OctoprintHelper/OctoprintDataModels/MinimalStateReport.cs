namespace OctoprintHelper;

/// <summary>
/// Represents a minimal state report containing essential operational flags from an OctoPrint printer instance.
/// This lightweight structure provides the core status information needed to determine if a printer is available for new jobs.
/// All properties are deserialized from OctoPrint's API response using the corresponding JSON property names.
/// </summary>
/// <param name="Operational">Indicates whether the printer is operational and able to receive commands.</param>
/// <param name="Printing">Indicates whether the printer is currently executing a print job.</param>
/// <param name="Error">Indicates whether the printer is in an error state that requires attention.</param>
/// <param name="Ready">Indicates whether the printer is ready to accept new print jobs.</param>
public readonly record struct MinimalStateReport(
    /// <summary>
    /// Gets a value indicating whether the printer is operational and able to receive commands.
    /// This flag is true when the printer is connected and functioning normally.
    /// </summary>
    /// <value>True if the printer is operational; otherwise, false.</value>
    [property: System.Text.Json.Serialization.JsonPropertyName("operational")] bool Operational,

    /// <summary>
    /// Gets a value indicating whether the printer is currently executing a print job.
    /// This flag is true when a print is in progress, paused, or otherwise actively managed.
    /// </summary>
    /// <value>True if the printer is currently printing; otherwise, false.</value>
    [property: System.Text.Json.Serialization.JsonPropertyName("printing")] bool Printing,

    /// <summary>
    /// Gets a value indicating whether the printer is in an error state that requires attention.
    /// This flag is true when hardware errors, communication issues, or other problems are detected.
    /// </summary>
    /// <value>True if the printer is in an error state; otherwise, false.</value>
    [property: System.Text.Json.Serialization.JsonPropertyName("error")] bool Error,

    /// <summary>
    /// Gets a value indicating whether the printer is ready to accept new print jobs.
    /// This flag is true when the printer is operational, not printing, and not in an error state.
    /// </summary>
    /// <value>True if the printer is ready for new jobs; otherwise, false.</value>
    [property: System.Text.Json.Serialization.JsonPropertyName("ready")] bool Ready
    );
