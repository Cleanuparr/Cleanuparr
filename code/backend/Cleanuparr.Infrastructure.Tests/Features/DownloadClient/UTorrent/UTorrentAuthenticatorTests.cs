using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.DownloadClient.UTorrent;
using Cleanuparr.Persistence.Models.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.DownloadClient.UTorrent;

public sealed class UTorrentAuthenticatorTests : IDisposable
{
    private static readonly TimeSpan TokenExpiryDuration = TimeSpan.FromMinutes(20);

    private readonly MemoryCache _cache = new(new MemoryCacheOptions());
    private readonly IUTorrentHttpService _httpService = Substitute.For<IUTorrentHttpService>();
    // far enough ahead that the wall clock would call every session valid
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2100, 1, 1, 12, 0, 0, TimeSpan.Zero));
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
            NullLogger<UTorrentAuthenticator>.Instance,
            _timeProvider);
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
        session.CreatedAt.ShouldBe(_timeProvider.GetUtcNow());
        session.ExpiresAt.ShouldBe(session.CreatedAt.Add(TokenExpiryDuration));
    }

    [Fact]
    public async Task EnsureAuthenticatedAsync_ReusesAValidSession()
    {
        (await _authenticator.EnsureAuthenticatedAsync()).ShouldBeTrue();
        (await _authenticator.EnsureAuthenticatedAsync()).ShouldBeTrue();

        _authenticator.IsAuthenticated.ShouldBeTrue();
        await _httpService.Received(1).GetTokenAndCookieAsync();
    }

    [Fact]
    public async Task EnsureAuthenticatedAsync_PastTheExpiryWindow_ReauthenticatesOnTheInjectedClock()
    {
        (await _authenticator.EnsureAuthenticatedAsync()).ShouldBeTrue();

        _timeProvider.Advance(TokenExpiryDuration.Add(TimeSpan.FromMinutes(1)));

        _authenticator.IsAuthenticated.ShouldBeFalse();
        (await _authenticator.EnsureAuthenticatedAsync()).ShouldBeTrue();
        await _httpService.Received(2).GetTokenAndCookieAsync();
    }

    [Fact]
    public async Task GetValidTokenAsync_PastTheExpiryWindow_MintsAFreshSession()
    {
        await _authenticator.EnsureAuthenticatedAsync();
        _timeProvider.Advance(TokenExpiryDuration.Add(TimeSpan.FromMinutes(1)));

        (await _authenticator.GetValidTokenAsync()).ShouldBe("token");

        CachedSession().CreatedAt.ShouldBe(_timeProvider.GetUtcNow());
    }
}
