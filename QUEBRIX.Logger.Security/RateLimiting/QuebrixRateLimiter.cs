using System.Collections.Concurrent;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QUEBRIX.Logger.Common.Options;

namespace QUEBRIX.Logger.Security.RateLimiting;

/// <summary>
/// Sliding window rate limiter for ingestion endpoints.
/// </summary>
public sealed class QuebrixRateLimiter
{
    private readonly ConcurrentDictionary<string, SlidingWindow> _windows = new(StringComparer.OrdinalIgnoreCase);
    private readonly int _maxRequestsPerMinute;
    private readonly ILogger<QuebrixRateLimiter> _logger;

    public QuebrixRateLimiter(IOptions<QuebrixServerOptions> options, ILogger<QuebrixRateLimiter> logger)
    {
        _maxRequestsPerMinute = options.Value.RateLimitPerMinute;
        _logger = logger;
    }

    /// <summary>
    /// Checks if a request from the given client IP is allowed.
    /// </summary>
    public bool IsRequestAllowed(string clientIp)
    {
        var window = _windows.GetOrAdd(clientIp, _ => new SlidingWindow(_maxRequestsPerMinute));
        var allowed = window.TryAcquire();

        if (!allowed)
        {
            _logger.LogWarning("Rate limit exceeded for IP {ClientIp}", clientIp);
        }

        return allowed;
    }

    /// <summary>
    /// Middleware that enforces rate limiting.
    /// </summary>
    public static RequestDelegate CreateMiddleware(QuebrixRateLimiter limiter)
    {
        return async context =>
        {
            var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            if (!limiter.IsRequestAllowed(clientIp))
            {
                context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.Response.Headers["Retry-After"] = "60";
                await context.Response.WriteAsync("Rate limit exceeded. Try again later.");
                return;
            }

            // Continue to next middleware via reference to next delegate
        };
    }

    private sealed class SlidingWindow
    {
        private readonly int _maxRequests;
        private readonly ConcurrentQueue<DateTime> _timestamps = new();
        private readonly object _lock = new();

        public SlidingWindow(int maxRequests)
        {
            _maxRequests = maxRequests;
        }

        public bool TryAcquire()
        {
            var now = DateTime.UtcNow;

            lock (_lock)
            {
                // Remove timestamps older than 1 minute
                while (_timestamps.TryPeek(out var ts) && (now - ts).TotalMinutes >= 1)
                {
                    _timestamps.TryDequeue(out _);
                }

                if (_timestamps.Count >= _maxRequests)
                {
                    return false;
                }

                _timestamps.Enqueue(now);
                return true;
            }
        }
    }
}