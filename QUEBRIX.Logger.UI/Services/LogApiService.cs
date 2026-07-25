using System.Net.Http.Json;
using System.Text.Json;
using QUEBRIX.Logger.UI.Models;

namespace QUEBRIX.Logger.UI.Services;

/// <summary>
/// Service for communicating with the QUEBRIX Logger backend API.
/// </summary>
public class LogApiService
{
    private readonly HttpClient _http;
    private readonly ILogger<LogApiService> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public LogApiService(HttpClient http, ILogger<LogApiService> logger)
    {
        _http = http;
        _logger = logger;
    }

    /// <summary>
    /// Searches logs with the given request parameters.
    /// </summary>
    public async Task<LogSearchResponse> SearchAsync(LogSearchRequest request, CancellationToken ct = default)
    {
        try
        {
            var response = await _http.PostAsJsonAsync("api/logs/search", request, JsonOptions, ct);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<LogSearchResponse>(JsonOptions, ct);
            return result ?? new LogSearchResponse { Error = "Empty response" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching logs");
            return new LogSearchResponse
            {
                Error = ex.Message,
                TotalCount = 0,
                Events = new List<LogEvent>()
            };
        }
    }

    /// <summary>
    /// Gets a single log event by ID.
    /// </summary>
    public async Task<LogEvent?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        try
        {
            return await _http.GetFromJsonAsync<LogEvent>($"api/logs/{id}", JsonOptions, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching log event {Id}", id);
            return null;
        }
    }

    /// <summary>
    /// Gets suggestions for a given field (autocomplete).
    /// </summary>
    public async Task<List<string>> GetSuggestionsAsync(string field, string? query = null, CancellationToken ct = default)
    {
        try
        {
            var queryParam = string.IsNullOrWhiteSpace(query) ? "" : $"?query={Uri.EscapeDataString(query)}";
            var result = await _http.GetFromJsonAsync<SuggestionsResponse>($"api/logs/suggestions/{field}{queryParam}", JsonOptions, ct);
            return result?.Values ?? new List<string>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting suggestions for {Field}", field);
            return new List<string>();
        }
    }

    private class SuggestionsResponse
    {
        public List<string> Values { get; set; } = new();
    }
}