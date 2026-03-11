using System.ComponentModel.DataAnnotations.Schema;
using System.Net;
using Cleanuparr.Domain.Exceptions;

namespace Cleanuparr.Persistence.Models.Configuration.General;

[ComplexType]
public sealed record AuthConfig : IConfig
{
    public bool DisableAuthForLocalAddresses { get; set; }

    public bool TrustForwardedHeaders { get; set; }

    public List<string> TrustedNetworks { get; set; } = [];

    public void Validate()
    {
        foreach (var entry in TrustedNetworks)
        {
            if (!IsValidIpOrCidr(entry))
            {
                throw new ValidationException($"Invalid IP address or CIDR range: {entry}");
            }
        }
    }

    private static bool IsValidIpOrCidr(string value)
    {
        // CIDR notation: 192.168.1.0/24
        if (value.Contains('/'))
        {
            var parts = value.Split('/');
            if (parts.Length != 2) return false;

            if (!IPAddress.TryParse(parts[0], out _)) return false;
            if (!int.TryParse(parts[1], out var prefix)) return false;

            // IPv4: 0-32, IPv6: 0-128
            var maxPrefix = parts[0].Contains(':') ? 128 : 32;
            return prefix >= 0 && prefix <= maxPrefix;
        }

        // Plain IP address
        return IPAddress.TryParse(value, out _);
    }
}
