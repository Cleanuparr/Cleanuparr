using Cleanuparr.Domain.Entities.UTorrent.Request;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Domain.Exceptions;
using Cleanuparr.Infrastructure.Features.DownloadClient.UTorrent;
using Cleanuparr.Persistence.Models.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.DownloadClient.UTorrent;

public class UTorrentClientTests
{
    private readonly IUTorrentAuthenticator _authenticator = Substitute.For<IUTorrentAuthenticator>();
    private readonly IUTorrentHttpService _httpService = Substitute.For<IUTorrentHttpService>();
    private readonly UTorrentClient _client;

    public UTorrentClientTests()
    {
        _authenticator.GetValidTokenAsync().Returns("csrf-token");
        _authenticator.GetValidGuidCookieAsync().Returns("guid-cookie");

        DownloadClientConfig config = new()
        {
            Name = "utorrent",
            TypeName = DownloadClientTypeName.uTorrent,
            Type = DownloadClientType.Torrent,
            Host = new Uri("http://localhost:8080"),
        };

        _client = new UTorrentClient(
            config,
            _authenticator,
            _httpService,
            Substitute.For<IUTorrentResponseParser>(),
            Substitute.For<ILogger<UTorrentClient>>());
    }

    [Fact]
    public async Task StopTorrentsAsync_SendsOneStopRequestPerHash()
    {
        _httpService.SendRawRequestAsync(Arg.Any<UTorrentRequest>(), "guid-cookie").Returns("{}");

        await _client.StopTorrentsAsync(["HASH1", "HASH2"]);

        await _httpService.Received(1).SendRawRequestAsync(
            Arg.Is<UTorrentRequest>(r =>
                r.Action == "action=stop" && r.Token == "csrf-token" && HasHash(r, "HASH1")),
            "guid-cookie");
        await _httpService.Received(1).SendRawRequestAsync(
            Arg.Is<UTorrentRequest>(r =>
                r.Action == "action=stop" && HasHash(r, "HASH2")),
            "guid-cookie");
    }

    [Fact]
    public async Task StopTorrentsAsync_WhenTheClientFails_ThrowsUTorrentException()
    {
        _httpService.SendRawRequestAsync(Arg.Any<UTorrentRequest>(), Arg.Any<string>())
            .ThrowsAsync(new HttpRequestException("connection refused"));

        UTorrentException ex = await Should.ThrowAsync<UTorrentException>(
            () => _client.StopTorrentsAsync(["HASH1"]));

        ex.Message.ShouldContain("Failed to stop torrents");
    }

    private static bool HasHash(UTorrentRequest request, string hash) =>
        request.Parameters.Any(p => p.Name == "hash" && p.Value == hash);
}
