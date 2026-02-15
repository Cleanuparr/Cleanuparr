using Cleanuparr.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cleanuparr.Api.Middleware;

public class SetupGuardMiddleware
{
    private readonly RequestDelegate _next;
    private volatile bool _setupCompleted;

    public SetupGuardMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Fast path: setup already completed
        if (_setupCompleted)
        {
            await _next(context);
            return;
        }

        var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";

        // Always allow these paths regardless of setup state
        if (IsAllowedPath(path))
        {
            await _next(context);
            return;
        }

        // Check database for setup completion
        await using var usersContext = UsersContext.CreateStaticInstance();
        var user = await usersContext.Users.AsNoTracking().FirstOrDefaultAsync();

        if (user is { SetupCompleted: true })
        {
            _setupCompleted = true;
            await _next(context);
            return;
        }

        // Setup not complete - block non-auth requests
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new { error = "Setup required" });
    }

    /// <summary>
    /// Resets the cached setup state. Call this if the user database is reset.
    /// </summary>
    public void ResetSetupState()
    {
        _setupCompleted = false;
    }

    private static bool IsAllowedPath(string path)
    {
        return path.StartsWith("/api/auth/")
               || path == "/api/auth"
               || path.StartsWith("/health")
               || !path.StartsWith("/api/");
    }
}
