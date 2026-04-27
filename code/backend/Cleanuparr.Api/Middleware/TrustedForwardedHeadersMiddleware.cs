using System.Net;
using Cleanuparr.Api.Auth;
using Cleanuparr.Infrastructure.Extensions;
using Cleanuparr.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cleanuparr.Api.Middleware;

/// <summary>
/// Mirrors ASP.NET Core's <see cref="Microsoft.AspNetCore.HttpOverrides.ForwardedHeadersMiddleware"/> but reads the trusted-network set from
/// <c>AuthConfig</c> dynamically per request, so admins can toggle <c>TrustForwardedHeaders</c> / <c>TrustedNetworks</c> without restarting the app.
/// Walks <c>X-Forwarded-For</c> right-to-left, popping entries contributed by trusted hops, and stops at the first untrusted entry
/// which becomes the new <see cref="HttpContext.Connection"/>.<see cref="ConnectionInfo.RemoteIpAddress"/>.
/// </summary>
public class TrustedForwardedHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public TrustedForwardedHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var trustedNetworks = await LoadTrustedNetworksAsync();
        if (trustedNetworks is not null)
        {
            ApplyForwardedHeaders(context, trustedNetworks);
        }

        await _next(context);
    }

    private static async Task<List<string>?> LoadTrustedNetworksAsync()
    {
        await using var dataContext = DataContext.CreateStaticInstance();
        var config = await dataContext.GeneralConfigs.AsNoTracking().FirstOrDefaultAsync();
        if (config is null || !config.Auth.TrustForwardedHeaders)
        {
            return null;
        }

        return config.Auth.TrustedNetworks;
    }

    public static void ApplyForwardedHeaders(HttpContext context, List<string> trustedNetworks)
    {
        var peer = context.Connection.RemoteIpAddress;
        if (peer is null || !IsTrustedHop(peer, trustedNetworks))
        {
            return;
        }

        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (string.IsNullOrEmpty(forwardedFor))
        {
            return;
        }

        var entries = forwardedFor.Split(
            ',',
            StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (entries.Length == 0)
        {
            return;
        }

        // Walk right-to-left: each iteration consumes the rightmost remaining XFF entry and treats it as the new peer. 
        // Stop on the first entry that did not come from a trusted hop.
        var consumedAtLeastOne = false;
        for (var i = entries.Length - 1; i >= 0; i--)
        {
            if (!IPAddress.TryParse(entries[i], out var entryIp))
            {
                // Malformed entry
                return;
            }

            context.Connection.RemoteIpAddress = entryIp;
            consumedAtLeastOne = true;

            if (!IsTrustedHop(entryIp, trustedNetworks))
            {
                break;
            }
        }

        if (!consumedAtLeastOne)
        {
            return;
        }

        var forwardedProto = context.Request.Headers["X-Forwarded-Proto"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedProto))
        {
            context.Request.Scheme = forwardedProto;
        }

        var forwardedHost = context.Request.Headers["X-Forwarded-Host"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedHost))
        {
            context.Request.Host = new HostString(forwardedHost);
        }
    }

    private static bool IsTrustedHop(IPAddress address, List<string> trustedNetworks)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (address.IsLocalAddress())
        {
            return true;
        }

        foreach (var cidr in trustedNetworks)
        {
            if (TrustedNetworkAuthenticationHandler.MatchesCidr(address, cidr))
            {
                return true;
            }
        }

        return false;
    }
}
