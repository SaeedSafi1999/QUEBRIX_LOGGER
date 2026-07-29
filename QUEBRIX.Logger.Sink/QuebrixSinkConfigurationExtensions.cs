using QUEBRIX.Logger.Common.Options;
using Serilog;
using Serilog.Configuration;
using Serilog.Events;

namespace QUEBRIX.Logger.Sink;

/// <summary>
/// Extends Serilog's WriteTo configuration with QUEBRIX sink support.
/// Enables the fluent API: .WriteTo.QUEBRIX(options => ...)
/// </summary>
public static class QuebrixSinkConfigurationExtensions
{
    /// <summary>
    /// Writes log events to the QUEBRIX Logger server.
    /// Drop-in replacement for WriteTo.Seq().
    /// </summary>
    /// <param name="sinkConfiguration">Serilog sink configuration.</param>
    /// <param name="configureOptions">Action to configure QUEBRIX sink options.</param>
    /// <param name="restrictedToMinimumLevel">Minimum log level for this sink.</param>
    /// <returns>Logger configuration for chaining.</returns>
    public static LoggerConfiguration QUEBRIX(
        this LoggerSinkConfiguration sinkConfiguration,
        Action<QuebrixSinkOptions> configureOptions,
        LogEventLevel restrictedToMinimumLevel = LogEventLevel.Verbose)
    {
        if (sinkConfiguration == null)
            throw new ArgumentNullException(nameof(sinkConfiguration));

        if (configureOptions == null)
            throw new ArgumentNullException(nameof(configureOptions));

        var options = new QuebrixSinkOptions();
        configureOptions(options);

        if (string.IsNullOrEmpty(options.Url.ToString()))
            throw new ArgumentException("Server URL is not set in options");

        var sink = new QuebrixSink(options);

        return sinkConfiguration.Sink(sink, restrictedToMinimumLevel);
    }

    /// <summary>
    /// Writes log events to the QUEBRIX Logger server with URI shortcut.
    /// </summary>
    /// <param name="sinkConfiguration">Serilog sink configuration.</param>
    /// <param name="serverUrl">The URL of the QUEBRIX Logger server.</param>
    /// <param name="apiKey">API key for authentication.</param>
    /// <param name="application">Application name.</param>
    /// <param name="environment">Environment name.</param>
    /// <param name="restrictedToMinimumLevel">Minimum log level for this sink.</param>
    /// <returns>Logger configuration for chaining.</returns>
    public static LoggerConfiguration QUEBRIX(
        this LoggerSinkConfiguration sinkConfiguration,
        string serverUrl,
        string? apiKey = null,
        string? application = null,
        string? environment = null,
        LogEventLevel restrictedToMinimumLevel = LogEventLevel.Verbose)
    {
        if (sinkConfiguration == null)
            throw new ArgumentNullException(nameof(sinkConfiguration));

        if (string.IsNullOrWhiteSpace(serverUrl))
            throw new ArgumentNullException(nameof(serverUrl));

        return sinkConfiguration.QUEBRIX(options =>
        {
            options.Url = new Uri(serverUrl);
            if (!string.IsNullOrEmpty(apiKey)) options.ApiKey = apiKey;
            if (!string.IsNullOrEmpty(application)) options.Application = application;
            if (!string.IsNullOrEmpty(environment)) options.Environment = environment;
        }, restrictedToMinimumLevel);
    }
}