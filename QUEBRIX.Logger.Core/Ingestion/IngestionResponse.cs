using System.Collections.ObjectModel;

namespace QUEBRIX.Logger.Core.Ingestion;

/// <summary>
/// Represents the result of a log ingestion operation.
/// </summary>
public sealed class IngestionResponse
{
    /// <summary>
    /// Whether the ingestion succeeded.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Number of events ingested.
    /// </summary>
    public int EventsIngested { get; init; }

    /// <summary>
    /// Time taken in milliseconds.
    /// </summary>
    public long ElapsedMs { get; init; }

    /// <summary>
    /// Error message if ingestion failed.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Collection of individual errors during batch processing.
    /// </summary>
    public List<string> Errors { get; init; } = new();

    /// <summary>
    /// Creates a successful ingestion response.
    /// </summary>
    public static IngestionResponse SuccessResult(int eventsIngested, long elapsedMs) => new()
    {
        Success = true,
        EventsIngested = eventsIngested,
        ElapsedMs = elapsedMs
    };

    /// <summary>
    /// Creates a failed ingestion response.
    /// </summary>
    public static IngestionResponse Failure(string error) => new()
    {
        Success = false,
        Error = error
    };
}