using System.Diagnostics;
using System.Text;
using Serilog;

namespace QUEBRIX.Logger.SampleCustomerApi.Middleware;

/// <summary>
/// Middleware that logs all incoming HTTP requests and outgoing responses
/// to QUEBRIX Logger with structured properties.
/// </summary>
public class RequestResponseLoggingMiddleware : IMiddleware
{
    private readonly ILogger<RequestResponseLoggingMiddleware> _logger;

    public RequestResponseLoggingMiddleware(ILogger<RequestResponseLoggingMiddleware> logger)
    {
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var sw = Stopwatch.StartNew();
        var request = context.Request;

        // Read request body for logging (if applicable)
        string requestBody = string.Empty;
        if (request.ContentLength > 0 && request.ContentType?.Contains("json") == true)
        {
            request.EnableBuffering();
            using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
            requestBody = await reader.ReadToEndAsync();
            request.Body.Position = 0;
        }

        // Capture response body
        var originalBodyStream = context.Response.Body;
        using var responseBodyStream = new MemoryStream();
        context.Response.Body = responseBodyStream;

        try
        {
            await next(context);
        }
        finally
        {
            sw.Stop();
            context.Response.Body.Seek(0, SeekOrigin.Begin);
            var responseBody = await new StreamReader(context.Response.Body).ReadToEndAsync();
            context.Response.Body.Seek(0, SeekOrigin.Begin);
            await responseBodyStream.CopyToAsync(originalBodyStream);

            // Structured logging to QUEBRIX
            Log.ForContext("RequestMethod", request.Method)
               .ForContext("RequestPath", request.Path)
               .ForContext("RequestQueryString", request.QueryString.ToString())
               .ForContext("StatusCode", context.Response.StatusCode)
               .ForContext("ElapsedMs", sw.Elapsed.TotalMilliseconds)
               .ForContext("UserAgent", request.Headers.UserAgent.ToString())
               .ForContext("ClientIp", context.Connection.RemoteIpAddress?.ToString())
               .ForContext("RequestId", context.TraceIdentifier)
               .ForContext("RequestBody", requestBody)
               .ForContext("ResponseBody", responseBody)
               .Information("HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {ElapsedMs:F1}ms");
        }
    }
}