using Serilog;
using QUEBRIX.Logger.Sink;
using QUEBRIX.Logger.SampleCustomerApi.Services;
using QUEBRIX.Logger.SampleCustomerApi.Middleware;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // ═══════════════════════════════════════════════════════════════
    // QUEBRIX Logger Configuration
    // This demonstrates how a customer API can use QUEBRIX Logger
    // as a drop-in replacement for Seq with WriteTo.QUEBRIX()
    // ═══════════════════════════════════════════════════════════════

    builder.Host.UseSerilog((context, services, configuration) =>
    {
        configuration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithThreadId()
            .WriteTo.Console()
            // ★ QUEBRIX SINK - Primary log destination
            // Drop-in replacement for WriteTo.Seq()
            .WriteTo.QUEBRIX(options =>
            {
                // Connection
                options.Url = new Uri(context.Configuration.GetValue<string>("QuebrixLogger:Url") ?? "http://localhost:8080");
                options.ApiKey = context.Configuration.GetValue<string>("QuebrixLogger:ApiKey") ?? "your-api-key";

                // Metadata
                options.Application = context.Configuration.GetValue<string>("QuebrixLogger:Application") ?? "SampleCustomerApi";
                options.Environment = context.Configuration.GetValue<string>("QuebrixLogger:Environment") ?? "Development";

                // Batching configuration
                options.BatchSize = context.Configuration.GetValue<int>("QuebrixLogger:BatchSize");
                options.FlushPeriodSeconds = context.Configuration.GetValue<int>("QuebrixLogger:FlushPeriodSeconds");
                options.QueueSize = context.Configuration.GetValue<int>("QuebrixLogger:QueueSize");
                options.TimeoutSeconds = context.Configuration.GetValue<int>("QuebrixLogger:TimeoutSeconds");

                // Compression & buffering
                options.UseCompression = context.Configuration.GetValue<bool>("QuebrixLogger:UseCompression");
                options.EnableBuffering = context.Configuration.GetValue<bool>("QuebrixLogger:EnableBuffering");
                options.EnableOfflineMode = context.Configuration.GetValue<bool>("QuebrixLogger:EnableOfflineMode");
                options.EnableDurableMode = context.Configuration.GetValue<bool>("QuebrixLogger:EnableDurableMode");

                // Retry policy
                options.MaxRetries = context.Configuration.GetValue<int>("QuebrixLogger:MaxRetries");
                options.MaxBackoffSeconds = context.Configuration.GetValue<int>("QuebrixLogger:MaxBackoffSeconds");

                // Tags for grouping/filtering in QUEBRIX UI
                options.Tags = new HashSet<string>(
                    (context.Configuration.GetSection("QuebrixLogger:Tags").Get<string[]>() ?? Array.Empty<string>()),
                    StringComparer.OrdinalIgnoreCase
                );

                // Custom headers if needed
                if (context.Configuration.GetSection("QuebrixLogger:CustomHeaders").Exists())
                {
                    foreach (var header in context.Configuration.GetSection("QuebrixLogger:CustomHeaders").Get<Dictionary<string, string>>() ?? new())
                    {
                        options.CustomHeaders[header.Key] = header.Value;
                    }
                }
            });
    });

    // Services
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();

    // ★ Swagger / OpenAPI
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new()
        {
            Title = "QUEBRIX Logger - Sample Customer API",
            Version = "v1",
            Description = "A sample customer management API demonstrating structured logging with QUEBRIX Logger. " +
                          "Provides CRUD operations for customers and orders with full observability."
        });
    });

    // Register background service for simulated log generation (demo purposes)
    builder.Services.AddHostedService<SimulatedLogGeneratorService>();

    // Register custom middleware for request/response logging
    builder.Services.AddSingleton<RequestResponseLoggingMiddleware>();

    var app = builder.Build();

    // ★ Swagger middleware (available at /swagger)
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "QUEBRIX Sample Customer API v1");
        options.RoutePrefix = "swagger";
    });

    // Middleware pipeline
    app.UseMiddleware<RequestResponseLoggingMiddleware>();
    app.MapControllers();

    // Log startup information
    app.Lifetime.ApplicationStarted.Register(() =>
    {
        Log.ForContext("Application", "SampleCustomerApi")
           .Information("Sample Customer API started successfully. Listening on {Urls}", string.Join(", ", app.Urls));
    });

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Sample Customer API terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}