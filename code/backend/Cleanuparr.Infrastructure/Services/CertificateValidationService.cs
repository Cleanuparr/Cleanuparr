using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Extensions;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Services;

public class CertificateValidationService
{
    private readonly ILogger<CertificateValidationService> _logger;

    public CertificateValidationService(ILogger<CertificateValidationService> logger)
    {
        _logger = logger;
    }

    public bool ShouldByPassValidationError(
        CertificateValidationType certificateValidationType,
        object sender,
        X509Certificate? certificate,
        X509Chain? chain,
        SslPolicyErrors sslPolicyErrors
    )
    {
        string targetHostName = sender switch
        {
            HttpRequestMessage request => request.RequestUri?.Host ?? string.Empty,
            SslStream stream => stream.TargetHostName,
            _ => string.Empty
        };

        if (certificate is X509Certificate2 cert2 && cert2.SignatureAlgorithm.FriendlyName == "md5RSA")
        {
            _logger.LogError(
                "https://{TargetHostName} uses the obsolete md5 hash in its https certificate, if that is your certificate, please (re)create certificate with better algorithm as soon as possible.",
                targetHostName);
        }

        if (sslPolicyErrors == SslPolicyErrors.None)
        {
            return true;
        }

        if (targetHostName is "localhost" or "127.0.0.1")
        {
            return true;
        }

        if (certificateValidationType == CertificateValidationType.Disabled)
        {
            return true;
        }

        if (certificateValidationType == CertificateValidationType.DisabledForLocalAddresses &&
            // All() is true for an empty list, so an unresolved host must not count as local
            GetIpAddresses(targetHostName) is { Length: > 0 } ipAddresses &&
            ipAddresses.All(i => i.IsLocalAddress()))
        {
            return true;
        }

        _logger.LogError("certificate validation for {TargetHostName} failed. {SslPolicyErrors}", targetHostName, sslPolicyErrors);

        return false;
    }

    private static IPAddress[] GetIpAddresses(string host)
    {
        if (IPAddress.TryParse(host, out IPAddress? ipAddress))
        {
            return [ipAddress];
        }

        if (string.IsNullOrEmpty(host))
        {
            return [];
        }

        try
        {
            return Dns.GetHostEntry(host).AddressList;
        }
        catch (SocketException)
        {
            return [];
        }
    }
}