using System.Text.Json;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Aggregations;
using Elastic.Clients.Elasticsearch.QueryDsl;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QUEBRIX.Logger.Contracts;

namespace QUEBRIX.Logger.Server.Controllers;

/// <summary>
/// Provides search and query capabilities for stored log events.
/// Used by the QUEBRIX Logger UI to view and search logs.
/// </summary>
[ApiController]
[Route("api/logs")]
[AllowAnonymous]
public sealed class LogsQueryController : ControllerBase
{
    private readonly ElasticsearchClient _client;
    private readonly ILogger<LogsQueryController> _logger;

    public LogsQueryController(ElasticsearchClient client, ILogger<LogsQueryController> logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Searches log events with full-text search, filters, pagination.
    /// </summary>
    [HttpPost("search")]
    [Produces("application/json")]
    public async Task<IActionResult> Search([FromBody] LogSearchRequest request, CancellationToken cancellationToken)
    {
        if (request == null)
            request = new LogSearchRequest();

        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 200);
        var from = (page - 1) * pageSize;

        var mustQueries = new List<Action<QueryDescriptor<LogEvent>>>();
        var filterQueries = new List<Action<QueryDescriptor<LogEvent>>>();

        // Full-text search across message, exception, and source context
        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim();
            mustQueries.Add(q => q
                .MultiMatch(m => m
                    .Fields(f => f
                        .Field("@m", 3.0)
                        .Field("@x", 1.5)
                        .Field("SourceContext", 2.0)
                        .Field("@mt", 2.0)
                        .Field("Application", 1.0)
                        .Field("Environment", 1.0)
                        .Field("MachineName", 1.0)
                        .Field("TraceId", 1.5)
                        .Field("CorrelationId", 1.5)
                        .Field("RequestId", 1.0)
                        .Field("Host", 1.0)
                    )
                    .Query(term)
                    .Type(TextQueryType.BestFields)
                ));
        }

        // Level filter
        if (request.Levels != null && request.Levels.Count > 0)
        {
            filterQueries.Add(q => q
                .Terms(t => t
                    .Field("@level")
                    .Terms(new TermsQueryField(request.Levels.Select(l => FieldValue.String(l)).ToArray()))
                ));
        }

        // Source context filter
        if (!string.IsNullOrWhiteSpace(request.SourceContext))
        {
            filterQueries.Add(q => q
                .MatchPhrase(m => m
                    .Field("SourceContext")
                    .Query(request.SourceContext)
                ));
        }

        // Application filter
        if (!string.IsNullOrWhiteSpace(request.Application))
        {
            filterQueries.Add(q => q
                .MatchPhrase(m => m
                    .Field("Application")
                    .Query(request.Application)
                ));
        }

        // Environment filter
        if (!string.IsNullOrWhiteSpace(request.Environment))
        {
            filterQueries.Add(q => q
                .MatchPhrase(m => m
                    .Field("Environment")
                    .Query(request.Environment)
                ));
        }

        // Time range filter
        if (request.StartTime.HasValue || request.EndTime.HasValue)
        {
            filterQueries.Add(q => q
                .DateRange(r =>
                {
                    r.Field("@timestamp");
                    if (request.StartTime.HasValue)
                        r.Gte(request.StartTime.Value);
                    if (request.EndTime.HasValue)
                        r.Lte(request.EndTime.Value);
                    return r;
                }));
        }

        // Exception filter
        if (request.HasException == true)
        {
            filterQueries.Add(q => q
                .Exists(e => e.Field("@x")));
        }
        else if (request.HasException == false)
        {
            filterQueries.Add(q => q
                .Bool(b => b
                    .MustNot(mn => mn.Exists(e => e.Field("@x")))));
        }

        // Trace ID filter
        if (!string.IsNullOrWhiteSpace(request.TraceId))
        {
            filterQueries.Add(q => q
                .MatchPhrase(m => m
                    .Field("TraceId")
                    .Query(request.TraceId)
                ));
        }

        // Correlation ID filter
        if (!string.IsNullOrWhiteSpace(request.CorrelationId))
        {
            filterQueries.Add(q => q
                .MatchPhrase(m => m
                    .Field("CorrelationId")
                    .Query(request.CorrelationId)
                ));
        }

        // Build the bool query
        var boolQuery = new Action<QueryDescriptor<LogEvent>>(q => q
            .Bool(b =>
            {
                if (mustQueries.Count > 0)
                    b.Must(mustQueries.ToArray());

                if (filterQueries.Count > 0)
                    b.Filter(filterQueries.ToArray());

                // If no queries, match all
                if (mustQueries.Count == 0 && filterQueries.Count == 0)
                    b.Must(m => m.MatchAll(mm => { }));
            }));

