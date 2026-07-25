namespace QUEBRIX.Logger.Common.Options;

/// <summary>
/// Configuration options for the QUEBRIX Logger server.
/// </summary>
public sealed class QuebrixServerOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "QuebrixServer";

    /// <summary>
    /// The URL the server listens on.
    /// </summary>
    public Uri ListenUrl { get; set; } = new Uri($"http://0.0.0.0:{QuebrixConstants.DefaultPort}");

    /// <summary>
    /// Path for the ingestion API endpoint.
    /// </summary>
    public string IngestionPath { get; set; } = QuebrixConstants.DefaultIngestionPath;

    /// <summary>
    /// Path for health checks.
    /// </summary>
    public string HealthPath { get; set; } = QuebrixConstants.DefaultHealthPath;

    /// <summary>
    /// Path for Prometheus metrics.
    /// </summary>
    public string MetricsPath { get; set; } = QuebrixConstants.DefaultMetricsPath;

    /// <summary>
    /// Maximum request body size in bytes.
    /// </summary>
    public long MaxRequestBodySize { get; set; } = QuebrixConstants.MaxBatchSizeBytes;

    /// <summary>
    /// Whether CORS is enabled.
    /// </summary>
    public bool EnableCors { get; set; } = true;

    /// <summary>
    /// Comma-separated allowed CORS origins.
    /// </summary>
    public string CorsOrigins { get; set; } = "*";

    /// <summary>
    /// Rate limit: max requests per minute per IP.
    /// </summary>
    public int RateLimitPerMinute { get; set; } = 1000;

    /// <summary>
    /// Whether to enable Prometheus metrics.
    /// </summary>
    public bool EnableMetrics { get; set; } = true;

    /// <summary>
    /// Whether to enable OpenTelemetry.
    /// </summary>
    public bool EnableOpenTelemetry { get; set; } = true;

    /// <summary>
    /// Application name to be added to ingested log events.
    /// </summary>
    public string? Application { get; set; }

    /// <summary>
    /// Environment name (e.g., Production, Staging, Development) to be added to ingested log events.
    /// </summary>
    public string? Environment { get; set; }

    /// <summary>
    /// Whether to forward headers from proxies.
    /// </summary>
    public bool EnableForwardedHeaders { get; set; } = true;
}
