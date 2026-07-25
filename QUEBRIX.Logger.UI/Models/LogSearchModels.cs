using System.Text.Json.Serialization;

namespace QUEBRIX.Logger.UI.Models;

/// <summary>
/// Request model for searching logs.
/// </summary>
public class LogSearchRequest
{
    [JsonPropertyName("searchTerm")]
    public string? SearchTerm { get; set; }

    [JsonPropertyName("levels")]
    public List<string>? Levels { get; set; }

    [JsonPropertyName("sourceContext")]
    public string? SourceContext { get; set; }

    [JsonPropertyName("application")]
    public string? Application { get; set; }

    [JsonPropertyName("environment")]
    public string? Environment { get; set; }

    [JsonPropertyName("startTime")]
    public DateTime? StartTime { get; set; }

    [JsonPropertyName("endTime")]
    public DateTime? EndTime { get; set; }

    [JsonPropertyName("hasException")]
    public bool? HasException { get; set; }

    [JsonPropertyName("traceId")]
    public string? TraceId { get; set; }

    [JsonPropertyName("correlationId")]
    public string? CorrelationId { get; set; }

    [JsonPropertyName("page")]
    public int Page { get; set; } = 1;

    [JsonPropertyName("pageSize")]
    public int PageSize { get; set; } = 50;
}

/// <summary>
/// Response model from the search API.
/// </summary>
public class LogSearchResponse
{
    [JsonPropertyName("events")]
    public List<LogEvent> Events { get; set; } = new();

    [JsonPropertyName("totalCount")]
    public long TotalCount { get; set; }

    [JsonPropertyName("page")]
    public int Page { get; set; }

    [JsonPropertyName("pageSize")]
    public int PageSize { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("aggregations")]
    public SearchAggregations? Aggregations { get; set; }
}

/// <summary>
/// Aggregation data for filter suggestions.
/// </summary>
public class SearchAggregations
{
    [JsonPropertyName("levels")]
    public List<AggregationItem> Levels { get; set; } = new();

    [JsonPropertyName("sourceContexts")]
    public List<AggregationItem> SourceContexts { get; set; } = new();

    [JsonPropertyName("applications")]
    public List<AggregationItem> Applications { get; set; } = new();

    [JsonPropertyName("environments")]
    public List<AggregationItem> Environments { get; set; } = new();
}

/// <summary>
/// A single aggregation bucket.
/// </summary>
public class AggregationItem
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("count")]
    public long Count { get; set; }
}