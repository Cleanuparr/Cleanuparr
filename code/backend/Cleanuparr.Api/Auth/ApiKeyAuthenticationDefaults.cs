namespace Cleanuparr.Api.Auth;

public static class ApiKeyAuthenticationDefaults
{
    public const string AuthenticationScheme = "ApiKey";
    public const string HeaderName = "X-Api-Key";
    public const string QueryParameterName = "apikey";
}
