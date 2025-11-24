using FileProcessor.Services;
using FileProcessor.Services.Interfaces;
using Polly;
using Polly.Extensions.Http;
using RabbitMQHelper;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File("logs/fileprocessor-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container.
builder.Services.AddControllers();

// Configure HTTP client with Polly retry policy
builder.Services.AddHttpClient<IFileDownloaderService, FileDownloaderService>()
    .AddPolicyHandler(GetRetryPolicy());

// Register services
builder.Services.AddSingleton<IMinioService, MinioService>();
builder.Services.AddSingleton<IRmqHelper, RmqHelper>();
builder.Services.AddHostedService<ConfigurationValidationService>();
builder.Services.AddHostedService<RabbitMqConsumerService>();
builder.Services.AddHostedService<FileDeletionService>();

// Configure CORS - require origins from environment configuration
var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>()
                     ?? throw new InvalidOperationException(
                         "AllowedOrigins configuration is required. Please set the AllowedOrigins environment variable.");

builder.Services.AddCors(options =>
{
    options.AddPolicy("RestrictedCors", policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseCors("RestrictedCors");
app.UseAuthorization();
app.MapControllers();

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

app.Run();
return;

static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
{
    return HttpPolicyExtensions
        .HandleTransientHttpError()
        .OrResult(msg => !msg.IsSuccessStatusCode)
        .WaitAndRetryAsync(
            3,
            retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
            (_, timespan, retryCount, _) =>
            {
                Log.Warning("Retry {RetryCount} after {TimeSpan}ms", retryCount, timespan.TotalMilliseconds);
            }
        );
}