using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cleanuparr.Domain.Entities;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.DownloadClient;
using Cleanuparr.Infrastructure.Features.ItemStriker;
using Cleanuparr.Infrastructure.Services;
using Cleanuparr.Infrastructure.Services.Interfaces;
using Cleanuparr.Persistence.Models.Configuration.QueueCleaner;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Services;

public class RuleEvaluatorTests
{
    [Fact]
    public async Task ResetStrikes_ShouldRespectMinimumProgressThreshold()
    {
        var ruleManagerMock = new Mock<IRuleManager>();
        var strikerMock = new Mock<IStriker>();
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var loggerMock = new Mock<ILogger<RuleEvaluator>>();

        var evaluator = new RuleEvaluator(ruleManagerMock.Object, strikerMock.Object, memoryCache, loggerMock.Object);

        var stallRule = new StallRule
        {
            Id = Guid.NewGuid(),
            QueueCleanerConfigId = Guid.NewGuid(),
            Name = "Stall Rule",
            Enabled = true,
            MaxStrikes = 3,
            PrivacyType = TorrentPrivacyType.Public,
            MinCompletionPercentage = 0,
            MaxCompletionPercentage = 100,
            ResetStrikesOnProgress = true,
            MinimumProgress = "10 MB",
            DeletePrivateTorrentsFromClient = false,
        };

        ruleManagerMock
            .Setup(x => x.GetMatchingRulesAsync<StallRule>(It.IsAny<ITorrentInfo>()))
            .ReturnsAsync(new List<StallRule> { stallRule });

        strikerMock
            .Setup(x => x.StrikeAndCheckLimit(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ushort>(), StrikeType.Stalled))
            .ReturnsAsync(false);

        strikerMock
            .Setup(x => x.ResetStrikeAsync(It.IsAny<string>(), It.IsAny<string>(), StrikeType.Stalled))
            .Returns(Task.CompletedTask);

        long downloadedBytes = 0;

        var torrentMock = new Mock<ITorrentInfo>();
        torrentMock.SetupGet(t => t.Hash).Returns("hash");
        torrentMock.SetupGet(t => t.Name).Returns("Example Torrent");
        torrentMock.SetupGet(t => t.IsPrivate).Returns(false);
        torrentMock.SetupGet(t => t.Size).Returns(ByteSize.Parse("100 MB").Bytes);
        torrentMock.SetupGet(t => t.CompletionPercentage).Returns(50);
        torrentMock.SetupGet(t => t.Trackers).Returns(Array.Empty<string>());
        torrentMock.SetupGet(t => t.DownloadedBytes).Returns(() => downloadedBytes);

        // Seed cache with initial observation (no reset expected)
        await evaluator.EvaluateStallRulesAsync(torrentMock.Object);
        strikerMock.Verify(x => x.ResetStrikeAsync(It.IsAny<string>(), It.IsAny<string>(), StrikeType.Stalled), Times.Never);

        // Progress below threshold should not reset strikes
        downloadedBytes = ByteSize.Parse("1 MB").Bytes;
        await evaluator.EvaluateStallRulesAsync(torrentMock.Object);
        strikerMock.Verify(x => x.ResetStrikeAsync(It.IsAny<string>(), It.IsAny<string>(), StrikeType.Stalled), Times.Never);

        // Progress beyond threshold should trigger reset
        downloadedBytes = ByteSize.Parse("12 MB").Bytes;
        await evaluator.EvaluateStallRulesAsync(torrentMock.Object);
        strikerMock.Verify(x => x.ResetStrikeAsync(It.IsAny<string>(), It.IsAny<string>(), StrikeType.Stalled), Times.Once);
    }

    [Fact]
    public async Task EvaluateStallRulesAsync_NoMatchingRules_ShouldReturnFoundWithoutRemoval()
    {
        var ruleManagerMock = new Mock<IRuleManager>();
        var strikerMock = new Mock<IStriker>();
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var loggerMock = new Mock<ILogger<RuleEvaluator>>();

        var evaluator = new RuleEvaluator(ruleManagerMock.Object, strikerMock.Object, memoryCache, loggerMock.Object);

        ruleManagerMock
            .Setup(x => x.GetMatchingRulesAsync<StallRule>(It.IsAny<ITorrentInfo>()))
            .ReturnsAsync(new List<StallRule>());

        var torrentMock = CreateTorrentMock();

        var result = await evaluator.EvaluateStallRulesAsync(torrentMock.Object);

        Assert.True(result.Found);
        Assert.False(result.ShouldRemove);
        strikerMock.Verify(x => x.StrikeAndCheckLimit(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ushort>(), StrikeType.Stalled), Times.Never);
    }

