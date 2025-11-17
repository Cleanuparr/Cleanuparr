using Cleanuparr.Infrastructure.Features.DownloadClient.Transmission;
using Shouldly;
using Transmission.API.RPC.Entity;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.DownloadClient;

public class TransmissionItemWrapperTests
{
    [Fact]
    public void Constructor_WithNullTorrentInfo_ThrowsArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new TransmissionItemWrapper(null!));
    }

    [Fact]
    public void Hash_ReturnsCorrectValue()
    {
        // Arrange
        var expectedHash = "test-hash-123";
        var torrentInfo = new TorrentInfo { HashString = expectedHash };
        var wrapper = new TransmissionItemWrapper(torrentInfo);

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
        var wrapper = new TransmissionItemWrapper(torrentInfo);

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
        var wrapper = new TransmissionItemWrapper(torrentInfo);

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
        var wrapper = new TransmissionItemWrapper(torrentInfo);

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
        var wrapper = new TransmissionItemWrapper(torrentInfo);

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
        var wrapper = new TransmissionItemWrapper(torrentInfo);

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
        var wrapper = new TransmissionItemWrapper(torrentInfo);

        // Act
        var result = wrapper.CompletionPercentage;

        // Assert
        result.ShouldBe(expectedPercentage);
    }

    [Fact]
    public void IsIgnored_WithEmptyList_ReturnsFalse()
    {
        // Arrange
        var torrentInfo = new TorrentInfo { HashString = "abc123", Name = "Test Torrent" };
        var wrapper = new TransmissionItemWrapper(torrentInfo);

        // Act
        var result = wrapper.IsIgnored(Array.Empty<string>());

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void IsIgnored_MatchingHash_ReturnsTrue()
    {
        // Arrange
        var torrentInfo = new TorrentInfo { HashString = "abc123", Name = "Test Torrent" };
        var wrapper = new TransmissionItemWrapper(torrentInfo);
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
        var torrentInfo = new TorrentInfo
        {
            HashString = "abc123",
            Name = "Test Torrent",
            DownloadDir = "/downloads/test-category"
        };
        var wrapper = new TransmissionItemWrapper(torrentInfo);
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
        var torrentInfo = new TorrentInfo
        {
            HashString = "abc123",
            Name = "Test Torrent",
            Trackers = new TransmissionTorrentTrackers[]
            {
                new() { Announce = "http://tracker.example.com/announce" }
            }
        };
        var wrapper = new TransmissionItemWrapper(torrentInfo);
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
        var torrentInfo = new TorrentInfo
        {
            HashString = "abc123",
            Name = "Test Torrent",
            Labels = new[] { "some-category" },
            Trackers = new TransmissionTorrentTrackers[]
            {
                new() { Announce = "http://tracker.example.com/announce" }
            }
        };
        var wrapper = new TransmissionItemWrapper(torrentInfo);
        var ignoredDownloads = new[] { "notmatching" };

        // Act
        var result = wrapper.IsIgnored(ignoredDownloads);

        // Assert
        result.ShouldBeFalse();
    }
}