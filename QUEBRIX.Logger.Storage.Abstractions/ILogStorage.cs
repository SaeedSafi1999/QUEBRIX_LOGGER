using QUEBRIX.Logger.Contracts;

namespace QUEBRIX.Logger.Storage.Abstractions;

/// <summary>
/// Defines the contract for log storage backends.
/// </summary>
public interface ILogStorage
{
    /// <summary>
    /// Stores a single log event.
    /// </summary>
    ValueTask StoreAsync(LogEvent logEvent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores a batch of log events.
    /// </summary>
    ValueTask StoreBatchAsync(IReadOnlyList<LogEvent> logEvents, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks the health of the storage backend.
    /// </summary>
    ValueTask<bool> IsHealthyAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets storage statistics.
    /// </summary>
    ValueTask<StorageStats> GetStatsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Storage statistics.
/// </summary>
public sealed record StorageStats
{
    /// <summary>
    /// Total number of stored events.
    /// </summary>
    public long TotalEventsStored { get; init; }

    /// <summary>
    /// Total storage size in bytes.
    /// </summary>
    public long StorageSizeBytes { get; init; }

    /// <summary>
    /// Number of failed writes.
    /// </summary>
    public long FailedWrites { get; init; }

    /// <summary>
    /// Number of events in dead letter queue.
    /// </summary>
    public long DeadLetterCount { get; init; }
}

/// <summary>
/// Defines the contract for log storage index management.
/// </summary>
public interface IIndexManager
{
    /// <summary>
    /// Ensures the storage index exists and is configured.
    /// </summary>
    ValueTask EnsureIndexAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an index by name.
    /// </summary>
    ValueTask<bool> DeleteIndexAsync(string indexName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current index name being written to.
    /// </summary>
    ValueTask<string> GetCurrentIndexNameAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Defines dead letter queue handling for failed storage operations.
/// </summary>
public interface IDeadLetterHandler
{
    /// <summary>
    /// Handles events that failed to be stored.
    /// </summary>
    ValueTask HandleFailedEventsAsync(IReadOnlyList<LogEvent> failedEvents, string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts to replay events from the dead letter queue.
    /// </summary>
    ValueTask<int> ReplayDeadLetterQueueAsync(CancellationToken cancellationToken = default);
}