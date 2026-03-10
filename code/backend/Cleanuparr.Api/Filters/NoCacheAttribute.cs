using Microsoft.AspNetCore.Mvc.Filters;

namespace Cleanuparr.Api.Filters;

/// <summary>
/// Prevents caching of sensitive responses by setting appropriate HTTP headers.
/// Applies Cache-Control: no-cache, no-store, Pragma: no-cache, and a past Expires date
/// for maximum compatibility with HTTP/1.0 and HTTP/1.1 clients and intermediaries.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class NoCacheAttribute : ActionFilterAttribute
{
    public static void Apply(IHeaderDictionary headers)
    {
        headers.CacheControl = "no-cache, no-store";
        headers.Pragma = "no-cache";
        headers.Expires = "Thu, 01 Jan 1970 00:00:00 GMT";
    }

    public override void OnResultExecuting(ResultExecutingContext context)
    {
        Apply(context.HttpContext.Response.Headers);
        base.OnResultExecuting(context);
    }
}
