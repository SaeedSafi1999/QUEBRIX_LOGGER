using System.Text.Json.Serialization;

namespace QUEBRIX.Logger.UI.Models;

/// <summary>
/// Represents a single log event as stored in Elasticsearch.
/// Matches the Serilog/Elasticsearch schema.
/// </summary>
public class LogEvent
{
    [JsonPropertyName("@timestamp")]
    public DateTime? Timestamp { get; set; }

    [JsonPropertyName("@level")]
    public string Level { get; set; } = "Information";

    [JsonPropertyName("@m")]
    public string? Message { get; set; }

    [JsonPropertyName("@mt")]
    public string? MessageTemplate { get; set; }

    [JsonPropertyName("@x")]
    public string? Exception { get; set; }

    [JsonPropertyName("@i")]
    public string? EventId { get; set; }

    [JsonPropertyName("@r")]
    public string? Renderings { get; set; }

    [JsonPropertyName("@tr")]
    public string? TraceId { get; set; }

    [JsonPropertyName("@sp")]
    public string? SpanId { get; set; }

    [JsonPropertyName("@l")]
    public string? CorrelationId { get; set; }

    [JsonPropertyName("SourceContext")]
    public string? SourceContext { get; set; }

    [JsonPropertyName("Application")]
    public string? Application { get; set; }

    [JsonPropertyName("Environment")]
    public string? Environment { get; set; }

    [JsonPropertyName("MachineName")]
    public string? MachineName { get; set; }

    [JsonPropertyName("Host")]
    public string? Host { get; set; }

    [JsonPropertyName("ProcessId")]
    public int? ProcessId { get; set; }

    [JsonPropertyName("ThreadId")]
    public int? ThreadId { get; set; }

    [JsonPropertyName("RequestId")]
    public string? RequestId { get; set; }

    [JsonPropertyName("UserId")]
    public string? UserId { get; set; }

    [JsonPropertyName("SessionId")]
    public string? SessionId { get; set; }

    [JsonPropertyName("Tags")]
    public List<string>? Tags { get; set; }

    /// <summary>
    /// Additional properties that don't map to known fields.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, object>? Properties { get; set; }
}