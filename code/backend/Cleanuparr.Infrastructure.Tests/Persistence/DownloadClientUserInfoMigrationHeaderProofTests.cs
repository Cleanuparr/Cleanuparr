using System.Net;
using System.Text;
using Cleanuparr.Domain.Entities.UTorrent.Request;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.DownloadClient.RTorrent;
using Cleanuparr.Infrastructure.Features.DownloadClient.UTorrent;
using Cleanuparr.Infrastructure.Http.DynamicHttpClientSystem;
using Cleanuparr.Infrastructure.Tests.TestHelpers;
using Cleanuparr.Persistence.Models.Configuration;
using Cleanuparr.Persistence.Startup;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Persistence;

/// <summary>
/// After migration, each client sends the Basic header its old userinfo encoded.
/// </summary>
public sealed class DownloadClientUserInfoMigrationHeaderProofTests
{
    private const string Username = "user";

    private const string Password = "pass";

    private static readonly string ExpectedHeader =
        Convert.ToBase64String(Encoding.UTF8.GetBytes($"{Username}:{Password}"));

    [Fact]
    public async Task RTorrent_AfterMigration_SendsTheSameBasicHeader()
    {
        DownloadClientConfig config = MigratedConfig(DownloadClientTypeName.rTorrent);

        FakeHttpMessageHandler handler = new();
        handler.SetupResponse((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""<?xml version="1.0"?><methodResponse><params><param><value><i4>0</i4></value></param></params></methodResponse>""")
        }));
        RTorrentClient client = new(config, new HttpClient(handler));

        await client.StopTorrentAsync("HASH123");

        handler.CapturedRequests[0].Headers.Authorization!.Scheme.ShouldBe("Basic");
        handler.CapturedRequests[0].Headers.Authorization!.Parameter.ShouldBe(ExpectedHeader);
    }

    [Fact]
    public async Task UTorrent_AfterMigration_SendsTheSameBasicHeader()
    {
        DownloadClientConfig config = MigratedConfig(DownloadClientTypeName.uTorrent);

        FakeHttpMessageHandler handler = new();
        handler.SetupResponse((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{}")
        }));
        UTorrentHttpService service = new(new HttpClient(handler), config, NullLogger<UTorrentHttpService>.Instance);

        await service.SendRawRequestAsync(new UTorrentRequest { Action = "list" }, "GUID=abc");

        handler.CapturedRequests[0].Headers.Authorization!.Scheme.ShouldBe("Basic");
        handler.CapturedRequests[0].Headers.Authorization!.Parameter.ShouldBe(ExpectedHeader);
    }

    [Theory]
    [InlineData(DownloadClientTypeName.qBittorrent)]
    [InlineData(DownloadClientTypeName.Deluge)]
    [InlineData(DownloadClientTypeName.Sabnzbd)]
    public async Task AppLoginClient_AfterMigration_PreemptiveHandlerSendsTheSameBasicHeader(DownloadClientTypeName typeName)
    {
        DownloadClientConfig config = MigratedConfig(typeName);

        NetworkCredential? credential = config.GetHandlerHttpAuthCredential();
        credential.ShouldNotBeNull();

        FakeHttpMessageHandler inner = new();
        BasicAuthHttpMessageHandler handler = new(credential) { InnerHandler = inner };
        using HttpClient client = new(handler);

        await client.GetAsync("http://localhost/");

        inner.CapturedRequests[0].Headers.Authorization!.Scheme.ShouldBe("Basic");
        inner.CapturedRequests[0].Headers.Authorization!.Parameter.ShouldBe(ExpectedHeader);
    }

    /// <summary>
    /// Builds the migrated config through the parser helpers, so no database is needed.
    /// </summary>
    private static DownloadClientConfig MigratedConfig(DownloadClientTypeName typeName)
    {
        Uri originalHost = new($"http://{Username}:{Password}@original-host/");
        UserInfoCredential credential = UserInfoMigration.ParseCredential(originalHost.UserInfo);
        Uri strippedHost = UserInfoMigration.StripUserInfo(originalHost);

        DownloadClientConfig config = new()
        {
            Name = "client",
            TypeName = typeName,
            Type = typeName.ClientType(),
            Host = strippedHost,
            HttpAuthType = HttpAuthType.Basic,
        };

        if (typeName.UsesDedicatedHttpAuthFields())
        {
            config.HttpAuthUsername = credential.Username;
            config.HttpAuthPassword = credential.Password;
        }
        else
        {
            config.Username = credential.Username;
            config.Password = credential.Password;
        }

        return config;
    }
}
