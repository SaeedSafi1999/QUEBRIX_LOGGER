using System.Diagnostics;
using Microsoft.Extensions.Logging;
using QUEBRIX.Logger.Contracts;
using QUEBRIX.Logger.Core.Processing;
using QUEBRIX.Logger.Storage.Abstractions;

namespace QUEBRIX.Logger.Core.Ingestion;

/// <summary>
/// Orchestrates the ingestion of log events into storage after pipeline processing.
/// </summary>
public sealed class LogIngestor
{
    private readonly LogEventPipeline _pipeline;
    private readonly ILogStorage _storage;
    private readonly ILogger<LogIngestor> _logger;

    public LogIngestor(
        LogEventPipeline pipeline,
        ILogStorage storage,
        ILogger<LogIngestor> logger)
    {
        _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Ingests a single log event.
    /// </summary>
    public async ValueTask<IngestionResponse> IngestAsync(LogEvent logEvent, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var processed = await _pipeline.ProcessAsync(logEvent, cancellationToken);
            if (processed == null)
            {
                return IngestionResponse.SuccessResult(0, sw.ElapsedMilliseconds);
            }

            await _storage.StoreAsync(processed, cancellationToken);
            sw.Stop();

            return IngestionResponse.SuccessResult(1, sw.ElapsedMilliseconds);
        }
        catch (OperationCanceledException)
        {
            return IngestionResponse.Failure("Ingestion cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ingest log event");
            return IngestionResponse.Failure(ex.Message);
        }
    }

    /// <summary>
    /// Ingests a batch of log events.
    /// </summary>
    public async ValueTask<IngestionResponse> IngestBatchAsync(IReadOnlyList<LogEvent> logEvents, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var processedEvents = new List<LogEvent>(logEvents.Count);
            foreach (var logEvent in logEvents)
            {
                var processed = await _pipeline.ProcessAsync(logEvent, cancellationToken);
                if (processed != null)
                {
                    processedEvents.Add(processed);
                }
            }

            if (processedEvents.Count == 0)
            {
                return IngestionResponse.SuccessResult(0, sw.ElapsedMilliseconds);
            }

            await _storage.StoreBatchAsync(processedEvents, cancellationToken);
            sw.Stop();

            return IngestionResponse.SuccessResult(processedEvents.Count, sw.ElapsedMilliseconds);
        }
        catch (OperationCanceledException)
        {
            return IngestionResponse.Failure("Ingestion cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ingest batch of {Count} log events", logEvents.Count);
            return IngestionResponse.Failure(ex.Message);
        }
    }
}