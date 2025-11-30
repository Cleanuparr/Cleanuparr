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