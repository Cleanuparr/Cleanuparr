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
            .Setup(x => x.GetMatchingStallRuleAsync(It.IsAny<ITorrentItem>()))
            .Returns(stallRule);

        strikerMock
            .Setup(x => x.StrikeAndCheckLimit(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ushort>(), StrikeType.Stalled))
            .ReturnsAsync(false);

        strikerMock
            .Setup(x => x.ResetStrikeAsync(It.IsAny<string>(), It.IsAny<string>(), StrikeType.Stalled))
            .Returns(Task.CompletedTask);

        long downloadedBytes = 0;

        var torrentMock = new Mock<ITorrentItem>();
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
            .Setup(x => x.GetMatchingStallRuleAsync(It.IsAny<ITorrentItem>()))
            .Returns((StallRule?)null);

        var torrentMock = CreateTorrentMock();

        var result = await evaluator.EvaluateStallRulesAsync(torrentMock.Object);
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
            .Setup(x => x.GetMatchingStallRuleAsync(It.IsAny<ITorrentItem>()))
            .Returns(stallRule);

        strikerMock
            .Setup(x => x.StrikeAndCheckLimit(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ushort>(), StrikeType.Stalled))
            .ReturnsAsync(false);

        var torrentMock = CreateTorrentMock();

        var result = await evaluator.EvaluateStallRulesAsync(torrentMock.Object);
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
            .Setup(x => x.GetMatchingStallRuleAsync(It.IsAny<ITorrentItem>()))
            .Returns(stallRule);

        strikerMock
            .Setup(x => x.StrikeAndCheckLimit(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ushort>(), StrikeType.Stalled))
            .ReturnsAsync(true);

        var torrentMock = CreateTorrentMock();

        var result = await evaluator.EvaluateStallRulesAsync(torrentMock.Object);
        Assert.True(result.ShouldRemove);
        Assert.Equal(DeleteReason.Stalled, result.Reason);

        strikerMock.Verify(x => x.StrikeAndCheckLimit("hash", "Example Torrent", (ushort)stallRule.MaxStrikes, StrikeType.Stalled), Times.Once);
    }

    [Fact]
    public async Task EvaluateStallRulesAsync_WhenStrikeThrows_ShouldHandleExceptionGracefully()
    {
        var ruleManagerMock = new Mock<IRuleManager>();
        var strikerMock = new Mock<IStriker>();
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var loggerMock = new Mock<ILogger<RuleEvaluator>>();

        var evaluator = new RuleEvaluator(ruleManagerMock.Object, strikerMock.Object, memoryCache, loggerMock.Object);

        var failingRule = CreateStallRule("Failing", resetOnProgress: false, maxStrikes: 4);

        ruleManagerMock
            .Setup(x => x.GetMatchingStallRuleAsync(It.IsAny<ITorrentItem>()))
            .Returns(failingRule);

        strikerMock
            .Setup(x => x.StrikeAndCheckLimit(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ushort>(), StrikeType.Stalled))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var torrentMock = CreateTorrentMock();

        var result = await evaluator.EvaluateStallRulesAsync(torrentMock.Object);
        Assert.False(result.ShouldRemove);

        strikerMock.Verify(x => x.StrikeAndCheckLimit(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ushort>(), StrikeType.Stalled), Times.Once);
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
            .Setup(x => x.GetMatchingSlowRuleAsync(It.IsAny<ITorrentItem>()))
            .Returns((SlowRule?)null);

        var torrentMock = CreateTorrentMock();

        var result = await evaluator.EvaluateSlowRulesAsync(torrentMock.Object);
        Assert.False(result.ShouldRemove);

        strikerMock.Verify(x => x.StrikeAndCheckLimit(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ushort>(), StrikeType.SlowTime), Times.Never);
    }

    [Fact]
    public async Task EvaluateSlowRulesAsync_WithMatchingRule_ShouldApplyStrikeWithoutRemoval()
    {
        var ruleManagerMock = new Mock<IRuleManager>();
        var strikerMock = new Mock<IStriker>();
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var loggerMock = new Mock<ILogger<RuleEvaluator>>();

        var evaluator = new RuleEvaluator(ruleManagerMock.Object, strikerMock.Object, memoryCache, loggerMock.Object);

        var slowRule = CreateSlowRule("Slow Apply", resetOnProgress: false, maxStrikes: 3, minSpeed: null, maxTimeHours: 1);

        ruleManagerMock
            .Setup(x => x.GetMatchingSlowRuleAsync(It.IsAny<ITorrentItem>()))
            .Returns(slowRule);

        strikerMock
            .Setup(x => x.StrikeAndCheckLimit(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ushort>(), StrikeType.SlowTime))
            .ReturnsAsync(false);

        var torrentMock = CreateTorrentMock();
        torrentMock.SetupGet(t => t.Eta).Returns(7200); // 2 hours in seconds, exceeds maxTimeHours of 1

        var result = await evaluator.EvaluateSlowRulesAsync(torrentMock.Object);
        Assert.False(result.ShouldRemove);

        strikerMock.Verify(x => x.StrikeAndCheckLimit("hash", "Example Torrent", (ushort)slowRule.MaxStrikes, StrikeType.SlowTime), Times.Once);
    }

    [Fact]
    public async Task EvaluateSlowRulesAsync_WhenStrikeLimitReached_ShouldMarkForRemoval()
    {
        var ruleManagerMock = new Mock<IRuleManager>();
        var strikerMock = new Mock<IStriker>();
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var loggerMock = new Mock<ILogger<RuleEvaluator>>();

        var evaluator = new RuleEvaluator(ruleManagerMock.Object, strikerMock.Object, memoryCache, loggerMock.Object);

        var slowRule = CreateSlowRule("Slow Remove", resetOnProgress: false, maxStrikes: 8, minSpeed: null, maxTimeHours: 1);

        ruleManagerMock
            .Setup(x => x.GetMatchingSlowRuleAsync(It.IsAny<ITorrentItem>()))
            .Returns(slowRule);

        strikerMock
            .Setup(x => x.StrikeAndCheckLimit(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ushort>(), StrikeType.SlowTime))
            .ReturnsAsync(true);

        var torrentMock = CreateTorrentMock();
        torrentMock.SetupGet(t => t.Eta).Returns(7200); // 2 hours in seconds, exceeds maxTimeHours of 1

        var result = await evaluator.EvaluateSlowRulesAsync(torrentMock.Object);
        Assert.True(result.ShouldRemove);
        Assert.Equal(DeleteReason.SlowTime, result.Reason);

        strikerMock.Verify(x => x.StrikeAndCheckLimit("hash", "Example Torrent", (ushort)slowRule.MaxStrikes, StrikeType.SlowTime), Times.Once);
    }

    [Fact]
    public async Task EvaluateSlowRulesAsync_WithProgress_ShouldResetAfterThreshold()
    {
        var ruleManagerMock = new Mock<IRuleManager>();
        var strikerMock = new Mock<IStriker>();
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var loggerMock = new Mock<ILogger<RuleEvaluator>>();

        var evaluator = new RuleEvaluator(ruleManagerMock.Object, strikerMock.Object, memoryCache, loggerMock.Object);

        var slowRule = CreateSlowRule("Slow Progress", resetOnProgress: true, maxStrikes: 4, minSpeed: null, maxTimeHours: 1);

        ruleManagerMock
            .Setup(x => x.GetMatchingSlowRuleAsync(It.IsAny<ITorrentItem>()))
            .Returns(slowRule);

        strikerMock
            .Setup(x => x.StrikeAndCheckLimit(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ushort>(), StrikeType.SlowTime))
            .ReturnsAsync(false);

        strikerMock
            .Setup(x => x.ResetStrikeAsync(It.IsAny<string>(), It.IsAny<string>(), StrikeType.SlowTime))
            .Returns(Task.CompletedTask);

        var torrentMock = CreateTorrentMock();
        torrentMock.SetupGet(t => t.Eta).Returns(7200); // 2 hours - exceeds maxTime, triggers strike

        // First call - torrent has high ETA, should strike
        await evaluator.EvaluateSlowRulesAsync(torrentMock.Object);
        strikerMock.Verify(x => x.StrikeAndCheckLimit("hash", "Example Torrent", (ushort)slowRule.MaxStrikes, StrikeType.SlowTime), Times.Once);
        strikerMock.Verify(x => x.ResetStrikeAsync(It.IsAny<string>(), It.IsAny<string>(), StrikeType.SlowTime), Times.Never);

        // Second call - torrent now has low ETA (condition not met), should reset
        torrentMock.SetupGet(t => t.Eta).Returns(1800); // 30 minutes - below maxTime
        await evaluator.EvaluateSlowRulesAsync(torrentMock.Object);
        strikerMock.Verify(x => x.ResetStrikeAsync("hash", "Example Torrent", StrikeType.SlowTime), Times.Once);
    }

    [Fact]
    public async Task EvaluateSlowRulesAsync_WhenStrikeThrows_ShouldPropagateException()
    {
        var ruleManagerMock = new Mock<IRuleManager>();
        var strikerMock = new Mock<IStriker>();
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var loggerMock = new Mock<ILogger<RuleEvaluator>>();

        var evaluator = new RuleEvaluator(ruleManagerMock.Object, strikerMock.Object, memoryCache, loggerMock.Object);

        var failingRule = CreateSlowRule("Failing Slow", resetOnProgress: false, maxStrikes: 4, minSpeed: null, maxTimeHours: 1);

        ruleManagerMock
            .Setup(x => x.GetMatchingSlowRuleAsync(It.IsAny<ITorrentItem>()))
            .Returns(failingRule);

        strikerMock
            .Setup(x => x.StrikeAndCheckLimit(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ushort>(), StrikeType.SlowTime))
            .ThrowsAsync(new InvalidOperationException("slow fail"));

        var torrentMock = CreateTorrentMock();
        torrentMock.SetupGet(t => t.Eta).Returns(7200); // 2 hours - exceeds maxTime

        // Should throw the exception, not handle it gracefully
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await evaluator.EvaluateSlowRulesAsync(torrentMock.Object));

        strikerMock.Verify(x => x.StrikeAndCheckLimit(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ushort>(), StrikeType.SlowTime), Times.Once);
    }

    [Fact]
    public async Task EvaluateSlowRulesAsync_WithSpeedBasedRule_ShouldUseSlowSpeedStrikeType()
    {
        var ruleManagerMock = new Mock<IRuleManager>();
        var strikerMock = new Mock<IStriker>();
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var loggerMock = new Mock<ILogger<RuleEvaluator>>();

        var evaluator = new RuleEvaluator(ruleManagerMock.Object, strikerMock.Object, memoryCache, loggerMock.Object);

        var slowRule = CreateSlowRule(
            name: "Speed Rule",
            resetOnProgress: false,
            maxStrikes: 3,
            minSpeed: "1 MB",
            maxTimeHours: 0);

        ruleManagerMock
            .Setup(x => x.GetMatchingSlowRuleAsync(It.IsAny<ITorrentItem>()))
            .Returns(slowRule);

        strikerMock
            .Setup(x => x.StrikeAndCheckLimit(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ushort>(), StrikeType.SlowSpeed))
            .ReturnsAsync(true);

        var torrentMock = CreateTorrentMock();
        torrentMock.SetupGet(t => t.DownloadSpeed).Returns(500_000); // 500 KB/s, below 1 MB/s threshold

        var result = await evaluator.EvaluateSlowRulesAsync(torrentMock.Object);
        Assert.True(result.ShouldRemove);
        Assert.Equal(DeleteReason.SlowSpeed, result.Reason);

        strikerMock.Verify(
            x => x.StrikeAndCheckLimit("hash", "Example Torrent", (ushort)slowRule.MaxStrikes, StrikeType.SlowSpeed),
            Times.Once);
        strikerMock.Verify(
            x => x.ResetStrikeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<StrikeType>()),
            Times.Never);
    }

    [Fact]
    public async Task EvaluateSlowRulesAsync_WithBothSpeedAndTimeConfigured_ShouldCheckBoth()
    {
        var ruleManagerMock = new Mock<IRuleManager>();
        var strikerMock = new Mock<IStriker>();
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var loggerMock = new Mock<ILogger<RuleEvaluator>>();

        var evaluator = new RuleEvaluator(ruleManagerMock.Object, strikerMock.Object, memoryCache, loggerMock.Object);

        var slowRule = CreateSlowRule(
            name: "Both Rule",
            resetOnProgress: false,
            maxStrikes: 2,
            minSpeed: "500 KB",
            maxTimeHours: 2);

        ruleManagerMock
            .Setup(x => x.GetMatchingSlowRuleAsync(It.IsAny<ITorrentItem>()))
            .Returns(slowRule);

        strikerMock
            .Setup(x => x.StrikeAndCheckLimit(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ushort>(), StrikeType.SlowSpeed))
            .ReturnsAsync(true);

        var torrentMock = CreateTorrentMock();
        torrentMock.SetupGet(t => t.DownloadSpeed).Returns(400_000); // 400 KB/s, below 500 KB/s threshold
        torrentMock.SetupGet(t => t.Eta).Returns(10800); // 3 hours - exceeds maxTime

        var result = await evaluator.EvaluateSlowRulesAsync(torrentMock.Object);
        Assert.True(result.ShouldRemove);
        Assert.Equal(DeleteReason.SlowSpeed, result.Reason);

        // Should strike for slow speed and return early (not check time)
        strikerMock.Verify(x => x.StrikeAndCheckLimit("hash", "Example Torrent", (ushort)slowRule.MaxStrikes, StrikeType.SlowSpeed), Times.Once);
        strikerMock.Verify(x => x.StrikeAndCheckLimit(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ushort>(), StrikeType.SlowTime), Times.Never);
    }

    [Fact]
    public async Task EvaluateSlowRulesAsync_WithNeitherSpeedNorTimeConfigured_ShouldNotStrike()
    {
        var ruleManagerMock = new Mock<IRuleManager>();
        var strikerMock = new Mock<IStriker>();
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var loggerMock = new Mock<ILogger<RuleEvaluator>>();

        var evaluator = new RuleEvaluator(ruleManagerMock.Object, strikerMock.Object, memoryCache, loggerMock.Object);

        // Neither minSpeed nor maxTime set (maxTimeHours = 0, minSpeed = null) => no strike
        var slowRule = CreateSlowRule(
            name: "Fallback Rule",
            resetOnProgress: false,
            maxStrikes: 1,
            minSpeed: null,
            maxTimeHours: 0);

        ruleManagerMock
            .Setup(x => x.GetMatchingSlowRuleAsync(It.IsAny<ITorrentItem>()))
            .Returns(slowRule);

        var torrentMock = CreateTorrentMock();

        var result = await evaluator.EvaluateSlowRulesAsync(torrentMock.Object);
        Assert.False(result.ShouldRemove);

        // No strikes should occur since neither condition is configured
        strikerMock.Verify(x => x.StrikeAndCheckLimit(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ushort>(), It.IsAny<StrikeType>()), Times.Never);
    }

    [Fact]
    public async Task EvaluateSlowRulesAsync_SpeedBasedRule_WithResetOnProgress_ShouldResetSlowSpeedStrikes()
    {
        var ruleManagerMock = new Mock<IRuleManager>();
        var strikerMock = new Mock<IStriker>();
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var loggerMock = new Mock<ILogger<RuleEvaluator>>();

        var evaluator = new RuleEvaluator(ruleManagerMock.Object, strikerMock.Object, memoryCache, loggerMock.Object);

        var slowRule = CreateSlowRule(
            name: "Speed Reset",
            resetOnProgress: true,
            maxStrikes: 3,
            minSpeed: "1 MB",
            maxTimeHours: 0);

        ruleManagerMock
            .Setup(x => x.GetMatchingSlowRuleAsync(It.IsAny<ITorrentItem>()))
            .Returns(slowRule);

        strikerMock
            .Setup(x => x.StrikeAndCheckLimit(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ushort>(), StrikeType.SlowSpeed))
            .ReturnsAsync(false);

        strikerMock
            .Setup(x => x.ResetStrikeAsync(It.IsAny<string>(), It.IsAny<string>(), StrikeType.SlowSpeed))
            .Returns(Task.CompletedTask);

        var torrentMock = CreateTorrentMock();
        torrentMock.SetupGet(t => t.DownloadSpeed).Returns(500_000); // 500 KB/s, below 1 MB/s threshold

        // First call - slow speed, should strike
        await evaluator.EvaluateSlowRulesAsync(torrentMock.Object);
        strikerMock.Verify(x => x.StrikeAndCheckLimit("hash", "Example Torrent", (ushort)slowRule.MaxStrikes, StrikeType.SlowSpeed), Times.Once);
        strikerMock.Verify(x => x.ResetStrikeAsync(It.IsAny<string>(), It.IsAny<string>(), StrikeType.SlowSpeed), Times.Never);

        // Second call - speed is now adequate, should reset
        torrentMock.SetupGet(t => t.DownloadSpeed).Returns(2_000_000); // 2 MB/s, above 1 MB/s threshold
        await evaluator.EvaluateSlowRulesAsync(torrentMock.Object);
        strikerMock.Verify(x => x.ResetStrikeAsync("hash", "Example Torrent", StrikeType.SlowSpeed), Times.Once);
    }

    private static Mock<ITorrentItem> CreateTorrentMock(
        Func<long>? downloadedBytesFactory = null,
        bool isPrivate = false,
        string hash = "hash",
        string name = "Example Torrent",
        double completionPercentage = 50,
        string size = "100 MB")
    {
        var torrentMock = new Mock<ITorrentItem>();
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

    private static SlowRule CreateSlowRule(
        string name,
        bool resetOnProgress,
        int maxStrikes,
        string? minSpeed = null,
        double maxTimeHours = 1)
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
            MaxTimeHours = maxTimeHours,
            MinSpeed = minSpeed ?? string.Empty,
            IgnoreAboveSize = string.Empty,
            DeletePrivateTorrentsFromClient = false,
        };
    }
}
