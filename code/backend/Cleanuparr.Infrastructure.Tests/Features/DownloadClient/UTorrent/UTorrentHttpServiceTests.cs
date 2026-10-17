using System.Net;
using Cleanuparr.Domain.Entities.UTorrent.Request;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.DownloadClient.UTorrent;
using Cleanuparr.Infrastructure.Tests.TestHelpers;
using Cleanuparr.Persistence.Models.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.DownloadClient.UTorrent;

public sealed class UTorrentHttpServiceTests
{
    private static (UTorrentHttpService service, FakeHttpMessageHandler handler) CreateService(HttpAuthType httpAuthType)
    {
        FakeHttpMessageHandler handler = new();
        handler.SetupResponse((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{}")
        }));

        DownloadClientConfig config = new()
        {
            Name = "utorrent",
            TypeName = DownloadClientTypeName.uTorrent,
            Type = DownloadClientType.Torrent,
            Host = new Uri("http://localhost:8080"),
            HttpAuthType = httpAuthType,
            Username = "user",
            Password = "pass",
        };

        return (new UTorrentHttpService(new HttpClient(handler), config, NullLogger<UTorrentHttpService>.Instance), handler);
    }

    [Fact]
    public async Task SendRawRequestAsync_InBasicMode_SendsBasicHeader()
    {
        (UTorrentHttpService service, FakeHttpMessageHandler handler) = CreateService(HttpAuthType.Basic);

        await service.SendRawRequestAsync(new UTorrentRequest { Action = "list" }, "GUID=abc");

        handler.CapturedRequests[0].Headers.Authorization!.Scheme.ShouldBe("Basic");
    }

    [Fact]
    public async Task SendRawRequestAsync_InDigestMode_SendsNoManualHeader()
    {
        (UTorrentHttpService service, FakeHttpMessageHandler handler) = CreateService(HttpAuthType.Digest);

        await service.SendRawRequestAsync(new UTorrentRequest { Action = "list" }, "GUID=abc");

        handler.CapturedRequests[0].Headers.Authorization.ShouldBeNull();
    }

    [Fact]
    public async Task GetTokenAndCookieAsync_InBasicMode_SendsBasicHeader()
    {
        (UTorrentHttpService service, FakeHttpMessageHandler handler) = CreateService(HttpAuthType.Basic);
        handler.SetupResponse((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("<div id='token'>tok</div>"),
            Headers = { { "Set-Cookie", "GUID=abc; path=/" } },
        }));

        await service.GetTokenAndCookieAsync();

        handler.CapturedRequests[0].Headers.Authorization!.Scheme.ShouldBe("Basic");
    }

    [Fact]
    public async Task GetTokenAndCookieAsync_InDigestMode_SendsNoManualHeader()
    {
        (UTorrentHttpService service, FakeHttpMessageHandler handler) = CreateService(HttpAuthType.Digest);
        handler.SetupResponse((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("<div id='token'>tok</div>"),
            Headers = { { "Set-Cookie", "GUID=abc; path=/" } },
        }));

        await service.GetTokenAndCookieAsync();

        handler.CapturedRequests[0].Headers.Authorization.ShouldBeNull();
    }
}
