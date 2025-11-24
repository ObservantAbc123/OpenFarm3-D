# FileServer

A containerized ASP.NET Core service for processing file downloads via RabbitMQ messages and storing them in MinIO
object storage. Supports Google Drive integration, G-code files, and images with REST APIs.

## Features

- **RabbitMQ Integration**: Async file processing via message consumption
- **MinIO Storage**: Separate buckets for G-code files and images
- **Google Drive Integration**: Direct download using file IDs
- **REST APIs**: File upload and download
- **Security**: CORS configuration, input validation, resilient services
- **Health Monitoring**: Health check endpoint and structured logging

## Architecture

### Services

- **FileProcessor**: ASP.NET Core service (port 5001)
- **MinIO**: Object storage server (ports 9000, 9001)
- **RabbitMQ**: Message broker (ports 5672, 15672)

**Core Services**:

- `IFileDownloaderService` - Downloads from URLs and Google Drive
- `IGoogleDriveService` - Converts Google Drive file IDs to download URLs
- `IMinioService` - Manages MinIO object storage operations

**Background Services**:

- `RabbitMqConsumerService` - Processes file download messages
- `ConfigurationValidationService` - Validates required configuration on startup

**Utility Services**:

- `FileNameService` - Generates consistent filenames and constants
- `ValidationService` - URL and message validation

## Quick Start

### 1. Environment Configuration

**All variables are required** - the application will not start without them:

```bash
# MinIO Configuration
MINIO_ROOT_USER=your-minio-username
MINIO_ROOT_PASSWORD=your-minio-password-min8chars
MINIO_USE_SSL=false

# RabbitMQ Configuration
RABBITMQ_HOST=rabbitmq
RABBITMQ_USER=your-rabbitmq-user
RABBITMQ_PASSWORD=your-rabbitmq-password

# CORS Configuration
ALLOWEDORIGINS__0=http://localhost:3000
ALLOWEDORIGINS__1=http://localhost:3001
ALLOWEDORIGINS__2=http://yourapp.com
...

# Optional Configuration
MAX_FILE_SIZE_MB=250
DOWNLOAD_TIMEOUT_SECONDS=300
```

### Verify Health

```bash
curl http://localhost:5001/health
```

Access points:

- MinIO Console: http://localhost:9001
- RabbitMQ Management: http://localhost:15672
- File Processor Health: http://localhost:5001/health

## API Endpoints

### G-code Files

#### Process via RabbitMQ

Send message to `file-download-queue`:

```json
{
  "JobId": 12345,
  "DownloadType": 0,  // 0=Test, 1=GoogleDrive, 2=Jira
  "DownloadUrl": "https://example.com/file.gcode"
}
```

For Google Drive files:

```json
{
  "JobId": 12345,
  "DownloadType": 1,
  "DownloadUrl": "1abc123def456ghi789"  // Google Drive file ID
}
```

#### Retrieve G-code Files

```bash
# Get as byte array
curl http://localhost:5001/api/gcode/{printJobID}/bytes

# Download as file
curl http://localhost:5001/api/gcode/{printJobID}
```

#### List G-code Files

```bash
curl http://localhost:5001/api/gcode
```

### Images

#### Upload Image

```bash
curl -X POST http://localhost:5001/api/images/{printJobID} \
  -F "file=@image.png"
```

Response (success):

```json
{
  "success": true,
  "printJobID": 12345
}
```

#### Download Image

```bash
curl http://localhost:5001/api/images/{printJobID}
```

#### List Images

```bash
curl http://localhost:5001/api/images
```

### Testing Endpoints

#### Send Test Message

```bash
curl -X POST http://localhost:5001/api/test/send-file-message \
  -H "Content-Type: application/json" \
  -d '{"sourceUrl": "https://example.com/file.gcode"}'
```

#### Send Google Drive Test

```bash
curl -X POST http://localhost:5001/api/test/google-drive-message \
  -H "Content-Type: application/json" \
  -d '{"fileId": "1abc123def456ghi789"}'
```

## File Processing Flow

1. **Message Received**: RabbitMQ message consumed
2. **Download Type Check**:
    - Google Drive: Uses `IGoogleDriveService.GetDirectDownloadUrl()`
    - Regular URL: Uses `IFileDownloaderService.DownloadFileAsync()`
3. **File Download**: With retry logic and size validation
4. **Storage**: File uploaded to appropriate MinIO bucket
5. **Notification**: Success message sent back to RabbitMQ

### Supported File Types

**G-code files**: `.gcode` files stored in `gcode-files` bucket
**Images**: `.png` files stored in `images` bucket

### File Naming Convention

- G-code: `job_{printJobID}.gcode`
- Images: `job_{printJobID}_image.png`

## Development

### Service Structure

```
FileProcessor/
├── Controllers/          # API controllers
├── Services/
│   ├── Interfaces/       # Service interfaces
│   └── *Service.cs       # Service implementations
├── Models/               # Data models
└── FileProcessor.cs      # Main application
```

## Monitoring & Troubleshooting

### Health Checks

```bash
curl http://localhost:5001/health
```

### Logs

```bash
# View logs
docker-compose logs -f file-processor

# Structured logging available in:
# - Console output
# - Daily rotating files (logs/)
```

### Common Issues

**Application won't start**:

- Check required environment variables are set
- Verify CORS `AllowedOrigins` configuration
- Check MinIO/RabbitMQ credentials

**File processing failures**:

- Check file size limits (`MAX_FILE_SIZE_MB`)
- Verify file types are supported
- Check MinIO bucket connectivity
- Review RabbitMQ message format

**API errors**:

- **400**: Bad request (validation errors)
- **404**: File not found
- **500**: Internal server error

### Testing

Run integration tests:

