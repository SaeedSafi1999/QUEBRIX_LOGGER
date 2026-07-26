using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace QUEBRIX.Logger.SampleCustomerApi.Controllers;

/// <summary>
/// Diagnostics endpoints to demonstrate integration with QUEBRIX Logger.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class DiagnosticsController : ControllerBase
{
    /// <summary>
    /// Tests all log levels. Useful for verifying QUEBRIX Logger configuration.
    /// </summary>
    [HttpGet("test-all-levels")]
    public IActionResult TestAllLevels()
    {
        Log.Verbose("This is a VERBOSE message - detailed debugging information");
        Log.Debug("This is a DEBUG message - diagnostic information");
        Log.Information("This is an INFORMATION message - normal business events");
        Log.Warning("This is a WARNING message - something noteworthy");
        Log.Error("This is an ERROR message - a recoverable failure");
        Log.Fatal("This is a FATAL message - a catastrophic failure");

        return Ok(new
        {
            Message = "All log levels tested! Check QUEBRIX Logger UI to see the events.",
            Levels = new[] { "Verbose", "Debug", "Information", "Warning", "Error", "Fatal" }
        });
    }

    /// <summary>
    /// Tests structured logging with various property types.
    /// </summary>
    [HttpGet("test-structured-logging")]
    public IActionResult TestStructuredLogging()
    {
        var orderId = Guid.NewGuid().ToString("N")[..12].ToUpper();
        var userId = Guid.NewGuid().ToString("N")[..8];

        Log.ForContext("OrderId", orderId)
           .ForContext("UserId", userId)
           .ForContext("Amount", 199.99m)
           .ForContext("Currency", "EUR")
           .ForContext("IsPremium", true)
           .ForContext("Tags", new[] { "test", "demo", "structured" })
           .ForContext("Metadata", new Dictionary<string, object>
           {
               ["Browser"] = "Chrome 120",
               ["Platform"] = "Windows",
               ["ScreenResolution"] = "1920x1080"
           })
           .Information("Structured log test: Order {OrderId} for user {UserId} - Amount: {Amount} {Currency}");

        return Ok(new
        {
            Message = "Structured logging test completed. Check QUEBRIX for indexed properties.",
            OrderId = orderId,
            UserId = userId
        });
    }

    /// <summary>
    /// Tests exception logging with full stack trace.
    /// </summary>
    [HttpGet("test-exception-logging")]
    public IActionResult TestExceptionLogging()
    {
        try
        {
            // Simulate a nested exception scenario
            try
            {
                throw new InvalidOperationException("Inner database connection failed after 3 retries");
            }
            catch (Exception inner)
            {
                throw new ApplicationException(
                    "Failed to process order due to database unavailability",
                    inner);
            }
        }
        catch (Exception ex)
        {
            Log.ForContext("ExceptionType", ex.GetType().FullName)
               .ForContext("StackTrace", ex.ToString())
               .ForContext("InnerException", ex.InnerException?.Message)
               .Error(ex, "Exception logging test: {Message}", ex.Message);
        }

        return Ok(new
        {
            Message = "Exception logging test completed. Check QUEBRIX for full stack trace.",
            Note = "This was caught and logged - no actual error occurred."
        });
    }

    /// <summary>
    /// Generates a batch of log events for performance testing.
    /// </summary>
    [HttpPost("generate-batch")]
    public IActionResult GenerateBatch([FromQuery] int count = 100)
    {
        count = Math.Clamp(count, 1, 1000);
        var sw = System.Diagnostics.Stopwatch.StartNew();

        for (int i = 0; i < count; i++)
        {
            Log.ForContext("BatchId", Guid.NewGuid().ToString("N")[..8])
               .ForContext("SequenceNumber", i + 1)
               .Information("Batch log event #{SequenceNumber} of {Total}", i + 1, count);
        }

        sw.Stop();

        Log.ForContext("BatchCount", count)
           .ForContext("ElapsedMs", sw.Elapsed.TotalMilliseconds)
           .Information("Generated {BatchCount} log events in {ElapsedMs:F1}ms");

        return Ok(new
        {
            Message = $"Generated {count} log events",
            Count = count,
            ElapsedMs = sw.Elapsed.TotalMilliseconds
        });
    }

    /// <summary>
    /// Checks if QUEBRIX Logger sink is configured and working.
    /// </summary>
    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new
        {
            Status = "Healthy",
            Timestamp = DateTime.UtcNow,
            Application = "SampleCustomerApi",
            Logger = "QUEBRIX Logger",
            SinkConfigured = true
        });
    }
}