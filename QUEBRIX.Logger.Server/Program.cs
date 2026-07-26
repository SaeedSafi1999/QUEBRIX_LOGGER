using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Elastic.Clients.Elasticsearch;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using QUEBRIX.Logger.Common.Options;
using QUEBRIX.Logger.Contracts;
using QUEBRIX.Logger.Core.Ingestion;
using QUEBRIX.Logger.Core.Processing;
using QUEBRIX.Logger.Security.Authentication;
using QUEBRIX.Logger.Security.Authorization;
using QUEBRIX.Logger.Security.RateLimiting;
using QUEBRIX.Logger.Storage.Abstractions;
using QUEBRIX.Logger.Storage.Elasticsearch;
using Nest;

var builder = WebApplication.CreateBuilder(args);

// Configuration
builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
    .AddEnvironmentVariables("QUEBRIX_")
    .AddCommandLine(args);

builder.Services.Configure<QuebrixServerOptions>(builder.Configuration.GetSection("Quebrix"));
builder.Services.Configure<QuebrixElasticsearchOptions>(builder.Configuration.GetSection("Quebrix:Elasticsearch"));

// Authentication & Authorization
builder.Services.AddSingleton<IApiKeyValidator, DefaultApiKeyValidator>();
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = "QuebrixAuth";
    options.DefaultChallengeScheme = "QuebrixAuth";
})
.AddScheme<QuebrixAuthenticationOptions, QuebrixAuthenticationHandler>("QuebrixAuth", null);

builder.Services.AddAuthorization(options =>
{
    QuebrixPolicies.ConfigurePolicies(options);
});

// Rate Limiting
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("IngestionPolicy", config =>
    {
        config.PermitLimit = 1000;
        config.Window = TimeSpan.FromSeconds(1);
        config.QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst;
        config.QueueLimit = 100;
    });
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

// CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Elasticsearch (Elastic.Clients.Elasticsearch for ingestion pipeline)
builder.Services.AddSingleton(sp =>
{
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<QuebrixElasticsearchOptions>>().Value;
    var uri =  new Uri("http://elasticsearch:9200");
    var settings = new ElasticsearchClientSettings(uri)
        .DefaultMappingFor<LogEvent>(m => m.IndexName("quebrix-logs"))
        .EnableDebugMode()
        .ServerCertificateValidationCallback((_, _, _, _) => true);

    if (!string.IsNullOrEmpty(options.Username) && !string.IsNullOrEmpty(options.Password))
    {
        settings = settings.Authentication(new Elastic.Transport.BasicAuthentication(options.Username, options.Password));
    }

    return new ElasticsearchClient(settings);

});

// NEST (for UI query layer)
builder.Services.AddSingleton<IElasticClient>(sp =>
{
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<QuebrixElasticsearchOptions>>().Value;
    var uri =  new Uri("http://elasticsearch:9200");

    var settings = new ConnectionSettings(uri)
        .DefaultMappingFor<QUEBRIX.Logger.Contracts.LogEvent>(m => m.IndexName("quebrix-logs"))
        .EnableDebugMode()
        .ServerCertificateValidationCallback((_, _, _, _) => true);

    if (!string.IsNullOrEmpty(options.Username) && !string.IsNullOrEmpty(options.Password))
    {
        settings = settings.BasicAuthentication(options.Username, options.Password);
    }

    return new ElasticClient(settings);
});

// Storage
builder.Services.AddSingleton<ILogStorage, ElasticsearchLogStorage>();
builder.Services.AddSingleton<ElasticsearchIndexManager>();

// Processing Pipeline
builder.Services.AddSingleton<LogEventPipeline>();
builder.Services.AddSingleton<IEnumerable<ILogEventEnricher>>(sp =>
{
    var serverOptions = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<QuebrixServerOptions>>();
    var enrichers = new List<ILogEventEnricher>
    {
        new DefaultEnricher(serverOptions)
    };
    return enrichers;
});
builder.Services.AddSingleton<IEnumerable<ILogEventFilter>>([]);
builder.Services.AddSingleton<IEnumerable<ILogEventProcessor>>([]);

// Ingestion
builder.Services.AddSingleton<LogIngestor>();

// Controllers
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
    });

// OpenTelemetry
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService("QUEBRIX.Logger.Server", serviceVersion: "1.0.0"))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddRuntimeInstrumentation()
        .AddMeter("QUEBRIX.Logger"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation());

// Health Checks
builder.Services.AddHealthChecks()
    .AddCheck<ElasticsearchHealthCheck>("elasticsearch", Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy, new[] { "storage" });

// Forwarded Headers
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto;
});

var app = builder.Build();

// Middleware pipeline
app.UseForwardedHeaders();
app.UseCors();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var result = JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                duration = e.Value.Duration.TotalMilliseconds
            })
        });
        await context.Response.WriteAsync(result);
    }
});

// Startup logging
var logger = app.Services.GetRequiredService<ILogger<Program>>();
var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";
logger.LogInformation("QUEBRIX Logger Server v{Version} starting on {Platform}", version, Environment.OSVersion);
logger.LogInformation("Environment: {Env}", app.Environment.EnvironmentName);

// Initialize Elasticsearch index on startup
var indexManager = app.Services.GetRequiredService<ElasticsearchIndexManager>();
await indexManager.EnsureIndexAsync();

app.Run();