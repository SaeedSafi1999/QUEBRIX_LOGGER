using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using QUEBRIX.Logger.Common.Options;
using QUEBRIX.Logger.Sink;

namespace QUEBRIX.Logger.SDK;

/// <summary>
/// Factory for creating and configuring QUEBRIX loggers programmatically.
/// </summary>
public static class QuebrixLoggerFactory
{
    /// <summary>
    /// Creates a Serilog logger configured to send events to QUEBRIX.
    /// </summary>
    public static Serilog.ILogger CreateLogger(Action<QuebrixSinkOptions> configureOptions)
    {
        return new LoggerConfiguration()
            .WriteTo.QUEBRIX(configureOptions)
            .CreateLogger();
    }

    /// <summary>
    /// Creates a Serilog logger using the specified configuration section.
    /// </summary>
    public static Serilog.ILogger CreateLogger(IConfiguration configuration, string sectionName = QuebrixSinkOptions.SectionName)
    {
        var options = new QuebrixSinkOptions();
        configuration.GetSection(sectionName).Bind(options);

        return new LoggerConfiguration()
            .WriteTo.QUEBRIX(cfg =>
            {
                cfg.Url = options.Url;
                cfg.ApiKey = options.ApiKey;
                cfg.Application = options.Application;
                cfg.Environment = options.Environment;
                cfg.MinimumLevel = options.MinimumLevel;
                cfg.BatchSize = options.BatchSize;
                cfg.FlushPeriodSeconds = options.FlushPeriodSeconds;
                cfg.QueueSize = options.QueueSize;
                cfg.TimeoutSeconds = options.TimeoutSeconds;
                cfg.UseCompression = options.UseCompression;
                cfg.Tags = options.Tags;
            })
            .CreateLogger();
    }

    /// <summary>
    /// Configures the application's logging pipeline to use QUEBRIX.
    /// </summary>
    public static ILoggingBuilder AddQuebrix(this ILoggingBuilder builder, Action<QuebrixSinkOptions>? configureOptions = null)
    {
        var options = new QuebrixSinkOptions();
        configureOptions?.Invoke(options);

        var logger = new LoggerConfiguration()
            .WriteTo.QUEBRIX(cfg =>
            {
                cfg.Url = options.Url;
                cfg.ApiKey = options.ApiKey;
                cfg.Application = options.Application;
                cfg.Environment = options.Environment;
                cfg.MinimumLevel = options.MinimumLevel;
                cfg.BatchSize = options.BatchSize;
                cfg.FlushPeriodSeconds = options.FlushPeriodSeconds;
                cfg.QueueSize = options.QueueSize;
                cfg.TimeoutSeconds = options.TimeoutSeconds;
                cfg.UseCompression = options.UseCompression;
                cfg.Tags = options.Tags;
                cfg.EnableBuffering = options.EnableBuffering;
                cfg.EnableOfflineMode = options.EnableOfflineMode;
                cfg.EnableDurableMode = options.EnableDurableMode;
                cfg.MaxRetries = options.MaxRetries;
                cfg.MaxBackoffSeconds = options.MaxBackoffSeconds;
                cfg.UseNdjson = options.UseNdjson;
            })
            .CreateLogger();

        builder.AddSerilog(logger, dispose: true);
        return builder;
    }
}