        try
        {
            var indexPrefix = "quebrix-logs";
            var response = await _client.SearchAsync<LogEvent>(s => s
                .Index(indexPrefix + "*")
                .Query(boolQuery)
                .From(from)
                .Size(pageSize)
                .Sort(ss => ss
                    .Field("@timestamp", f => f.Order(SortOrder.Desc)))
                , cancellationToken);

            if (!response.IsValidResponse)
            {
                _logger.LogError("Elasticsearch search failed: {DebugInfo}", response.DebugInformation);
                return StatusCode(502, new LogSearchResponse
                {
                    Error = "Search backend unavailable",
                    TotalCount = 0,
                    Events = []
                });
            }

            // Get aggregations for filter suggestions
            var aggsResponse = await _client.SearchAsync<LogEvent>(s => s
                .Index(indexPrefix + "*")
                .Size(0)
                .Query(boolQuery)
                .Aggregations(aggs => aggs
                    .Terms("levels", t => t.Field("@level").Size(20))
                    .Terms("sources", t => t.Field("SourceContext").Size(50))
                    .Terms("applications", t => t.Field("Application").Size(20))
                    .Terms("environments", t => t.Field("Environment").Size(20))
                ), cancellationToken);

            var levels = new List<AggregationItem>();
            var sources = new List<AggregationItem>();
            var applications = new List<AggregationItem>();
            var environments = new List<AggregationItem>();

            if (aggsResponse.IsValidResponse && aggsResponse.Aggregations != null)
            {
                levels = ExtractTerms(aggsResponse.Aggregations.Terms("levels"));
                sources = ExtractTerms(aggsResponse.Aggregations.Terms("sources"));
                applications = ExtractTerms(aggsResponse.Aggregations.Terms("applications"));
                environments = ExtractTerms(aggsResponse.Aggregations.Terms("environments"));
            }

            var events = response.Hits.Select(h => h.Source!).ToList();

            return Ok(new LogSearchResponse
            {
                Events = events,
                TotalCount = response.Total,
                Page = page,
                PageSize = pageSize,
                Aggregations = new SearchAggregations
                {
                    Levels = levels,
                    SourceContexts = sources,
                    Applications = applications,
                    Environments = environments
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching logs");
            return StatusCode(502, new LogSearchResponse
            {
                Error = "Search failed: " + ex.Message,
                TotalCount = 0,
                Events = []
            });
        }
    }

    /// <summary>
    /// Gets a single log event by ID.
    /// </summary>
    [HttpGet("{id}")]
    [Produces("application/json")]
    public async Task<IActionResult> GetById(string id, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _client.GetAsync<LogEvent>("quebrix-logs*", id, cancellationToken);
            if (!response.IsValidResponse || !response.Found)
                return NotFound();

            return Ok(response.Source);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching log event {Id}", id);
            return StatusCode(502, new { Error = "Failed to fetch log event" });
        }
    }

    /// <summary>
    /// Gets distinct values for auto-complete suggestions.
    /// </summary>
    [HttpGet("suggestions/{field}")]
    [Produces("application/json")]
    public async Task<IActionResult> GetSuggestions(string field, [FromQuery] string? query, CancellationToken cancellationToken)
    {
        var allowedFields = new[] { "SourceContext", "Application", "Environment", "MachineName", "TraceId", "CorrelationId" };
        if (!allowedFields.Contains(field))
            return BadRequest(new { Error = $"Field '{field}' is not supported for suggestions" });

        try
        {
            var response = await _client.SearchAsync<LogEvent>(s => s
                .Index("quebrix-logs*")
                .Size(0)
                .Query(q => q
                    .Bool(b => b
                        .Filter(f => f
                            .Prefix(new PrefixQuery
                            {
                                Field = field,
                                Value = query ?? ""
                            }))
                    ))
                .Aggregations(aggs => aggs
                    .Terms("values", t => t.Field(field).Size(20))
                ), cancellationToken);

            var values = new List<string>();
            if (response.IsValidResponse && response.Aggregations != null)
            {
                var terms = response.Aggregations.Terms("values");
                if (terms?.Buckets != null)
                {
                    foreach (var bucket in terms.Buckets)
                    {
                        values.Add(bucket.Key?.ToString() ?? "");
                    }
                }
            }

            return Ok(new { values });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting suggestions for {Field}", field);
            return Ok(new { values = new List<string>() });
        }
    }

    private static List<AggregationItem> ExtractTerms(TermsAggregate? termsAgg)
    {
        var items = new List<AggregationItem>();
        if (termsAgg?.Buckets != null)
        {
            foreach (var bucket in termsAgg.Buckets)
            {
                items.Add(new AggregationItem
                {
                    Key = bucket.Key?.ToString() ?? "unknown",
                    Count = bucket.DocCount ?? 0
                });
            }
        }
        return items;
    }
}

// --- Request / Response Models ---

public sealed class LogSearchRequest
{
    public string? SearchTerm { get; set; }
    public List<string>? Levels { get; set; }
    public string? SourceContext { get; set; }
    public string? Application { get; set; }
    public string? Environment { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public bool? HasException { get; set; }
    public string? TraceId { get; set; }
    public string? CorrelationId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

public sealed class LogSearchResponse
{
    public List<LogEvent> Events { get; set; } = [];
    public long TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public string? Error { get; set; }
    public SearchAggregations? Aggregations { get; set; }
}

public sealed class SearchAggregations
{
    public List<AggregationItem> Levels { get; set; } = [];
    public List<AggregationItem> SourceContexts { get; set; } = [];
    public List<AggregationItem> Applications { get; set; } = [];
    public List<AggregationItem> Environments { get; set; } = [];
}

public sealed class AggregationItem
{
    public string Key { get; set; } = "";
    public long Count { get; set; }
}