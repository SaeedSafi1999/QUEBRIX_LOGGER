using System.Collections.Concurrent;
using Serilog.Events;
using QUEBRIX.Logger.Common;
using QuebrixLogEvent = QUEBRIX.Logger.Contracts.LogEvent;

namespace QUEBRIX.Logger.Sink;

/// <summary>
/// Converts Serilog <see cref="Serilog.Events.LogEvent"/> instances to QUEBRIX <see cref="QuebrixLogEvent"/> instances.
/// Thread-safe and optimized for high throughput.
/// </summary>
public sealed class QuebrixLogEventConverter
{
    private readonly Common.Options.QuebrixSinkOptions _options;
    private static readonly ConcurrentDictionary<string, QuebrixLogLevel> LevelCache = new();

    /// <summary>
    /// Initializes a new instance of <see cref="QuebrixLogEventConverter"/>.
    /// </summary>
    /// <param name="options">Sink configuration options.</param>
    public QuebrixLogEventConverter(Common.Options.QuebrixSinkOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Converts a Serilog <see cref="Serilog.Events.LogEvent"/> to a QUEBRIX <see cref="QuebrixLogEvent"/>.
    /// </summary>
    public QuebrixLogEvent Convert(Serilog.Events.LogEvent serilogEvent)
    {
        ArgumentNullException.ThrowIfNull(serilogEvent);

        var level = ConvertLevel(serilogEvent.Level);
        var properties = new Dictionary<string, object?>();

        // Copy all Serilog properties
        foreach (var property in serilogEvent.Properties)
        {
            properties[property.Key] = ConvertPropertyValue(property.Value);
        }

        // Add QUEBRIX enrichment properties
        if (!string.IsNullOrEmpty(_options.Application))
            properties[QuebrixConstants.PropertyApplication] = _options.Application;

        if (!string.IsNullOrEmpty(_options.Environment))
            properties[QuebrixConstants.PropertyEnvironment] = _options.Environment;


        foreach (var tag in _options.Tags)
        {
            if (!properties.ContainsKey("tag_" + tag))
                properties["tag_" + tag] = tag;
        }

        return new QuebrixLogEvent
        {
            Timestamp = serilogEvent.Timestamp.UtcDateTime,
            Level = level,
            MessageTemplate = serilogEvent.MessageTemplate?.Text ?? string.Empty,
            RenderedMessage = serilogEvent.RenderMessage(),
            Exception = serilogEvent.Exception?.ToString(),
            Properties = properties,
            MachineName = Environment.MachineName,
            ProcessId = Environment.ProcessId,
            ThreadId = Environment.CurrentManagedThreadId,
            Environment = _options.Environment,
            Application = _options.Application,
            Host = Environment.MachineName
        };
    }

    private static QuebrixLogLevel ConvertLevel(Serilog.Events.LogEventLevel level) => level switch
    {
        Serilog.Events.LogEventLevel.Verbose => QuebrixLogLevel.Verbose,
        Serilog.Events.LogEventLevel.Debug => QuebrixLogLevel.Debug,
        Serilog.Events.LogEventLevel.Information => QuebrixLogLevel.Information,
        Serilog.Events.LogEventLevel.Warning => QuebrixLogLevel.Warning,
        Serilog.Events.LogEventLevel.Error => QuebrixLogLevel.Error,
        Serilog.Events.LogEventLevel.Fatal => QuebrixLogLevel.Fatal,
        _ => QuebrixLogLevel.Information
    };

    private static object? ConvertPropertyValue(LogEventPropertyValue value)
    {
        return value switch
        {
            ScalarValue scalar => scalar.Value,
            SequenceValue seq => seq.Elements.Select(ConvertPropertyValue).ToList(),
            StructureValue structVal => structVal.Properties.ToDictionary(p => p.Name, p => ConvertPropertyValue(p.Value)),
            DictionaryValue dict => dict.Elements.ToDictionary(
                d => d.Key.Value?.ToString() ?? string.Empty,
                d => ConvertPropertyValue(d.Value)),
            _ => value.ToString()
        };
    }
}