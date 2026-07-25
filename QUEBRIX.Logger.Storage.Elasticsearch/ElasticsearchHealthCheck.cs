using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using QUEBRIX.Logger.Storage.Abstractions;

namespace QUEBRIX.Logger.Storage.Elasticsearch;

/// <summary>
/// Health check that verifies connectivity to Elasticsearch.
/// </summary>
public sealed class ElasticsearchHealthCheck : IHealthCheck
{
    private readonly ILogStorage _storage;
    private readonly ILogger<ElasticsearchHealthCheck> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="ElasticsearchHealthCheck"/>.
    /// </summary>
    public ElasticsearchHealthCheck(ILogStorage storage, ILogger<ElasticsearchHealthCheck> logger)
    {
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var healthy = await _storage.IsHealthyAsync(cancellationToken);
            if (healthy)
            {
                var stats = await _storage.GetStatsAsync(cancellationToken);
                return HealthCheckResult.Healthy("Elasticsearch is healthy", data: new Dictionary<string, object>
                {
                    ["totalEventsStored"] = stats.TotalEventsStored,
                    ["failedWrites"] = stats.FailedWrites,
                    ["deadLetterCount"] = stats.DeadLetterCount
                });
            }

            return HealthCheckResult.Unhealthy("Elasticsearch ping failed");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Elasticsearch health check failed");
            return HealthCheckResult.Unhealthy("Elasticsearch health check failed", ex);
        }
    }
}