    [Fact]
    public async Task EvaluateStallRulesAsync_WithMatchingRule_ShouldApplyStrikeWithoutRemoval()
    {
        var ruleManagerMock = new Mock<IRuleManager>();
        var strikerMock = new Mock<IStriker>();
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var loggerMock = new Mock<ILogger<RuleEvaluator>>();

        var evaluator = new RuleEvaluator(ruleManagerMock.Object, strikerMock.Object, memoryCache, loggerMock.Object);

        var stallRule = CreateStallRule("Stall Apply", resetOnProgress: false, maxStrikes: 5);

        ruleManagerMock
            .Setup(x => x.GetMatchingRulesAsync<StallRule>(It.IsAny<ITorrentInfo>()))
            .ReturnsAsync(new List<StallRule> { stallRule });

        strikerMock
            .Setup(x => x.StrikeAndCheckLimit(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ushort>(), StrikeType.Stalled))
            .ReturnsAsync(false);

        var torrentMock = CreateTorrentMock();

        var result = await evaluator.EvaluateStallRulesAsync(torrentMock.Object);

        Assert.True(result.Found);
        Assert.False(result.ShouldRemove);
        strikerMock.Verify(x => x.StrikeAndCheckLimit("hash", "Example Torrent", (ushort)stallRule.MaxStrikes, StrikeType.Stalled), Times.Once);
        strikerMock.Verify(x => x.ResetStrikeAsync(It.IsAny<string>(), It.IsAny<string>(), StrikeType.Stalled), Times.Never);
    }

    [Fact]
    public async Task EvaluateStallRulesAsync_WhenStrikeLimitReached_ShouldMarkForRemoval()
    {
        var ruleManagerMock = new Mock<IRuleManager>();
        var strikerMock = new Mock<IStriker>();
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var loggerMock = new Mock<ILogger<RuleEvaluator>>();

        var evaluator = new RuleEvaluator(ruleManagerMock.Object, strikerMock.Object, memoryCache, loggerMock.Object);

        var stallRule = CreateStallRule("Stall Remove", resetOnProgress: false, maxStrikes: 6);

        ruleManagerMock
            .Setup(x => x.GetMatchingRulesAsync<StallRule>(It.IsAny<ITorrentInfo>()))
            .ReturnsAsync(new List<StallRule> { stallRule });

        strikerMock
            .Setup(x => x.StrikeAndCheckLimit(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ushort>(), StrikeType.Stalled))
            .ReturnsAsync(true);

        var torrentMock = CreateTorrentMock();

        var result = await evaluator.EvaluateStallRulesAsync(torrentMock.Object);

        Assert.True(result.ShouldRemove);
        Assert.Equal(DeleteReason.Stalled, result.DeleteReason);
        strikerMock.Verify(x => x.StrikeAndCheckLimit("hash", "Example Torrent", (ushort)stallRule.MaxStrikes, StrikeType.Stalled), Times.Once);
    }

    [Fact]
    public async Task EvaluateStallRulesAsync_WhenStrikeThrows_ShouldContinueWithNextRule()
    {
        var ruleManagerMock = new Mock<IRuleManager>();
        var strikerMock = new Mock<IStriker>();
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var loggerMock = new Mock<ILogger<RuleEvaluator>>();

        var evaluator = new RuleEvaluator(ruleManagerMock.Object, strikerMock.Object, memoryCache, loggerMock.Object);

        var failingRule = CreateStallRule("Failing", resetOnProgress: false, maxStrikes: 4);
        var succeedingRule = CreateStallRule("Fallback", resetOnProgress: false, maxStrikes: 4);

        ruleManagerMock
            .Setup(x => x.GetMatchingRulesAsync<StallRule>(It.IsAny<ITorrentInfo>()))
            .ReturnsAsync(new List<StallRule> { failingRule, succeedingRule });

        strikerMock
            .SetupSequence(x => x.StrikeAndCheckLimit(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ushort>(), StrikeType.Stalled))
            .ThrowsAsync(new InvalidOperationException("boom"))
            .ReturnsAsync(true);

        var torrentMock = CreateTorrentMock();

        var result = await evaluator.EvaluateStallRulesAsync(torrentMock.Object);

        Assert.True(result.ShouldRemove);
        Assert.Equal(DeleteReason.Stalled, result.DeleteReason);
        strikerMock.Verify(x => x.StrikeAndCheckLimit(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ushort>(), StrikeType.Stalled), Times.Exactly(2));
    }

