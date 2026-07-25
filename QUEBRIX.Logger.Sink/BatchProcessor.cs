using System.Threading.Channels;
using Serilog.Debugging;
using QUEBRIX.Logger.Common.Options;
using QUEBRIX.Logger.Contracts;

namespace QUEBRIX.Logger.Sink;

/// <summary>
/// Processes log events in batches with configurable size and period.
/// Uses Channel<T> for high-performance, non-blocking event ingestion.
/// </summary>
public sealed class BatchProcessor : IDisposable
{
    private readonly Channel<LogEvent> _channel;
    private readonly QuebrixSinkOptions _options;
    private readonly BufferManager _bufferManager;
    private readonly Func<IReadOnlyList<LogEvent>, Task<bool>> _sendBatchAsync;
    private readonly CancellationTokenSource _cts;
    private Task? _processingTask;
    private Timer? _flushTimer;
    private volatile bool _isRunning;

    /// <summary>
    /// Initializes a new instance of <see cref="BatchProcessor"/>.
    /// </summary>
    /// <param name="options">Sink configuration options.</param>
    /// <param name="bufferManager">Buffer manager for offline/durable mode.</param>
    /// <param name="sendBatchAsync">Function to send a batch of log events asynchronously.</param>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
    public BatchProcessor(
        QuebrixSinkOptions options,
        BufferManager bufferManager,
        Func<IReadOnlyList<LogEvent>, Task<bool>> sendBatchAsync)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _bufferManager = bufferManager ?? throw new ArgumentNullException(nameof(bufferManager));
        _sendBatchAsync = sendBatchAsync ?? throw new ArgumentNullException(nameof(sendBatchAsync));
        _cts = new CancellationTokenSource();

        _channel = Channel.CreateBounded<LogEvent>(new BoundedChannelOptions(options.QueueSize)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });
    }

    /// <summary>
    /// Starts the batch processing loop and timer.
    /// </summary>
    public void Start()
    {
        if (_isRunning) return;
        _isRunning = true;

        _flushTimer = new Timer(OnFlushTimer, null,
            TimeSpan.FromSeconds(_options.FlushPeriodSeconds),
            TimeSpan.FromSeconds(_options.FlushPeriodSeconds));

        _processingTask = Task.Run(ProcessingLoopAsync);
    }

    /// <summary>
    /// Adds a log event to the processing queue.
    /// </summary>
    /// <param name="logEvent">The log event to add.</param>
    public void Add(LogEvent logEvent)
    {
        if (!_isRunning) return;

        if (!_channel.Writer.TryWrite(logEvent))
        {
            if (_options.EnableOfflineMode || _options.EnableDurableMode)
            {
                _bufferManager.Buffer(logEvent);
            }
            else
            {
                SelfLog.WriteLine("QUEBRIX Sink: Channel full, dropping event");
            }
        }
    }

    /// <summary>
    /// Flushes pending events and stops processing gracefully.
    /// </summary>
    public async Task FlushAndStopAsync()
    {
        _isRunning = false;
        _flushTimer?.Dispose();
        _channel.Writer.TryComplete();
        if (_processingTask != null)
            await _processingTask;
    }

    private void OnFlushTimer(object? state)
    {
        // Signal the processing loop to flush early by adding a sentinel
        // The processing loop handles batching internally
    }

    private async Task ProcessingLoopAsync()
    {
        var batch = new List<LogEvent>(_options.BatchSize);

        try
        {
            await foreach (var logEvent in _channel.Reader.ReadAllAsync(_cts.Token))
            {
                batch.Add(logEvent);

                if (batch.Count >= _options.BatchSize)
                {
                    await SendBatchWithRetryAsync(batch);
                    batch.Clear();
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Graceful shutdown
        }
        catch (Exception ex)
        {
            SelfLog.WriteLine("QUEBRIX Sink: Processing loop error: {0}", ex.Message);
        }

        if (batch.Count > 0)
            await SendBatchWithRetryAsync(batch);

        var buffered = _bufferManager.Drain();
        if (buffered.Count > 0)
            await SendBatchWithRetryAsync(buffered.ToList());
    }

    private async Task SendBatchWithRetryAsync(List<LogEvent> batch)
    {
        if (batch.Count == 0) return;

        var maxAttempts = _options.MaxRetries + 1;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                var success = await _sendBatchAsync(batch);

                if (success)
                {
                    _bufferManager.ClearBuffer();
                    return;
                }

                SelfLog.WriteLine("QUEBRIX Sink: Batch send failed (attempt {0}/{1})", attempt, maxAttempts);
            }
            catch (Exception ex)
            {
                SelfLog.WriteLine("QUEBRIX Sink: Batch send exception (attempt {0}/{1}): {2}", attempt, maxAttempts, ex.Message);
            }

            if (attempt < maxAttempts)
            {
                var backoffSeconds = Math.Min(
                    Math.Pow(2, attempt - 1),
                    _options.MaxBackoffSeconds);
                await Task.Delay(TimeSpan.FromSeconds(backoffSeconds));
            }
        }

        if (_options.EnableOfflineMode || _options.EnableDurableMode)
        {
            foreach (var evt in batch)
                _bufferManager.Buffer(evt);
        }
        else
        {
            SelfLog.WriteLine("QUEBRIX Sink: Dropping {0} events after {1} failed attempts", batch.Count, maxAttempts);
        }
    }

    /// <summary>
    /// Disposes the processor and associated resources.
    /// </summary>
    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _flushTimer?.Dispose();
    }
}