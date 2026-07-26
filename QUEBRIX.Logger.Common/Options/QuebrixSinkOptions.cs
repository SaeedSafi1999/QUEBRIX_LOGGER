namespace QUEBRIX.Logger.Common.Options;

/// <summary>
/// Configuration options for the QUEBRIX Serilog sink.
/// </summary>
public sealed class QuebrixSinkOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "QuebrixLogger";

    /// <summary>
    /// The URL of the QUEBRIX Logger server.
    /// </summary>
    public Uri Url { get; set; } = new Uri("http://localhost:6062");

    /// <summary>
    /// API key for authentication.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Application name for log events.
    /// </summary>
    public string Application { get; set; } = QuebrixConstants.DefaultApplication;

    /// <summary>
    /// Environment name for log events.
    /// </summary>
    public string Environment { get; set; } = QuebrixConstants.DefaultEnvironment;

    /// <summary>
    /// Minimum log level to capture.
    /// </summary>
    public QuebrixLogLevel MinimumLevel { get; set; } = QuebrixLogLevel.Information;

    /// <summary>
    /// Maximum number of log events per batch.
    /// </summary>
    public int BatchSize { get; set; } = QuebrixConstants.DefaultBatchSize;

    /// <summary>
    /// Period in seconds between batch flushes.
    /// </summary>
    public int FlushPeriodSeconds { get; set; } = QuebrixConstants.DefaultFlushPeriodSeconds;

    /// <summary>
    /// Maximum size of the internal queue.
    /// </summary>
    public int QueueSize { get; set; } = QuebrixConstants.DefaultQueueSize;

    /// <summary>
    /// HTTP request timeout in seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = QuebrixConstants.DefaultTimeoutSeconds;

    /// <summary>
    /// Whether to enable GZip compression on requests.
    /// </summary>
    public bool UseCompression { get; set; } = true;

    /// <summary>
    /// Custom headers to include with every request.
    /// </summary>
    public Dictionary<string, string> CustomHeaders { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Tags to attach to every log event.
    /// </summary>
    public HashSet<string> Tags { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Whether to enable buffering of log events.
    /// </summary>
    public bool EnableBuffering { get; set; } = true;

    /// <summary>
    /// Whether to enable offline mode (buffer when server unreachable).
    /// </summary>
    public bool EnableOfflineMode { get; set; } = false;

    /// <summary>
    /// Whether to enable durable mode (write to disk when server unreachable).
    /// </summary>
    public bool EnableDurableMode { get; set; } = false;

    /// <summary>
    /// Path for buffer files when durable mode is enabled.
    /// </summary>
    public string? BufferPath { get; set; }

    /// <summary>
    /// Maximum number of retry attempts for failed requests.
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Maximum backoff interval in seconds between retries.
    /// </summary>
    public int MaxBackoffSeconds { get; set; } = 30;

    /// <summary>
    /// Whether to send events as NDJSON.
    /// </summary>
    public bool UseNdjson { get; set; } = false;
}