```bash
cd FileServer/Tests
./run-integration-test.sh
```

## C# Usage Examples

The following examples show how to use the FileServer endpoints from C# applications using `HttpClient`.

### Setup HttpClient

```csharp
using System.Text;
using System.Text.Json;

public class FileServerClient {
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;

    public FileServerClient(string baseUrl = "http://localhost:5001") {
        _httpClient = new HttpClient();
        _baseUrl = baseUrl;
    }

    public void Dispose() => _httpClient?.Dispose();
}
```

### G-code File Operations

#### 1. Download G-code File as Bytes

```csharp
/// <summary>
/// Downloads a G-code file as a byte array for processing in memory.
/// </summary>
/// <param name="printJobId">The print job ID</param>
/// <returns>The G-code file content as bytes</returns>
public async Task<byte[]> DownloadGcodeAsBytesAsync(int printJobId) {
    try {
        var response = await _httpClient.GetAsync($"{_baseUrl}/api/gcode/{printJobId}/bytes");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync();
    } catch (HttpRequestException ex) {
        throw new Exception($"Failed to download G-code file for job {printJobId}: {ex.Message}", ex);
    }
}
```

#### 2. Download G-code File as Stream

```csharp
/// <summary>
/// Downloads a G-code file as a stream for direct file saving.
/// </summary>
/// <param name="printJobId">The print job ID</param>
/// <param name="outputPath">Path to save the downloaded file</param>
public async Task DownloadGcodeAsFileAsync(int printJobId, string outputPath) {
    try {
        var response = await _httpClient.GetAsync($"{_baseUrl}/api/gcode/{printJobId}");
        response.EnsureSuccessStatusCode();

        using var fileStream = File.Create(outputPath);
        using var httpStream = await response.Content.ReadAsStreamAsync();
        await httpStream.CopyToAsync(fileStream);
    } catch (HttpRequestException ex) {
        throw new Exception($"Failed to download G-code file for job {printJobId}: {ex.Message}", ex);
    }
}
```

### Image Operations

#### 1. Upload Image

```csharp
/// <summary>
/// Uploads an image file for a specific print job.
/// </summary>
/// <param name="printJobId">The print job ID</param>
/// <param name="imagePath">Path to the image file</param>
/// <returns>Upload response indicating success</returns>
public async Task<bool> UploadImageAsync(int printJobId, string imagePath) {
    try {
        using var form = new MultipartFormDataContent();
        using var fileContent = new ByteArrayContent(await File.ReadAllBytesAsync(imagePath));

        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/png");
        form.Add(fileContent, "file", Path.GetFileName(imagePath));

        var response = await _httpClient.PostAsync($"{_baseUrl}/api/images/{printJobId}", form);
        response.EnsureSuccessStatusCode();

        var responseContent = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<UploadResponse>(responseContent);

        return result?.Success ?? false;
    } catch (HttpRequestException ex) {
        throw new Exception($"Failed to upload image for job {printJobId}: {ex.Message}", ex);
    }
}

/// <summary>
/// Uploads an image from a byte array.
/// </summary>
/// <param name="printJobId">The print job ID</param>
/// <param name="imageBytes">Image data as bytes</param>
/// <param name="fileName">Name for the uploaded file</param>
/// <returns>Upload response indicating success</returns>
public async Task<bool> UploadImageFromBytesAsync(int printJobId, byte[] imageBytes, string fileName = "image.png") {
    try {
        using var form = new MultipartFormDataContent();
        using var fileContent = new ByteArrayContent(imageBytes);

        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/png");
        form.Add(fileContent, "file", fileName);

        var response = await _httpClient.PostAsync($"{_baseUrl}/api/images/{printJobId}", form);
        response.EnsureSuccessStatusCode();

        var responseContent = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<UploadResponse>(responseContent);

        return result?.Success ?? false;
    } catch (HttpRequestException ex) {
        throw new Exception($"Failed to upload image for job {printJobId}: {ex.Message}", ex);
    }
}
```

#### 2. Download Image

```csharp
/// <summary>
/// Downloads an image for a specific print job.
/// </summary>
/// <param name="printJobId">The print job ID</param>
/// <returns>The image content as bytes</returns>
public async Task<byte[]> DownloadImageAsync(int printJobId) {
    try {
        var response = await _httpClient.GetAsync($"{_baseUrl}/api/images/{printJobId}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync();
    } catch (HttpRequestException ex) {
        throw new Exception($"Failed to download image for job {printJobId}: {ex.Message}", ex);
    }
}

/// <summary>
/// Downloads an image and saves it to a file.
/// </summary>
/// <param name="printJobId">The print job ID</param>
/// <param name="outputPath">Path to save the downloaded image</param>
public async Task DownloadImageToFileAsync(int printJobId, string outputPath) {
    try {
        var response = await _httpClient.GetAsync($"{_baseUrl}/api/images/{printJobId}");
        response.EnsureSuccessStatusCode();

        using var fileStream = File.Create(outputPath);
        using var httpStream = await response.Content.ReadAsStreamAsync();
        await httpStream.CopyToAsync(fileStream);
    } catch (HttpRequestException ex) {
        throw new Exception($"Failed to download image for job {printJobId}: {ex.Message}", ex);
    }
}
```

### Usage Example

```csharp
public async Task ExampleUsage() {
    using var client = new FileServerClient("http://localhost:5001");

    // Upload an image
    bool uploadSuccess = await client.UploadImageAsync(12345, @"C:\path\to\image.png");

    // Download the image
    byte[] imageData = await client.DownloadImageAsync(12345);
    await File.WriteAllBytesAsync(@"C:\downloaded\image.png", imageData);

    // Download G-code file
    await client.DownloadGcodeAsFileAsync(12345, @"C:\downloaded\file.gcode");
}
```
