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
}