    [Fact]
    public async Task EvaluateSlowRulesAsync_NoMatchingRules_ShouldReturnFoundWithoutRemoval()
    {
        var ruleManagerMock = new Mock<IRuleManager>();
        var strikerMock = new Mock<IStriker>();
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var loggerMock = new Mock<ILogger<RuleEvaluator>>();

        var evaluator = new RuleEvaluator(ruleManagerMock.Object, strikerMock.Object, memoryCache, loggerMock.Object);

        ruleManagerMock
            .Setup(x => x.GetMatchingRulesAsync<SlowRule>(It.IsAny<ITorrentInfo>()))
            .ReturnsAsync(new List<SlowRule>());

        var torrentMock = CreateTorrentMock();

        var result = await evaluator.EvaluateSlowRulesAsync(torrentMock.Object);

        Assert.True(result.Found);
        Assert.False(result.ShouldRemove);
        strikerMock.Verify(x => x.StrikeAndCheckLimit(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ushort>(), StrikeType.SlowSpeed), Times.Never);
    }

    [Fact]
    public async Task EvaluateSlowRulesAsync_WithMatchingRule_ShouldApplyStrikeWithoutRemoval()
    {
        var ruleManagerMock = new Mock<IRuleManager>();
        var strikerMock = new Mock<IStriker>();
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var loggerMock = new Mock<ILogger<RuleEvaluator>>();

        var evaluator = new RuleEvaluator(ruleManagerMock.Object, strikerMock.Object, memoryCache, loggerMock.Object);

        var slowRule = CreateSlowRule("Slow Apply", resetOnProgress: false, maxStrikes: 3);

        ruleManagerMock
            .Setup(x => x.GetMatchingRulesAsync<SlowRule>(It.IsAny<ITorrentInfo>()))
            .ReturnsAsync(new List<SlowRule> { slowRule });

        strikerMock
            .Setup(x => x.StrikeAndCheckLimit(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ushort>(), StrikeType.SlowSpeed))
            .ReturnsAsync(false);

        var torrentMock = CreateTorrentMock();

        var result = await evaluator.EvaluateSlowRulesAsync(torrentMock.Object);

        Assert.True(result.Found);
        Assert.False(result.ShouldRemove);
        strikerMock.Verify(x => x.StrikeAndCheckLimit("hash", "Example Torrent", (ushort)slowRule.MaxStrikes, StrikeType.SlowSpeed), Times.Once);
    }

    [Fact]
    public async Task EvaluateSlowRulesAsync_WhenStrikeLimitReached_ShouldMarkForRemoval()
    {
        var ruleManagerMock = new Mock<IRuleManager>();
        var strikerMock = new Mock<IStriker>();
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var loggerMock = new Mock<ILogger<RuleEvaluator>>();

        var evaluator = new RuleEvaluator(ruleManagerMock.Object, strikerMock.Object, memoryCache, loggerMock.Object);

        var slowRule = CreateSlowRule("Slow Remove", resetOnProgress: false, maxStrikes: 8);

        ruleManagerMock
            .Setup(x => x.GetMatchingRulesAsync<SlowRule>(It.IsAny<ITorrentInfo>()))
            .ReturnsAsync(new List<SlowRule> { slowRule });

        strikerMock
            .Setup(x => x.StrikeAndCheckLimit(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ushort>(), StrikeType.SlowSpeed))
            .ReturnsAsync(true);

        var torrentMock = CreateTorrentMock();

        var result = await evaluator.EvaluateSlowRulesAsync(torrentMock.Object);

        Assert.True(result.ShouldRemove);
        Assert.Equal(DeleteReason.SlowSpeed, result.DeleteReason);
        strikerMock.Verify(x => x.StrikeAndCheckLimit("hash", "Example Torrent", (ushort)slowRule.MaxStrikes, StrikeType.SlowSpeed), Times.Once);
    }

    [Fact]
    public async Task EvaluateSlowRulesAsync_WithProgress_ShouldResetAfterThreshold()
    {
        var ruleManagerMock = new Mock<IRuleManager>();
        var strikerMock = new Mock<IStriker>();
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var loggerMock = new Mock<ILogger<RuleEvaluator>>();

        var evaluator = new RuleEvaluator(ruleManagerMock.Object, strikerMock.Object, memoryCache, loggerMock.Object);

        var slowRule = CreateSlowRule("Slow Progress", resetOnProgress: true, maxStrikes: 4);

        ruleManagerMock
            .Setup(x => x.GetMatchingRulesAsync<SlowRule>(It.IsAny<ITorrentInfo>()))
            .ReturnsAsync(new List<SlowRule> { slowRule });

        strikerMock
            .Setup(x => x.StrikeAndCheckLimit(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ushort>(), StrikeType.SlowSpeed))
            .ReturnsAsync(false);

        long downloadedBytes = 0;
        var torrentMock = CreateTorrentMock(downloadedBytesFactory: () => downloadedBytes);

        await evaluator.EvaluateSlowRulesAsync(torrentMock.Object);
        strikerMock.Verify(x => x.ResetStrikeAsync(It.IsAny<string>(), It.IsAny<string>(), StrikeType.SlowSpeed), Times.Never);

        downloadedBytes = ByteSize.Parse("5 MB").Bytes;
        await evaluator.EvaluateSlowRulesAsync(torrentMock.Object);
        strikerMock.Verify(x => x.ResetStrikeAsync("hash", "Example Torrent", StrikeType.SlowSpeed), Times.Once);

        downloadedBytes = ByteSize.Parse("1 MB").Bytes;
        await evaluator.EvaluateSlowRulesAsync(torrentMock.Object);
        strikerMock.Verify(x => x.ResetStrikeAsync("hash", "Example Torrent", StrikeType.SlowSpeed), Times.Once);
    }

