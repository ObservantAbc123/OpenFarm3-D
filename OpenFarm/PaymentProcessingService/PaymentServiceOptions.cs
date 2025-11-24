namespace PaymentProcessingService;

public class PaymentServiceOptions
{
    public const string SectionName = "PaymentService";

    public int MaxJobIdsPerRequest { get; set; } = 100;
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public bool RequireHttps { get; set; } = true;
    public long MaxRequestBodySize { get; set; } = 1024 * 1024; // 1MB
}

public class PaymentRateLimiterOptions
{
    public const string SectionName = "RateLimiterOptions";

    public GlobalLimiterOptions GlobalLimiter { get; set; } = new();
}

public class GlobalLimiterOptions
{
    public TimeSpan Window { get; set; } = TimeSpan.FromMinutes(1);
    public int PermitLimit { get; set; } = 60;
    public int QueueLimit { get; set; } = 10;
    public string QueueProcessingOrder { get; set; } = "OldestFirst";
}
