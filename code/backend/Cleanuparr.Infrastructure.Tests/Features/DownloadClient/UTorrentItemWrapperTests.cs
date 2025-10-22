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
    public void Trackers_WithValidUrls_ReturnsHostNames()
    {
        // Arrange
        var torrentItem = new UTorrentItem();
        var torrentProperties = new UTorrentProperties
        {
            Trackers = "http://tracker1.example.com:8080/announce\r\nhttps://tracker2.example.com/announce\r\nudp://tracker3.example.com:1337/announce"
        };
        var wrapper = new UTorrentItemWrapper(torrentItem, torrentProperties);

        // Act
        var result = wrapper.Trackers;

        // Assert
        result.Count.ShouldBe(3);
        result.ShouldContain("tracker1.example.com");
        result.ShouldContain("tracker2.example.com");
        result.ShouldContain("tracker3.example.com");
    }

    [Fact]
    public void Trackers_WithDuplicateHosts_ReturnsDistinctHosts()
    {
        // Arrange
        var torrentItem = new UTorrentItem();
        var torrentProperties = new UTorrentProperties
        {
            Trackers = "http://tracker1.example.com:8080/announce\r\nhttps://tracker1.example.com/announce\r\nudp://tracker1.example.com:1337/announce"
        };
        var wrapper = new UTorrentItemWrapper(torrentItem, torrentProperties);

        // Act
        var result = wrapper.Trackers;

        // Assert
        result.Count.ShouldBe(1);
        result.ShouldContain("tracker1.example.com");
    }

    [Fact]
    public void Trackers_WithInvalidUrls_SkipsInvalidEntries()
    {
        // Arrange
        var torrentItem = new UTorrentItem();
        var torrentProperties = new UTorrentProperties
        {
            Trackers = "http://valid.example.com/announce\r\ninvalid-url\r\n\r\n "
        };
        var wrapper = new UTorrentItemWrapper(torrentItem, torrentProperties);

        // Act
        var result = wrapper.Trackers;

        // Assert
        result.Count.ShouldBe(1);
        result.ShouldContain("valid.example.com");
    }

    [Fact]
    public void Trackers_WithEmptyList_ReturnsEmptyList()
    {
        // Arrange
        var torrentItem = new UTorrentItem();
        var torrentProperties = new UTorrentProperties
        {
            Trackers = ""
        };
        var wrapper = new UTorrentItemWrapper(torrentItem, torrentProperties);

        // Act
        var result = wrapper.Trackers;

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public void Trackers_WithNullTrackerList_ReturnsEmptyList()
    {
        // Arrange
        var torrentItem = new UTorrentItem();
        var torrentProperties = new UTorrentProperties(); // Trackers defaults to empty string
        var wrapper = new UTorrentItemWrapper(torrentItem, torrentProperties);

        // Act
        var result = wrapper.Trackers;

        // Assert
        result.ShouldBeEmpty();
    }
}