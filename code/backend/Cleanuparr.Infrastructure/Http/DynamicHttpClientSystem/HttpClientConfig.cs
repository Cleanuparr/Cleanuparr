using System.Net;
using Cleanuparr.Domain.Enums;

namespace Cleanuparr.Infrastructure.Http.DynamicHttpClientSystem;

/// <summary>
/// Configuration for a dynamic HTTP client
/// </summary>
public class HttpClientConfig
{
    public string Name { get; set; } = string.Empty;
    public int Timeout { get; set; }
    public HttpClientType Type { get; set; }
    public RetryConfig? RetryConfig { get; set; }

    // Deluge-specific settings
    public bool AllowAutoRedirect { get; set; } = true;
    public DecompressionMethods AutomaticDecompression { get; set; } = DecompressionMethods.GZip | DecompressionMethods.Deflate;

    public CertificateValidationType CertificateValidationType { get; set; } = CertificateValidationType.Enabled;

    /// <summary>
    /// Whether requests from this client carry a User-Agent header.
    /// </summary>
    public bool SendUserAgent { get; set; }

    /// <summary>
    /// HTTP authentication scheme to answer a reverse proxy's challenge with.
    /// </summary>
    public HttpAuthType HttpAuthType { get; set; } = HttpAuthType.Basic;

    /// <summary>
    /// Credential to send as HTTP auth, or null when this client has none configured.
    /// </summary>
    public NetworkCredential? HttpAuthCredential { get; set; }

    /// <summary>
    /// Scheme, host and port of the client's URL, used as the Digest <see cref="CredentialCache"/> key.
    /// </summary>
    public Uri? BaseUri { get; set; }
}
