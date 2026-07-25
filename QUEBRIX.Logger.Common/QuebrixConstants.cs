namespace QUEBRIX.Logger.Common;

/// <summary>
/// Defines global constants for the QUEBRIX Logger platform.
/// </summary>
public static class QuebrixConstants
{
    /// <summary>
    /// The name of the logging platform.
    /// </summary>
    public const string ProductName = "QUEBRIX Logger";

    /// <summary>
    /// Current version of the platform.
    /// </summary>
    public const string Version = "1.0.0";

    /// <summary>
    /// Default HTTP ingestion endpoint path.
    /// </summary>
    public const string DefaultIngestionPath = "/api/ingest";

    /// <summary>
    /// Default health check endpoint path.
    /// </summary>
    public const string DefaultHealthPath = "/health";

    /// <summary>
    /// Default metrics endpoint path.
    /// </summary>
    public const string DefaultMetricsPath = "/metrics";

    /// <summary>
    /// Default server port.
    /// </summary>
    public const int DefaultPort = 8080;

    /// <summary>
    /// Default batch size for log event batches.
    /// </summary>
    public const int DefaultBatchSize = 100;

    /// <summary>
    /// Default period in seconds for flushing batches.
    /// </summary>
    public const int DefaultFlushPeriodSeconds = 2;

    /// <summary>
    /// Default queue size limit for buffering.
    /// </summary>
    public const int DefaultQueueSize = 10000;

    /// <summary>
    /// Default timeout in seconds for HTTP operations.
    /// </summary>
    public const int DefaultTimeoutSeconds = 30;

    /// <summary>
    /// Maximum event size in bytes (1 MB).
    /// </summary>
    public const int MaxEventSizeBytes = 1_048_576;

    /// <summary>
    /// Maximum batch payload size in bytes (10 MB).
    /// </summary>
    public const int MaxBatchSizeBytes = 10_485_760;

    /// <summary>
    /// Default Elasticsearch index prefix.
    /// </summary>
    public const string DefaultIndexPrefix = "quebrix-logs";

    /// <summary>
    /// Content type for JSON.
    /// </summary>
    public const string ContentTypeJson = "application/json";

    /// <summary>
    /// Content type for NDJSON.
    /// </summary>
    public const string ContentTypeNdjson = "application/x-ndjson";

    /// <summary>
    /// Name of the API key header.
    /// </summary>
    public const string ApiKeyHeaderName = "X-API-Key";

    /// <summary>
    /// Activity source name for OpenTelemetry.
    /// </summary>
    public const string ActivitySourceName = "QUEBRIX.Logger";

    /// <summary>
    /// Name of the default application if not specified.
    /// </summary>
    public const string DefaultApplication = "Default";

    /// <summary>
    /// Name of the default environment if not specified.
    /// </summary>
    public const string DefaultEnvironment = "Production";

    /// <summary>
    /// Property name for application name.
    /// </summary>
    public const string PropertyApplication = "Application";

    /// <summary>
    /// Property name for environment name.
    /// </summary>
    public const string PropertyEnvironment = "Environment";

    /// <summary>
    /// Property name for source context.
    /// </summary>
    public const string PropertySourceContext = "SourceContext";
}
