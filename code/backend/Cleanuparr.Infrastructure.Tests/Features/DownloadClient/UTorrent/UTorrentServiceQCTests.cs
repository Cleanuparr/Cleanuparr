using Cleanuparr.Domain.Entities;
using Cleanuparr.Domain.Entities.UTorrent.Response;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.DownloadClient;
using Cleanuparr.Infrastructure.Features.DownloadClient.UTorrent;
using Cleanuparr.Infrastructure.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.DownloadClient.UTorrent;

public class UTorrentServiceQCTests
{
    private readonly Mock<IRuleEvaluator> _mockRuleEvaluator;
    private readonly Mock<ILogger<UTorrentService>> _mockLogger;

    public UTorrentServiceQCTests()
    {
        _mockRuleEvaluator = new Mock<IRuleEvaluator>();
        _mockLogger = new Mock<ILogger<UTorrentService>>();
    }

    [Fact]
    public async Task ShouldRemoveFromArrQueueAsync_WhenTorrentNotFound_ReturnsNotFoundResult()
    {
        // Arrange
        var mockService = new Mock<UTorrentService>();
        var hash = "test-hash";
        var ignoredDownloads = new List<string>();
        
        mockService.Setup(s => s.ShouldRemoveFromArrQueueAsync(hash, ignoredDownloads))
                  .ReturnsAsync(new DownloadCheckResult { Found = false });

        // Act
        var result = await mockService.Object.ShouldRemoveFromArrQueueAsync(hash, ignoredDownloads);

        // Assert
        result.Found.ShouldBeFalse();
        result.ShouldRemove.ShouldBeFalse();
    }

    [Fact]
    public async Task ShouldRemoveFromArrQueueAsync_WhenAllFilesSkipped_ReturnsRemoveWithCorrectReason()
    {
        // Arrange
        var mockService = new Mock<UTorrentService>();
        var hash = "test-hash";
        var ignoredDownloads = new List<string>();
        
        var result = new DownloadCheckResult
        {
            Found = true,
            ShouldRemove = true,
            DeleteReason = DeleteReason.AllFilesSkipped
        };
        
        mockService.Setup(s => s.ShouldRemoveFromArrQueueAsync(hash, ignoredDownloads))
                  .ReturnsAsync(result);

        // Act
        var actualResult = await mockService.Object.ShouldRemoveFromArrQueueAsync(hash, ignoredDownloads);

        // Assert
        actualResult.ShouldRemove.ShouldBeTrue();
        actualResult.DeleteReason.ShouldBe(DeleteReason.AllFilesSkipped);
    }

    [Fact]
    public async Task ShouldRemoveFromArrQueueAsync_WhenStallRuleMatches_ReturnsRemoveWithStallReason()
    {
        // Arrange
        var mockService = new Mock<UTorrentService>();
        var hash = "test-hash";
        var ignoredDownloads = new List<string>();
        
        var result = new DownloadCheckResult
        {
            Found = true,
            ShouldRemove = true,
            DeleteReason = DeleteReason.Stalled
        };
        
        mockService.Setup(s => s.ShouldRemoveFromArrQueueAsync(hash, ignoredDownloads))
                  .ReturnsAsync(result);

        // Act
        var actualResult = await mockService.Object.ShouldRemoveFromArrQueueAsync(hash, ignoredDownloads);

        // Assert
        actualResult.ShouldRemove.ShouldBeTrue();
        actualResult.DeleteReason.ShouldBe(DeleteReason.Stalled);
    }

    [Fact]
    public async Task ShouldRemoveFromArrQueueAsync_WhenSlowRuleMatches_ReturnsRemoveWithSlowReason()
    {
        // Arrange
        var mockService = new Mock<UTorrentService>();
        var hash = "test-hash";
        var ignoredDownloads = new List<string>();
        
        var result = new DownloadCheckResult
        {
            Found = true,
            ShouldRemove = true,
            DeleteReason = DeleteReason.SlowSpeed
        };
        
        mockService.Setup(s => s.ShouldRemoveFromArrQueueAsync(hash, ignoredDownloads))
                  .ReturnsAsync(result);

        // Act
        var actualResult = await mockService.Object.ShouldRemoveFromArrQueueAsync(hash, ignoredDownloads);

        // Assert
        actualResult.ShouldRemove.ShouldBeTrue();
        actualResult.DeleteReason.ShouldBe(DeleteReason.SlowSpeed);
    }

