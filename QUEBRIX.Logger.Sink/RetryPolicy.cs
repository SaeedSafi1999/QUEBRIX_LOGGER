using Serilog.Debugging;
using QUEBRIX.Logger.Common.Options;

namespace QUEBRIX.Logger.Sink;

/// <summary>
/// Implements exponential backoff retry policy for HTTP operations.
/// </summary>
public sealed class RetryPolicy
{
    private readonly QuebrixSinkOptions _options;

    public RetryPolicy(QuebrixSinkOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Executes an async operation with retry and exponential backoff.
    /// </summary>
    public async Task<T?> ExecuteAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken = default)
    {
        var attempt = 0;
        var maxAttempts = _options.MaxRetries + 1;

        while (attempt < maxAttempts)
        {
            attempt++;
            try
            {
                return await operation();
            }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                SelfLog.WriteLine("QUEBRIX Sink: Retry policy attempt {0}/{1} failed: {2}",
                    attempt, maxAttempts, ex.Message);

                var delay = TimeSpan.FromSeconds(Math.Min(
                    Math.Pow(2, attempt) - 1,
                    _options.MaxBackoffSeconds));

                await Task.Delay(delay, cancellationToken);
            }
        }

        return default;
    }

    /// <summary>
    /// Calculates the delay for a given retry attempt using exponential backoff.
    /// </summary>
    public TimeSpan GetDelay(int attempt)
    {
        var seconds = Math.Min(
            Math.Pow(2, attempt) - 1,
            _options.MaxBackoffSeconds);
        return TimeSpan.FromSeconds(seconds);
    }
}