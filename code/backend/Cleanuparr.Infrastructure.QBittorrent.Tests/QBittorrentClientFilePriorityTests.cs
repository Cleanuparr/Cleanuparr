using Cleanuparr.Infrastructure.QBittorrent.Tests.TestHelpers;
using QBittorrent.Client;
using Shouldly;

namespace Cleanuparr.Infrastructure.QBittorrent.Tests;

public class QBittorrentClientFilePriorityTests
{
    private const string Hash = "8c212779b4abde7c6bc608063a0d008b7e40ce32";

    [Theory]
    [InlineData(TorrentContentPriority.Skip, "0")]
    [InlineData(TorrentContentPriority.Minimal, "1")]
    [InlineData(TorrentContentPriority.VeryHigh, "6")]
    [InlineData(TorrentContentPriority.Maximal, "7")]
    public async Task SetFilePriorityAsync_AcceptedPriority_PostsValue(TorrentContentPriority priority, string expected)
    {
        using FakeQBittorrentServer server = new();

        await server.Client.SetFilePriorityAsync(Hash, 2, priority);
        await server.Client.SetFilePriorityAsync(Hash, [2], priority);

        server.ApiRequests.Count.ShouldBe(2);
        server.ApiRequests.ShouldAllBe(request => request.Body.EndsWith("priority=" + expected));
    }

    [Theory]
    [InlineData(TorrentContentPriority.VeryLow)]
    [InlineData(TorrentContentPriority.Low)]
    [InlineData(TorrentContentPriority.Normal)]
    [InlineData(TorrentContentPriority.High)]
    [InlineData((TorrentContentPriority)8)]
    public async Task SetFilePriorityAsync_RejectedPriority_Throws(TorrentContentPriority priority)
    {
        using FakeQBittorrentServer server = new();

        await Should.ThrowAsync<ArgumentOutOfRangeException>(() => server.Client.SetFilePriorityAsync(Hash, 2, priority));
        await Should.ThrowAsync<ArgumentOutOfRangeException>(() => server.Client.SetFilePriorityAsync(Hash, [2], priority));

        server.ApiRequests.ShouldBeEmpty();
    }
}
