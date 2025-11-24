namespace FileProcessor.Services;

/// <summary>
///     Hosted service that validates required environment variables and configuration on startup.
///     Ensures all security-critical configurations like CORS origins are explicitly set.
/// </summary>
public class ConfigurationValidationService : IHostedService
{
    private readonly IHostApplicationLifetime _applicationLifetime;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ConfigurationValidationService> _logger;

    private readonly string[] _requiredEnvironmentVariables =
    [
        "MINIO_ROOT_USER",
        "MINIO_ROOT_PASSWORD",
        "RABBITMQ_HOST",
        "RABBITMQ_USER",
        "RABBITMQ_PASSWORD"
    ];

    /// <summary>
    ///     Initializes a new instance of the ConfigurationValidationService class.
    /// </summary>
    /// <param name="configuration">The application configuration</param>
    /// <param name="logger">The logger instance</param>
    /// <param name="applicationLifetime">The application lifetime service</param>
    public ConfigurationValidationService(IConfiguration configuration, ILogger<ConfigurationValidationService> logger,
        IHostApplicationLifetime applicationLifetime)
    {
        _configuration = configuration;
        _logger = logger;
        _applicationLifetime = applicationLifetime;
    }

    /// <summary>
    ///     Validates all required environment variables and configuration when the service starts.
    ///     Includes validation of CORS AllowedOrigins configuration for security.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A completed task if validation succeeds</returns>
    /// <exception cref="InvalidOperationException">Thrown when required environment variables are missing</exception>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var missingVars = new List<string>();

        foreach (var varName in _requiredEnvironmentVariables)
        {
            var value = _configuration[varName];
            if (!string.IsNullOrWhiteSpace(value))
                continue;

            missingVars.Add(varName);
            _logger.LogError("Required environment variable '{VariableName}' is not set", varName);
        }

        var allowedOrigins = _configuration.GetSection("AllowedOrigins").Get<string[]>();
        if (allowedOrigins == null || allowedOrigins.Length == 0)
        {
            missingVars.Add("AllowedOrigins");
            _logger.LogError("Required configuration 'AllowedOrigins' is not set or is empty");
        }

        if (missingVars.Count != 0)
        {
            _logger.LogCritical("Missing required environment variables: {Variables}. Application cannot start.",
                string.Join(", ", missingVars));

            _applicationLifetime.StopApplication();
            throw new InvalidOperationException(
                $"Missing required environment variables: {string.Join(", ", missingVars)}");
        }

        ValidateOptionalConfigurations();

        _logger.LogInformation("All required environment variables are configured correctly");
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Called when the service is stopping. No cleanup required.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A completed task</returns>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Validates optional configuration values and logs warnings for invalid values.
    /// </summary>
    private void ValidateOptionalConfigurations()
    {
        var maxFileSizeMb = _configuration.GetValue<int?>("MAX_FILE_SIZE_MB");
        if (maxFileSizeMb is <= 0)
            _logger.LogWarning("MAX_FILE_SIZE_MB must be greater than 0. Using default value of 250MB");

        var downloadTimeout = _configuration.GetValue<int?>("DOWNLOAD_TIMEOUT_SECONDS");
        if (downloadTimeout is <= 0)
            _logger.LogWarning("DOWNLOAD_TIMEOUT_SECONDS must be greater than 0. Using default value of 300 seconds");

        var maxConcurrentDownloads = _configuration.GetValue<int?>("MAX_CONCURRENT_DOWNLOADS");
        if (maxConcurrentDownloads is <= 0)
            _logger.LogWarning("MAX_CONCURRENT_DOWNLOADS must be greater than 0. Using default value of 5");
    }
}