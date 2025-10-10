using Cleanuparr.Domain.Entities.Deluge.Response;
using Cleanuparr.Infrastructure.Features.DownloadClient.Deluge;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.DownloadClient;

public class DelugeItemTests
{
    [Fact]
    public void Constructor_WithNullDownloadStatus_ThrowsArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new DelugeItem(null!));
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
        var wrapper = new DelugeItem(downloadStatus);

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
        var wrapper = new DelugeItem(downloadStatus);

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
        var wrapper = new DelugeItem(downloadStatus);

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
        var wrapper = new DelugeItem(downloadStatus);

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
        var wrapper = new DelugeItem(downloadStatus);

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
        var wrapper = new DelugeItem(downloadStatus);

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
        var wrapper = new DelugeItem(downloadStatus);

        // Act
        var result = wrapper.CompletionPercentage;

        // Assert
        result.ShouldBe(expectedPercentage);
    }

    [Fact]
    public void Trackers_WithValidUrls_ReturnsHostNames()
    {
        // Arrange
        var downloadStatus = new DownloadStatus
        {
            Trackers = new List<Tracker>
            {
                new() { Url = "http://tracker1.example.com:8080/announce" },
                new() { Url = "https://tracker2.example.com/announce" },
                new() { Url = "udp://tracker3.example.com:1337/announce" }
            },
            DownloadLocation = "/test/path"
        };
        var wrapper = new DelugeItem(downloadStatus);

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
        var downloadStatus = new DownloadStatus
        {
            Trackers = new List<Tracker>
            {
                new() { Url = "http://tracker1.example.com:8080/announce" },
                new() { Url = "https://tracker1.example.com/announce" },
                new() { Url = "udp://tracker1.example.com:1337/announce" }
            },
            DownloadLocation = "/test/path"
        };
        var wrapper = new DelugeItem(downloadStatus);

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
        var downloadStatus = new DownloadStatus
        {
            Trackers = new List<Tracker>
            {
                new() { Url = "http://valid.example.com/announce" },
                new() { Url = "invalid-url" },
                new() { Url = "" },
                new() { Url = null! }
            },
            DownloadLocation = "/test/path"
        };
        var wrapper = new DelugeItem(downloadStatus);

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
        var downloadStatus = new DownloadStatus
        {
            Trackers = new List<Tracker>(),
            DownloadLocation = "/test/path"
        };
        var wrapper = new DelugeItem(downloadStatus);

        // Act
        var result = wrapper.Trackers;

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public void Trackers_WithNullTrackers_ReturnsEmptyList()
    {
        // Arrange
        var downloadStatus = new DownloadStatus
        {
            Trackers = null!,
            DownloadLocation = "/test/path"
        };
        var wrapper = new DelugeItem(downloadStatus);

        // Act
        var result = wrapper.Trackers;

        // Assert
        result.ShouldBeEmpty();
    }
}