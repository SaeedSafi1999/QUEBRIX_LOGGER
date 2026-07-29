using System.Text.Json.Serialization;
using QUEBRIX.Logger.Common;

namespace QUEBRIX.Logger.Contracts;

/// <summary>
/// Represents a single log event compatible with Serilog's Elasticsearch schema.
/// Field names follow the Serilog convention (@timestamp, @level, @m, @mt, @x, @tr, @sp, @l, @i).
/// </summary>
public sealed class LogEvent
{
    /// <summary>
    /// The timestamp when the log event occurred (UTC).
    /// </summary>
    [JsonPropertyName("@timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// The log level as a string (e.g. "Information", "Error", "Debug").
    /// </summary>
    [JsonPropertyName("@level")]
    public string Level { get; set; } = QuebrixLogLevel.Information.ToString();

    /// <summary>
    /// The rendered/formatted log message (equivalent to Serilog's @m).
    /// </summary>
    [JsonPropertyName("@m")]
    public string? Message { get; set; }

    /// <summary>
    /// The message template (e.g. "User {UserId} logged in").
    /// </summary>
    [JsonPropertyName("@mt")]
    public string MessageTemplate { get; set; } = "Text";

    /// <summary>
    /// The exception information, if any (equivalent to Serilog's @x).
    /// </summary>
    [JsonPropertyName("@x")]
    public string? Exception { get; set; }

    /// <summary>
    /// The event ID from Serilog event IDs (equivalent to Serilog's @i).
    /// </summary>
    [JsonPropertyName("@i")]
    public string? EventId { get; set; }

    /// <summary>
    /// The W3C trace ID for distributed tracing (equivalent to Serilog's @tr).
    /// </summary>
    [JsonPropertyName("@tr")]
    public string? TraceId { get; set; }

    /// <summary>
    /// The W3C span ID for distributed tracing (equivalent to Serilog's @sp).
    /// </summary>
    [JsonPropertyName("@sp")]
    public string? SpanId { get; set; }

    /// <summary>
    /// The correlation ID for tracking requests across services (equivalent to Serilog's @l).
    /// </summary>
    [JsonPropertyName("@l")]
    public string? CorrelationId { get; set; }

    /// <summary>
    /// The source context (class name) that emitted the log event.
    /// </summary>
    [JsonPropertyName("SourceContext")]
    public string? SourceContext { get; set; }

    /// <summary>
    /// The application name that produced the log.
    /// </summary>
    [JsonPropertyName("Application")]
    public string Application { get; set; } = QuebrixConstants.DefaultApplication;

    /// <summary>
    /// The environment name (e.g. Production, Staging).
    /// </summary>
    [JsonPropertyName("Environment")]
    public string Environment { get; set; } = QuebrixConstants.DefaultEnvironment;

    /// <summary>
    /// The machine or host name.
    /// </summary>
    [JsonPropertyName("MachineName")]
    public string? MachineName { get; set; }

    /// <summary>
    /// The process ID.
    /// </summary>
    [JsonPropertyName("ProcessId")]
    public int? ProcessId { get; set; }

    /// <summary>
    /// The thread ID.
    /// </summary>
    [JsonPropertyName("ThreadId")]
    public int? ThreadId { get; set; }

    /// <summary>
    /// The request ID.
    /// </summary>
    [JsonPropertyName("RequestId")]
    public string? RequestId { get; set; }

    /// <summary>
    /// The user ID associated with the log.
    /// </summary>
    [JsonPropertyName("UserId")]
    public string? UserId { get; set; }

    /// <summary>
    /// The session ID associated with the log.
    /// </summary>
    [JsonPropertyName("SessionId")]
    public string? SessionId { get; set; }

    /// <summary>
    /// The host name or IP address.
    /// </summary>
    [JsonPropertyName("Host")]
    public string? Host { get; set; }

    /// <summary>
    /// Tags associated with the log event.
    /// </summary>
    [JsonPropertyName("Tags")]
    public List<string>? Tags { get; set; }

    /// <summary>
    /// Custom properties attached to the log event.
    /// Only contains properties not mapped to top-level fields.
    /// </summary>
    [JsonPropertyName("Properties")]
    public Dictionary<string, object?>? Properties { get; set; }

    /// <summary>
    /// Creates a shallow copy of this log event.
    /// </summary>
    public LogEvent Clone() => new()
    {
        Timestamp = Timestamp,
        Level = Level,
        Message = Message,
        MessageTemplate = MessageTemplate,
        Exception = Exception,
        EventId = EventId,
        SourceContext = SourceContext,
        Application = Application,
        Environment = Environment,
        MachineName = MachineName,
        ProcessId = ProcessId,
        ThreadId = ThreadId,
        TraceId = TraceId,
        SpanId = SpanId,
        CorrelationId = CorrelationId,
        RequestId = RequestId,
        UserId = UserId,
        SessionId = SessionId,
        Host = Host,
        Tags = Tags?.ToList(),
        Properties = Properties?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase)
    };
}