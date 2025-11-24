# OctoPrint Helper Library

This library simplifies interaction with OctoPrint's REST API, offering strongly-typed methods for printer control, job
management, and file operations.

## Overview

OctoPrint Helper abstracts the complexity of OctoPrint's REST API into a clean, easy-to-use .NET interface. It handles
HTTP communication, JSON serialization/deserialization, and provides robust error handling for reliable 3D printer
management in .NET applications.

## Features

- **Complete API Coverage**: Supports all major OctoPrint REST API endpoints
- **Strongly Typed**: Uses custom data models for type-safe operations
- **Async/Await Support**: All operations are asynchronous with cancellation token support
- **Error Handling**: Comprehensive exception handling with meaningful error messages
- **Connection Management**: Automatic connection state management and validation
- **File Operations**: Upload G-code files directly from memory or disk
- **Job Control**: Start, pause, resume, and cancel print jobs
- **Real-Time Monitoring**: Get live printer status and job progress
- **Debug Support**: Raw response inspection for troubleshooting

## Components

### Core Interface

- **`IOctoprintHelper`**: Main interface defining all OctoPrint operations
- **`OctoHelper`**: Concrete implementation of the interface

### Data Models

- **`MinimalStateReport`**: Essential printer status flags (operational, printing, error, ready)
- **`ProgressReport`**: Print job progress information (completion %, file position, timing)
- **`ConnectionRequestPacket`**: Connection/disconnection request parameters

## Quick Start

### Basic Setup

```csharp
using OctoprintHelper;

// Create HTTP client with OctoPrint configuration
var httpClient = new HttpClient();
httpClient.BaseAddress = new Uri("http://your-octoprint-ip");
httpClient.DefaultRequestHeaders.Add("X-Api-Key", "your-api-key");

// Create helper instance
IOctoprintHelper octoPrint = new OctoHelper();
```

### Get Printer Status

```csharp
// Get full printer state
var response = await octoPrint.GetPrinterState(httpClient);
var stateJson = await response.Content.ReadAsStringAsync();

// Get minimal state report
var state = await octoPrint.GetPrinterStateMinimal(httpClient);
Console.WriteLine($"Operational: {state.Operational}, Printing: {state.Printing}");
```

### Upload and Print File

```csharp
// From byte array
byte[] gcode = File.ReadAllBytes("model.gcode");
var response = await octoPrint.UploadAndPrintFile(httpClient, gcode, "model.gcode");

// From file path (testing/development)
var response = await octoPrint.UploadAndPrintFile(httpClient, "path/to/model.gcode");
```

### Job Control

```csharp
// Pause current print
await octoPrint.PausePrintingJob(httpClient);

// Resume paused print
await octoPrint.ResumePrintingJob(httpClient);

// Send custom command
await octoPrint.SendJobControlCommand(httpClient, "cancel");
```

### Monitor Print Progress

```csharp
var progress = await octoPrint.GetPrintProgress(httpClient);
Console.WriteLine($"Progress: {progress.Completion}%");
Console.WriteLine($"Print Time: {progress.PrintTime} seconds");
Console.WriteLine($"File Position: {progress.FilePos}");
```

## API Reference

### GET Operations

| Method                   | Description                                     | Returns               |
|--------------------------|-------------------------------------------------|-----------------------|
| `GetPrinterState`        | Full printer state (excluding temp/SD)          | `HttpResponseMessage` |
| `GetPrinterStateMinimal` | Essential status flags only                     | `MinimalStateReport`  |
| `GetConnection`          | Connection status between OctoPrint and printer | `HttpResponseMessage` |
| `GetCurrentJob`          | Current job information                         | `HttpResponseMessage` |
| `GetPrintProgress`       | Print job progress details                      | `ProgressReport`      |
| `GetUploadedFiles`       | List of files on printer storage                | `HttpResponseMessage` |

### POST Operations

