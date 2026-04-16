using System.Net;

namespace Cleanuparr.Infrastructure.Tests.TestHelpers;

/// <summary>
/// Test double for HttpMessageHandler since NSubstitute cannot mock protected methods.
/// Captures requests and delegates to a configurable handler function.
/// </summary>
public class FakeHttpMessageHandler : HttpMessageHandler
{
    private Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler
        = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));

    public List<HttpRequestMessage> CapturedRequests { get; } = [];

    /// <summary>
    /// Eagerly captured request body strings (indexed the same as CapturedRequests).
    /// Useful because HttpClient disposes request content after SendAsync returns.
    /// </summary>
    public List<string?> CapturedRequestBodies { get; } = [];

    public void SetupResponse(HttpStatusCode statusCode)
    {
        _handler = (_, _) => Task.FromResult(new HttpResponseMessage(statusCode));
    }

    public void SetupResponse(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
    {
        _handler = handler;
    }

    public void SetupThrow(Exception exception)
    {
        _handler = (_, _) => throw exception;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        CapturedRequests.Add(request);
        CapturedRequestBodies.Add(request.Content != null
            ? await request.Content.ReadAsStringAsync(cancellationToken)
            : null);
        return await _handler(request, cancellationToken);
    }
}
