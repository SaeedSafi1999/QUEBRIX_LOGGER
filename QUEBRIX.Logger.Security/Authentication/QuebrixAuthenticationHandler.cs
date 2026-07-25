using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QUEBRIX.Logger.Common;

namespace QUEBRIX.Logger.Security.Authentication;

/// <summary>
/// Authentication handler for QUEBRIX API key and JWT authentication.
/// </summary>
public sealed class QuebrixAuthenticationHandler : AuthenticationHandler<QuebrixAuthenticationOptions>
{
    private readonly IApiKeyValidator _apiKeyValidator;

    /// <summary>
    /// Initializes a new instance of <see cref="QuebrixAuthenticationHandler"/>.
    /// </summary>
    public QuebrixAuthenticationHandler(
        IOptionsMonitor<QuebrixAuthenticationOptions> options,
        ILoggerFactory loggerFactory,
        UrlEncoder encoder,
        ISystemClock clock,
        IApiKeyValidator apiKeyValidator)
        : base(options, loggerFactory, encoder, clock)
    {
        _apiKeyValidator = apiKeyValidator ?? throw new ArgumentNullException(nameof(apiKeyValidator));
    }

    /// <summary>
    /// Handles authentication by first attempting API key validation,
    /// then falling through to JWT bearer middleware.
    /// </summary>
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Try API key authentication first
        if (Request.Headers.TryGetValue(QuebrixConstants.ApiKeyHeaderName, out var apiKeyValues))
        {
            var apiKey = apiKeyValues.FirstOrDefault();
            if (!string.IsNullOrEmpty(apiKey) && await _apiKeyValidator.ValidateApiKeyAsync(apiKey))
            {
                var claims = new[]
                {
                    new Claim(ClaimTypes.Name, "api-key-user"),
                    new Claim(ClaimTypes.AuthenticationMethod, "ApiKey"),
                    new Claim("QuebrixAuthType", "ApiKey")
                };

                var identity = new ClaimsIdentity(claims, Scheme.Name);
                var principal = new ClaimsPrincipal(identity);
                var ticket = new AuthenticationTicket(principal, Scheme.Name);

                return AuthenticateResult.Success(ticket);
            }

            return AuthenticateResult.Fail("Invalid API key.");
        }

        // Fall through to allow JWT handling via the default JWT bearer middleware
        return AuthenticateResult.NoResult();
    }
}

/// <summary>
/// Options for QUEBRIX authentication.
/// </summary>
public sealed class QuebrixAuthenticationOptions : AuthenticationSchemeOptions
{
    /// <summary>
    /// The authentication scheme name.
    /// </summary>
    public const string SchemeName = "QuebrixAuth";
}

/// <summary>
/// Validates API keys.
/// </summary>
public interface IApiKeyValidator
{
    /// <summary>
    /// Validates an API key asynchronously.
    /// </summary>
    /// <param name="apiKey">The API key to validate.</param>
    /// <returns>True if the API key is valid, otherwise false.</returns>
    ValueTask<bool> ValidateApiKeyAsync(string apiKey);
}

/// <summary>
/// Default API key validator that validates against configured keys.
/// </summary>
public sealed class DefaultApiKeyValidator : IApiKeyValidator
{
    private readonly HashSet<string> _validKeys;

    /// <summary>
    /// Initializes a new instance of <see cref="DefaultApiKeyValidator"/>.
    /// </summary>
    /// <param name="configuration">Application configuration.</param>
    public DefaultApiKeyValidator(IConfiguration configuration)
    {
        var keys = configuration.GetSection("Quebrix:ApiKeys").Get<string[]>();
        _validKeys = keys?.ToHashSet(StringComparer.OrdinalIgnoreCase) ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Validates the API key against the configured set of valid keys.
    /// </summary>
    public ValueTask<bool> ValidateApiKeyAsync(string apiKey)
    {
        return ValueTask.FromResult(_validKeys.Contains(apiKey));
    }
}