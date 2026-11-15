using System.Net;
using Cleanuparr.Infrastructure.QBittorrent.Tests.TestHelpers;
using QBittorrent.Client;
using Shouldly;

namespace Cleanuparr.Infrastructure.QBittorrent.Tests;

public class QBittorrentClientPeerTests
{
    private const string Hash = "8c212779b4abde7c6bc608063a0d008b7e40ce32";

    [Fact]
    public async Task AddTorrentPeersAsync_ResultKeysAreCaseInsensitive()
    {
        using FakeQBittorrentServer server = new();
        server.Route("api/v2/torrents/addPeers", $$$"""{"{{{Hash.ToUpperInvariant()}}}":{"added":1,"failed":0}}""");

        IReadOnlyDictionary<string, PeerAddResult> results = await server.Client.AddTorrentPeersAsync(
            [Hash], [new IPEndPoint(IPAddress.Parse("10.0.0.1"), 6881)]);

        results.TryGetValue(Hash, out PeerAddResult? result).ShouldBeTrue();
        result!.Added.ShouldBe(1);
    }
}
