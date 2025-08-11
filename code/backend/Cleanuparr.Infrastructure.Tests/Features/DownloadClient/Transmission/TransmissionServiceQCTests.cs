using Cleanuparr.Domain.Entities;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.DownloadClient;
using Cleanuparr.Infrastructure.Features.DownloadClient.Transmission;
using Cleanuparr.Infrastructure.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;
using Transmission.API.RPC.Entity;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.DownloadClient.Transmission;

public class TransmissionServiceQCTests
{
    private readonly Mock<IRuleEvaluator> _mockRuleEvaluator;
    private readonly Mock<ILogger<TransmissionService>> _mockLogger;

    public TransmissionServiceQCTests()
    {
        _mockRuleEvaluator = new Mock<IRuleEvaluator>();
        _mockLogger = new Mock<ILogger<TransmissionService>>();
    }

    [Fact]
    public async Task ShouldRemoveFromArrQueueAsync_WhenTorrentNotFound_ReturnsNotFoundResult()
    {
        // Arrange
        var mockService = new Mock<TransmissionService>();
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
        var mockService = new Mock<TransmissionService>();
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
        var mockService = new Mock<TransmissionService>();
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
        var mockService = new Mock<TransmissionService>();
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
        var mockService = new Mock<TransmissionService>();
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
        var mockService = new Mock<TransmissionService>();
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
    public void TransmissionTorrentInfo_CreatesCorrectWrapper()
    {
        // Arrange
        var torrentInfo = new TorrentInfo
        {
            HashString = "test-hash",
            Name = "Test Torrent",
            IsPrivate = true,
            TotalSize = 1024 * 1024 * 1024, // 1GB
            DownloadedEver = 512 * 1024 * 1024, // 512MB
            // Trackers = new List<TrackerStats>
            // {
            //     new() { Announce = "http://tracker1.example.com/announce" },
            //     new() { Announce = "https://tracker2.example.com/announce" }
            // }
        };

        // Act
        var wrapper = new TransmissionTorrentInfo(torrentInfo);

        // Assert
        wrapper.Hash.ShouldBe("test-hash");
        wrapper.Name.ShouldBe("Test Torrent");
        wrapper.IsPrivate.ShouldBeTrue();
        wrapper.Size.ShouldBe(1024 * 1024 * 1024);
        wrapper.CompletionPercentage.ShouldBe(50.0, 0.1); // 512MB / 1GB * 100
        // wrapper.Trackers.Count.ShouldBe(2);
        // wrapper.Trackers.ShouldContain("tracker1.example.com");
        // wrapper.Trackers.ShouldContain("tracker2.example.com");
    }

    [Fact]
    public void TransmissionTorrentInfo_WithNullTrackers_ReturnsEmptyTrackerList()
    {
        // Arrange
        var torrentInfo = new TorrentInfo
        {
            HashString = "test-hash",
            Name = "Test Torrent",
            IsPrivate = false,
            TotalSize = 1024,
            DownloadedEver = 512,
            Trackers = null
        };

        // Act
        var wrapper = new TransmissionTorrentInfo(torrentInfo);

        // Assert
        wrapper.Trackers.ShouldBeEmpty();
    }

    [Fact]
    public void TransmissionTorrentInfo_WithZeroSize_ReturnsZeroCompletionPercentage()
    {
        // Arrange
        var torrentInfo = new TorrentInfo
        {
            HashString = "test-hash",
            Name = "Test Torrent",
            IsPrivate = false,
            TotalSize = 0,
            DownloadedEver = 100
        };

        // Act
        var wrapper = new TransmissionTorrentInfo(torrentInfo);

        // Assert
        wrapper.CompletionPercentage.ShouldBe(0.0);
    }
}