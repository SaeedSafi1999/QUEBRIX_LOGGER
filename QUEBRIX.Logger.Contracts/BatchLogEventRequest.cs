using System.Text.Json.Serialization;

namespace QUEBRIX.Logger.Contracts;

/// <summary>
/// Represents a batch ingestion request containing multiple log events.
/// </summary>
public sealed class BatchLogEventRequest
{
    /// <summary>
    /// The list of log events to ingest.
    /// </summary>
    [JsonPropertyName("events")]
    public List<LogEvent> Events { get; set; } = new();
}