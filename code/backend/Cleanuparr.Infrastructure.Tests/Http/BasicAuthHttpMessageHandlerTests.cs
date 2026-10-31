using System.Net;
using System.Net.Http.Headers;
using Cleanuparr.Infrastructure.Http.DynamicHttpClientSystem;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Http;

public sealed class BasicAuthHttpMessageHandlerTests
{
    private sealed class CapturingHandler : DelegatingHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }

    private static (HttpClient client, CapturingHandler inner) CreateClient(NetworkCredential credential)
    {
        CapturingHandler inner = new();
        BasicAuthHttpMessageHandler basicAuth = new(credential) { InnerHandler = inner };
        return (new HttpClient(basicAuth), inner);
    }

    [Fact]
    public async Task SendAsync_AddsPreemptiveBasicHeader()
    {
        (HttpClient client, CapturingHandler inner) = CreateClient(new NetworkCredential("user", "pass"));

        await client.GetAsync("http://localhost/");

        inner.LastRequest!.Headers.Authorization!.Scheme.ShouldBe("Basic");
        inner.LastRequest!.Headers.Authorization!.Parameter.ShouldBe(Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("user:pass")));
    }

    [Fact]
    public async Task SendAsync_DoesNotOverwriteAnExistingAuthorizationHeader()
    {
        (HttpClient client, CapturingHandler inner) = CreateClient(new NetworkCredential("user", "pass"));

        using HttpRequestMessage request = new(HttpMethod.Get, "http://localhost/");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "existing-token");

        await client.SendAsync(request);

        inner.LastRequest!.Headers.Authorization!.Scheme.ShouldBe("Bearer");
        inner.LastRequest!.Headers.Authorization!.Parameter.ShouldBe("existing-token");
    }
}
