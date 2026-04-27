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
    /// Returns the external base URL (scheme + host + basePath).
    /// TrustedForwardedHeadersMiddleware has already applied X-Forwarded-Proto and X-Forwarded-Host to <see cref="HttpRequest.Scheme"/> / <see cref="HttpRequest.Host"/>.
    /// </summary>
    public static string GetExternalBaseUrl(this HttpContext context)
    {
        var request = context.Request;
        var basePath = request.GetSafeBasePath();
        return $"{request.Scheme}://{request.Host}{basePath}";
    }
}
