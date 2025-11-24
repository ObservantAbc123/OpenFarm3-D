namespace OctoprintHelper;

/// <summary>
/// Represents a connection request packet for establishing or terminating communication between OctoPrint and a 3D printer.
/// This class encapsulates all the parameters required for connecting to or disconnecting from a printer via OctoPrint's connection API.
/// </summary>
public class ConnectionRequestPacket {
    /// <summary>
    /// Gets or sets the connection command to execute. Valid values are "connect" to establish a connection or "disconnect" to terminate an existing connection.
    /// </summary>
    /// <value>The command string indicating the desired connection action.</value>
    public string command { get; set; }
    /// <summary>
    /// Gets or sets the serial port to use for the connection. Use "AUTO" to let OctoPrint automatically detect the appropriate port.
    /// </summary>
    /// <value>The serial port identifier or "AUTO" for automatic detection.</value>
    public string port { get; set; }
    /// <summary>
    /// Gets or sets the baud rate for serial communication with the printer. Common values include 115200, 250000, or 9600.
    /// </summary>
    /// <value>The baud rate in bits per second for serial communication.</value>
    public int baudrate { get; set; }
    /// <summary>
    /// Gets or sets the printer profile to use for the connection. Use "_default" to use the default printer profile configured in OctoPrint.
    /// </summary>
    /// <value>The name of the printer profile or "_default" for the default profile.</value>
    public string printerProfile { get; set; }
    /// <summary>
    /// Gets or sets a value indicating whether to save the connection parameters for future use. When true, OctoPrint will remember these settings.
    /// </summary>
    /// <value>True to save connection parameters; otherwise, false.</value>
    public bool save { get; set; }
    /// <summary>
    /// Gets or sets a value indicating whether to enable automatic connection on startup. When true, OctoPrint will automatically connect to the printer when it starts.
    /// </summary>
    /// <value>True to enable automatic connection; otherwise, false.</value>
    public bool autoconnect { get; set; }

    /// <summary>
    /// Initializes a new instance of the ConnectionRequestPacket class with the specified connection parameters.
    /// </summary>
    /// <param name="command">The connection command ("connect" or "disconnect").</param>
    /// <param name="port">The serial port identifier or "AUTO" for automatic detection.</param>
    /// <param name="baudrate">The baud rate for serial communication.</param>
    /// <param name="printerProfile">The printer profile name or "_default" for the default profile.</param>
    /// <param name="save">True to save connection parameters for future use; otherwise, false.</param>
    /// <param name="autoconnect">True to enable automatic connection on startup; otherwise, false.</param>
    public ConnectionRequestPacket(string command, string port, int baudrate, string printerProfile, bool save, bool autoconnect) {
        this.command = command;
        this.port = port;
        this.baudrate = baudrate;
        this.printerProfile = printerProfile;
        this.save = save;
        this.autoconnect = autoconnect;
    }
}
