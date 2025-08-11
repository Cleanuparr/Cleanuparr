using Cleanuparr.Infrastructure.Features.DownloadClient.QBittorrent;
using QBittorrent.Client;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.DownloadClient;

public class QBitTorrentInfoTests
{
    [Fact]
    public void Constructor_WithNullTorrentInfo_ThrowsArgumentNullException()
    {
        // Arrange
        var trackers = new List<TorrentTracker>();

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new QBitTorrentInfo(null!, trackers, false));
    }

    [Fact]
    public void Constructor_WithNullTrackers_ThrowsArgumentNullException()
    {
        // Arrange
        var torrentInfo = new TorrentInfo();

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new QBitTorrentInfo(torrentInfo, null!, false));
    }

    [Fact]
    public void Hash_ReturnsCorrectValue()
    {
        // Arrange
        var expectedHash = "test-hash-123";
        var torrentInfo = new TorrentInfo { Hash = expectedHash };
        var trackers = new List<TorrentTracker>();
        var wrapper = new QBitTorrentInfo(torrentInfo, trackers, false);

        // Act
        var result = wrapper.Hash;

        // Assert
        result.ShouldBe(expectedHash);
    }

    [Fact]
    public void Hash_WithNullValue_ReturnsEmptyString()
    {
        // Arrange
        var torrentInfo = new TorrentInfo { Hash = null };
        var trackers = new List<TorrentTracker>();
        var wrapper = new QBitTorrentInfo(torrentInfo, trackers, false);

        // Act
        var result = wrapper.Hash;

        // Assert
        result.ShouldBe(string.Empty);
    }

    [Fact]
    public void Name_ReturnsCorrectValue()
    {
        // Arrange
        var expectedName = "Test Torrent";
        var torrentInfo = new TorrentInfo { Name = expectedName };
        var trackers = new List<TorrentTracker>();
        var wrapper = new QBitTorrentInfo(torrentInfo, trackers, false);

        // Act
        var result = wrapper.Name;

        // Assert
        result.ShouldBe(expectedName);
    }

    [Fact]
    public void Name_WithNullValue_ReturnsEmptyString()
    {
        // Arrange
        var torrentInfo = new TorrentInfo { Name = null };
        var trackers = new List<TorrentTracker>();
        var wrapper = new QBitTorrentInfo(torrentInfo, trackers, false);

        // Act
        var result = wrapper.Name;

        // Assert
        result.ShouldBe(string.Empty);
    }

    [Fact]
    public void IsPrivate_ReturnsCorrectValue()
    {
        // Arrange
        var torrentInfo = new TorrentInfo();
        var trackers = new List<TorrentTracker>();
        var wrapper = new QBitTorrentInfo(torrentInfo, trackers, true);

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
        var torrentInfo = new TorrentInfo { Size = expectedSize };
        var trackers = new List<TorrentTracker>();
        var wrapper = new QBitTorrentInfo(torrentInfo, trackers, false);

        // Act
        var result = wrapper.Size;

        // Assert
        result.ShouldBe(expectedSize);
    }

    [Fact]
    public void Size_WithZeroValue_ReturnsZero()
    {
        // Arrange
        var torrentInfo = new TorrentInfo { Size = 0 };
        var trackers = new List<TorrentTracker>();
        var wrapper = new QBitTorrentInfo(torrentInfo, trackers, false);

        // Act
        var result = wrapper.Size;

        // Assert
        result.ShouldBe(0);
    }

    [Theory]
    [InlineData(0.0, 0.0)]
    [InlineData(0.5, 50.0)]
    [InlineData(0.75, 75.0)]
    [InlineData(1.0, 100.0)]
    public void CompletionPercentage_ReturnsCorrectValue(double progress, double expectedPercentage)
    {
        // Arrange
        var torrentInfo = new TorrentInfo { Progress = progress };
        var trackers = new List<TorrentTracker>();
        var wrapper = new QBitTorrentInfo(torrentInfo, trackers, false);

        // Act
        var result = wrapper.CompletionPercentage;

        // Assert
        result.ShouldBe(expectedPercentage);
    }

    [Fact]
    public void Trackers_WithValidUrls_ReturnsHostNames()
    {
        // Arrange
        var torrentInfo = new TorrentInfo();
        var trackers = new List<TorrentTracker>
        {
            new() { Url = "http://tracker1.example.com:8080/announce" },
            new() { Url = "https://tracker2.example.com/announce" },
            new() { Url = "udp://tracker3.example.com:1337/announce" }
        };
        var wrapper = new QBitTorrentInfo(torrentInfo, trackers, false);

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
        var torrentInfo = new TorrentInfo();
        var trackers = new List<TorrentTracker>
        {
            new() { Url = "http://tracker1.example.com:8080/announce" },
            new() { Url = "https://tracker1.example.com/announce" },
            new() { Url = "udp://tracker1.example.com:1337/announce" }
        };
        var wrapper = new QBitTorrentInfo(torrentInfo, trackers, false);

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
        var torrentInfo = new TorrentInfo();
        var trackers = new List<TorrentTracker>
        {
            new() { Url = "http://valid.example.com/announce" },
            new() { Url = "invalid-url" },
            new() { Url = "" },
            new() { Url = null }
        };
        var wrapper = new QBitTorrentInfo(torrentInfo, trackers, false);

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
        var torrentInfo = new TorrentInfo();
        var trackers = new List<TorrentTracker>();
        var wrapper = new QBitTorrentInfo(torrentInfo, trackers, false);

        // Act
        var result = wrapper.Trackers;

        // Assert
        result.ShouldBeEmpty();
    }
}