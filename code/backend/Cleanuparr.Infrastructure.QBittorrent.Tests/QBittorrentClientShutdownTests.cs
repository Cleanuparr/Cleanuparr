using Cleanuparr.Infrastructure.QBittorrent.Tests.TestHelpers;
using Shouldly;

namespace Cleanuparr.Infrastructure.QBittorrent.Tests;

public class QBittorrentClientShutdownTests
{
    [Fact]
    public async Task ShutdownApplicationAsync_PostsEmptyForm()
    {
        using FakeQBittorrentServer server = new();

        await server.Client.ShutdownApplicationAsync();

        RecordedRequest request = server.SingleApiRequest;
        request.Method.ShouldBe(HttpMethod.Post);
        request.Uri.AbsolutePath.ShouldBe("/api/v2/app/shutdown");
        request.Body.ShouldBe("");
    }
}
