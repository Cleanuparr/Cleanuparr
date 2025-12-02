using Cleanuparr.Domain.Entities.UTorrent.Response;
using Cleanuparr.Infrastructure.Features.DownloadClient.UTorrent;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.DownloadClient;

public class UTorrentItemWrapperTests
{
    [Fact]
    public void Constructor_WithNullTorrentItem_ThrowsArgumentNullException()
    {
        // Arrange
        var torrentProperties = new UTorrentProperties();

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new UTorrentItemWrapper(null!, torrentProperties));
    }

    [Fact]
    public void Constructor_WithNullTorrentProperties_ThrowsArgumentNullException()
    {
        // Arrange
        var torrentItem = new UTorrentItem();

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new UTorrentItemWrapper(torrentItem, null!));
    }

    [Fact]
    public void Hash_ReturnsCorrectValue()
    {
        // Arrange
        var expectedHash = "test-hash-123";
        var torrentItem = new UTorrentItem { Hash = expectedHash };
        var torrentProperties = new UTorrentProperties();
        var wrapper = new UTorrentItemWrapper(torrentItem, torrentProperties);

        // Act
        var result = wrapper.Hash;

        // Assert
        result.ShouldBe(expectedHash);
    }

    [Fact]
    public void Name_ReturnsCorrectValue()
    {
        // Arrange
        var expectedName = "Test Torrent";
        var torrentItem = new UTorrentItem { Name = expectedName };
        var torrentProperties = new UTorrentProperties();
        var wrapper = new UTorrentItemWrapper(torrentItem, torrentProperties);

        // Act
        var result = wrapper.Name;

        // Assert
        result.ShouldBe(expectedName);
    }

    [Fact]
    public void IsPrivate_ReturnsCorrectValue()
    {
        // Arrange
        var torrentItem = new UTorrentItem();
        var torrentProperties = new UTorrentProperties { Pex = -1 }; // -1 means private torrent
        var wrapper = new UTorrentItemWrapper(torrentItem, torrentProperties);

        // Act
        var result = wrapper.IsPrivate;

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void Size_ReturnsCorrectValue()
    {
        // Arrange
        var expectedSize = 1024L * 1024 * 1024; // 1GB
        var torrentItem = new UTorrentItem { Size = expectedSize };
        var torrentProperties = new UTorrentProperties();
        var wrapper = new UTorrentItemWrapper(torrentItem, torrentProperties);

        // Act
        var result = wrapper.Size;

        // Assert
        result.ShouldBe(expectedSize);
    }

    [Theory]
    [InlineData(0, 0.0)]      // 0 permille = 0%
    [InlineData(500, 50.0)]   // 500 permille = 50%
    [InlineData(750, 75.0)]   // 750 permille = 75%
    [InlineData(1000, 100.0)] // 1000 permille = 100%
    public void CompletionPercentage_ReturnsCorrectValue(int progress, double expectedPercentage)
    {
        // Arrange
        var torrentItem = new UTorrentItem { Progress = progress };
        var torrentProperties = new UTorrentProperties();
        var wrapper = new UTorrentItemWrapper(torrentItem, torrentProperties);

        // Act
        var result = wrapper.CompletionPercentage;

        // Assert
        result.ShouldBe(expectedPercentage);
    }

    [Theory]
    [InlineData(1024L * 1024 * 100, 1024L * 1024 * 100)] // 100MB
    [InlineData(0L, 0L)]
    public void DownloadedBytes_ReturnsCorrectValue(long downloaded, long expected)
    {
        // Arrange
        var torrentItem = new UTorrentItem { Downloaded = downloaded };
        var torrentProperties = new UTorrentProperties();
        var wrapper = new UTorrentItemWrapper(torrentItem, torrentProperties);

        // Act
        var result = wrapper.DownloadedBytes;

        // Assert
        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData(2000, 2.0)] // 2000 permille = 2.0 ratio
    [InlineData(500, 0.5)]  // 500 permille = 0.5 ratio
    [InlineData(1000, 1.0)] // 1000 permille = 1.0 ratio
    [InlineData(0, 0.0)]    // No ratio
    public void Ratio_ReturnsCorrectValue(int ratioRaw, double expected)
    {
        // Arrange
        var torrentItem = new UTorrentItem { RatioRaw = ratioRaw };
        var torrentProperties = new UTorrentProperties();
        var wrapper = new UTorrentItemWrapper(torrentItem, torrentProperties);

        // Act
        var result = wrapper.Ratio;

        // Assert
        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData(3600, 3600L)] // 1 hour
    [InlineData(0, 0L)]
    [InlineData(-1, -1L)] // Unknown/infinite
    public void Eta_ReturnsCorrectValue(int eta, long expected)
    {
        // Arrange
        var torrentItem = new UTorrentItem { ETA = eta };
        var torrentProperties = new UTorrentProperties();
        var wrapper = new UTorrentItemWrapper(torrentItem, torrentProperties);

        // Act
        var result = wrapper.Eta;

        // Assert
        result.ShouldBe(expected);
    }

    [Fact]
    public void SeedingTimeSeconds_WithCompletedDate_ReturnsPositiveValue()
    {
        // Arrange - Set DateCompleted to 1 hour ago
        var oneHourAgo = DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeSeconds();
        var torrentItem = new UTorrentItem { DateCompleted = oneHourAgo };
        var torrentProperties = new UTorrentProperties();
        var wrapper = new UTorrentItemWrapper(torrentItem, torrentProperties);

        // Act
        var result = wrapper.SeedingTimeSeconds;

        // Assert - Should be approximately 3600 seconds (1 hour), allow some tolerance
        result.ShouldBeInRange(3599L, 3601L);
    }

    [Fact]
    public void SeedingTimeSeconds_WithNoCompletedDate_ReturnsZero()
    {
        // Arrange - DateCompleted = 0 means not completed
        var torrentItem = new UTorrentItem { DateCompleted = 0 };
        var torrentProperties = new UTorrentProperties();
        var wrapper = new UTorrentItemWrapper(torrentItem, torrentProperties);

        // Act
        var result = wrapper.SeedingTimeSeconds;

        // Assert
        result.ShouldBe(0L);
    }

    [Fact]
    public void IsIgnored_WithEmptyList_ReturnsFalse()
    {
        // Arrange
        var torrentItem = new UTorrentItem { Hash = "abc123", Name = "Test Torrent" };
        var torrentProperties = new UTorrentProperties();
        var wrapper = new UTorrentItemWrapper(torrentItem, torrentProperties);

        // Act
        var result = wrapper.IsIgnored(Array.Empty<string>());

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void IsIgnored_MatchingHash_ReturnsTrue()
    {
        // Arrange
        var torrentItem = new UTorrentItem { Hash = "abc123", Name = "Test Torrent" };
        var torrentProperties = new UTorrentProperties();
        var wrapper = new UTorrentItemWrapper(torrentItem, torrentProperties);
        var ignoredDownloads = new[] { "abc123" };

        // Act
        var result = wrapper.IsIgnored(ignoredDownloads);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void IsIgnored_MatchingCategory_ReturnsTrue()
    {
        // Arrange
        var torrentItem = new UTorrentItem { Hash = "abc123", Name = "Test Torrent", Label = "test-category" };
        var torrentProperties = new UTorrentProperties();
        var wrapper = new UTorrentItemWrapper(torrentItem, torrentProperties);
        var ignoredDownloads = new[] { "test-category" };

        // Act
        var result = wrapper.IsIgnored(ignoredDownloads);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void IsIgnored_MatchingTracker_ReturnsTrue()
    {
        // Arrange
        var torrentItem = new UTorrentItem { Hash = "abc123", Name = "Test Torrent" };
        var torrentProperties = new UTorrentProperties
        {
            Trackers = "http://tracker.example.com/announce"
        };
        var wrapper = new UTorrentItemWrapper(torrentItem, torrentProperties);
        var ignoredDownloads = new[] { "tracker.example.com" };

        // Act
        var result = wrapper.IsIgnored(ignoredDownloads);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void IsIgnored_NotMatching_ReturnsFalse()
    {
        // Arrange
        var torrentItem = new UTorrentItem { Hash = "abc123", Name = "Test Torrent", Label = "some-category" };
        var torrentProperties = new UTorrentProperties
        {
            Trackers = "http://tracker.example.com/announce"
        };
        var wrapper = new UTorrentItemWrapper(torrentItem, torrentProperties);
        var ignoredDownloads = new[] { "notmatching" };

        // Act
        var result = wrapper.IsIgnored(ignoredDownloads);

        // Assert
        result.ShouldBeFalse();
    }
}