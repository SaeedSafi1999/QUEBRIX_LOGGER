using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using QUEBRIX.Logger.Security.Authorization;

namespace QUEBRIX.Logger.Server.Controllers;

/// <summary>
/// Handles log event ingestion from Serilog sinks and external sources.
/// Supports JSON, NDJSON, compressed payloads, batch, and single event uploads.
/// </summary>
[ApiController]
[Route("api/ingest")]
[Authorize(Policy = QuebrixPolicies.IngestionPolicy)]
[EnableRateLimiting("IngestionPolicy")]
public sealed class IngestionController : ControllerBase
{
    private readonly Core.Ingestion.LogIngestor _ingestor;
    private readonly ILogger<IngestionController> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="IngestionController"/>.
    /// </summary>
    public IngestionController(Core.Ingestion.LogIngestor ingestor, ILogger<IngestionController> logger)
    {
        _ingestor = ingestor ?? throw new ArgumentNullException(nameof(ingestor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Ingests a single log event as JSON.
    /// </summary>
    [HttpPost("event")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(Core.Ingestion.IngestionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> IngestSingleEvent(
        [FromBody] Contracts.LogEvent logEvent,
        CancellationToken cancellationToken)
    {
        if (logEvent == null)
            return BadRequest(new Core.Ingestion.IngestionResponse
            {
                Success = false,
                Error = "Request body cannot be null"
            });

        var response = await _ingestor.IngestAsync(logEvent, cancellationToken);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    /// <summary>
    /// Ingests a batch of log events as JSON array.
    /// </summary>
    [HttpPost("events")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(Core.Ingestion.IngestionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> IngestBatchEvents(
        [FromBody] Contracts.BatchLogEventRequest request,
        CancellationToken cancellationToken)
    {
        if (request?.Events == null || request.Events.Count == 0)
            return BadRequest(new Core.Ingestion.IngestionResponse
            {
                Success = false,
                Error = "Request must contain at least one event"
            });

        var response = await _ingestor.IngestBatchAsync(request.Events, cancellationToken);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    /// <summary>
    /// Ingests events in NDJSON format (one JSON object per line).
    /// Supports optional GZip compression via Content-Encoding: gzip.
    /// </summary>
    [HttpPost("ndjson")]
    [Consumes("application/x-ndjson", "text/plain")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(Core.Ingestion.IngestionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> IngestNdjson(CancellationToken cancellationToken)
    {
        var events = new List<Contracts.LogEvent>();
        var response = new Core.Ingestion.IngestionResponse();

        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        string? line;
        int lineNumber = 0;

        while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line)) continue;

            try
            {
                var logEvent = JsonSerializer.Deserialize<Contracts.LogEvent>(line, JsonSerializerOptions.Default);
                if (logEvent != null)
                {
                    events.Add(logEvent);
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to parse NDJSON line {LineNumber}", lineNumber);
                response.Errors.Add($"Line {lineNumber}: {ex.Message}");
                response.Success = false;
            }
        }

        if (events.Count == 0)
        {
            return BadRequest(new Core.Ingestion.IngestionResponse
            {
                Success = false,
                Error = "No valid events found in NDJSON payload"
            });
        }

        var ingestResponse = await _ingestor.IngestBatchAsync(events, cancellationToken);
        return ingestResponse.Success ? Ok(ingestResponse) : BadRequest(ingestResponse);
    }

    /// <summary>
    /// Health check endpoint for the ingestion API.
    /// </summary>
    [HttpGet("health")]
    [AllowAnonymous]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Health()
    {
        return Ok(new { Status = "healthy", Timestamp = DateTime.UtcNow });
    }
}