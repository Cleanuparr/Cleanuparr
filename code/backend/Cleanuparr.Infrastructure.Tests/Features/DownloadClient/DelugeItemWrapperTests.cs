using Cleanuparr.Domain.Entities.Deluge.Response;
using Cleanuparr.Infrastructure.Features.DownloadClient.Deluge;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.DownloadClient;

public class DelugeItemWrapperTests
{
    [Fact]
    public void Constructor_WithNullDownloadStatus_ThrowsArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new DelugeItemWrapper(null!));
    }

    [Fact]
    public void Hash_ReturnsCorrectValue()
    {
        // Arrange
        var expectedHash = "test-hash-123";
        var downloadStatus = new DownloadStatus 
        { 
            Hash = expectedHash,
            Trackers = new List<Tracker>(),
            DownloadLocation = "/test/path"
        };
        var wrapper = new DelugeItemWrapper(downloadStatus);

        // Act
        var result = wrapper.Hash;

        // Assert
        result.ShouldBe(expectedHash);
    }

    [Fact]
    public void Hash_WithNullValue_ReturnsEmptyString()
    {
        // Arrange
        var downloadStatus = new DownloadStatus 
        { 
            Hash = null,
            Trackers = new List<Tracker>(),
            DownloadLocation = "/test/path"
        };
        var wrapper = new DelugeItemWrapper(downloadStatus);

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
        var downloadStatus = new DownloadStatus 
        { 
            Name = expectedName,
            Trackers = new List<Tracker>(),
            DownloadLocation = "/test/path"
        };
        var wrapper = new DelugeItemWrapper(downloadStatus);

        // Act
        var result = wrapper.Name;

        // Assert
        result.ShouldBe(expectedName);
    }

    [Fact]
    public void Name_WithNullValue_ReturnsEmptyString()
    {
        // Arrange
        var downloadStatus = new DownloadStatus 
        { 
            Name = null,
            Trackers = new List<Tracker>(),
            DownloadLocation = "/test/path"
        };
        var wrapper = new DelugeItemWrapper(downloadStatus);

        // Act
        var result = wrapper.Name;

        // Assert
        result.ShouldBe(string.Empty);
    }

    [Fact]
    public void IsPrivate_ReturnsCorrectValue()
    {
        // Arrange
        var downloadStatus = new DownloadStatus 
        { 
            Private = true,
            Trackers = new List<Tracker>(),
            DownloadLocation = "/test/path"
        };
        var wrapper = new DelugeItemWrapper(downloadStatus);

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
        var downloadStatus = new DownloadStatus 
        { 
            Size = expectedSize,
            Trackers = new List<Tracker>(),
            DownloadLocation = "/test/path"
        };
        var wrapper = new DelugeItemWrapper(downloadStatus);

        // Act
        var result = wrapper.Size;

        // Assert
        result.ShouldBe(expectedSize);
    }

    [Theory]
    [InlineData(0, 1024, 0.0)]
    [InlineData(512, 1024, 50.0)]
    [InlineData(768, 1024, 75.0)]
    [InlineData(1024, 1024, 100.0)]
    [InlineData(0, 0, 0.0)] // Edge case: zero size
    public void CompletionPercentage_ReturnsCorrectValue(long totalDone, long size, double expectedPercentage)
    {
        // Arrange
        var downloadStatus = new DownloadStatus
        {
            TotalDone = totalDone,
            Size = size,
            Trackers = new List<Tracker>(),
            DownloadLocation = "/test/path"
        };
        var wrapper = new DelugeItemWrapper(downloadStatus);

        // Act
        var result = wrapper.CompletionPercentage;

        // Assert
        result.ShouldBe(expectedPercentage);
    }

    [Theory]
    [InlineData(1024L * 1024 * 100, 1024L * 1024 * 100)] // 100MB
    [InlineData(0L, 0L)]
    public void DownloadedBytes_ReturnsCorrectValue(long totalDone, long expected)
    {
        // Arrange
        var downloadStatus = new DownloadStatus
        {
            TotalDone = totalDone,
            Trackers = new List<Tracker>(),
            DownloadLocation = "/test/path"
        };
        var wrapper = new DelugeItemWrapper(downloadStatus);

        // Act
        var result = wrapper.DownloadedBytes;

        // Assert
        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData(2.0f, 2.0)]
    [InlineData(0.5f, 0.5)]
    [InlineData(1.0f, 1.0)]
    [InlineData(0.0f, 0.0)]
    public void Ratio_ReturnsCorrectValue(float ratio, double expected)
    {
        // Arrange
        var downloadStatus = new DownloadStatus
        {
            Ratio = ratio,
            Trackers = new List<Tracker>(),
            DownloadLocation = "/test/path"
        };
        var wrapper = new DelugeItemWrapper(downloadStatus);

        // Act
        var result = wrapper.Ratio;

        // Assert
        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData(3600UL, 3600L)] // 1 hour
    [InlineData(0UL, 0L)]
    [InlineData(86400UL, 86400L)] // 1 day
    public void Eta_ReturnsCorrectValue(ulong eta, long expected)
    {
        // Arrange
        var downloadStatus = new DownloadStatus
        {
            Eta = eta,
            Trackers = new List<Tracker>(),
            DownloadLocation = "/test/path"
        };
        var wrapper = new DelugeItemWrapper(downloadStatus);

        // Act
        var result = wrapper.Eta;

        // Assert
        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData(86400L, 86400L)] // 1 day
    [InlineData(0L, 0L)]
    [InlineData(3600L, 3600L)] // 1 hour
    public void SeedingTimeSeconds_ReturnsCorrectValue(long seedingTime, long expected)
    {
        // Arrange
        var downloadStatus = new DownloadStatus
        {
            SeedingTime = seedingTime,
            Trackers = new List<Tracker>(),
            DownloadLocation = "/test/path"
        };
        var wrapper = new DelugeItemWrapper(downloadStatus);

        // Act
        var result = wrapper.SeedingTimeSeconds;

        // Assert
        result.ShouldBe(expected);
    }

    [Fact]
    public void IsIgnored_WithEmptyList_ReturnsFalse()
    {
        // Arrange
        var downloadStatus = new DownloadStatus
        {
            Hash = "abc123",
            Name = "Test Torrent",
            Trackers = new List<Tracker>(),
            DownloadLocation = "/test/path"
        };
        var wrapper = new DelugeItemWrapper(downloadStatus);

        // Act
        var result = wrapper.IsIgnored(Array.Empty<string>());

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void IsIgnored_MatchingHash_ReturnsTrue()
    {
        // Arrange
        var downloadStatus = new DownloadStatus
        {
            Hash = "abc123",
            Name = "Test Torrent",
            Trackers = new List<Tracker>(),
            DownloadLocation = "/test/path"
        };
        var wrapper = new DelugeItemWrapper(downloadStatus);
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
        var downloadStatus = new DownloadStatus
        {
            Hash = "abc123",
            Name = "Test Torrent",
            Label = "test-category",
            Trackers = new List<Tracker>(),
            DownloadLocation = "/test/path"
        };
        var wrapper = new DelugeItemWrapper(downloadStatus);
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
        var downloadStatus = new DownloadStatus
        {
            Hash = "abc123",
            Name = "Test Torrent",
            Trackers = new List<Tracker>
            {
                new() { Url = "http://tracker.example.com/announce" }
            },
            DownloadLocation = "/test/path"
        };
        var wrapper = new DelugeItemWrapper(downloadStatus);
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
        var downloadStatus = new DownloadStatus
        {
            Hash = "abc123",
            Name = "Test Torrent",
            Label = "some-category",
            Trackers = new List<Tracker>
            {
                new() { Url = "http://tracker.example.com/announce" }
            },
            DownloadLocation = "/test/path"
        };
        var wrapper = new DelugeItemWrapper(downloadStatus);
        var ignoredDownloads = new[] { "notmatching" };

        // Act
        var result = wrapper.IsIgnored(ignoredDownloads);

        // Assert
        result.ShouldBeFalse();
    }

    [Theory]
    [InlineData(1024L * 1024, 1024L * 1024)] // 1MB/s
    [InlineData(0L, 0L)]
    [InlineData(500L, 500L)]
    public void DownloadSpeed_ReturnsCorrectValue(long downloadSpeed, long expected)
    {
        // Arrange
        var downloadStatus = new DownloadStatus
        {
            DownloadSpeed = downloadSpeed,
            Trackers = new List<Tracker>(),
            DownloadLocation = "/test/path"
        };
        var wrapper = new DelugeItemWrapper(downloadStatus);

        // Act
        var result = wrapper.DownloadSpeed;

        // Assert
        result.ShouldBe(expected);
    }

    [Fact]
    public void Category_Setter_SetsLabel()
    {
        // Arrange
        var downloadStatus = new DownloadStatus
        {
            Label = "original-category",
            Trackers = new List<Tracker>(),
            DownloadLocation = "/test/path"
        };
        var wrapper = new DelugeItemWrapper(downloadStatus);

        // Act
        wrapper.Category = "new-category";

        // Assert
        wrapper.Category.ShouldBe("new-category");
        downloadStatus.Label.ShouldBe("new-category");
    }

    [Theory]
    [InlineData("Downloading", true)]
    [InlineData("downloading", true)]
    [InlineData("DOWNLOADING", true)]
    [InlineData("Seeding", false)]
    [InlineData("Paused", false)]
    [InlineData(null, false)]
    public void IsDownloading_ReturnsCorrectValue(string? state, bool expected)
    {
        // Arrange
        var downloadStatus = new DownloadStatus
        {
            State = state,
            Trackers = new List<Tracker>(),
            DownloadLocation = "/test/path"
        };
        var wrapper = new DelugeItemWrapper(downloadStatus);

        // Act
        var result = wrapper.IsDownloading();

        // Assert
        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData("Downloading", 0, 0UL, true)] // Downloading with no speed and no ETA = stalled
    [InlineData("Downloading", 1000, 0UL, false)] // Has download speed = not stalled
    [InlineData("Downloading", 0, 100UL, false)] // Has ETA = not stalled
    [InlineData("Downloading", 1000, 100UL, false)] // Has both = not stalled
    [InlineData("Seeding", 0, 0UL, false)] // Not downloading state = not stalled
    [InlineData("Paused", 0, 0UL, false)] // Not downloading state = not stalled
    [InlineData(null, 0, 0UL, false)] // Null state = not stalled
    public void IsStalled_ReturnsCorrectValue(string? state, long downloadSpeed, ulong eta, bool expected)
    {
        // Arrange
        var downloadStatus = new DownloadStatus
        {
            State = state,
            DownloadSpeed = downloadSpeed,
            Eta = eta,
            Trackers = new List<Tracker>(),
            DownloadLocation = "/test/path"
        };
        var wrapper = new DelugeItemWrapper(downloadStatus);

        // Act
        var result = wrapper.IsStalled();

        // Assert
        result.ShouldBe(expected);
    }
}