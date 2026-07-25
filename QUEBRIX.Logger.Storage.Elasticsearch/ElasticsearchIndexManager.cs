using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.IndexManagement;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace QUEBRIX.Logger.Storage.Elasticsearch;

/// <summary>
/// Manages Elasticsearch index creation and templates for QUEBRIX log events.
/// Thread-safe with singleton initialization pattern.
/// </summary>
public sealed class ElasticsearchIndexManager : IDisposable
{
    private readonly ElasticsearchClient _client;
    private readonly QuebrixElasticsearchOptions _options;
    private readonly ILogger<ElasticsearchIndexManager> _logger;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private volatile bool _initialized;
    private string _currentIndexName = string.Empty;
    private int _disposed;

    public ElasticsearchIndexManager(
        ElasticsearchClient client,
        IOptions<QuebrixElasticsearchOptions> options,
        ILogger<ElasticsearchIndexManager> logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _options = options.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async ValueTask EnsureIndexAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized) return;
        await _initLock.WaitAsync(cancellationToken);
        try
        {
            if (_initialized) return;
            await EnsureIndexTemplateAsync(cancellationToken);
            await EnsureCurrentIndexAsync(cancellationToken);
            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    public ValueTask<string> GetCurrentIndexNameAsync(CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrEmpty(_currentIndexName))
            return ValueTask.FromResult(_currentIndexName);
        var suffix = _options.Cadence switch
        {
            IndexCadence.Daily => DateTime.UtcNow.ToString("yyyy.MM.dd"),
            IndexCadence.Monthly => DateTime.UtcNow.ToString("yyyy.MM"),
            _ => DateTime.UtcNow.ToString("yyyy.MM.dd")
        };
        _currentIndexName = $"{_options.IndexPrefix}-{suffix}";
        return ValueTask.FromResult(_currentIndexName);
    }

    private async Task EnsureIndexTemplateAsync(CancellationToken cancellationToken)
    {
        var templateName = $"{_options.IndexPrefix}-template";
        try
        {
            var existsResponse = await _client.Indices.ExistsIndexTemplateAsync(templateName, cancellationToken);
            if (existsResponse.IsValidResponse && existsResponse.Exists)
                return;

            var response = await _client.Indices.PutIndexTemplateAsync(templateName, t =>
            {
                t.IndexPatterns($"{_options.IndexPrefix}-*");
                t.Priority(100);
                t.Template(new IndexTemplateMapping
                {
                    Settings = new IndexSettings
                    {
                        NumberOfShards = _options.NumberOfShards,
                        NumberOfReplicas = _options.NumberOfReplicas
                    },
                    Mappings = new Elastic.Clients.Elasticsearch.Mapping.TypeMapping
                    {
                        Properties = new Elastic.Clients.Elasticsearch.Mapping.Properties
                        {
                            { "timestamp", new Elastic.Clients.Elasticsearch.Mapping.DateProperty() },
                            { "level", new Elastic.Clients.Elasticsearch.Mapping.KeywordProperty() },
                            { "messageTemplate", new Elastic.Clients.Elasticsearch.Mapping.TextProperty() },
                            { "renderedMessage", new Elastic.Clients.Elasticsearch.Mapping.TextProperty() },
                            { "exception", new Elastic.Clients.Elasticsearch.Mapping.TextProperty() },
                            { "machineName", new Elastic.Clients.Elasticsearch.Mapping.KeywordProperty() },
                            { "processId", new Elastic.Clients.Elasticsearch.Mapping.IntegerNumberProperty() },
                            { "threadId", new Elastic.Clients.Elasticsearch.Mapping.IntegerNumberProperty() },
                            { "environment", new Elastic.Clients.Elasticsearch.Mapping.KeywordProperty() },
                            { "application", new Elastic.Clients.Elasticsearch.Mapping.KeywordProperty() },
                            { "host", new Elastic.Clients.Elasticsearch.Mapping.KeywordProperty() },
                            { "traceId", new Elastic.Clients.Elasticsearch.Mapping.KeywordProperty() },
                            { "spanId", new Elastic.Clients.Elasticsearch.Mapping.KeywordProperty() },
                            { "correlationId", new Elastic.Clients.Elasticsearch.Mapping.KeywordProperty() }
                        },
                        Dynamic = Elastic.Clients.Elasticsearch.Mapping.DynamicMapping.True
                    }
                });
            }, cancellationToken);

            if (!response.IsValidResponse)
                _logger.LogWarning("Index template creation issue: {Error}", response.DebugInformation);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to configure index template");
        }
    }

    private async Task EnsureCurrentIndexAsync(CancellationToken cancellationToken)
    {
        var indexName = await GetCurrentIndexNameAsync(cancellationToken);
        try
        {
            var existsResponse = await _client.Indices.ExistsAsync(indexName, cancellationToken);
            if (existsResponse.IsValidResponse && existsResponse.Exists)
                return;

            var createResponse = await _client.Indices.CreateAsync(indexName, c =>
            {
                c.Settings(new IndexSettings
                {
                    NumberOfShards = _options.NumberOfShards,
                    NumberOfReplicas = _options.NumberOfReplicas
                });
            }, cancellationToken);

            if (!createResponse.IsValidResponse)
                _logger.LogInformation("Index may already exist or creation failed: {Error}", createResponse.DebugInformation);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to ensure index exists");
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
            _initLock.Dispose();
    }
}