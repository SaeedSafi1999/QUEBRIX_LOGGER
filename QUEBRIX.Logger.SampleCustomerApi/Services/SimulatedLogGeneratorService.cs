using Serilog;

namespace QUEBRIX.Logger.SampleCustomerApi.Services;

/// <summary>
/// Background service that generates simulated log events to demonstrate
/// QUEBRIX Logger's capabilities in real-time.
/// This simulates various business operations that would normally
/// come from different parts of the application.
/// </summary>
public class SimulatedLogGeneratorService : BackgroundService
{
    private readonly ILogger<SimulatedLogGeneratorService> _logger;
    private static readonly Random Rng = new();

    private static readonly string[] Actions = { "ProcessOrder", "ValidatePayment", "SendNotification", "UpdateInventory", "GenerateReport" };
    private static readonly string[] Modules = { "Billing", "Inventory", "Notifications", "Reporting", "UserManagement" };
    private static readonly string[] Statuses = { "Started", "Completed", "Failed", "Retrying" };

    public SimulatedLogGeneratorService(ILogger<SimulatedLogGeneratorService> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Log.Information("SimulatedLogGeneratorService started - generating demo log events every few seconds");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                GenerateRandomLogEvent();
                await Task.Delay(TimeSpan.FromSeconds(Rng.Next(2, 8)), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in SimulatedLogGeneratorService");
            }
        }

        Log.Information("SimulatedLogGeneratorService stopped");
    }

    private void GenerateRandomLogEvent()
    {
        var action = Actions[Rng.Next(Actions.Length)];
        var module = Modules[Rng.Next(Modules.Length)];
        var status = Statuses[Rng.Next(Statuses.Length)];
        var duration = Rng.Next(10, 5000);
        var correlationId = Guid.NewGuid().ToString("N")[..12];

        // Roll a dice to decide the log level
        var level = Rng.Next(100);

        var log = Log
            .ForContext("Action", action)
            .ForContext("Module", module)
            .ForContext("Status", status)
            .ForContext("DurationMs", duration)
            .ForContext("CorrelationId", correlationId);

        if (level < 50) // 50% Information - normal operations
        {
            log.Information("Operation {Action} in {Module} completed with status {Status} in {DurationMs}ms",
                action, module, status, duration);
        }
        else if (level < 75) // 25% Debug - diagnostic details
        {
            log.ForContext("MemoryUsageMB", Rng.Next(50, 500))
               .ForContext("ThreadCount", Rng.Next(5, 50))
               .Debug("Diagnostic: {Action} in {Module} - Memory: {MemoryUsageMB}MB, Threads: {ThreadCount}",
                   action, module);
        }
        else if (level < 90) // 15% Warning - notable events
        {
            log.ForContext("RetryCount", Rng.Next(1, 4))
               .Warning("Operation {Action} in {Module} requires attention - status: {Status}, retry: {RetryCount}",
                   action, module, status);
        }
        else if (level < 98) // 8% Error - failures
        {
            var errorCode = Rng.Next(3) switch
            {
                0 => "TIMEOUT",
                1 => "INVALID_DATA",
                _ => "SERVICE_UNAVAILABLE"
            };

            log.ForContext("ErrorCode", errorCode)
               .Error("Operation {Action} failed in {Module} with error {ErrorCode} (correlation: {CorrelationId})",
                   action, module, errorCode, correlationId);
        }
        else // 2% Fatal - critical
        {
            log.ForContext("Severity", "Critical")
               .ForContext("RequiresRestart", true)
               .Fatal("CRITICAL: {Module} subsystem failure during {Action} - manual intervention required!",
                   module, action);
        }
    }
}