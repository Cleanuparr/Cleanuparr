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
}