using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace PaymentProcessingService.Authentication;

public class ApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private const string ApiKeyHeaderName = "X-API-Key";

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Skip authentication for health check
        if (Request.Path.StartsWithSegments("/health"))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var clientIp = GetClientIpAddress();

        if (!Request.Headers.ContainsKey(ApiKeyHeaderName))
        {
            Logger.LogWarning("API Key header missing from IP: {ClientIP}", clientIp);
            return Task.FromResult(AuthenticateResult.Fail("API Key header not found"));
        }

        var apiKeyFromHeader = Request.Headers[ApiKeyHeaderName].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(apiKeyFromHeader))
        {
            Logger.LogWarning("Empty API Key provided from IP: {ClientIP}", clientIp);
            return Task.FromResult(AuthenticateResult.Fail("API Key not provided"));
        }

        // Validate API key length to prevent timing attacks on empty/short keys
        if (apiKeyFromHeader.Length < 32)
        {
            Logger.LogWarning("Invalid API Key format from IP: {ClientIP}", clientIp);
            return Task.FromResult(AuthenticateResult.Fail("Invalid API Key format"));
        }

        var validApiKey = GetValidApiKey();

        if (string.IsNullOrEmpty(validApiKey))
        {
            Logger.LogCritical("No API key configured for PaymentProcessingService. Set PAYMENT_API_KEY environment variable.");
            return Task.FromResult(AuthenticateResult.Fail("Service not configured"));
        }

        // Use constant-time comparison to prevent timing attacks
        if (!SecureEquals(apiKeyFromHeader, validApiKey))
        {
            Logger.LogWarning("Invalid API Key attempt from IP: {ClientIP}", clientIp);

            // Add delay to slow down brute force attempts
            Task.Delay(TimeSpan.FromMilliseconds(100 + Random.Shared.Next(0, 200))).Wait();

            return Task.FromResult(AuthenticateResult.Fail("Invalid API Key"));
        }

        // Create claims with additional context
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, "PaymentService"),
            new Claim(ClaimTypes.Authentication, DateTime.UtcNow.ToString("O")),
            new Claim("ClientIP", clientIp),
            new Claim("ApiKey", "Valid")
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        Logger.LogInformation("Successful API authentication from IP: {ClientIP}", clientIp);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = 401;
        Response.Headers.Append("WWW-Authenticate", "ApiKey");

        // Add security headers
        Response.Headers.Append("X-Content-Type-Options", "nosniff");
        Response.Headers.Append("X-Frame-Options", "DENY");
        Response.Headers.Append("X-XSS-Protection", "1; mode=block");

        return Response.WriteAsync("Unauthorized: Valid API Key required");
    }

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = 403;
        return Response.WriteAsync("Forbidden: Insufficient permissions");
    }

    private string? GetValidApiKey()
    {
        return Environment.GetEnvironmentVariable("PAYMENT_API_KEY");
    }

    private string GetClientIpAddress()
    {
        // Check for forwarded IP first (if behind proxy/load balancer)
        var forwardedFor = Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            var firstIp = forwardedFor.Split(',').FirstOrDefault()?.Trim();
            if (!string.IsNullOrEmpty(firstIp))
                return firstIp;
        }

        // Check real IP header
        var realIp = Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(realIp))
            return realIp;

        // Fall back to connection remote IP
        return Context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    private static bool SecureEquals(string a, string b)
    {
        if (a.Length != b.Length)
            return false;

        var result = 0;
        for (var i = 0; i < a.Length; i++)
        {
            result |= a[i] ^ b[i];
        }

        return result == 0;
    }


}