    [Fact]
    public async Task ShouldRemoveFromArrQueueAsync_WhenNoRulesMatch_ReturnsNoRemoval()
    {
        // Arrange
        var mockService = new Mock<UTorrentService>();
        var hash = "test-hash";
        var ignoredDownloads = new List<string>();
        
        var result = new DownloadCheckResult
        {
            Found = true,
            ShouldRemove = false
        };
        
        mockService.Setup(s => s.ShouldRemoveFromArrQueueAsync(hash, ignoredDownloads))
                  .ReturnsAsync(result);

        // Act
        var actualResult = await mockService.Object.ShouldRemoveFromArrQueueAsync(hash, ignoredDownloads);

        // Assert
        actualResult.Found.ShouldBeTrue();
        actualResult.ShouldRemove.ShouldBeFalse();
    }

    [Fact]
    public async Task ShouldRemoveFromArrQueueAsync_WhenTorrentIsIgnored_ReturnsNoRemoval()
    {
        // Arrange
        var mockService = new Mock<UTorrentService>();
        var hash = "test-hash";
        var ignoredDownloads = new List<string> { "Test Torrent" };
        
        var result = new DownloadCheckResult
        {
            Found = true,
            ShouldRemove = false
        };
        
        mockService.Setup(s => s.ShouldRemoveFromArrQueueAsync(hash, ignoredDownloads))
                  .ReturnsAsync(result);

        // Act
        var actualResult = await mockService.Object.ShouldRemoveFromArrQueueAsync(hash, ignoredDownloads);

        // Assert
        actualResult.Found.ShouldBeTrue();
        actualResult.ShouldRemove.ShouldBeFalse();
    }

    [Fact]
    public void UTorrentTorrentInfo_CreatesCorrectWrapper()
    {
        // Arrange
        var torrentItem = new UTorrentItem
        {
            Hash = "test-hash",
            Name = "Test Torrent",
            Size = 1024 * 1024 * 1024, // 1GB
            Progress = 500 // 50% in permille (500/1000)
        };

        var torrentProperties = new UTorrentProperties
        {
            Pex = -1, // Private torrent (PEX not allowed)
            Trackers = "http://tracker1.example.com/announce\r\nhttps://tracker2.example.com/announce"
        };

        // Act
        var wrapper = new UTorrentTorrentInfo(torrentItem, torrentProperties);

        // Assert
        wrapper.Hash.ShouldBe("test-hash");
        wrapper.Name.ShouldBe("Test Torrent");
        wrapper.IsPrivate.ShouldBeTrue();
        wrapper.Size.ShouldBe(1024 * 1024 * 1024);
        wrapper.CompletionPercentage.ShouldBe(50.0, 0.1); // 500/10 = 50%
        wrapper.Trackers.Count.ShouldBe(2);
        wrapper.Trackers.ShouldContain("tracker1.example.com");
        wrapper.Trackers.ShouldContain("tracker2.example.com");
    }

    [Fact]
    public void UTorrentTorrentInfo_WithNullTorrentItem_ThrowsArgumentNullException()
    {
        // Arrange
        var torrentProperties = new UTorrentProperties();

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new UTorrentTorrentInfo(null!, torrentProperties));
    }

    [Fact]
    public void UTorrentTorrentInfo_WithNullTorrentProperties_ThrowsArgumentNullException()
    {
        // Arrange
        var torrentItem = new UTorrentItem();

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new UTorrentTorrentInfo(torrentItem, null!));
    }

    [Fact]
    public void UTorrentTorrentInfo_WithEmptyTrackerList_ReturnsEmptyTrackers()
    {
        // Arrange
        var torrentItem = new UTorrentItem
        {
            Hash = "test-hash",
            Name = "Test Torrent",
            Size = 1024,
            Progress = 0
        };

        var torrentProperties = new UTorrentProperties
        {
            Pex = 1, // Public torrent (PEX allowed)
            Trackers = ""
        };

        // Act
        var wrapper = new UTorrentTorrentInfo(torrentItem, torrentProperties);

        // Assert
        wrapper.Trackers.ShouldBeEmpty();
    }

    [Fact]
    public void UTorrentTorrentInfo_WithInvalidTrackerUrls_SkipsInvalidEntries()
    {
        // Arrange
        var torrentItem = new UTorrentItem
        {
            Hash = "test-hash",
            Name = "Test Torrent",
            Size = 1024,
            Progress = 0
        };

        var torrentProperties = new UTorrentProperties
        {
            Pex = 1, // Public torrent (PEX allowed)
            Trackers = "http://valid.example.com/announce\r\ninvalid-url\r\n\r\nhttps://another-valid.example.com/announce"
        };

        // Act
        var wrapper = new UTorrentTorrentInfo(torrentItem, torrentProperties);

        // Assert
        wrapper.Trackers.Count.ShouldBe(2);
        wrapper.Trackers.ShouldContain("valid.example.com");
        wrapper.Trackers.ShouldContain("another-valid.example.com");
    }
}