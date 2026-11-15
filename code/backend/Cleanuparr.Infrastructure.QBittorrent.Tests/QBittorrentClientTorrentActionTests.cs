using Cleanuparr.Infrastructure.QBittorrent.Tests.TestHelpers;
using QBittorrent.Client;
using Shouldly;

namespace Cleanuparr.Infrastructure.QBittorrent.Tests;

public class QBittorrentClientTorrentActionTests
{
    private const string FirstHash = "8c212779b4abde7c6bc608063a0d008b7e40ce32";
    private const string SecondHash = "e4be9e4db876e3e3179778b03e906297be5c8dbe";

    [Theory]
    [InlineData(true, "deleteFiles=true")]
    [InlineData(false, "deleteFiles=false")]
    public async Task DeleteAsync_PostsHashesAndDeleteFlag(bool deleteFiles, string expectedFlag)
    {
        using FakeQBittorrentServer server = new();

        await server.Client.DeleteAsync([FirstHash, SecondHash], deleteFiles);

        RecordedRequest request = server.SingleApiRequest;
        request.Method.ShouldBe(HttpMethod.Post);
        request.Uri.AbsolutePath.ShouldBe("/api/v2/torrents/delete");
        request.Body.ShouldBe($"hashes={FirstHash}%7C{SecondHash}&{expectedFlag}");
    }

    [Fact]
    public async Task PauseAsync_ApiAtLeast2_11_UsesStop()
    {
        using FakeQBittorrentServer server = new("2.11.4");

        await server.Client.PauseAsync([FirstHash]);

        RecordedRequest request = server.SingleApiRequest;
        request.Method.ShouldBe(HttpMethod.Post);
        request.Uri.AbsolutePath.ShouldBe("/api/v2/torrents/stop");
        request.Body.ShouldBe($"hashes={FirstHash}");
    }

    [Fact]
    public async Task PauseAsync_ApiBefore2_11_UsesPause()
    {
        using FakeQBittorrentServer server = new("2.8.3");

        await server.Client.PauseAsync([FirstHash, SecondHash]);

        RecordedRequest request = server.SingleApiRequest;
        request.Method.ShouldBe(HttpMethod.Post);
        request.Uri.AbsolutePath.ShouldBe("/api/v2/torrents/pause");
        request.Body.ShouldBe($"hashes={FirstHash}%7C{SecondHash}");
    }

    [Fact]
    public async Task ResumeAsync_ApiAtLeast2_11_UsesStart()
    {
        using FakeQBittorrentServer server = new("2.11.4");

        await server.Client.ResumeAsync([FirstHash]);

        RecordedRequest request = server.SingleApiRequest;
        request.Method.ShouldBe(HttpMethod.Post);
        request.Uri.AbsolutePath.ShouldBe("/api/v2/torrents/start");
        request.Body.ShouldBe($"hashes={FirstHash}");
    }

    [Fact]
    public async Task ResumeAsync_ApiBefore2_11_UsesResume()
    {
        using FakeQBittorrentServer server = new("2.8.3");

        await server.Client.ResumeAsync([FirstHash]);

        RecordedRequest request = server.SingleApiRequest;
        request.Method.ShouldBe(HttpMethod.Post);
        request.Uri.AbsolutePath.ShouldBe("/api/v2/torrents/resume");
        request.Body.ShouldBe($"hashes={FirstHash}");
    }

    [Fact]
    public async Task AddTorrentTagAsync_PostsHashesAndTag()
    {
        using FakeQBittorrentServer server = new();

        await server.Client.AddTorrentTagAsync([FirstHash, SecondHash], "cleanuparr");

        RecordedRequest request = server.SingleApiRequest;
        request.Method.ShouldBe(HttpMethod.Post);
        request.Uri.AbsolutePath.ShouldBe("/api/v2/torrents/addTags");
        request.Body.ShouldBe($"hashes={FirstHash}%7C{SecondHash}&tags=cleanuparr");
    }

    [Fact]
    public async Task SetFilePriorityAsync_Skip_PostsPriorityZero()
    {
        using FakeQBittorrentServer server = new();

        await server.Client.SetFilePriorityAsync(FirstHash, 3, TorrentContentPriority.Skip);

        RecordedRequest request = server.SingleApiRequest;
        request.Method.ShouldBe(HttpMethod.Post);
        request.Uri.AbsolutePath.ShouldBe("/api/v2/torrents/filePrio");
        request.Body.ShouldBe($"hash={FirstHash}&id=3&priority=0");
    }

    [Fact]
    public async Task ServerError_ThrowsRequestExceptionWithStatus()
    {
        using FakeQBittorrentServer server = new();
        server.Route("api/v2/torrents/delete", "", System.Net.HttpStatusCode.Forbidden);

        QBittorrentClientRequestException exception = await Should.ThrowAsync<QBittorrentClientRequestException>(
            () => server.Client.DeleteAsync([FirstHash], true));

        exception.StatusCode.ShouldBe(System.Net.HttpStatusCode.Forbidden);
    }
}
