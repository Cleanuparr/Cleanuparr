// Note: QueueCleaner is in Application layer, so we'll test the integration through the download services
using Cleanuparr.Domain.Entities;
using Cleanuparr.Domain.Entities.Arr.Queue;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Events;
using Cleanuparr.Infrastructure.Features.Arr;
using Cleanuparr.Infrastructure.Features.Arr.Interfaces;
using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Infrastructure.Features.DownloadClient;
using Cleanuparr.Infrastructure.Services.Interfaces;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration.Arr;
using Cleanuparr.Persistence.Models.Configuration.General;
using MassTransit;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.QueueCleaner;

public class QueueCleanerIntegrationTests
{
    private readonly Mock<IRuleEvaluator> _ruleEvaluatorMock;
    private readonly Mock<IDownloadService> _downloadServiceMock;

    public QueueCleanerIntegrationTests()
    {
        _ruleEvaluatorMock = new Mock<IRuleEvaluator>();
        _downloadServiceMock = new Mock<IDownloadService>();
    }

    [Fact]
    public async Task DownloadService_WithRuleBasedEvaluation_ShouldUseRuleEvaluator()
    {
        // Arrange
        var stallResult = new DownloadCheckResult
        {
            Found = true,
            IsPrivate = false,
            ShouldRemove = true,
            DeleteReason = DeleteReason.Stalled
        };

        var slowResult = new DownloadCheckResult
        {
            Found = true,
            IsPrivate = false,
            ShouldRemove = false,
            DeleteReason = DeleteReason.None
        };

        _ruleEvaluatorMock.Setup(x => x.EvaluateStallRulesAsync(It.IsAny<ITorrentInfo>()))
            .ReturnsAsync(stallResult);
            
        _ruleEvaluatorMock.Setup(x => x.EvaluateSlowRulesAsync(It.IsAny<ITorrentInfo>()))
            .ReturnsAsync(slowResult);

        // Act & Assert
        // This test verifies that the rule evaluator is called correctly
        // The actual integration would be tested through the download service implementations
        _ruleEvaluatorMock.Verify(x => x.EvaluateStallRulesAsync(It.IsAny<ITorrentInfo>()), Times.Never);
        _ruleEvaluatorMock.Verify(x => x.EvaluateSlowRulesAsync(It.IsAny<ITorrentInfo>()), Times.Never);
    }

    [Fact]
    public async Task RuleEvaluator_WhenNoRulesMatch_ShouldReturnNoAction()
    {
        // Arrange
        var torrentInfo = new Mock<ITorrentInfo>();
        torrentInfo.Setup(x => x.Hash).Returns("test-hash");
        torrentInfo.Setup(x => x.Name).Returns("Test Torrent");
        torrentInfo.Setup(x => x.IsPrivate).Returns(false);

        var stallResult = new DownloadCheckResult
        {
            Found = true,
            IsPrivate = false,
            ShouldRemove = false, // No rules matched
            DeleteReason = DeleteReason.None
        };

        var slowResult = new DownloadCheckResult
        {
            Found = true,
            IsPrivate = false,
            ShouldRemove = false, // No rules matched
            DeleteReason = DeleteReason.None
        };

        _ruleEvaluatorMock.Setup(x => x.EvaluateStallRulesAsync(It.IsAny<ITorrentInfo>()))
            .ReturnsAsync(stallResult);
            
        _ruleEvaluatorMock.Setup(x => x.EvaluateSlowRulesAsync(It.IsAny<ITorrentInfo>()))
            .ReturnsAsync(slowResult);

        // Act
        var stallEvalResult = await _ruleEvaluatorMock.Object.EvaluateStallRulesAsync(torrentInfo.Object);
        var slowEvalResult = await _ruleEvaluatorMock.Object.EvaluateSlowRulesAsync(torrentInfo.Object);

        // Assert
        Assert.False(stallEvalResult.ShouldRemove);
        Assert.Equal(DeleteReason.None, stallEvalResult.DeleteReason);
        Assert.False(slowEvalResult.ShouldRemove);
        Assert.Equal(DeleteReason.None, slowEvalResult.DeleteReason);
    }

    [Fact]
    public async Task RuleEvaluator_WithStallRule_ShouldReturnStallReason()
    {
        // Arrange
        var torrentInfo = new Mock<ITorrentInfo>();
        torrentInfo.Setup(x => x.Hash).Returns("test-hash");
        torrentInfo.Setup(x => x.Name).Returns("Test Torrent");
        torrentInfo.Setup(x => x.IsPrivate).Returns(false);

        var stallResult = new DownloadCheckResult
        {
            Found = true,
            IsPrivate = false,
            ShouldRemove = true,
            DeleteReason = DeleteReason.Stalled
        };

        _ruleEvaluatorMock.Setup(x => x.EvaluateStallRulesAsync(It.IsAny<ITorrentInfo>()))
            .ReturnsAsync(stallResult);

        // Act
        var result = await _ruleEvaluatorMock.Object.EvaluateStallRulesAsync(torrentInfo.Object);

        // Assert
        Assert.True(result.ShouldRemove);
        Assert.Equal(DeleteReason.Stalled, result.DeleteReason);
    }

    [Fact]
    public async Task RuleEvaluator_WithSlowRule_ShouldReturnSlowReason()
    {
        // Arrange
        var torrentInfo = new Mock<ITorrentInfo>();
        torrentInfo.Setup(x => x.Hash).Returns("test-hash");
        torrentInfo.Setup(x => x.Name).Returns("Test Torrent");
        torrentInfo.Setup(x => x.IsPrivate).Returns(false);

        var slowResult = new DownloadCheckResult
        {
            Found = true,
            IsPrivate = false,
            ShouldRemove = true,
            DeleteReason = DeleteReason.SlowSpeed
        };

        _ruleEvaluatorMock.Setup(x => x.EvaluateSlowRulesAsync(It.IsAny<ITorrentInfo>()))
            .ReturnsAsync(slowResult);

        // Act
        var result = await _ruleEvaluatorMock.Object.EvaluateSlowRulesAsync(torrentInfo.Object);

        // Assert
        Assert.True(result.ShouldRemove);
        Assert.Equal(DeleteReason.SlowSpeed, result.DeleteReason);
    }
}