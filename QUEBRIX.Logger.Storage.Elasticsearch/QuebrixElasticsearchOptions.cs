using QUEBRIX.Logger.Common;

namespace QUEBRIX.Logger.Storage.Elasticsearch;

/// <summary>
/// Configuration options for Elasticsearch storage.
/// </summary>
public sealed class QuebrixElasticsearchOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "QuebrixElasticsearch";

    /// <summary>
    /// Elasticsearch server URIs.
    /// </summary>
    public List<Uri> Urls { get; set; } = new() { new Uri("http://localhost:9222") };

    /// <summary>
    /// Authentication username.
    /// </summary>
    public string? Username { get; set; } //elastic

    /// <summary>
    /// Authentication password.
    /// </summary>
    public string? Password { get; set; } //b1U0xyd5ZFNabQsh=v7K

    /// <summary>
    /// API key for Elasticsearch.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Index prefix for log indices.
    /// </summary>
    public string IndexPrefix { get; set; } = QuebrixConstants.DefaultIndexPrefix;

    /// <summary>
    /// Indexing cadence: Daily or Monthly.
    /// </summary>
    public IndexCadence Cadence { get; set; } = IndexCadence.Daily;

    /// <summary>
    /// Number of shards for each index.
    /// </summary>
    public int NumberOfShards { get; set; } = 1;

    /// <summary>
    /// Number of replicas for each index.
    /// </summary>
    public int NumberOfReplicas { get; set; } = 1;

    /// <summary>
    /// Maximum number of retries for bulk operations.
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Maximum retry timeout in seconds.
    /// </summary>
    public int MaxRetryTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Whether to enable automatic index creation.
    /// </summary>
    public bool AutoCreateIndex { get; set; } = true;

    /// <summary>
    /// Whether to enable ILM (Index Lifecycle Management).
    /// </summary>
    public bool EnableIlm { get; set; } = true;

    /// <summary>
    /// ILM policy name to use.
    /// </summary>
    public string IlmPolicyName { get; set; } = "quebrix-logs-policy";

    /// <summary>
    /// Number of days to retain logs (for ILM).
    /// </summary>
    public int RetentionDays { get; set; } = 30;

    /// <summary>
    /// Bulk insert size limit.
    /// </summary>
    public int BulkSize { get; set; } = 500;

    /// <summary>
    /// Whether to enable dead letter queue.
    /// </summary>
    public bool EnableDeadLetter { get; set; } = true;

    /// <summary>
    /// Path for dead letter queue files.
    /// </summary>
    public string? DeadLetterPath { get; set; }

    /// <summary>
    /// Connection pool size.
    /// </summary>
    public int ConnectionPoolSize { get; set; } = Environment.ProcessorCount;

    /// <summary>
    /// Whether to use compressed requests.
    /// </summary>
    public bool UseCompression { get; set; } = true;

    /// <summary>
    /// Whether the server uses HTTPS.
    /// </summary>
    public bool UseSsl { get; set; } = false;

    /// <summary>
    /// Whether to validate server certificate.
    /// </summary>
    public bool ValidateCertificates { get; set; } = true;
}

/// <summary>
/// Index cadence options.
/// </summary>
public enum IndexCadence
{
    /// <summary>
    /// Create a new index daily.
    /// </summary>
    Daily,

    /// <summary>
    /// Create a new index monthly.
    /// </summary>
    Monthly
}