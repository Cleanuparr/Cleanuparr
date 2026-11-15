using System.Net;
using Cleanuparr.Infrastructure.QBittorrent.Tests.TestHelpers;
using QBittorrent.Client;
using Shouldly;

namespace Cleanuparr.Infrastructure.QBittorrent.Tests;

public class QBittorrentClientLoginTests
{
    [Fact]
    public async Task LoginAsync_PostsCredentialsAsForm()
    {
        using FakeQBittorrentServer server = new();
        server.Route("api/v2/auth/login", "Ok.");

        await server.Client.LoginAsync("admin", "adminadmin");

        RecordedRequest request = server.SingleApiRequest;
        request.Method.ShouldBe(HttpMethod.Post);
        request.Uri.AbsolutePath.ShouldBe("/api/v2/auth/login");
        request.Body.ShouldBe("username=admin&password=adminadmin");
    }

    [Fact]
    public async Task LoginAsync_EncodesSpecialCharacters()
    {
        using FakeQBittorrentServer server = new();
        server.Route("api/v2/auth/login", "Ok.");

        await server.Client.LoginAsync("user name", "p&ss=w+rd");

        server.SingleApiRequest.Body.ShouldBe("username=user%20name&password=p%26ss%3Dw%2Brd");
    }

    [Fact]
    public async Task LoginAsync_OkBody_Succeeds()
    {
        using FakeQBittorrentServer server = new();
        server.Route("api/v2/auth/login", "Ok.");

        await Should.NotThrowAsync(() => server.Client.LoginAsync("admin", "adminadmin"));
    }

    [Theory]
    [InlineData(HttpStatusCode.OK, "")]
    [InlineData(HttpStatusCode.NoContent, "")]
    public async Task LoginAsync_EmptyBody_Succeeds(HttpStatusCode status, string body)
    {
        using FakeQBittorrentServer server = new();
        server.Route("api/v2/auth/login", body, status);

        await Should.NotThrowAsync(() => server.Client.LoginAsync("admin", "adminadmin"));
    }

    [Fact]
    public async Task LoginAsync_FailsBodyWithStatus200_Throws()
    {
        using FakeQBittorrentServer server = new();
        server.Route("api/v2/auth/login", "Fails.");

        QBittorrentClientRequestException exception = await Should.ThrowAsync<QBittorrentClientRequestException>(
            () => server.Client.LoginAsync("admin", "wrong"));

        exception.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task LoginAsync_ErrorStatus_ThrowsRequestException(HttpStatusCode status)
    {
        using FakeQBittorrentServer server = new();
        server.Route("api/v2/auth/login", "", status);

        QBittorrentClientRequestException exception = await Should.ThrowAsync<QBittorrentClientRequestException>(
            () => server.Client.LoginAsync("admin", "wrong"));

        exception.StatusCode.ShouldBe(status);
    }
}
