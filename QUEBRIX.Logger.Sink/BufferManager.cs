using System.Collections.Concurrent;
using System.Text.Json;
using Serilog.Debugging;
using QUEBRIX.Logger.Common.Options;
using QUEBRIX.Logger.Contracts;

namespace QUEBRIX.Logger.Sink;

/// <summary>
/// Manages buffering of log events for offline/durable mode.
/// Stores events in memory and optionally persists to disk.
/// </summary>
public sealed class BufferManager
{
    private readonly QuebrixSinkOptions _options;
    private readonly ConcurrentQueue<LogEvent> _memoryBuffer = new();
    private readonly string? _bufferFilePath;
    private readonly SemaphoreSlim _fileLock = new(1, 1);
    private long _bufferedCount;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public BufferManager(QuebrixSinkOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));

        if (options.EnableDurableMode && !string.IsNullOrEmpty(options.BufferPath))
        {
            _bufferFilePath = options.BufferPath;
            Directory.CreateDirectory(Path.GetDirectoryName(_bufferFilePath)!);
            LoadBufferFromDisk().GetAwaiter().GetResult();
        }
    }

    /// <summary>
    /// Number of buffered events.
    /// </summary>
    public long BufferedCount => Interlocked.Read(ref _bufferedCount);

    /// <summary>
    /// Buffers a log event in memory and optionally on disk.
    /// </summary>
    public void Buffer(LogEvent logEvent)
    {
        _memoryBuffer.Enqueue(logEvent);
        Interlocked.Increment(ref _bufferedCount);

        if (_bufferFilePath != null)
        {
            _ = PersistToDiskAsync(logEvent);
        }
    }

    /// <summary>
    /// Returns all buffered events and clears the buffer.
    /// </summary>
    public IReadOnlyList<LogEvent> Drain()
    {
        var events = new List<LogEvent>();
        while (_memoryBuffer.TryDequeue(out var logEvent))
        {
            events.Add(logEvent);
        }

        Interlocked.Exchange(ref _bufferedCount, 0);

        // Clear disk buffer if applicable
        if (_bufferFilePath != null && File.Exists(_bufferFilePath))
        {
            try
            {
                File.WriteAllText(_bufferFilePath, string.Empty);
            }
            catch (Exception ex)
            {
                SelfLog.WriteLine("QUEBRIX Sink: Failed to clear disk buffer: {0}", ex.Message);
            }
        }

        return events;
    }

    /// <summary>
    /// Clears the buffer.
    /// </summary>
    public void ClearBuffer()
    {
        while (_memoryBuffer.TryDequeue(out _)) { }
        Interlocked.Exchange(ref _bufferedCount, 0);
    }

    private async Task PersistToDiskAsync(LogEvent logEvent)
    {
        if (_bufferFilePath == null) return;

        await _fileLock.WaitAsync();
        try
        {
            var json = JsonSerializer.Serialize(logEvent, JsonOptions) + Environment.NewLine;
            await File.AppendAllTextAsync(_bufferFilePath, json);
        }
        catch (Exception ex)
        {
            SelfLog.WriteLine("QUEBRIX Sink: Failed to write to disk buffer: {0}", ex.Message);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    private async Task LoadBufferFromDisk()
    {
        if (_bufferFilePath == null || !File.Exists(_bufferFilePath)) return;

        try
        {
            var lines = await File.ReadAllLinesAsync(_bufferFilePath);
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                try
                {
                    var logEvent = JsonSerializer.Deserialize<LogEvent>(line, JsonOptions);
                    if (logEvent != null)
                    {
                        _memoryBuffer.Enqueue(logEvent);
                        Interlocked.Increment(ref _bufferedCount);
                    }
                }
                catch (JsonException)
                {
                    // Skip malformed lines
                }
            }

            SelfLog.WriteLine("QUEBRIX Sink: Loaded {0} events from disk buffer", _bufferedCount);
        }
        catch (Exception ex)
        {
            SelfLog.WriteLine("QUEBRIX Sink: Failed to load disk buffer: {0}", ex.Message);
        }
    }
}