| Method                  | Description                       | Returns               |
|-------------------------|-----------------------------------|-----------------------|
| `Connect`               | Connect OctoPrint to printer      | `HttpResponseMessage` |
| `Disconnect`            | Disconnect OctoPrint from printer | `HttpResponseMessage` |
| `UploadAndPrintFile`    | Upload G-code and start printing  | `HttpResponseMessage` |
| `SendJobControlCommand` | Send job control command          | `HttpResponseMessage` |
| `PausePrintingJob`      | Pause active print job            | `HttpResponseMessage` |
| `ResumePrintingJob`     | Resume paused print job           | `HttpResponseMessage` |

### Utility Operations

| Method             | Description                    | Returns   |
|--------------------|--------------------------------|-----------|
| `PrintRawResponse` | Convert response to raw string | `string?` |

## Configuration

### HTTP Client Setup

```csharp
var httpClient = new HttpClient();
httpClient.BaseAddress = new Uri("http://192.168.1.100");  // OctoPrint IP
httpClient.DefaultRequestHeaders.Add("X-Api-Key", "your-api-key-here");
httpClient.Timeout = TimeSpan.FromSeconds(30);  // Optional timeout
```

### Dependency Injection

```csharp
// In Program.cs or Startup.cs
services.AddSingleton<IOctoprintHelper, OctoHelper>();

// Configure HTTP client per printer
services.AddHttpClient<IOctoprintHelper>("printer1", client => {
    client.BaseAddress = new Uri("http://192.168.1.100");
    client.DefaultRequestHeaders.Add("X-Api-Key", "api-key-1");
});
```

## Error Handling

### Common Exceptions

- **`HttpRequestException`**: Network or HTTP-level errors
- **`JsonException`**: JSON parsing errors from API responses
- **`TimeoutException`**: Request timeout exceeded
- **`UnauthorizedAccessException`**: Invalid API key or permissions

## Data Models

### MinimalStateReport

A readonly record struct containing essential printer status flags from OctoPrint's API:

```csharp
public readonly record struct MinimalStateReport(
    bool Operational,    // Printer is operational and able to receive commands
    bool Printing,       // Currently executing a print job
    bool Error,          // In an error state requiring attention
    bool Ready           // Ready to accept new print jobs
);
```

### ProgressReport

A readonly record struct containing print job progress information from OctoPrint's `/api/job` endpoint:

```csharp
public readonly record struct ProgressReport(
    float Completion,           // Completion percentage (0-100)
    int FilePos,               // Current byte position in G-code file
    int PrintTime,             // Elapsed print time in seconds
    int PrintTimeLeft,         // Estimated remaining time in seconds (-1 if unavailable)
    string PrintTimeLeftOrigin // Estimation method ("linear", "analysis", "estimate")
);
```

### ConnectionRequestPacket

A class representing connection/disconnection requests to OctoPrint:

```csharp
public class ConnectionRequestPacket
{
    public string command { get; set; }        // "connect" or "disconnect"
    public string port { get; set; }           // Serial port or "AUTO"
    public int baudrate { get; set; }          // Baud rate (e.g., 115200)
    public string printerProfile { get; set; } // Profile name or "_default"
    public bool save { get; set; }             // Save connection parameters
    public bool autoconnect { get; set; }      // Enable auto-connect on startup
}
```

#### JSON Serialization

All data models use `System.Text.Json.Serialization.JsonPropertyName` attributes to map to OctoPrint's API response
format. The structures are designed for efficient deserialization and minimal memory footprint.

## Troubleshooting

### Common Issues

**Connection Refused**

- Verify OctoPrint is running and accessible
- Check IP address and port configuration
- Ensure network connectivity

**Unauthorized (401)**

- Verify API key is correct
- Check API key permissions in OctoPrint settings
- Ensure key is properly formatted in headers

## License

[TODO, couldn't remember licensing]

## Support

For issues and questions:

- Check the troubleshooting section above
- Review OctoPrint API documentation
- Write your feelings down in a journal
