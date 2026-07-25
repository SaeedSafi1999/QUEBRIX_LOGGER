using QUEBRIX.Logger.Contracts;

namespace QUEBRIX.Logger.Core.Processing;

/// <summary>
/// Defines a log event processor in the pipeline.
/// </summary>
public interface ILogEventProcessor
{
    /// <summary>
    /// Processes a log event. Return the processed event or null to drop it.
    /// </summary>
    ValueTask<LogEvent?> ProcessAsync(LogEvent logEvent, CancellationToken cancellationToken = default);
}

/// <summary>
/// Defines a log event enricher that adds properties to events.
/// </summary>
public interface ILogEventEnricher
{
    /// <summary>
    /// Enriches a log event with additional properties.
    /// </summary>
    ValueTask EnrichAsync(LogEvent logEvent, CancellationToken cancellationToken = default);
}

/// <summary>
/// Defines a log event filter that determines if an event should be included.
/// </summary>
public interface ILogEventFilter
{
    /// <summary>
    /// Returns true if the event should be included.
    /// </summary>
    ValueTask<bool> ShouldIncludeAsync(LogEvent logEvent, CancellationToken cancellationToken = default);
}

/// <summary>
/// Default enricher that adds machine-level properties and server context.
/// </summary>
public sealed class DefaultEnricher : ILogEventEnricher
{
    private static readonly string MachineName = Environment.MachineName;
    private static readonly int ProcessId = Environment.ProcessId;
    private static readonly string? HostName = System.Net.Dns.GetHostName();

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultEnricher"/> class.
    /// </summary>
    public DefaultEnricher()
    {
        Application = null;
        EnvironmentName = null;
    }

    /// <summary>
    /// Initializes a new instance with server options.
    /// </summary>
    public DefaultEnricher(Microsoft.Extensions.Options.IOptions<QUEBRIX.Logger.Common.Options.QuebrixServerOptions> serverOptions)
    {
        Application = serverOptions?.Value?.Application;
        EnvironmentName = serverOptions?.Value?.Environment;
    }

    /// <summary>
    /// The application name to inject into events.
    /// </summary>
    public string? Application { get; }

    /// <summary>
    /// The environment name to inject into events.
    /// </summary>
    public string? EnvironmentName { get; }

    public ValueTask EnrichAsync(LogEvent logEvent, CancellationToken cancellationToken = default)
    {
        logEvent.MachineName ??= MachineName;
        logEvent.ProcessId ??= ProcessId;
        logEvent.Host ??= HostName;
        if (!string.IsNullOrEmpty(Application)) logEvent.Application ??= Application;
        if (!string.IsNullOrEmpty(EnvironmentName)) logEvent.Environment ??= EnvironmentName;
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// The log event processing pipeline that runs enrichers and filters.
/// </summary>
public sealed class LogEventPipeline
{
    private readonly IEnumerable<ILogEventEnricher> _enrichers;
    private readonly IEnumerable<ILogEventFilter> _filters;
    private readonly IEnumerable<ILogEventProcessor> _processors;

    public LogEventPipeline(
        IEnumerable<ILogEventEnricher> enrichers,
        IEnumerable<ILogEventFilter> filters,
        IEnumerable<ILogEventProcessor> processors)
    {
        _enrichers = enrichers ?? Enumerable.Empty<ILogEventEnricher>();
        _filters = filters ?? Enumerable.Empty<ILogEventFilter>();
        _processors = processors ?? Enumerable.Empty<ILogEventProcessor>();
    }

    /// <summary>
    /// Processes a log event through the entire pipeline.
    /// Returns null if the event should be dropped.
    /// </summary>
    public async ValueTask<LogEvent?> ProcessAsync(LogEvent logEvent, CancellationToken cancellationToken = default)
    {
        // Run enrichers
        foreach (var enricher in _enrichers)
        {
            await enricher.EnrichAsync(logEvent, cancellationToken);
        }

        // Run filters
        foreach (var filter in _filters)
        {
            if (!await filter.ShouldIncludeAsync(logEvent, cancellationToken))
            {
                return null;
            }
        }

        // Run processors
        foreach (var processor in _processors)
        {
            logEvent = await processor.ProcessAsync(logEvent, cancellationToken);
            if (logEvent == null) return null;
        }

        return logEvent;
    }
}