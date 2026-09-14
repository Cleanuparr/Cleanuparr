using Cleanuparr.Infrastructure.Features.DownloadClient.UTorrent;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.DownloadClient.UTorrent;

public class UTorrentAuthCacheTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void IsValid_AllFieldsSetAndExpiresInFuture_ReturnsTrue()
    {
        // Arrange
        UTorrentAuthCache cache = new()
        {
            AuthToken = "token",
            GuidCookie = "guid",
            CreatedAt = Now,
            ExpiresAt = Now.AddMinutes(5),
        };

        // Act / Assert
        cache.IsValid(Now).ShouldBeTrue();
    }

    [Fact]
    public void IsValid_Expired_ReturnsFalse()
    {
        // Arrange
        UTorrentAuthCache cache = new()
        {
            AuthToken = "token",
            GuidCookie = "guid",
            CreatedAt = Now.AddMinutes(-10),
            ExpiresAt = Now.AddMinutes(-1),
        };

        // Act / Assert
        cache.IsValid(Now).ShouldBeFalse();
    }

    [Fact]
    public void IsValid_ExpiresExactlyNow_ReturnsFalse()
    {
        // Arrange
        UTorrentAuthCache cache = new()
        {
            AuthToken = "token",
            GuidCookie = "guid",
            CreatedAt = Now.AddMinutes(-20),
            ExpiresAt = Now,
        };

        // Act / Assert
        cache.IsValid(Now).ShouldBeFalse();
    }

    [Fact]
    public void IsValid_MissingAuthToken_ReturnsFalse()
    {
        // Arrange
        UTorrentAuthCache cache = new()
        {
            AuthToken = string.Empty,
            GuidCookie = "guid",
            ExpiresAt = Now.AddMinutes(5),
        };

        // Act / Assert
        cache.IsValid(Now).ShouldBeFalse();
    }

    [Fact]
    public void IsValid_MissingGuidCookie_ReturnsFalse()
    {
        // Arrange
        UTorrentAuthCache cache = new()
        {
            AuthToken = "token",
            GuidCookie = string.Empty,
            ExpiresAt = Now.AddMinutes(5),
        };

        // Act / Assert
        cache.IsValid(Now).ShouldBeFalse();
    }

    [Fact]
    public void IsValid_DefaultInstance_ReturnsFalse()
    {
        // Arrange — defaults: empty token + cookie, ExpiresAt = MinValue
        UTorrentAuthCache cache = new();

        // Act / Assert
        cache.IsValid(Now).ShouldBeFalse();
    }
}
