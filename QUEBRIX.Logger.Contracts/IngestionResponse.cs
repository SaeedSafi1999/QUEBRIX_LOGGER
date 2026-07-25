using System.Text.Json.Serialization;

namespace QUEBRIX.Logger.Contracts;

/// <summary>
/// Response returned from the ingestion API.
/// </summary>
public sealed class IngestionResponse
{
    /// <summary>
    /// Whether the ingestion was successful.
    /// </summary>
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    /// <summary>
    /// Number of events successfully ingested.
    /// </summary>
    [JsonPropertyName("ingested")]
    public int Ingested { get; set; }

    /// <summary>
    /// Error message if ingestion failed.
    /// </summary>
    [JsonPropertyName("error")]
    public string? Error { get; set; }

    /// <summary>
    /// Elapsed time for ingestion in milliseconds.
    /// </summary>
    [JsonPropertyName("elapsedMs")]
    public long ElapsedMs { get; set; }

    /// <summary>
    /// Creates a successful response.
    /// </summary>
    public static IngestionResponse SuccessResult(int ingested, long elapsedMs) => new()
    {
        Success = true,
        Ingested = ingested,
        ElapsedMs = elapsedMs
    };

    /// <summary>
    /// Creates a failed response.
    /// </summary>
    public static IngestionResponse Failure(string error) => new()
    {
        Success = false,
        Error = error
    };
}