using Cleanuparr.Domain.Entities.UTorrent.Request;
using Cleanuparr.Infrastructure.Features.DownloadClient.UTorrent;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.DownloadClient.UTorrent;

public class UTorrentRequestFactoryTests
{
    [Fact]
    public void CreateStopTorrentRequest_UsesStopActionWithHash()
    {
        UTorrentRequest request = UTorrentRequestFactory.CreateStopTorrentRequest("HASH123");

        request.Action.ShouldBe("action=stop");
        request.Parameters.ShouldHaveSingleItem();
        request.Parameters[0].ShouldBe(("hash", "HASH123"));
    }

    [Fact]
    public void CreateStopTorrentRequest_QueryString_CarriesTokenActionAndHash()
    {
        UTorrentRequest request = UTorrentRequestFactory.CreateStopTorrentRequest("HASH123");
        request.Token = "csrf-token";

        request.ToQueryString().ShouldBe("token=csrf-token&action=stop&hash=HASH123");
    }
}
