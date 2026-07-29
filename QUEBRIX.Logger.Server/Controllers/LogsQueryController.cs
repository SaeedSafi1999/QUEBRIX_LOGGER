using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nest;
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
    private readonly IElasticClient _client;
    private readonly ILogger<LogsQueryController> _logger;

    public LogsQueryController(IElasticClient client, ILogger<LogsQueryController> logger)
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

        var mustQueries = new List<Func<QueryContainerDescriptor<LogEvent>, QueryContainer>>();
        var filterQueries = new List<Func<QueryContainerDescriptor<LogEvent>, QueryContainer>>();

        // Full-text search across message, exception, and source context
        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim();
            mustQueries.Add(q => q
                .MultiMatch(m => m
                    .Fields(f => f
                        .Field("@m", 3.0)
                        .Field("MessageTemplate", 3.0)
                        .Field("Exception", 1.5)
                        .Field("RenderedMessage")
                        .Field("@x", 1.5)
                        .Field("SourceContext", 2.0)
                        .Field("@mt", 2.0)
                        .Field("Application")
                        .Field("Message")
                        .Field("Environment")
                        .Field("MachineName")
                        .Field("@tr", 1.5)
                        .Field("@l", 1.5)
                        .Field("RequestId")
                        .Field("Host")
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
                    .Terms(request.Levels.Select(l => (object)l).ToArray())
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
                        r.GreaterThanOrEquals(request.StartTime.Value);
                    if (request.EndTime.HasValue)
                        r.LessThanOrEquals(request.EndTime.Value);
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

        // Trace ID filter (@tr field)
        if (!string.IsNullOrWhiteSpace(request.TraceId))
        {
            filterQueries.Add(q => q
                .MatchPhrase(m => m
                    .Field("@tr")
                    .Query(request.TraceId)
                ));
        }

        // Correlation ID filter (@l field)
        if (!string.IsNullOrWhiteSpace(request.CorrelationId))
        {
            filterQueries.Add(q => q
                .MatchPhrase(m => m
                    .Field("@l")
                    .Query(request.CorrelationId)
                ));
        }

        // Build the bool query
        Func<QueryContainerDescriptor<LogEvent>, QueryContainer> boolQuery = q => q
            .Bool(b =>
            {
                if (mustQueries.Count > 0)
                    b.Must(mustQueries.ToArray());

                if (filterQueries.Count > 0)
                    b.Filter(filterQueries.ToArray());

                // If no queries, match all
                if (mustQueries.Count == 0 && filterQueries.Count == 0)
                    b.Must(m => m.MatchAll());

                return b;
            });

        try
        {
            var indexPrefix = "quebrix-logs";

            // Main search request
            var searchResponse = await _client.SearchAsync<LogEvent>(s => s
                .Index(indexPrefix + "*")
                .Query(boolQuery)
                .From(from)
                .Size(pageSize)
                .Sort(ss => ss
                    .Descending("@timestamp")), cancellationToken);

            if (!searchResponse.IsValid)
            {
                _logger.LogError("Elasticsearch search failed: {DebugInfo}", searchResponse.DebugInformation);
                return StatusCode(502, new LogSearchResponse
                {
                    Error = "Search backend unavailable",
                    TotalCount = 0,
                    Events = []
                });
            }

            // Aggregation search for filter suggestions
            var aggResponse = await _client.SearchAsync<LogEvent>(s => s
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

            if (aggResponse.IsValid && aggResponse.Aggregations != null)
            {
                levels = ExtractTerms(aggResponse.Aggregations.Terms<string>("levels"));
                sources = ExtractTerms(aggResponse.Aggregations.Terms<string>("sources"));
                applications = ExtractTerms(aggResponse.Aggregations.Terms<string>("applications"));
                environments = ExtractTerms(aggResponse.Aggregations.Terms<string>("environments"));
            }

            var events = searchResponse.Hits.Select(h => h.Source).ToList();

            return Ok(new LogSearchResponse
            {
                Events = events,
                TotalCount = searchResponse.Total,
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
            var response = await _client.GetAsync<LogEvent>(id, idx => idx.Index("quebrix-logs*"), cancellationToken);
            if (!response.IsValid || !response.Found)
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
    /// Returns all log events without any filters (match_all query).
    /// Supports pagination via query parameters.
    /// </summary>
    [HttpGet("all")]
    [Produces("application/json")]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 500);
        var from = (page - 1) * pageSize;

        try
        {
            var response = await _client.SearchAsync<LogEvent>(s => s
                .Index("quebrix-logs*")
                .Query(q => q.MatchAll())
                .From(from)
                .Size(pageSize)
                .Sort(ss => ss.Descending("@timestamp")), cancellationToken);

            if (!response.IsValid)
            {
                _logger.LogError("Elasticsearch search failed: {DebugInfo}", response.DebugInformation);
                return StatusCode(502, new LogSearchResponse
                {
                    Error = "Search backend unavailable",
                    TotalCount = 0,
                    Events = []
                });
            }

            var events = response.Hits.Select(h => h.Source).ToList();
            return Ok(new LogSearchResponse
            {
                Events = events,
                TotalCount = response.Total,
                Page = page,
                PageSize = pageSize
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching all logs");
            return StatusCode(502, new LogSearchResponse
            {
                Error = "Failed to fetch all logs: " + ex.Message,
                TotalCount = 0,
                Events = []
            });
        }
    }

    /// <summary>
    /// Gets distinct values for auto-complete suggestions.
    /// </summary>
    [HttpGet("suggestions/{field}")]
    [Produces("application/json")]
    public async Task<IActionResult> GetSuggestions(
        string field,
        [FromQuery] string? query,
        CancellationToken cancellationToken)
    {
        var allowedFields = new[]
        {
            "SourceContext",
            "Application",
            "Environment",
            "MachineName",
            "@tr",
            "@l"
        };

        if (!allowedFields.Contains(field))
            return BadRequest(new
            {
                Error = $"Field '{field}' is not supported."
            });

        var keywordField = $"{field}.keyword";

        try
        {
            var response = await _client.SearchAsync<LogEvent>(s => s
                    .Index("quebrix-logs*")
                    .Size(0)
                    .Query(q =>
                    {
                        if (string.IsNullOrWhiteSpace(query))
                            return q.MatchAll();

                        return q.Prefix(p => p
                            .Field(keywordField)
                            .Value(query));
                    })
                    .Aggregations(a => a
                        .Terms("values", t => t
                            .Field(keywordField)
                            .Size(20)
                            .Order(o => o.CountDescending())
                        )
                    ),
                cancellationToken);

            var values = response.Aggregations?
                .Terms("values")?
                .Buckets
                .Select(x => x.Key)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList() ?? [];

            return Ok(new { values });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error getting suggestions for {Field}", field);

            return Ok(new { values = Array.Empty<string>() });
        }
    }

    private static List<AggregationItem> ExtractTerms(TermsAggregate<string>? termsAgg)
    {
        var items = new List<AggregationItem>();
        if (termsAgg?.Buckets != null)
        {
            foreach (var bucket in termsAgg.Buckets)
            {
                items.Add(new AggregationItem
                {
                    Key = bucket.Key,
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