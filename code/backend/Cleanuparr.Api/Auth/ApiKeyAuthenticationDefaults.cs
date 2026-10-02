namespace Cleanuparr.Api.Auth;

/// <summary>
/// Names used by the API key authentication scheme.
/// </summary>
public static class ApiKeyAuthenticationDefaults
{
    /// <summary>
    /// Scheme name registered for API key authentication.
    /// </summary>
    public const string AuthenticationScheme = "ApiKey";

    /// <summary>
    /// Request header that carries the API key.
    /// </summary>
    public const string HeaderName = "X-Api-Key";

    /// <summary>
    /// Query string parameter that carries the API key.
    /// </summary>
    public const string QueryParameterName = "apikey";
}
