using System.Net;

namespace Cleanuparr.Infrastructure.Transmission.Tests.TestHelpers;

/// <summary>
/// Test double for HttpMessageHandler since NSubstitute cannot mock protected methods.
/// Records requests with their bodies and answers through a configurable handler.
/// </summary>
public class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly object _lock = new();

    private Func<HttpRequestMessage, HttpResponseMessage> _handler
        = _ => new HttpResponseMessage(HttpStatusCode.OK);

    public List<RecordedRequest> Requests { get; } = [];

    public void SetupResponse(Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        _handler = handler;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string? body = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken);

        lock (_lock)
        {
            Requests.Add(new RecordedRequest(
                request.Method,
                request.RequestUri!,
                body,
                request.Headers.TryGetValues("X-Transmission-Session-Id", out IEnumerable<string>? sessionId) ? sessionId.First() : null,
                request.Headers.Authorization?.ToString()));
        }

        return _handler(request);
    }
}
