using System.Collections.Concurrent;
using Serilog.Events;
using QUEBRIX.Logger.Common;
using QuebrixLogEvent = QUEBRIX.Logger.Contracts.LogEvent;

namespace QUEBRIX.Logger.Sink;

/// <summary>
/// Converts Serilog <see cref="Serilog.Events.LogEvent"/> instances to QUEBRIX <see cref="QuebrixLogEvent"/> instances.
/// Thread-safe and optimized for high throughput.
/// Mapped fields follow the Serilog Elasticsearch schema (@m, @x, @tr, @sp, @l, @i, @mt).
/// Known Serilog properties are extracted to top-level fields and removed from Properties dictionary.
/// </summary>
public sealed class QuebrixLogEventConverter
{
    private readonly Common.Options.QuebrixSinkOptions _options;
    private static readonly ConcurrentDictionary<string, string> LevelCache = new();

    /// <summary>
    /// Known Serilog property names that should be mapped to top-level fields.
    /// </summary>
    private static readonly HashSet<string> KnownPropertyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "SourceContext",
        "RequestId",
        "TraceId",
        "SpanId",
        "ParentSpanId",
        "EventId",
        "UserId",
        "SessionId",
        "MachineName",
        "EnvironmentUserName",
        "ProcessId",
        "ThreadId",
        "Host",
        "Application",
        "Environment"
    };

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

        var level = ConvertLevelToString(serilogEvent.Level);
        var properties = new Dictionary<string, object?>();

        // Default values from options
        string? sourceContext = null;
        string? requestId = null;
        string? traceId = null;
        string? spanId = null;
        string? eventId = null;
        string? userId = null;
        string? sessionId = null;

        // Copy all Serilog properties and extract known ones
        foreach (var property in serilogEvent.Properties)
        {
            var propValue = ConvertPropertyValue(property.Value);

            // Extract known properties to top-level fields
            switch (property.Key)
            {
                case "SourceContext":
                    sourceContext = propValue?.ToString();
                    break;
                case "RequestId":
                    requestId = propValue?.ToString();
                    break;
                case "TraceId":
                    traceId = propValue?.ToString();
                    break;
                case "SpanId":
                    spanId = propValue?.ToString();
                    break;
                case "EventId":
                case "EventId.Id":
                    eventId = propValue?.ToString();
                    break;
                case "UserId":
                    userId = propValue?.ToString();
                    break;
                case "SessionId":
                    sessionId = propValue?.ToString();
                    break;
                default:
                    // Only add to properties if it's not a known top-level field
                    if (!KnownPropertyNames.Contains(property.Key))
                    {
                        properties[property.Key] = propValue;
                    }
                    break;
            }
        }

        // Add QUEBRIX enrichment properties
        if (!string.IsNullOrEmpty(_options.Application))
            properties[QuebrixConstants.PropertyApplication] = _options.Application;

        if (!string.IsNullOrEmpty(_options.Environment))
            properties[QuebrixConstants.PropertyEnvironment] = _options.Environment;

        foreach (var tag in _options.Tags)
        {
            var tagKey = "tag_" + tag;
            if (!properties.ContainsKey(tagKey))
                properties[tagKey] = tag;
        }

        // Build the log event with all Serilog-compatible fields
        var logEvent = new QuebrixLogEvent
        {
            Timestamp = serilogEvent.Timestamp.UtcDateTime,
            Level = level,
            Message = serilogEvent.RenderMessage(),
            MessageTemplate = serilogEvent.MessageTemplate?.Text ?? string.Empty,
            Exception = serilogEvent.Exception?.ToString(),
            SourceContext = sourceContext,
            RequestId = requestId,
            TraceId = traceId,
            SpanId = spanId,
            EventId = eventId,
            UserId = userId,
            SessionId = sessionId,
            MachineName = Environment.MachineName,
            ProcessId = Environment.ProcessId,
            ThreadId = Environment.CurrentManagedThreadId,
            Host = Environment.MachineName,
            Environment = _options.Environment,
            Application = _options.Application,
            Properties = properties.Count > 0 ? properties : null
        };

        return logEvent;
    }

    private static string ConvertLevelToString(Serilog.Events.LogEventLevel level) => level switch
    {
        Serilog.Events.LogEventLevel.Verbose => "Verbose",
        Serilog.Events.LogEventLevel.Debug => "Debug",
        Serilog.Events.LogEventLevel.Information => "Information",
        Serilog.Events.LogEventLevel.Warning => "Warning",
        Serilog.Events.LogEventLevel.Error => "Error",
        Serilog.Events.LogEventLevel.Fatal => "Fatal",
        _ => "Information"
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