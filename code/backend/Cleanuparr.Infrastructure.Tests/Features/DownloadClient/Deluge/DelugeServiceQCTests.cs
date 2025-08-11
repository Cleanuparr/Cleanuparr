using Cleanuparr.Domain.Entities;
using Cleanuparr.Domain.Entities.Deluge.Response;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.DownloadClient;
using Cleanuparr.Infrastructure.Features.DownloadClient.Deluge;
using Cleanuparr.Infrastructure.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.DownloadClient.Deluge;

public class DelugeServiceQCTests
{
    private readonly Mock<IRuleEvaluator> _mockRuleEvaluator;
    private readonly Mock<ILogger<DelugeService>> _mockLogger;

    public DelugeServiceQCTests()
    {
        _mockRuleEvaluator = new Mock<IRuleEvaluator>();
        _mockLogger = new Mock<ILogger<DelugeService>>();
    }

    [Fact]
    public async Task ShouldRemoveFromArrQueueAsync_WhenTorrentNotFound_ReturnsNotFoundResult()
    {
        // Arrange
        var mockService = new Mock<DelugeService>();
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
        var mockService = new Mock<DelugeService>();
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
        var mockService = new Mock<DelugeService>();
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
        var mockService = new Mock<DelugeService>();
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
        var mockService = new Mock<DelugeService>();
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
        var mockService = new Mock<DelugeService>();
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
    public void DelugeTorrentInfo_CreatesCorrectWrapper()
    {
        // Arrange
        var downloadStatus = new DownloadStatus
        {
            Hash = "test-hash",
            Name = "Test Torrent",
            Private = true,
            Size = 1024 * 1024 * 1024, // 1GB
            TotalDone = 512 * 1024 * 1024, // 512MB
            Trackers = new List<Tracker>
            {
                new() { Url = "http://tracker1.example.com/announce" },
                new() { Url = "https://tracker2.example.com/announce" }
            },
            DownloadLocation = "/downloads"
        };

        // Act
        var torrentInfo = new DelugeTorrentInfo(downloadStatus);

        // Assert
        torrentInfo.Hash.ShouldBe("test-hash");
        torrentInfo.Name.ShouldBe("Test Torrent");
        torrentInfo.IsPrivate.ShouldBeTrue();
        torrentInfo.Size.ShouldBe(1024 * 1024 * 1024);
        torrentInfo.CompletionPercentage.ShouldBe(50.0, 0.1); // 512MB / 1GB * 100
        torrentInfo.Trackers.Count.ShouldBe(2);
        torrentInfo.Trackers.ShouldContain("tracker1.example.com");
        torrentInfo.Trackers.ShouldContain("tracker2.example.com");
    }
}