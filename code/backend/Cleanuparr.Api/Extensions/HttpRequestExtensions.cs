using Cleanuparr.Infrastructure.Extensions;

namespace Cleanuparr.Api.Extensions;

public static class HttpRequestExtensions
{
    /// <summary>
    /// Returns the request PathBase as a safe relative path.
    /// Rejects absolute URLs (e.g. "://" or "//") to prevent open redirect attacks.
    /// </summary>
    public static string GetSafeBasePath(this HttpRequest request)
    {
        var basePath = request.PathBase.Value?.TrimEnd('/') ?? "";
        if (basePath.Contains("://") || basePath.StartsWith("//"))
        {
            return "";
        }
        return basePath;
    }

    /// <summary>
    /// Returns the external base URL (scheme + host + basePath), respecting
    /// X-Forwarded-Proto and X-Forwarded-Host headers when the connection
    /// originates from a local address.
    /// </summary>
    public static string GetExternalBaseUrl(this HttpContext context)
    {
        var request = context.Request;
        var scheme = request.Scheme;
        var host = request.Host.ToString();
        var remoteIp = context.Connection.RemoteIpAddress;

        // Trust forwarded headers only from local connections
        // (consistent with TrustedNetworkAuthenticationHandler)
        if (remoteIp is not null && remoteIp.IsLocalAddress())
        {
            scheme = request.Headers["X-Forwarded-Proto"].FirstOrDefault() ?? scheme;
            host = request.Headers["X-Forwarded-Host"].FirstOrDefault() ?? host;
        }

        var basePath = request.GetSafeBasePath();
        return $"{scheme}://{host}{basePath}";
    }
}
