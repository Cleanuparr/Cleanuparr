using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace Cleanuparr.Infrastructure.Http.DynamicHttpClientSystem;

/// <summary>
/// Sends Basic auth up front for qBittorrent, Deluge and SABnzbd behind a reverse proxy.
/// Skips requests that already carry an Authorization header.
/// </summary>
public sealed class BasicAuthHttpMessageHandler : DelegatingHandler
{
    private readonly string _headerValue;

    public BasicAuthHttpMessageHandler(NetworkCredential credential)
    {
        string raw = $"{credential.UserName}:{credential.Password}";
        _headerValue = Convert.ToBase64String(Encoding.UTF8.GetBytes(raw));
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Authorization ??= new AuthenticationHeaderValue("Basic", _headerValue);

        return base.SendAsync(request, cancellationToken);
    }
}
