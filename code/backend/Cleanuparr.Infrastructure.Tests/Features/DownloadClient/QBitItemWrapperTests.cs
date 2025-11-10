using Cleanuparr.Infrastructure.Features.DownloadClient.QBittorrent;
using QBittorrent.Client;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.DownloadClient;

public class QBitItemWrapperTests
{
    [Fact]
    public void Constructor_WithNullTorrentInfo_ThrowsArgumentNullException()
    {
        // Arrange
        var trackers = new List<TorrentTracker>();

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new QBitItemWrapper(null!, trackers, false));
    }

    [Fact]
    public void Constructor_WithNullTrackers_ThrowsArgumentNullException()
    {
        // Arrange
        var torrentInfo = new TorrentInfo();

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new QBitItemWrapper(torrentInfo, null!, false));
    }

    [Fact]
    public void Hash_ReturnsCorrectValue()
    {
        // Arrange
        var expectedHash = "test-hash-123";
        var torrentInfo = new TorrentInfo { Hash = expectedHash };
        var trackers = new List<TorrentTracker>();
        var wrapper = new QBitItemWrapper(torrentInfo, trackers, false);

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
        var wrapper = new QBitItemWrapper(torrentInfo, trackers, false);

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
        var wrapper = new QBitItemWrapper(torrentInfo, trackers, false);

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
        var wrapper = new QBitItemWrapper(torrentInfo, trackers, false);

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
        var wrapper = new QBitItemWrapper(torrentInfo, trackers, true);

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
        var wrapper = new QBitItemWrapper(torrentInfo, trackers, false);

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
        var wrapper = new QBitItemWrapper(torrentInfo, trackers, false);

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
        var wrapper = new QBitItemWrapper(torrentInfo, trackers, false);

        // Act
        var result = wrapper.CompletionPercentage;

        // Assert
        result.ShouldBe(expectedPercentage);
    }

    [Fact]
    public void DownloadedBytes_ReturnsCorrectValue()
    {
        // Arrange
        var expectedDownloaded = 1024L * 1024 * 500; // 500MB
        var torrentInfo = new TorrentInfo { Downloaded = expectedDownloaded };
        var trackers = new List<TorrentTracker>();
        var wrapper = new QBitItemWrapper(torrentInfo, trackers, false);

        // Act
        var result = wrapper.DownloadedBytes;

        // Assert
        result.ShouldBe(expectedDownloaded);
    }

    [Fact]
    public void DownloadedBytes_WithNullValue_ReturnsZero()
    {
        // Arrange
        var torrentInfo = new TorrentInfo { Downloaded = null };
        var trackers = new List<TorrentTracker>();
        var wrapper = new QBitItemWrapper(torrentInfo, trackers, false);

        // Act
        var result = wrapper.DownloadedBytes;

        // Assert
        result.ShouldBe(0);
    }
    
    [Fact]
    public void DownloadSpeed_ReturnsCorrectValue()
    {
        // Arrange
        var expectedSpeed = 1024 * 512; // 512 KB/s
        var torrentInfo = new TorrentInfo { DownloadSpeed = expectedSpeed };
        var trackers = new List<TorrentTracker>();
        var wrapper = new QBitItemWrapper(torrentInfo, trackers, false);

        // Act
        var result = wrapper.DownloadSpeed;

        // Assert
        result.ShouldBe(expectedSpeed);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    [InlineData(2.5)]
    public void Ratio_ReturnsCorrectValue(double expectedRatio)
    {
        // Arrange
        var torrentInfo = new TorrentInfo { Ratio = expectedRatio };
        var trackers = new List<TorrentTracker>();
        var wrapper = new QBitItemWrapper(torrentInfo, trackers, false);

        // Act
        var result = wrapper.Ratio;

        // Assert
        result.ShouldBe(expectedRatio);
    }

    [Fact]
    public void Eta_ReturnsCorrectValue()
    {
        // Arrange
        var expectedEta = TimeSpan.FromMinutes(30);
        var torrentInfo = new TorrentInfo { EstimatedTime = expectedEta };
        var trackers = new List<TorrentTracker>();
        var wrapper = new QBitItemWrapper(torrentInfo, trackers, false);

        // Act
        var result = wrapper.Eta;

        // Assert
        result.ShouldBe((long)expectedEta.TotalSeconds);
    }

    [Fact]
    public void Eta_WithNullValue_ReturnsZero()
    {
        // Arrange
        var torrentInfo = new TorrentInfo { EstimatedTime = null };
        var trackers = new List<TorrentTracker>();
        var wrapper = new QBitItemWrapper(torrentInfo, trackers, false);

        // Act
        var result = wrapper.Eta;

        // Assert
        result.ShouldBe(0);
    }

    [Fact]
    public void SeedingTimeSeconds_ReturnsCorrectValue()
    {
        // Arrange
        var expectedTime = TimeSpan.FromHours(5);
        var torrentInfo = new TorrentInfo { SeedingTime = expectedTime };
        var trackers = new List<TorrentTracker>();
        var wrapper = new QBitItemWrapper(torrentInfo, trackers, false);

        // Act
        var result = wrapper.SeedingTimeSeconds;

        // Assert
        result.ShouldBe((long)expectedTime.TotalSeconds);
    }

    [Fact]
    public void SeedingTimeSeconds_WithNullValue_ReturnsZero()
    {
        // Arrange
        var torrentInfo = new TorrentInfo { SeedingTime = null };
        var trackers = new List<TorrentTracker>();
        var wrapper = new QBitItemWrapper(torrentInfo, trackers, false);

        // Act
        var result = wrapper.SeedingTimeSeconds;

        // Assert
        result.ShouldBe(0);
    }

    [Fact]
    public void Tags_ReturnsCorrectValue()
    {
        // Arrange
        var expectedTags = new List<string> { "tag1", "tag2", "tag3" };
        var torrentInfo = new TorrentInfo { Tags = expectedTags.AsReadOnly() };
        var trackers = new List<TorrentTracker>();
        var wrapper = new QBitItemWrapper(torrentInfo, trackers, false);

        // Act
        var result = wrapper.Tags;

        // Assert
        result.ShouldBe(expectedTags);
    }

    [Fact]
    public void Tags_WithNullValue_ReturnsEmptyList()
    {
        // Arrange
        var torrentInfo = new TorrentInfo { Tags = null };
        var trackers = new List<TorrentTracker>();
        var wrapper = new QBitItemWrapper(torrentInfo, trackers, false);

        // Act
        var result = wrapper.Tags;

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public void Tags_WithEmptyList_ReturnsEmptyList()
    {
        // Arrange
        var torrentInfo = new TorrentInfo { Tags = new List<string>().AsReadOnly() };
        var trackers = new List<TorrentTracker>();
        var wrapper = new QBitItemWrapper(torrentInfo, trackers, false);

        // Act
        var result = wrapper.Tags;

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public void Category_ReturnsCorrectValue()
    {
        // Arrange
        var expectedCategory = "movies";
        var torrentInfo = new TorrentInfo { Category = expectedCategory };
        var trackers = new List<TorrentTracker>();
        var wrapper = new QBitItemWrapper(torrentInfo, trackers, false);

        // Act
        var result = wrapper.Category;

        // Assert
        result.ShouldBe(expectedCategory);
    }

    [Fact]
    public void Category_WithNullValue_ReturnsNull()
    {
        // Arrange
        var torrentInfo = new TorrentInfo { Category = null };
        var trackers = new List<TorrentTracker>();
        var wrapper = new QBitItemWrapper(torrentInfo, trackers, false);

        // Act
        var result = wrapper.Category;

        // Assert
        result.ShouldBeNull();
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
        var wrapper = new QBitItemWrapper(torrentInfo, trackers, false);

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
        var wrapper = new QBitItemWrapper(torrentInfo, trackers, false);

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
        var wrapper = new QBitItemWrapper(torrentInfo, trackers, false);

        // Act
        var result = wrapper.IsSeeding();

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
        var wrapper = new QBitItemWrapper(torrentInfo, trackers, false);

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
        var wrapper = new QBitItemWrapper(torrentInfo, trackers, false);

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
        var wrapper = new QBitItemWrapper(torrentInfo, trackers, false);
        var ignoredDownloads = new[] { "abc123" };

        // Act
        var result = wrapper.IsIgnored(ignoredDownloads);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void IsIgnored_MatchingTag_ReturnsTrue()
    {
        // Arrange
        var torrentInfo = new TorrentInfo
        {
            Name = "Test Torrent",
            Hash = "abc123",
            Tags = new List<string> { "test-tag" }.AsReadOnly()
        };
        var trackers = new List<TorrentTracker>();
        var wrapper = new QBitItemWrapper(torrentInfo, trackers, false);
        var ignoredDownloads = new[] { "test-tag" };

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
            Name = "Test Torrent",
            Hash = "abc123",
            Category = "test-category"
        };
        var trackers = new List<TorrentTracker>();
        var wrapper = new QBitItemWrapper(torrentInfo, trackers, false);
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
            Name = "Test Torrent",
            Hash = "abc123"
        };
        var trackers = new List<TorrentTracker>
        {
            new() { Url = "http://tracker.example.com/announce" }
        };
        var wrapper = new QBitItemWrapper(torrentInfo, trackers, false);
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
            Name = "Test Torrent",
            Hash = "abc123",
            Category = "some-category",
            Tags = new List<string> { "some-tag" }.AsReadOnly()
        };
        var trackers = new List<TorrentTracker>
        {
            new() { Url = "http://tracker.example.com/announce" }
        };
        var wrapper = new QBitItemWrapper(torrentInfo, trackers, false);
        var ignoredDownloads = new[] { "notmatching" };

        // Act
        var result = wrapper.IsIgnored(ignoredDownloads);

        // Assert
        result.ShouldBeFalse();
    }
}