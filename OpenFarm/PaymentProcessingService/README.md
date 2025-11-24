# Payment Processing Service

### Environment Setup
Create a `.env` file or set these environment variables:

```bash
# Required
PAYMENT_API_KEY=your-secure-api-key-minimum-32-chars
DATABASE_CONNECTION_STRING="Host=localhost;Database=openfarm;Username=user;Password=pass"

# Optional (for Docker/production)
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=https://0.0.0.0:443
ASPNETCORE_CERT_PASSWORD=your-cert-password
```

### SSL Certificate Setup

The service **requires HTTPS** and needs proper SSL certificates. Here are your options:

**Option 1: Generate Development Certificate (Recommended)**
```bash
# Generate a proper development certificate
./generate-dev-cert.sh

# Trust the certificate system-wide (optional, removes need for --insecure)
./cert/trust-cert.sh

# Test the certificate setup
./cert/test-cert.sh
```

**Option 2: Use .NET Development Certificates**
```bash
# Clean and regenerate .NET dev certificates
dotnet dev-certs https --clean
dotnet dev-certs https --trust

# Use custom port to avoid permission issues
export ASPNETCORE_URLS="https://localhost:5001"
```

**Option 3: Production Certificates**
```bash
# Set certificate path and password
export ASPNETCORE_Kestrel__Certificates__Default__Path="/path/to/certificate.pfx"
export ASPNETCORE_Kestrel__Certificates__Default__Password="your-cert-password"
```

### Development Setup
```bash
# Install development certificates (first time only)
dotnet dev-certs https --clean
dotnet dev-certs https --trust

# Run on custom port to avoid permission issues
export ASPNETCORE_URLS="https://localhost:5001"
dotnet run --project PaymentProcessingService
```

## 📋 API Reference

All endpoints require authentication via API key header (except `/health`).

### Authentication
```http
X-API-Key: your-api-key-here
```

### Endpoints

#### Mark Multiple Jobs as Paid
**POST** `/api/payment/mark-paid`
```json
{
  "jobIds": [1, 2, 3]
}
```

**Response:**
```json
{
  "processedCount": 3,
  "successCount": 2,
  "results": [
    {"jobId": 1, "success": true, "error": null},
    {"jobId": 2, "success": true, "error": null},
    {"jobId": 3, "success": false, "error": "NotFound"}
  ]
}
```

#### Get Unpaid Jobs
**GET** `/api/payment/unpaid`

**Response:**
```json
{
  "unpaidJobIds": [4, 5, 6],
  "count": 3,
  "retrievedAt": "2024-01-15T10:30:00Z"
}
```

#### Health Check
**GET** `/health`

**Response:**
```json
{
  "status": "healthy",
  "timestamp": "2024-01-15T10:30:00Z",
  "version": "1.0.0",
  "dependencies": {
    "database": "configured",
    "rabbitmq": "connected"
  }
}
```

## Monitoring & Logging

### Structured Logging
All requests include correlation IDs for tracing:
```json
{
  "timestamp": "2024-01-15T10:30:00.123Z",
  "level": "Information",
  "message": "Payment marked for job 123",
  "correlationId": "abc12345",
  "requestPath": "/api/payment/mark-paid",
  "remoteIP": "192.168.1.100"
}
```

### Health Monitoring
The `/health` endpoint provides real-time status of:
- Service availability
- Database connectivity
- RabbitMQ connection status
- Configuration validation

## 🔧 Configuration

### Rate Limiting
Default: 60 requests/minute per IP. Customize in `appsettings.json`:
```json
{
  "RateLimiterOptions": {
    "GlobalLimiter": {
      "Window": "00:01:00",
      "PermitLimit": 60,
      "QueueLimit": 10
    }
  }
}
```

### SSL Certificate
For development, use .NET dev certificates. For production, set environment variables:
```bash
ASPNETCORE_Kestrel__Certificates__Default__Path=/path/to/certificate.pfx
ASPNETCORE_Kestrel__Certificates__Default__Password=certificate-password
```

Or use appsettings.Production.json:
```json
{
  "Kestrel": {
    "Certificates": {
      "Default": {
        "Path": "/path/to/certificate.pfx",
        "Password": "certificate-password"
      }
    }
  }
}
```
