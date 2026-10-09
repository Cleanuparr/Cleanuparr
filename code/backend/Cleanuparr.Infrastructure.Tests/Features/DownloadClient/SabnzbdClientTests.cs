using System.Net;
using Cleanuparr.Domain.Entities.Sabnzbd;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Domain.Exceptions;
using Cleanuparr.Infrastructure.Features.DownloadClient.Sabnzbd;
using Cleanuparr.Infrastructure.Tests.TestHelpers;
using Cleanuparr.Persistence.Models.Configuration;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.DownloadClient;

public class SabnzbdClientTests
{
    private static (SabnzbdClient client, FakeHttpMessageHandler handler) CreateClient()
    {
        FakeHttpMessageHandler handler = new();
        DownloadClientConfig config = new()
        {
            Name = "sabnzbd",
            TypeName = DownloadClientTypeName.Sabnzbd,
            Type = DownloadClientType.Usenet,
            Host = new Uri("http://localhost:8080"),
            ApiKey = "test-api-key",
        };

        return (new SabnzbdClient(config, new HttpClient(handler)), handler);
    }

    [Fact]
    public async Task ValidateApiKeyAsync_AcceptedKey_RequestsQueueMode()
    {
        (SabnzbdClient client, FakeHttpMessageHandler handler) = CreateClient();
        handler.SetupResponse((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"queue":{"slots":[]}}""")
        }));

        await client.ValidateApiKeyAsync();

        handler.CapturedRequests[0].RequestUri!.Query.ShouldContain("mode=queue");
    }

    [Fact]
    public async Task ValidateApiKeyAsync_RejectedKey_ThrowsWithSabnzbdsErrorText()
    {
        (SabnzbdClient client, FakeHttpMessageHandler handler) = CreateClient();
        handler.SetupResponse((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden)
        {
            Content = new StringContent("API Key Incorrect")
        }));

        SabnzbdClientException exception = await Should.ThrowAsync<SabnzbdClientException>(() => client.ValidateApiKeyAsync());

        exception.Message.ShouldContain("API Key Incorrect");
    }

    [Fact]
    public async Task GetQueueAsync_StatusFalseWithHttp200_ThrowsWithSabnzbdsErrorText()
    {
        (SabnzbdClient client, FakeHttpMessageHandler handler) = CreateClient();
        handler.SetupResponse((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"status":false,"error":"boom"}""")
        }));

        SabnzbdClientException exception = await Should.ThrowAsync<SabnzbdClientException>(() => client.GetQueueAsync());

        exception.Message.ShouldContain("boom");
    }

    [Fact]
    public async Task GetHistoryAsync_StatusFalseWithHttp200_ThrowsWithSabnzbdsErrorText()
    {
        (SabnzbdClient client, FakeHttpMessageHandler handler) = CreateClient();
        handler.SetupResponse((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"status":false,"error":"boom"}""")
        }));

        SabnzbdClientException exception = await Should.ThrowAsync<SabnzbdClientException>(() => client.GetHistoryAsync());

        exception.Message.ShouldContain("boom");
    }

    [Fact]
    public async Task GetQueueAsync_NormalResponse_ParsesQueue()
    {
        (SabnzbdClient client, FakeHttpMessageHandler handler) = CreateClient();
        handler.SetupResponse((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"queue":{"slots":[]}}""")
        }));

        SabnzbdQueueData? queue = await client.GetQueueAsync();

        queue.ShouldNotBeNull();
    }

    [Fact]
    public async Task DeleteFromQueueAsync_StatusFalse_ThrowsWithSabnzbdsErrorText()
    {
        (SabnzbdClient client, FakeHttpMessageHandler handler) = CreateClient();
        handler.SetupResponse((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"status":false,"error":"boom"}""")
        }));

        SabnzbdClientException exception = await Should.ThrowAsync<SabnzbdClientException>(() => client.DeleteFromQueueAsync("SABnzbd_nzo_1", false));

        exception.Message.ShouldContain("boom");
    }

    [Fact]
    public async Task DeleteFromQueueAsync_StatusTrue_DoesNotThrow()
    {
        (SabnzbdClient client, FakeHttpMessageHandler handler) = CreateClient();
        handler.SetupResponse((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"status":true}""")
        }));

        await Should.NotThrowAsync(() => client.DeleteFromQueueAsync("SABnzbd_nzo_1", false));
    }

    [Fact]
    public async Task GetQueueAsync_RequestTimesOut_ThrowsWithModeContext()
    {
        (SabnzbdClient client, FakeHttpMessageHandler handler) = CreateClient();
        handler.SetupThrow(new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout"));

        SabnzbdClientException exception = await Should.ThrowAsync<SabnzbdClientException>(() => client.GetQueueAsync());

        exception.Message.ShouldContain("SABnzbd request failed for mode 'queue'");
    }
}
