using Cleanuparr.Infrastructure.QBittorrent.Tests.TestHelpers;
using QBittorrent.Client;
using Shouldly;

namespace Cleanuparr.Infrastructure.QBittorrent.Tests;

public class QBittorrentClientRecheckTests
{
    private const string Hash = "8c212779b4abde7c6bc608063a0d008b7e40ce32";

    [Fact]
    public async Task RecheckAsync_LegacyServerSingleHash_UsesLegacyEndpoint()
    {
        using FakeQBittorrentServer server = new();
        server.Route("version/api", "10");

        await server.Client.RecheckAsync([Hash]);

        RecordedRequest request = server.SingleApiRequest;
        request.Uri.AbsolutePath.ShouldBe("/command/recheck");
        request.Body.ShouldBe($"hash={Hash}");
    }

    [Fact]
    public async Task RecheckAsync_LegacyServerSeveralHashes_ThrowsNotSupported()
    {
        using FakeQBittorrentServer server = new();
        server.Route("version/api", "10");

        await Should.ThrowAsync<ApiNotSupportedException>(() => server.Client.RecheckAsync([Hash, "e4be9e4db876e3e3179778b03e906297be5c8dbe"]));
    }
}
