using Cleanuparr.Infrastructure.Features.DownloadClient.Transmission;
using Shouldly;
using Transmission.API.RPC.Entity;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.DownloadClient;

public class TransmissionItemTests
{
    [Fact]
    public void Constructor_WithNullTorrentInfo_ThrowsArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new TransmissionItem(null!));
    }

    [Fact]
    public void Hash_ReturnsCorrectValue()
    {
        // Arrange
        var expectedHash = "test-hash-123";
        var torrentInfo = new TorrentInfo { HashString = expectedHash };
        var wrapper = new TransmissionItem(torrentInfo);

        // Act
        var result = wrapper.Hash;

        // Assert
        result.ShouldBe(expectedHash);
    }

    [Fact]
    public void Hash_WithNullValue_ReturnsEmptyString()
    {
        // Arrange
        var torrentInfo = new TorrentInfo { HashString = null };
        var wrapper = new TransmissionItem(torrentInfo);

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
        var wrapper = new TransmissionItem(torrentInfo);

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
        var wrapper = new TransmissionItem(torrentInfo);

        // Act
        var result = wrapper.Name;

        // Assert
        result.ShouldBe(string.Empty);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(null, false)]
    public void IsPrivate_ReturnsCorrectValue(bool? isPrivate, bool expected)
    {
        // Arrange
        var torrentInfo = new TorrentInfo { IsPrivate = isPrivate };
        var wrapper = new TransmissionItem(torrentInfo);

        // Act
        var result = wrapper.IsPrivate;

        // Assert
        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData(1024L * 1024 * 1024, 1024L * 1024 * 1024)] // 1GB
    [InlineData(0L, 0L)]
    [InlineData(null, 0L)]
    public void Size_ReturnsCorrectValue(long? totalSize, long expected)
    {
        // Arrange
        var torrentInfo = new TorrentInfo { TotalSize = totalSize };
        var wrapper = new TransmissionItem(torrentInfo);

        // Act
        var result = wrapper.Size;

        // Assert
        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData(0L, 1024L, 0.0)]
    [InlineData(512L, 1024L, 50.0)]
    [InlineData(768L, 1024L, 75.0)]
    [InlineData(1024L, 1024L, 100.0)]
    [InlineData(0L, 0L, 0.0)] // Edge case: zero size
    [InlineData(null, 1024L, 0.0)] // Edge case: null downloaded
    [InlineData(512L, null, 0.0)] // Edge case: null total size
    public void CompletionPercentage_ReturnsCorrectValue(long? downloadedEver, long? totalSize, double expectedPercentage)
    {
        // Arrange
        var torrentInfo = new TorrentInfo 
        { 
            DownloadedEver = downloadedEver,
            TotalSize = totalSize
        };
        var wrapper = new TransmissionItem(torrentInfo);

        // Act
        var result = wrapper.CompletionPercentage;

        // Assert
        result.ShouldBe(expectedPercentage);
    }

    [Fact]
    public void Trackers_WithValidUrls_ReturnsHostNames()
    {
        // Arrange
        var torrentInfo = new TorrentInfo
        {
            Trackers = new TransmissionTorrentTrackers[]
            {
                new() { Announce = "http://tracker1.example.com:8080/announce" },
                new() { Announce = "https://tracker2.example.com/announce" },
                new() { Announce = "udp://tracker3.example.com:1337/announce" }
            }
        };
        var wrapper = new TransmissionItem(torrentInfo);

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
        var torrentInfo = new TorrentInfo
        {
            Trackers = new TransmissionTorrentTrackers[]
            {
                new() { Announce = "http://tracker1.example.com:8080/announce" },
                new() { Announce = "https://tracker1.example.com/announce" },
                new() { Announce = "udp://tracker1.example.com:1337/announce" }
            }
        };
        var wrapper = new TransmissionItem(torrentInfo);

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
        var torrentInfo = new TorrentInfo
        {
            Trackers = new TransmissionTorrentTrackers[]
            {
                new() { Announce = "http://valid.example.com/announce" },
                new() { Announce = "invalid-url" },
                new() { Announce = "" },
                new() { Announce = null }
            }
        };
        var wrapper = new TransmissionItem(torrentInfo);

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
        var torrentInfo = new TorrentInfo
        {
            Trackers = new TransmissionTorrentTrackers[0]
        };
        var wrapper = new TransmissionItem(torrentInfo);

        // Act
        var result = wrapper.Trackers;

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public void Trackers_WithNullTrackers_ReturnsEmptyList()
    {
        // Arrange
        var torrentInfo = new TorrentInfo
        {
            Trackers = null
        };
        var wrapper = new TransmissionItem(torrentInfo);

        // Act
        var result = wrapper.Trackers;

        // Assert
        result.ShouldBeEmpty();
    }
}