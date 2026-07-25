using Microsoft.AspNetCore.Authorization;

namespace QUEBRIX.Logger.Security.Authorization;

/// <summary>
/// Defines authorization policies for QUEBRIX Logger.
/// </summary>
public static class QuebrixPolicies
{
    /// <summary>
    /// Policy name for ingestion access.
    /// </summary>
    public const string IngestionPolicy = "QuebrixIngestion";

    /// <summary>
    /// Policy name for administration access.
    /// </summary>
    public const string AdminPolicy = "QuebrixAdmin";

    /// <summary>
    /// Policy name for read-only access.
    /// </summary>
    public const string ReadOnlyPolicy = "QuebrixReadOnly";

    /// <summary>
    /// Configures the authorization policies.
    /// </summary>
    public static void ConfigurePolicies(AuthorizationOptions options)
    {
        options.AddPolicy(IngestionPolicy, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.AddAuthenticationSchemes("QuebrixAuth", "Bearer");
        });

        options.AddPolicy(AdminPolicy, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.AddAuthenticationSchemes("QuebrixAuth", "Bearer");
            policy.RequireClaim("QuebrixAuthType", "ApiKey");
        });

        options.AddPolicy(ReadOnlyPolicy, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.AddAuthenticationSchemes("Bearer");
        });
    }
}