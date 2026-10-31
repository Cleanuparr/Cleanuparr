using System.Net;
using System.Net.Sockets;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Http.DynamicHttpClientSystem;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Http;

/// <summary>
/// Answers a real Digest challenge with the handler from <see cref="DynamicHttpClientConfiguration.ApplyHttpAuth"/>.
/// </summary>
public sealed class DigestAuthRoundTripTests
{
    private static int GetFreePort()
    {
        using TcpListener probe = new(IPAddress.Loopback, 0);
        probe.Start();
        int port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    [Fact]
    public async Task DigestChallenge_IsAnsweredOnRetry()
    {
        int port = GetFreePort();
        string prefix = $"http://127.0.0.1:{port}/";

        using HttpListener listener = new();
        listener.Prefixes.Add(prefix);
        listener.Start();

        Task<string?> serverTask = AnswerDigestChallengeAsync(listener);

        using HttpClientHandler handler = new();
        List<DelegatingHandler> additionalHandlers = [];
        HttpClientConfig config = new()
        {
            Type = HttpClientType.WithRetry,
            HttpAuthType = HttpAuthType.Digest,
            HttpAuthCredential = new NetworkCredential("user", "pass"),
            BaseUri = new Uri(prefix),
        };
        DynamicHttpClientConfiguration.ApplyHttpAuth(additionalHandlers, handler, config);

        using HttpClient client = new(handler);

        try
        {
            HttpResponseMessage response = await client.GetAsync(prefix);

            string? capturedAuthorizationHeader = await serverTask.WaitAsync(TimeSpan.FromSeconds(5));

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            capturedAuthorizationHeader.ShouldNotBeNull();
            capturedAuthorizationHeader.ShouldStartWith("Digest ");
            capturedAuthorizationHeader.ShouldContain("username=\"user\"");
        }
        finally
        {
            listener.Stop();
        }
    }

    private static async Task<string?> AnswerDigestChallengeAsync(HttpListener listener)
    {
        HttpListenerContext challenge = await listener.GetContextAsync();
        challenge.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
        challenge.Response.Headers.Add("WWW-Authenticate", "Digest realm=\"test\", nonce=\"abc\", qop=\"auth\"");
        challenge.Response.Close();

        HttpListenerContext authenticated = await listener.GetContextAsync();
        string? authorizationHeader = authenticated.Request.Headers["Authorization"];
        authenticated.Response.StatusCode = (int)HttpStatusCode.OK;
        authenticated.Response.Close();
        return authorizationHeader;
    }
}
