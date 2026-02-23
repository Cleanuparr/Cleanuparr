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
        string path = context.Request.Path.Value?.ToLowerInvariant() ?? "";

        // Always allow health checks and non-API paths (static files, SPA, etc.)
        if (path.StartsWith("/health") || !path.StartsWith("/api/"))
        {
            await _next(context);
            return;
        }

        // Setup-only paths (/api/auth/setup/*) require setup to NOT be complete
        if (IsSetupOnlyPath(path))
        {
            if (await IsSetupCompleted())
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new { error = "Setup already completed" });
                return;
            }

            await _next(context);
            return;
        }

        // Non-setup auth paths (login, refresh, logout, status) are always allowed
        if (path.StartsWith("/api/auth/") || path == "/api/auth")
        {
            await _next(context);
            return;
        }

        // All other API paths require setup to be complete
        if (!await IsSetupCompleted())
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new { error = "Setup required" });
            return;
        }

        await _next(context);
    }

    public void ResetSetupState()
    {
        _setupCompleted = false;
    }

    private async Task<bool> IsSetupCompleted()
    {
        if (_setupCompleted)
        {
            return true;
        }

        await using var usersContext = UsersContext.CreateStaticInstance();
        var user = await usersContext.Users.AsNoTracking().FirstOrDefaultAsync();

        if (user is { SetupCompleted: true })
        {
            _setupCompleted = true;
            return true;
        }

        return false;
    }

    private static bool IsSetupOnlyPath(string path)
    {
        return path.StartsWith("/api/auth/setup/") || path == "/api/auth/setup";
    }
}
