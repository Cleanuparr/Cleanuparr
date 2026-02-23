using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Cleanuparr.Infrastructure.Extensions;
using Cleanuparr.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Cleanuparr.Api.Auth;

public static class TrustedNetworkAuthenticationDefaults
{
    public const string AuthenticationScheme = "TrustedNetwork";
}

public class TrustedNetworkAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public TrustedNetworkAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Load auth config from database
        await using var dataContext = DataContext.CreateStaticInstance();
        var config = await dataContext.GeneralConfigs.AsNoTracking().FirstOrDefaultAsync();

        if (config is null || !config.Auth.DisableAuthForLocalAddresses)
        {
            return AuthenticateResult.NoResult();
        }

        // Determine client IP
        var clientIp = GetClientIp(config.Auth.TrustForwardedHeaders);
        if (clientIp is null)
        {
            return AuthenticateResult.NoResult();
        }

        // Check if the client IP is trusted
        if (!IsTrustedAddress(clientIp, config.Auth.TrustedNetworks))
        {
            return AuthenticateResult.NoResult();
        }

        // Load the admin user
        await using var usersContext = UsersContext.CreateStaticInstance();
        var user = await usersContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.SetupCompleted);

        if (user is null)
        {
            return AuthenticateResult.NoResult();
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim("auth_method", "trusted_network")
        };

        var identity = new ClaimsIdentity(claims, TrustedNetworkAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, TrustedNetworkAuthenticationDefaults.AuthenticationScheme);

        return AuthenticateResult.Success(ticket);
    }

    private IPAddress? GetClientIp(bool trustForwardedHeaders) =>
        ResolveClientIp(Context, trustForwardedHeaders);

    public static IPAddress? ResolveClientIp(HttpContext httpContext, bool trustForwardedHeaders)
    {
        var remoteIp = httpContext.Connection.RemoteIpAddress;
        if (remoteIp is null)
        {
            return null;
        }

        // Only trust forwarded headers if the direct connection is from a local address
        if (trustForwardedHeaders && remoteIp.IsLocalAddress())
        {
            // Check X-Forwarded-For first, then X-Real-IP
            var forwardedFor = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrEmpty(forwardedFor))
            {
                // X-Forwarded-For can contain multiple IPs: client, proxy1, proxy2
                // The first one is the original client
                var firstIp = forwardedFor.Split(',')[0].Trim();
                if (IPAddress.TryParse(firstIp, out var parsedIp))
                {
                    return parsedIp;
                }
            }

            var realIp = httpContext.Request.Headers["X-Real-IP"].FirstOrDefault();
            if (!string.IsNullOrEmpty(realIp) && IPAddress.TryParse(realIp, out var realParsedIp))
            {
                return realParsedIp;
            }
        }

        return remoteIp;
    }

    public static bool IsTrustedAddress(IPAddress clientIp, List<string> trustedNetworks)
    {
        // Normalize IPv4-mapped IPv6 addresses
        if (clientIp.IsIPv4MappedToIPv6)
        {
            clientIp = clientIp.MapToIPv4();
        }

        // Check if it's a local address (built-in ranges)
        if (clientIp.IsLocalAddress())
        {
            return true;
        }

        // Check against custom trusted networks
        foreach (var network in trustedNetworks)
        {
            if (MatchesCidr(clientIp, network))
            {
                return true;
            }
        }

        return false;
    }

    public static bool MatchesCidr(IPAddress address, string cidr)
    {
        if (cidr.Contains('/'))
        {
            var parts = cidr.Split('/');
            if (!IPAddress.TryParse(parts[0], out var networkAddress) ||
                !int.TryParse(parts[1], out var prefixLength))
            {
                return false;
            }

            // Normalize both addresses
            if (networkAddress.IsIPv4MappedToIPv6)
                networkAddress = networkAddress.MapToIPv4();
            if (address.IsIPv4MappedToIPv6)
                address = address.MapToIPv4();

            // Must be same address family
            if (address.AddressFamily != networkAddress.AddressFamily)
                return false;

            var addressBytes = address.GetAddressBytes();
            var networkBytes = networkAddress.GetAddressBytes();

            // Compare bytes up to prefix length
            var fullBytes = prefixLength / 8;
            var remainingBits = prefixLength % 8;

            for (var i = 0; i < fullBytes && i < addressBytes.Length; i++)
            {
                if (addressBytes[i] != networkBytes[i])
                    return false;
            }

            if (remainingBits > 0 && fullBytes < addressBytes.Length)
            {
                var mask = (byte)(0xFF << (8 - remainingBits));
                if ((addressBytes[fullBytes] & mask) != (networkBytes[fullBytes] & mask))
                    return false;
            }

            return true;
        }

        // Plain IP match
        if (!IPAddress.TryParse(cidr, out var singleIp))
            return false;

        if (singleIp.IsIPv4MappedToIPv6)
            singleIp = singleIp.MapToIPv4();

        return address.Equals(singleIp);
    }
}
