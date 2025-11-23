using Cleanuparr.Infrastructure.Features.DownloadClient.QBittorrent;
using QBittorrent.Client;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.DownloadClient;

public class QBitItemTests
{
    [Fact]
    public void Constructor_WithNullTorrentInfo_ThrowsArgumentNullException()
    {
        // Arrange
        var trackers = new List<TorrentTracker>();

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new QBitItem(null!, trackers, false));
    }

    [Fact]
    public void Constructor_WithNullTrackers_ThrowsArgumentNullException()
    {
        // Arrange
        var torrentInfo = new TorrentInfo();

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new QBitItem(torrentInfo, null!, false));
    }

    [Fact]
    public void Hash_ReturnsCorrectValue()
    {
        // Arrange
        var expectedHash = "test-hash-123";
        var torrentInfo = new TorrentInfo { Hash = expectedHash };
        var trackers = new List<TorrentTracker>();
        var wrapper = new QBitItem(torrentInfo, trackers, false);

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
        var wrapper = new QBitItem(torrentInfo, trackers, false);

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
        var wrapper = new QBitItem(torrentInfo, trackers, false);

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
        var wrapper = new QBitItem(torrentInfo, trackers, false);

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
        var wrapper = new QBitItem(torrentInfo, trackers, true);

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
        var wrapper = new QBitItem(torrentInfo, trackers, false);

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
        var wrapper = new QBitItem(torrentInfo, trackers, false);

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
        var wrapper = new QBitItem(torrentInfo, trackers, false);

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
        var wrapper = new QBitItem(torrentInfo, trackers, false);

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
        var wrapper = new QBitItem(torrentInfo, trackers, false);

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
        var wrapper = new QBitItem(torrentInfo, trackers, false);

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
        var wrapper = new QBitItem(torrentInfo, trackers, false);

        // Act
        var result = wrapper.Trackers;

        // Assert
        result.ShouldBeEmpty();
    }

    // State checking method tests
    [Theory]
    [InlineData(TorrentState.Downloading, true)]
    [InlineData(TorrentState.ForcedDownload, true)]
    [InlineData(TorrentState.StalledDownload, false)]
    [InlineData(TorrentState.Uploading, false)]
    [InlineData(TorrentState.PausedDownload, false)]
    public void IsDownloading_ReturnsCorrectValue(TorrentState state, bool expected)
    {
        // Arrange
        var torrentInfo = new TorrentInfo { State = state };
        var trackers = new List<TorrentTracker>();
        var wrapper = new QBitItem(torrentInfo, trackers, false);

        // Act
        var result = wrapper.IsDownloading();

        // Assert
        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData(TorrentState.StalledDownload, true)]
    [InlineData(TorrentState.Downloading, false)]
    [InlineData(TorrentState.ForcedDownload, false)]
    [InlineData(TorrentState.Uploading, false)]
    public void IsStalled_ReturnsCorrectValue(TorrentState state, bool expected)
    {
        // Arrange
        var torrentInfo = new TorrentInfo { State = state };
        var trackers = new List<TorrentTracker>();
        var wrapper = new QBitItem(torrentInfo, trackers, false);

        // Act
        var result = wrapper.IsStalled();

        // Assert
        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData(TorrentState.Uploading, true)]
    [InlineData(TorrentState.ForcedUpload, true)]
    [InlineData(TorrentState.StalledUpload, true)]
    [InlineData(TorrentState.Downloading, false)]
    [InlineData(TorrentState.PausedUpload, false)]
    public void IsSeeding_ReturnsCorrectValue(TorrentState state, bool expected)
    {
        // Arrange
        var torrentInfo = new TorrentInfo { State = state };
        var trackers = new List<TorrentTracker>();
        var wrapper = new QBitItem(torrentInfo, trackers, false);

        // Act
        var result = wrapper.IsSeeding();

        // Assert
        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData(0.0, false)]
    [InlineData(0.5, false)]
    [InlineData(0.99, false)]
    [InlineData(1.0, true)]
    public void IsCompleted_ReturnsCorrectValue(double progress, bool expected)
    {
        // Arrange
        var torrentInfo = new TorrentInfo { Progress = progress };
        var trackers = new List<TorrentTracker>();
        var wrapper = new QBitItem(torrentInfo, trackers, false);

        // Act
        var result = wrapper.IsCompleted();

        // Assert
        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData(TorrentState.PausedDownload, true)]
    [InlineData(TorrentState.PausedUpload, true)]
    [InlineData(TorrentState.Downloading, false)]
    [InlineData(TorrentState.Uploading, false)]
    public void IsPaused_ReturnsCorrectValue(TorrentState state, bool expected)
    {
        // Arrange
        var torrentInfo = new TorrentInfo { State = state };
        var trackers = new List<TorrentTracker>();
        var wrapper = new QBitItem(torrentInfo, trackers, false);

        // Act
        var result = wrapper.IsPaused();

        // Assert
        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData(TorrentState.QueuedDownload, true)]
    [InlineData(TorrentState.QueuedUpload, true)]
    [InlineData(TorrentState.Downloading, false)]
    [InlineData(TorrentState.Uploading, false)]
    public void IsQueued_ReturnsCorrectValue(TorrentState state, bool expected)
    {
        // Arrange
        var torrentInfo = new TorrentInfo { State = state };
        var trackers = new List<TorrentTracker>();
        var wrapper = new QBitItem(torrentInfo, trackers, false);

        // Act
        var result = wrapper.IsQueued();

        // Assert
        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData(TorrentState.CheckingDownload, true)]
    [InlineData(TorrentState.CheckingUpload, true)]
    [InlineData(TorrentState.CheckingResumeData, true)]
    [InlineData(TorrentState.Downloading, false)]
    [InlineData(TorrentState.Uploading, false)]
    public void IsChecking_ReturnsCorrectValue(TorrentState state, bool expected)
    {
        // Arrange
        var torrentInfo = new TorrentInfo { State = state };
        var trackers = new List<TorrentTracker>();
        var wrapper = new QBitItem(torrentInfo, trackers, false);

        // Act
        var result = wrapper.IsChecking();

        // Assert
        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData(TorrentState.Allocating, true)]
    [InlineData(TorrentState.Downloading, false)]
    [InlineData(TorrentState.Uploading, false)]
    public void IsAllocating_ReturnsCorrectValue(TorrentState state, bool expected)
    {
        // Arrange
        var torrentInfo = new TorrentInfo { State = state };
        var trackers = new List<TorrentTracker>();
        var wrapper = new QBitItem(torrentInfo, trackers, false);

        // Act
        var result = wrapper.IsAllocating();

        // Assert
        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData(TorrentState.FetchingMetadata, true)]
    [InlineData(TorrentState.ForcedFetchingMetadata, true)]
    [InlineData(TorrentState.Downloading, false)]
    [InlineData(TorrentState.StalledDownload, false)]
    public void IsMetadataDownloading_ReturnsCorrectValue(TorrentState state, bool expected)
    {
        // Arrange
        var torrentInfo = new TorrentInfo { State = state };
        var trackers = new List<TorrentTracker>();
        var wrapper = new QBitItem(torrentInfo, trackers, false);

        // Act
        var result = wrapper.IsMetadataDownloading();

        // Assert
        result.ShouldBe(expected);
    }

    [Fact]
    public void IsIgnored_WithEmptyList_ReturnsFalse()
    {
        // Arrange
        var torrentInfo = new TorrentInfo { Name = "Test Torrent", Hash = "abc123" };
        var trackers = new List<TorrentTracker>();
        var wrapper = new QBitItem(torrentInfo, trackers, false);

        // Act
        var result = wrapper.IsIgnored(Array.Empty<string>());

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void IsIgnored_MatchingHash_ReturnsTrue()
    {
        // Arrange
        var torrentInfo = new TorrentInfo { Name = "Test Torrent", Hash = "abc123" };
        var trackers = new List<TorrentTracker>();
        var wrapper = new QBitItem(torrentInfo, trackers, false);
        var ignoredDownloads = new[] { "abc123" };

        // Act
        var result = wrapper.IsIgnored(ignoredDownloads);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void IsIgnored_MatchingTracker_ReturnsTrue()
    {
        // Arrange
        var torrentInfo = new TorrentInfo { Name = "Test Torrent", Hash = "abc123" };
        var trackers = new List<TorrentTracker>
        {
            new() { Url = "http://tracker.example.com/announce" }
        };
        var wrapper = new QBitItem(torrentInfo, trackers, false);
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
        var torrentInfo = new TorrentInfo { Name = "Test Torrent", Hash = "abc123" };
        var trackers = new List<TorrentTracker>
        {
            new() { Url = "http://tracker.example.com/announce" }
        };
        var wrapper = new QBitItem(torrentInfo, trackers, false);
        var ignoredDownloads = new[] { "notmatching" };

        // Act
        var result = wrapper.IsIgnored(ignoredDownloads);

        // Assert
        result.ShouldBeFalse();
    }
}