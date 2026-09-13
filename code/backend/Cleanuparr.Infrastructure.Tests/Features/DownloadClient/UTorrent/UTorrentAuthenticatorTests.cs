using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.DownloadClient.UTorrent;
using Cleanuparr.Persistence.Models.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.DownloadClient.UTorrent;

public sealed class UTorrentAuthenticatorTests : IDisposable
{
    private static readonly TimeSpan TokenExpiryDuration = TimeSpan.FromMinutes(20);

    private readonly MemoryCache _cache = new(new MemoryCacheOptions());
    private readonly IUTorrentHttpService _httpService = Substitute.For<IUTorrentHttpService>();
    private readonly UTorrentAuthenticator _authenticator;

    public UTorrentAuthenticatorTests()
    {
        DownloadClientConfig config = new()
        {
            Id = Guid.NewGuid(),
            Enabled = true,
            Name = "utorrent",
            TypeName = DownloadClientTypeName.uTorrent,
            Type = DownloadClientType.Torrent,
            Host = new Uri("http://localhost:8080"),
        };

        _httpService.GetTokenAndCookieAsync().Returns(("token", "guid"));

        _authenticator = new UTorrentAuthenticator(
            _cache,
            _httpService,
            config,
            NullLogger<UTorrentAuthenticator>.Instance);
    }

    public void Dispose()
    {
        _cache.Dispose();
    }

    private UTorrentAuthCache CachedSession() =>
        _cache.Keys
            .Select(key => _cache.Get(key))
            .OfType<UTorrentAuthCache>()
            .Single();

    [Fact]
    public async Task RefreshSessionAsync_StampsTheSessionWindow()
    {
        await _authenticator.RefreshSessionAsync();

        UTorrentAuthCache session = CachedSession();
        session.AuthToken.ShouldBe("token");
        session.GuidCookie.ShouldBe("guid");
        session.CreatedAt.ShouldBe(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
        session.ExpiresAt.ShouldBe(session.CreatedAt.Add(TokenExpiryDuration), TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task EnsureAuthenticatedAsync_ReusesAValidSession()
    {
        (await _authenticator.EnsureAuthenticatedAsync()).ShouldBeTrue();
        (await _authenticator.EnsureAuthenticatedAsync()).ShouldBeTrue();

        _authenticator.IsAuthenticated.ShouldBeTrue();
        await _httpService.Received(1).GetTokenAndCookieAsync();
    }
}
