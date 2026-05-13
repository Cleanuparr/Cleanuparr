using Cleanuparr.Infrastructure.Features.DownloadClient.UTorrent;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.DownloadClient.UTorrent;

public class UTorrentAuthCacheTests
{
    [Fact]
    public void IsValid_AllFieldsSetAndExpiresInFuture_ReturnsTrue()
    {
        // Arrange
        var cache = new UTorrentAuthCache
        {
            AuthToken = "token",
            GuidCookie = "guid",
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(5),
        };

        // Act / Assert
        cache.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void IsValid_Expired_ReturnsFalse()
    {
        // Arrange
        var cache = new UTorrentAuthCache
        {
            AuthToken = "token",
            GuidCookie = "guid",
            CreatedAt = DateTime.UtcNow.AddMinutes(-10),
            ExpiresAt = DateTime.UtcNow.AddMinutes(-1),
        };

        // Act / Assert
        cache.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void IsValid_MissingAuthToken_ReturnsFalse()
    {
        // Arrange
        var cache = new UTorrentAuthCache
        {
            AuthToken = string.Empty,
            GuidCookie = "guid",
            ExpiresAt = DateTime.UtcNow.AddMinutes(5),
        };

        // Act / Assert
        cache.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void IsValid_MissingGuidCookie_ReturnsFalse()
    {
        // Arrange
        var cache = new UTorrentAuthCache
        {
            AuthToken = "token",
            GuidCookie = string.Empty,
            ExpiresAt = DateTime.UtcNow.AddMinutes(5),
        };

        // Act / Assert
        cache.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void IsValid_DefaultInstance_ReturnsFalse()
    {
        // Arrange — defaults: empty token + cookie, ExpiresAt = MinValue
        var cache = new UTorrentAuthCache();

        // Act / Assert
        cache.IsValid.ShouldBeFalse();
    }
}
