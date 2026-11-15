using Cleanuparr.Infrastructure.QBittorrent.Tests.TestHelpers;
using QBittorrent.Client;
using Shouldly;

namespace Cleanuparr.Infrastructure.QBittorrent.Tests;

public class QBittorrentClientCategoryTests
{
    [Fact]
    public async Task GetCategoriesAsync_ParsesCategoriesByName()
    {
        using FakeQBittorrentServer server = new();
        server.Route("api/v2/torrents/categories", Fixtures.Read("categories.json"));

        IReadOnlyDictionary<string, Category> categories = await server.Client.GetCategoriesAsync();

        server.SingleApiRequest.Method.ShouldBe(HttpMethod.Get);
        server.SingleApiRequest.Uri.AbsolutePath.ShouldBe("/api/v2/torrents/categories");
        categories.Keys.ShouldBe(["movies", "tv"], ignoreOrder: true);
        categories["movies"].Name.ShouldBe("movies");
        categories["movies"].SavePath.ShouldBe("/downloads/movies");
        categories["tv"].Name.ShouldBe("tv");
        categories["tv"].SavePath.ShouldBe("");
    }

    [Fact]
    public async Task GetCategoriesAsync_EmptyObject_ReturnsEmptyDictionary()
    {
        using FakeQBittorrentServer server = new();
        server.Route("api/v2/torrents/categories", "{}");

        (await server.Client.GetCategoriesAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task AddCategoryAsync_PostsCategoryName()
    {
        using FakeQBittorrentServer server = new();

        await server.Client.AddCategoryAsync("cleanuparr-unlinked");

        RecordedRequest request = server.SingleApiRequest;
        request.Method.ShouldBe(HttpMethod.Post);
        request.Uri.AbsolutePath.ShouldBe("/api/v2/torrents/createCategory");
        request.Body.ShouldBe("category=cleanuparr-unlinked");
    }

    [Fact]
    public async Task SetTorrentCategoryAsync_PostsHashesAndCategory()
    {
        using FakeQBittorrentServer server = new();

        await server.Client.SetTorrentCategoryAsync(["8c212779b4abde7c6bc608063a0d008b7e40ce32", "e4be9e4db876e3e3179778b03e906297be5c8dbe"], "movies");

        RecordedRequest request = server.SingleApiRequest;
        request.Method.ShouldBe(HttpMethod.Post);
        request.Uri.AbsolutePath.ShouldBe("/api/v2/torrents/setCategory");
        request.Body.ShouldBe("hashes=8c212779b4abde7c6bc608063a0d008b7e40ce32%7Ce4be9e4db876e3e3179778b03e906297be5c8dbe&category=movies");
    }
}
