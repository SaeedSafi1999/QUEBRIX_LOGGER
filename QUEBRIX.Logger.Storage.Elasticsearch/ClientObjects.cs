using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Nodes;
using Microsoft.Extensions.Logging;
using Nest;
using QUEBRIX.Logger.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace QUEBRIX.Logger.Storage.Elasticsearch
{
    public interface IElasticService
    {
        ValueTask<bool> AddDocAsync<T>(T document, CancellationToken cancellationToken = default)
            where T : class;

        ValueTask<bool> SetDocAsync<T>(T document, CancellationToken cancellationToken = default)
            where T : class;

        ValueTask<bool> BulkAddAsync<T>(IReadOnlyList<T> documents, CancellationToken cancellationToken = default)
            where T : class;
    }

    public sealed class ElasticService : IElasticService
    {
        private readonly ElasticsearchClient _client;
        private readonly ElasticsearchIndexManager _indexManager;
        private readonly ILogger<ElasticService> _logger;

        public ElasticService(
            ElasticsearchClient client,
            ElasticsearchIndexManager indexManager,
            ILogger<ElasticService> logger)
        {
            _client = client;
            _indexManager = indexManager;
            _logger = logger;
        }

        public async ValueTask<bool> AddDocAsync<T>(
            T document,
            CancellationToken cancellationToken = default)
            where T : class
        {
            await _indexManager.EnsureIndexAsync(cancellationToken);
            var indexName = await _indexManager.GetCurrentIndexNameAsync(cancellationToken);

            var response = await _client.IndexAsync(
                document,
                i => i.Index(indexName),
                cancellationToken);

            if (!response.IsValidResponse)
            {
                _logger.LogError("Failed to index document: {Error}", response.DebugInformation);
            }

            return response.IsValidResponse;
        }

        public async ValueTask<bool> SetDocAsync<T>(
            T document,
            CancellationToken cancellationToken = default)
            where T : class
        {
            await _indexManager.EnsureIndexAsync(cancellationToken);
            var indexName = await _indexManager.GetCurrentIndexNameAsync(cancellationToken);

            var response = await _client.UpdateAsync<T, T>(
                indexName,
                u => u
                    .Doc(document)
                    .DocAsUpsert(true),
                cancellationToken);

            if (!response.IsValidResponse)
            {
                _logger.LogError("Failed to upsert document: {Error}", response.DebugInformation);
            }

            return response.IsValidResponse;
        }

        public async ValueTask<bool> DeleteDocAsync<T>(
            string id,
            CancellationToken cancellationToken = default)
            where T : class
        {
            await _indexManager.EnsureIndexAsync(cancellationToken);
            var indexName = await _indexManager.GetCurrentIndexNameAsync(cancellationToken);

            var response = await _client.DeleteAsync<T>(
                indexName,
                id,
                cancellationToken);

            if (!response.IsValidResponse)
            {
                _logger.LogError("Failed to delete document: {Error}", response.DebugInformation);
            }

            return response.IsValidResponse;
        }

        public async ValueTask<bool> BulkAddAsync<T>(
            IReadOnlyList<T> documents,
            CancellationToken cancellationToken = default)
            where T : class
        {
            if (documents.Count == 0)
                return true;

            await _indexManager.EnsureIndexAsync(cancellationToken);
            var indexName = await _indexManager.GetCurrentIndexNameAsync(cancellationToken);

            var response = await _client.BulkAsync(b => b
                .Index(indexName)
                .IndexMany(documents),
                cancellationToken);

            if (!response.IsValidResponse)
            {
                _logger.LogError("Bulk insert failed: {Error}", response.DebugInformation);
                return false;
            }

            if (response.Errors)
            {
                foreach (var item in response.Items.Where(i => i.Status >= 400))
                {
                    _logger.LogError(
                        "Bulk item failed. Id={Id}, Status={Status}, Error={Error}",
                        item.Id,
                        item.Status,
                        item.Error?.Reason);
                }

                return false;
            }

            return true;
        }
    }
}