    [Fact]
    public async Task EvaluateSlowRulesAsync_WhenStrikeThrows_ShouldContinueWithNextRule()
    {
        var ruleManagerMock = new Mock<IRuleManager>();
        var strikerMock = new Mock<IStriker>();
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var loggerMock = new Mock<ILogger<RuleEvaluator>>();

        var evaluator = new RuleEvaluator(ruleManagerMock.Object, strikerMock.Object, memoryCache, loggerMock.Object);

        var failingRule = CreateSlowRule("Failing Slow", resetOnProgress: false, maxStrikes: 4);
        var succeedingRule = CreateSlowRule("Fallback Slow", resetOnProgress: false, maxStrikes: 5);

        ruleManagerMock
            .Setup(x => x.GetMatchingRulesAsync<SlowRule>(It.IsAny<ITorrentInfo>()))
            .ReturnsAsync(new List<SlowRule> { failingRule, succeedingRule });

        strikerMock
            .SetupSequence(x => x.StrikeAndCheckLimit(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ushort>(), StrikeType.SlowSpeed))
            .ThrowsAsync(new InvalidOperationException("slow fail"))
            .ReturnsAsync(false);

        var torrentMock = CreateTorrentMock();

        var result = await evaluator.EvaluateSlowRulesAsync(torrentMock.Object);

        Assert.True(result.Found);
        Assert.False(result.ShouldRemove);
        strikerMock.Verify(x => x.StrikeAndCheckLimit(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ushort>(), StrikeType.SlowSpeed), Times.Exactly(2));
    }

    private static Mock<ITorrentInfo> CreateTorrentMock(
        Func<long>? downloadedBytesFactory = null,
        bool isPrivate = false,
        string hash = "hash",
        string name = "Example Torrent",
        double completionPercentage = 50,
        string size = "100 MB")
    {
        var torrentMock = new Mock<ITorrentInfo>();
        torrentMock.SetupGet(t => t.Hash).Returns(hash);
        torrentMock.SetupGet(t => t.Name).Returns(name);
        torrentMock.SetupGet(t => t.IsPrivate).Returns(isPrivate);
        torrentMock.SetupGet(t => t.CompletionPercentage).Returns(completionPercentage);
        torrentMock.SetupGet(t => t.Size).Returns(ByteSize.Parse(size).Bytes);
        torrentMock.SetupGet(t => t.Trackers).Returns(Array.Empty<string>());
        torrentMock.SetupGet(t => t.DownloadedBytes).Returns(() => downloadedBytesFactory?.Invoke() ?? 0);
        return torrentMock;
    }

    private static StallRule CreateStallRule(string name, bool resetOnProgress, int maxStrikes, string? minimumProgress = null)
    {
        return new StallRule
        {
            Id = Guid.NewGuid(),
            QueueCleanerConfigId = Guid.NewGuid(),
            Name = name,
            Enabled = true,
            MaxStrikes = maxStrikes,
            PrivacyType = TorrentPrivacyType.Public,
            MinCompletionPercentage = 0,
            MaxCompletionPercentage = 100,
            ResetStrikesOnProgress = resetOnProgress,
            MinimumProgress = minimumProgress,
            DeletePrivateTorrentsFromClient = false,
        };
    }

    private static SlowRule CreateSlowRule(string name, bool resetOnProgress, int maxStrikes)
    {
        return new SlowRule
        {
            Id = Guid.NewGuid(),
            QueueCleanerConfigId = Guid.NewGuid(),
            Name = name,
            Enabled = true,
            MaxStrikes = maxStrikes,
            PrivacyType = TorrentPrivacyType.Public,
            MinCompletionPercentage = 0,
            MaxCompletionPercentage = 100,
            ResetStrikesOnProgress = resetOnProgress,
            MaxTimeHours = 1,
            MinSpeed = string.Empty,
            MaxTime = 0,
            IgnoreAboveSize = string.Empty,
            DeletePrivateTorrentsFromClient = false,
        };
    }
}
