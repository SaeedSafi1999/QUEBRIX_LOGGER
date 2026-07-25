using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Elastic.Clients.Elasticsearch;
using QUEBRIX.Logger.Contracts;
using QUEBRIX.Logger.Storage.Abstractions;

namespace QUEBRIX.Logger.Storage.Elasticsearch;

/// <summary>
/// Implementation of log storage using Elasticsearch 8.x.
/// Supports automatic retries, dead letter handling, and health checks.
/// </summary>
public sealed class ElasticsearchLogStorage : ILogStorage, IDisposable
{
    private readonly ElasticsearchClient _client;
    private readonly ElasticsearchIndexManager _indexManager;
    private readonly QuebrixElasticsearchOptions _options;
    private readonly ILogger<ElasticsearchLogStorage> _logger;
    private readonly IDeadLetterHandler _deadLetterHandler;
    private long _totalEventsStored;
    private long _failedWrites;

    /// <summary>
    /// Initializes a new instance of the <see cref="ElasticsearchLogStorage"/> class.
    /// </summary>
    public ElasticsearchLogStorage(
        ElasticsearchClient client,
        ElasticsearchIndexManager indexManager,
        IOptions<QuebrixElasticsearchOptions> options,
        ILogger<ElasticsearchLogStorage> logger,
        IDeadLetterHandler? deadLetterHandler = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _indexManager = indexManager ?? throw new ArgumentNullException(nameof(indexManager));
        _options = options.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _deadLetterHandler = deadLetterHandler ?? new NullDeadLetterHandler();
    }

    /// <inheritdoc/>
    public async ValueTask StoreAsync(LogEvent logEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(logEvent);

        await _indexManager.EnsureIndexAsync(cancellationToken);
        var indexName = await _indexManager.GetCurrentIndexNameAsync(cancellationToken);

        var response = await _client.IndexAsync(logEvent, idx => idx.Index(indexName), cancellationToken);

        if (response.IsValidResponse)
        {
            Interlocked.Increment(ref _totalEventsStored);
        }
        else
        {
            Interlocked.Increment(ref _failedWrites);
            await _deadLetterHandler.HandleFailedEventsAsync(new[] { logEvent }, response.DebugInformation, cancellationToken);
        }
    }

    /// <inheritdoc/>
    public async ValueTask StoreBatchAsync(IReadOnlyList<LogEvent> logEvents, CancellationToken cancellationToken = default)
    {
        if (logEvents == null || logEvents.Count == 0) return;

        await _indexManager.EnsureIndexAsync(cancellationToken);
        var indexName = await _indexManager.GetCurrentIndexNameAsync(cancellationToken);

        var bulkResponse = await _client.BulkAsync(b => b
            .Index(indexName)
            .IndexMany(logEvents),
            cancellationToken);

        if (!bulkResponse.IsValidResponse)
        {
            Interlocked.Add(ref _failedWrites, logEvents.Count);
            await _deadLetterHandler.HandleFailedEventsAsync(logEvents, bulkResponse.DebugInformation, cancellationToken);
            _logger.LogError("Bulk insert failed: {Error}", bulkResponse.DebugInformation);
            return;
        }

        if (bulkResponse.Errors)
        {
            var failedItems = new List<LogEvent>();
            var items = bulkResponse.Items;
            for (int i = 0; i < items.Count && i < logEvents.Count; i++)
            {
                var item = items[i];
                if (item.Status >= 200 && item.Status < 300)
                {
                    Interlocked.Increment(ref _totalEventsStored);
                }
                else
                {
                    Interlocked.Increment(ref _failedWrites);
                    if (item.Status >= 400)
                    {
                        failedItems.Add(logEvents[i]);
                    }
                }
            }

            if (failedItems.Count > 0)
            {
                await _deadLetterHandler.HandleFailedEventsAsync(failedItems, "Partial bulk failure", cancellationToken);
            }
        }
        else
        {
            Interlocked.Add(ref _totalEventsStored, logEvents.Count);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _client.PingAsync(cancellationToken);
            return response.IsValidResponse;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Elasticsearch health check failed");
            return false;
        }
    }

    /// <inheritdoc/>
    public ValueTask<StorageStats> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(new StorageStats
        {
            TotalEventsStored = Interlocked.Read(ref _totalEventsStored),
            FailedWrites = Interlocked.Read(ref _failedWrites)
        });
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _indexManager.Dispose();
    }
}

/// <summary>
/// Null object pattern for dead letter handler when not configured.
/// </summary>
internal sealed class NullDeadLetterHandler : IDeadLetterHandler
{
    public ValueTask HandleFailedEventsAsync(IReadOnlyList<LogEvent> failedEvents, string reason, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    public ValueTask<int> ReplayDeadLetterQueueAsync(CancellationToken cancellationToken = default)
        => ValueTask.FromResult(0);
}