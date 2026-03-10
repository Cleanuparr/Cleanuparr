using Microsoft.AspNetCore.Mvc.Filters;

namespace Cleanuparr.Api.Filters;

/// <summary>
/// Prevents caching of sensitive responses by setting appropriate HTTP headers.
/// Applies Cache-Control: no-cache, no-store, Pragma: no-cache, and Expires: -1
/// for maximum compatibility with HTTP/1.0 and HTTP/1.1 clients and intermediaries.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class NoCacheAttribute : ActionFilterAttribute
{
    public override void OnResultExecuting(ResultExecutingContext context)
    {
        var headers = context.HttpContext.Response.Headers;
        headers.CacheControl = "no-cache, no-store";
        headers.Pragma = "no-cache";
        headers.Expires = "-1";
        base.OnResultExecuting(context);
    }
}
