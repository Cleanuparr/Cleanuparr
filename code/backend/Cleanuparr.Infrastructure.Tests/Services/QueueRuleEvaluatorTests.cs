using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cleanuparr.Domain.Entities;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.DownloadClient;
using Cleanuparr.Infrastructure.Features.ItemStriker;
using Cleanuparr.Infrastructure.Services;
using Cleanuparr.Infrastructure.Services.Interfaces;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration.QueueCleaner;
using Cleanuparr.Persistence.Models.State;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Services;

public class QueueRuleEvaluatorTests : IDisposable
{
    private readonly EventsContext _context;

    public QueueRuleEvaluatorTests()
    {
        _context = CreateInMemoryEventsContext();
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    private static EventsContext CreateInMemoryEventsContext()
    {
        var options = new DbContextOptionsBuilder<EventsContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new EventsContext(options);
    }

    [Fact]
    public async Task ResetStrikes_ShouldRespectMinimumProgressThreshold()
    {
        // Arrange
        var ruleManager = Substitute.For<IQueueRuleManager>();
        var striker = Substitute.For<IStriker>();
        var logger = Substitute.For<ILogger<QueueRuleEvaluator>>();
        var context = CreateInMemoryEventsContext();

        var evaluator = new QueueRuleEvaluator(ruleManager, striker, context, logger);

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

        ruleManager
            .GetMatchingStallRule(Arg.Any<ITorrentItemWrapper>())
            .Returns(stallRule);

        striker
            .StrikeAndCheckLimit(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ushort>(), StrikeType.Stalled, Arg.Any<long?>())
            .Returns(false);

        striker
            .ResetStrikeAsync(Arg.Any<string>(), Arg.Any<string>(), StrikeType.Stalled)
            .Returns(Task.CompletedTask);

        long downloadedBytes = 0;

        var torrent = Substitute.For<ITorrentItemWrapper>();
        torrent.Hash.Returns("hash");
        torrent.Name.Returns("Example Torrent");
        torrent.IsPrivate.Returns(false);
        torrent.Size.Returns(ByteSize.Parse("100 MB").Bytes);
        torrent.CompletionPercentage.Returns(50);
        torrent.DownloadedBytes.Returns(callInfo => downloadedBytes);

        // Seed database with a DownloadItem and initial strike (simulating first observation at 0 bytes)
        var downloadItem = new DownloadItem { DownloadId = "hash", Title = "Example Torrent" };
        context.DownloadItems.Add(downloadItem);
        await context.SaveChangesAsync();

        var initialStrike = new Strike { DownloadItemId = downloadItem.Id, Type = StrikeType.Stalled, LastDownloadedBytes = 0 };
        context.Strikes.Add(initialStrike);
        await context.SaveChangesAsync();

        // Progress below threshold should not reset strikes
        downloadedBytes = ByteSize.Parse("1 MB").Bytes;
        await evaluator.EvaluateStallRulesAsync(torrent);
        await striker.DidNotReceive().ResetStrikeAsync(Arg.Any<string>(), Arg.Any<string>(), StrikeType.Stalled);

        // Progress beyond threshold should trigger reset
        downloadedBytes = ByteSize.Parse("12 MB").Bytes;
        await evaluator.EvaluateStallRulesAsync(torrent);
        await striker.Received(1).ResetStrikeAsync(Arg.Any<string>(), Arg.Any<string>(), StrikeType.Stalled);
    }

    [Fact]
    public async Task EvaluateStallRulesAsync_NoMatchingRules_ShouldReturnFoundWithoutRemoval()
    {
        var ruleManager = Substitute.For<IQueueRuleManager>();
        var striker = Substitute.For<IStriker>();
        var logger = Substitute.For<ILogger<QueueRuleEvaluator>>();
        var context = CreateInMemoryEventsContext();

        var evaluator = new QueueRuleEvaluator(ruleManager, striker, context, logger);

        ruleManager
            .GetMatchingStallRule(Arg.Any<ITorrentItemWrapper>())
            .Returns((StallRule?)null);

        var torrent = CreateTorrentMock();

        var result = await evaluator.EvaluateStallRulesAsync(torrent);
        result.ShouldRemove.ShouldBeFalse();

        await striker.DidNotReceive().StrikeAndCheckLimit(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ushort>(), StrikeType.Stalled, Arg.Any<long?>());
    }

    [Fact]
    public async Task EvaluateStallRulesAsync_WithMatchingRule_ShouldApplyStrikeWithoutRemoval()
    {
        var ruleManager = Substitute.For<IQueueRuleManager>();
        var striker = Substitute.For<IStriker>();
        var logger = Substitute.For<ILogger<QueueRuleEvaluator>>();
        var context = CreateInMemoryEventsContext();

        var evaluator = new QueueRuleEvaluator(ruleManager, striker, context, logger);

        var stallRule = CreateStallRule("Stall Apply", resetOnProgress: false, maxStrikes: 5);

        ruleManager
            .GetMatchingStallRule(Arg.Any<ITorrentItemWrapper>())
            .Returns(stallRule);

        striker
            .StrikeAndCheckLimit(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ushort>(), StrikeType.Stalled, Arg.Any<long?>())
            .Returns(false);

        var torrent = CreateTorrentMock();

        var result = await evaluator.EvaluateStallRulesAsync(torrent);
        result.ShouldRemove.ShouldBeFalse();

        await striker.Received(1).StrikeAndCheckLimit("hash", "Example Torrent", (ushort)stallRule.MaxStrikes, StrikeType.Stalled, Arg.Any<long?>());
        await striker.DidNotReceive().ResetStrikeAsync(Arg.Any<string>(), Arg.Any<string>(), StrikeType.Stalled);
    }

    [Fact]
    public async Task EvaluateStallRulesAsync_WhenStrikeLimitReached_ShouldMarkForRemoval()
    {
        var ruleManager = Substitute.For<IQueueRuleManager>();
        var striker = Substitute.For<IStriker>();
        var logger = Substitute.For<ILogger<QueueRuleEvaluator>>();
        var context = CreateInMemoryEventsContext();

        var evaluator = new QueueRuleEvaluator(ruleManager, striker, context, logger);

        var stallRule = CreateStallRule("Stall Remove", resetOnProgress: false, maxStrikes: 6);

        ruleManager
            .GetMatchingStallRule(Arg.Any<ITorrentItemWrapper>())
            .Returns(stallRule);

        striker
            .StrikeAndCheckLimit(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ushort>(), StrikeType.Stalled, Arg.Any<long?>())
            .Returns(true);

        var torrent = CreateTorrentMock();

        var result = await evaluator.EvaluateStallRulesAsync(torrent);
        result.ShouldRemove.ShouldBeTrue();

        await striker.Received(1).StrikeAndCheckLimit("hash", "Example Torrent", (ushort)stallRule.MaxStrikes, StrikeType.Stalled, Arg.Any<long?>());
    }

    [Fact]
    public async Task EvaluateStallRulesAsync_WhenStrikeThrows_ShouldThrowException()
    {
        var ruleManager = Substitute.For<IQueueRuleManager>();
        var striker = Substitute.For<IStriker>();
        var logger = Substitute.For<ILogger<QueueRuleEvaluator>>();
        var context = CreateInMemoryEventsContext();

        var evaluator = new QueueRuleEvaluator(ruleManager, striker, context, logger);

        var failingRule = CreateStallRule("Failing", resetOnProgress: false, maxStrikes: 4);

        ruleManager
            .GetMatchingStallRule(Arg.Any<ITorrentItemWrapper>())
            .Returns(failingRule);

        striker
            .StrikeAndCheckLimit(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ushort>(), StrikeType.Stalled, Arg.Any<long?>())
            .Returns<bool>(x => throw new InvalidOperationException("boom"));

        var torrent = CreateTorrentMock();

        await Should.ThrowAsync<InvalidOperationException>(() => evaluator.EvaluateStallRulesAsync(torrent));

        await striker.Received(1).StrikeAndCheckLimit(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ushort>(), StrikeType.Stalled, Arg.Any<long?>());
    }

    [Fact]
    public async Task EvaluateSlowRulesAsync_NoMatchingRules_ShouldReturnFoundWithoutRemoval()
    {
        var ruleManager = Substitute.For<IQueueRuleManager>();
        var striker = Substitute.For<IStriker>();
        var logger = Substitute.For<ILogger<QueueRuleEvaluator>>();
        var context = CreateInMemoryEventsContext();

        var evaluator = new QueueRuleEvaluator(ruleManager, striker, context, logger);

        ruleManager
            .GetMatchingSlowRule(Arg.Any<ITorrentItemWrapper>())
            .Returns((SlowRule?)null);

        var torrent = CreateTorrentMock();

        var result = await evaluator.EvaluateSlowRulesAsync(torrent);
        result.ShouldRemove.ShouldBeFalse();

        await striker.DidNotReceive().StrikeAndCheckLimit(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ushort>(), StrikeType.SlowTime, Arg.Any<long?>());
    }

    [Fact]
    public async Task EvaluateSlowRulesAsync_WithMatchingRule_ShouldApplyStrikeWithoutRemoval()
    {
        var ruleManager = Substitute.For<IQueueRuleManager>();
        var striker = Substitute.For<IStriker>();
        var logger = Substitute.For<ILogger<QueueRuleEvaluator>>();
        var context = CreateInMemoryEventsContext();

        var evaluator = new QueueRuleEvaluator(ruleManager, striker, context, logger);

        var slowRule = CreateSlowRule("Slow Apply", resetOnProgress: false, maxStrikes: 3);

        ruleManager
            .GetMatchingSlowRule(Arg.Any<ITorrentItemWrapper>())
            .Returns(slowRule);

        striker
            .StrikeAndCheckLimit(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ushort>(), StrikeType.SlowTime, Arg.Any<long?>())
            .Returns(false);

        var torrent = CreateTorrentMock();

        var result = await evaluator.EvaluateSlowRulesAsync(torrent);
        result.ShouldRemove.ShouldBeFalse();

        await striker.Received(1).StrikeAndCheckLimit("hash", "Example Torrent", (ushort)slowRule.MaxStrikes, StrikeType.SlowTime, Arg.Any<long?>());
    }

    [Fact]
    public async Task EvaluateSlowRulesAsync_WhenStrikeLimitReached_ShouldMarkForRemoval()
    {
        var ruleManager = Substitute.For<IQueueRuleManager>();
        var striker = Substitute.For<IStriker>();
        var logger = Substitute.For<ILogger<QueueRuleEvaluator>>();
        var context = CreateInMemoryEventsContext();

        var evaluator = new QueueRuleEvaluator(ruleManager, striker, context, logger);

        var slowRule = CreateSlowRule("Slow Remove", resetOnProgress: false, maxStrikes: 8);

        ruleManager
            .GetMatchingSlowRule(Arg.Any<ITorrentItemWrapper>())
            .Returns(slowRule);

        striker
            .StrikeAndCheckLimit(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ushort>(), StrikeType.SlowTime, Arg.Any<long?>())
            .Returns(true);

        var torrent = CreateTorrentMock();

        var result = await evaluator.EvaluateSlowRulesAsync(torrent);
        result.ShouldRemove.ShouldBeTrue();

        await striker.Received(1).StrikeAndCheckLimit("hash", "Example Torrent", (ushort)slowRule.MaxStrikes, StrikeType.SlowTime, Arg.Any<long?>());
    }

    [Fact]
    public async Task EvaluateSlowRulesAsync_TimeBasedRule_WhenEtaIsAcceptable_ShouldResetStrikes()
    {
        var ruleManager = Substitute.For<IQueueRuleManager>();
        var striker = Substitute.For<IStriker>();
        var logger = Substitute.For<ILogger<QueueRuleEvaluator>>();
        var context = CreateInMemoryEventsContext();

        var evaluator = new QueueRuleEvaluator(ruleManager, striker, context, logger);

        var slowRule = CreateSlowRule("Slow Progress", resetOnProgress: true, maxStrikes: 4);

        ruleManager
            .GetMatchingSlowRule(Arg.Any<ITorrentItemWrapper>())
            .Returns(slowRule);

        striker
            .ResetStrikeAsync(Arg.Any<string>(), Arg.Any<string>(), StrikeType.SlowTime)
            .Returns(Task.CompletedTask);

        var torrent = CreateTorrentMock();
        torrent.Eta.Returns(1800); // ETA is 0.5 hours, below the 1 hour threshold

        await evaluator.EvaluateSlowRulesAsync(torrent);
        await striker.Received(1).ResetStrikeAsync("hash", "Example Torrent", StrikeType.SlowTime);
    }

    [Fact]
    public async Task EvaluateSlowRulesAsync_WhenStrikeThrows_ShouldThrowException()
    {
        var ruleManager = Substitute.For<IQueueRuleManager>();
        var striker = Substitute.For<IStriker>();
        var logger = Substitute.For<ILogger<QueueRuleEvaluator>>();
        var context = CreateInMemoryEventsContext();

        var evaluator = new QueueRuleEvaluator(ruleManager, striker, context, logger);

        var failingRule = CreateSlowRule("Failing Slow", resetOnProgress: false, maxStrikes: 4);

        ruleManager
            .GetMatchingSlowRule(Arg.Any<ITorrentItemWrapper>())
            .Returns(failingRule);

        striker
            .StrikeAndCheckLimit(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ushort>(), StrikeType.SlowTime, Arg.Any<long?>())
            .Returns<bool>(x => throw new InvalidOperationException("slow fail"));

        var torrent = CreateTorrentMock();

        await Should.ThrowAsync<InvalidOperationException>(() => evaluator.EvaluateSlowRulesAsync(torrent));

        await striker.Received(1).StrikeAndCheckLimit(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ushort>(), StrikeType.SlowTime, Arg.Any<long?>());
    }

    [Fact]
    public async Task EvaluateSlowRulesAsync_WithSpeedBasedRule_ShouldUseSlowSpeedStrikeType()
    {
        var ruleManager = Substitute.For<IQueueRuleManager>();
        var striker = Substitute.For<IStriker>();
        var logger = Substitute.For<ILogger<QueueRuleEvaluator>>();
        var context = CreateInMemoryEventsContext();

        var evaluator = new QueueRuleEvaluator(ruleManager, striker, context, logger);

        var slowRule = CreateSlowRule(
            name: "Speed Rule",
            resetOnProgress: false,
            maxStrikes: 3,
            minSpeed: "1 MB",
            maxTimeHours: 0);

        ruleManager
            .GetMatchingSlowRule(Arg.Any<ITorrentItemWrapper>())
            .Returns(slowRule);

        striker
            .StrikeAndCheckLimit(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ushort>(), StrikeType.SlowSpeed, Arg.Any<long?>())
            .Returns(true);

        var torrent = CreateTorrentMock();

        var result = await evaluator.EvaluateSlowRulesAsync(torrent);
        result.ShouldRemove.ShouldBeTrue();

        await striker.Received(1).StrikeAndCheckLimit("hash", "Example Torrent", (ushort)slowRule.MaxStrikes, StrikeType.SlowSpeed, Arg.Any<long?>());
        await striker.DidNotReceive().ResetStrikeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<StrikeType>());
    }

    [Fact]
    public async Task EvaluateSlowRulesAsync_WithBothSpeedAndTimeConfigured_ShouldTreatAsSlowSpeed()
    {
        var ruleManager = Substitute.For<IQueueRuleManager>();
        var striker = Substitute.For<IStriker>();
        var logger = Substitute.For<ILogger<QueueRuleEvaluator>>();
        var context = CreateInMemoryEventsContext();

        var evaluator = new QueueRuleEvaluator(ruleManager, striker, context, logger);

        var slowRule = CreateSlowRule(
            name: "Both Rule",
            resetOnProgress: false,
            maxStrikes: 2,
            minSpeed: "500 KB",
            maxTimeHours: 2);

        ruleManager
            .GetMatchingSlowRule(Arg.Any<ITorrentItemWrapper>())
            .Returns(slowRule);

        striker
            .StrikeAndCheckLimit(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ushort>(), StrikeType.SlowSpeed, Arg.Any<long?>())
            .Returns(true);

        var torrent = CreateTorrentMock();

        var result = await evaluator.EvaluateSlowRulesAsync(torrent);
        result.ShouldRemove.ShouldBeTrue();

        await striker.Received(1).StrikeAndCheckLimit("hash", "Example Torrent", (ushort)slowRule.MaxStrikes, StrikeType.SlowSpeed, Arg.Any<long?>());
    }

    [Fact]
    public async Task EvaluateSlowRulesAsync_WithNeitherSpeedNorTimeConfigured_ShouldNotStrike()
    {
        var ruleManager = Substitute.For<IQueueRuleManager>();
        var striker = Substitute.For<IStriker>();
        var logger = Substitute.For<ILogger<QueueRuleEvaluator>>();
        var context = CreateInMemoryEventsContext();

        var evaluator = new QueueRuleEvaluator(ruleManager, striker, context, logger);

        // Neither minSpeed nor maxTime set (maxTimeHours = 0, minSpeed = null)
        var slowRule = CreateSlowRule(
            name: "Fallback Rule",
            resetOnProgress: false,
            maxStrikes: 1,
            minSpeed: null,
            maxTimeHours: 0);

        ruleManager
            .GetMatchingSlowRule(Arg.Any<ITorrentItemWrapper>())
            .Returns(slowRule);

        var torrent = CreateTorrentMock();

        var result = await evaluator.EvaluateSlowRulesAsync(torrent);
        result.ShouldRemove.ShouldBeFalse();

        await striker.DidNotReceive().StrikeAndCheckLimit(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ushort>(), Arg.Any<StrikeType>(), Arg.Any<long?>());
    }

    [Fact]
    public async Task EvaluateSlowRulesAsync_SpeedBasedRule_WhenSpeedIsAcceptable_ShouldResetSlowSpeedStrikes()
    {
        var ruleManager = Substitute.For<IQueueRuleManager>();
        var striker = Substitute.For<IStriker>();
        var logger = Substitute.For<ILogger<QueueRuleEvaluator>>();
        var context = CreateInMemoryEventsContext();

        var evaluator = new QueueRuleEvaluator(ruleManager, striker, context, logger);

        var slowRule = CreateSlowRule(
            name: "Speed Reset",
            resetOnProgress: true,
            maxStrikes: 3,
            minSpeed: "1 MB",
            maxTimeHours: 0);

        ruleManager
            .GetMatchingSlowRule(Arg.Any<ITorrentItemWrapper>())
            .Returns(slowRule);

        striker
            .ResetStrikeAsync(Arg.Any<string>(), Arg.Any<string>(), StrikeType.SlowSpeed)
            .Returns(Task.CompletedTask);

        var torrent = CreateTorrentMock();
        torrent.DownloadSpeed.Returns(ByteSize.Parse("2 MB").Bytes); // Speed is above 1 MB threshold

        await evaluator.EvaluateSlowRulesAsync(torrent);
        await striker.Received(1).ResetStrikeAsync("hash", "Example Torrent", StrikeType.SlowSpeed);
    }

    [Fact]
    public async Task EvaluateSlowRulesAsync_SpeedBasedRule_WithResetDisabled_ShouldNotReset()
    {
        var ruleManager = Substitute.For<IQueueRuleManager>();
        var striker = Substitute.For<IStriker>();
        var logger = Substitute.For<ILogger<QueueRuleEvaluator>>();
        var context = CreateInMemoryEventsContext();

        var evaluator = new QueueRuleEvaluator(ruleManager, striker, context, logger);

        var slowRule = CreateSlowRule(
            name: "Speed No Reset",
            resetOnProgress: false,
            maxStrikes: 3,
            minSpeed: "1 MB",
            maxTimeHours: 0);

        ruleManager
            .GetMatchingSlowRule(Arg.Any<ITorrentItemWrapper>())
            .Returns(slowRule);

        var torrent = CreateTorrentMock();
        torrent.DownloadSpeed.Returns(ByteSize.Parse("2 MB").Bytes); // Speed is above threshold

        await evaluator.EvaluateSlowRulesAsync(torrent);
        await striker.DidNotReceive().ResetStrikeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<StrikeType>());
    }

    [Fact]
    public async Task EvaluateSlowRulesAsync_TimeBasedRule_WithResetDisabled_ShouldNotReset()
    {
        var ruleManager = Substitute.For<IQueueRuleManager>();
        var striker = Substitute.For<IStriker>();
        var logger = Substitute.For<ILogger<QueueRuleEvaluator>>();
        var context = CreateInMemoryEventsContext();

        var evaluator = new QueueRuleEvaluator(ruleManager, striker, context, logger);

        var slowRule = CreateSlowRule(
            name: "Time No Reset",
            resetOnProgress: false,
            maxStrikes: 4,
            minSpeed: null,
            maxTimeHours: 2);

        ruleManager
            .GetMatchingSlowRule(Arg.Any<ITorrentItemWrapper>())
            .Returns(slowRule);

        var torrent = CreateTorrentMock();
        torrent.Eta.Returns(1800); // ETA below threshold

        await evaluator.EvaluateSlowRulesAsync(torrent);
        await striker.DidNotReceive().ResetStrikeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<StrikeType>());
    }

    [Fact]
    public async Task EvaluateSlowRulesAsync_SpeedBased_BelowThreshold_ShouldStrike()
    {
        var ruleManager = Substitute.For<IQueueRuleManager>();
        var striker = Substitute.For<IStriker>();
        var logger = Substitute.For<ILogger<QueueRuleEvaluator>>();
        var context = CreateInMemoryEventsContext();

        var evaluator = new QueueRuleEvaluator(ruleManager, striker, context, logger);

        var slowRule = CreateSlowRule(
            name: "Speed Strike",
            resetOnProgress: false,
            maxStrikes: 3,
            minSpeed: "5 MB",
            maxTimeHours: 0);

        ruleManager
            .GetMatchingSlowRule(Arg.Any<ITorrentItemWrapper>())
            .Returns(slowRule);

        striker
            .StrikeAndCheckLimit(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ushort>(), StrikeType.SlowSpeed, Arg.Any<long?>())
            .Returns(false);

        var torrent = CreateTorrentMock();
        torrent.DownloadSpeed.Returns(ByteSize.Parse("1 MB").Bytes); // Speed below 5 MB threshold

        var result = await evaluator.EvaluateSlowRulesAsync(torrent);
        result.ShouldRemove.ShouldBeFalse();

        await striker.Received(1).StrikeAndCheckLimit("hash", "Example Torrent", 3, StrikeType.SlowSpeed, Arg.Any<long?>());
    }

    [Fact]
    public async Task EvaluateSlowRulesAsync_TimeBased_AboveThreshold_ShouldStrike()
    {
        var ruleManager = Substitute.For<IQueueRuleManager>();
        var striker = Substitute.For<IStriker>();
        var logger = Substitute.For<ILogger<QueueRuleEvaluator>>();
        var context = CreateInMemoryEventsContext();

        var evaluator = new QueueRuleEvaluator(ruleManager, striker, context, logger);

        var slowRule = CreateSlowRule(
            name: "Time Strike",
            resetOnProgress: false,
            maxStrikes: 5,
            minSpeed: null,
            maxTimeHours: 1);

        ruleManager
            .GetMatchingSlowRule(Arg.Any<ITorrentItemWrapper>())
            .Returns(slowRule);

        striker
            .StrikeAndCheckLimit(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ushort>(), StrikeType.SlowTime, Arg.Any<long?>())
            .Returns(false);

        var torrent = CreateTorrentMock();
        torrent.Eta.Returns(7200); // 2 hours, above 1 hour threshold

        var result = await evaluator.EvaluateSlowRulesAsync(torrent);
        result.ShouldRemove.ShouldBeFalse();

        await striker.Received(1).StrikeAndCheckLimit("hash", "Example Torrent", 5, StrikeType.SlowTime, Arg.Any<long?>());
    }

    [Fact]
    public async Task EvaluateStallRulesAsync_WithResetDisabled_ShouldNotReset()
    {
        var ruleManager = Substitute.For<IQueueRuleManager>();
        var striker = Substitute.For<IStriker>();
        var logger = Substitute.For<ILogger<QueueRuleEvaluator>>();
        var context = CreateInMemoryEventsContext();

        var evaluator = new QueueRuleEvaluator(ruleManager, striker, context, logger);

        var stallRule = CreateStallRule("No Reset", resetOnProgress: false, maxStrikes: 3);

        ruleManager
            .GetMatchingStallRule(Arg.Any<ITorrentItemWrapper>())
            .Returns(stallRule);

        striker
            .StrikeAndCheckLimit(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ushort>(), StrikeType.Stalled, Arg.Any<long?>())
            .Returns(false);

        long downloadedBytes = ByteSize.Parse("50 MB").Bytes;
        var torrent = CreateTorrentMock(downloadedBytesFactory: () => downloadedBytes);

        await evaluator.EvaluateStallRulesAsync(torrent);

        // Progress made but reset disabled, so no reset
        downloadedBytes = ByteSize.Parse("60 MB").Bytes;
        await evaluator.EvaluateStallRulesAsync(torrent);

        await striker.DidNotReceive().ResetStrikeAsync(Arg.Any<string>(), Arg.Any<string>(), StrikeType.Stalled);
    }

    [Fact]
    public async Task EvaluateStallRulesAsync_WithProgressAndNoMinimumThreshold_ShouldReset()
    {
        // Arrange
        var ruleManager = Substitute.For<IQueueRuleManager>();
        var striker = Substitute.For<IStriker>();
        var logger = Substitute.For<ILogger<QueueRuleEvaluator>>();
        var context = CreateInMemoryEventsContext();

        // Seed database with a DownloadItem and initial strike (simulating first observation at 0 bytes)
        var downloadItem = new DownloadItem { DownloadId = "hash", Title = "Example Torrent" };
        context.DownloadItems.Add(downloadItem);
        await context.SaveChangesAsync();

        var initialStrike = new Strike { DownloadItemId = downloadItem.Id, Type = StrikeType.Stalled, LastDownloadedBytes = 0 };
        context.Strikes.Add(initialStrike);
        await context.SaveChangesAsync();

        var evaluator = new QueueRuleEvaluator(ruleManager, striker, context, logger);

        var stallRule = CreateStallRule("Reset No Minimum", resetOnProgress: true, maxStrikes: 3, minimumProgress: null);

        ruleManager
            .GetMatchingStallRule(Arg.Any<ITorrentItemWrapper>())
            .Returns(stallRule);

        striker
            .StrikeAndCheckLimit(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ushort>(), StrikeType.Stalled, Arg.Any<long?>())
            .Returns(false);

        striker
            .ResetStrikeAsync(Arg.Any<string>(), Arg.Any<string>(), StrikeType.Stalled)
            .Returns(Task.CompletedTask);

        // Act - Any progress should trigger reset when no minimum is set
        long downloadedBytes = ByteSize.Parse("1 KB").Bytes;
        var torrent = CreateTorrentMock(downloadedBytesFactory: () => downloadedBytes);
        await evaluator.EvaluateStallRulesAsync(torrent);

        // Assert
        await striker.Received(1).ResetStrikeAsync("hash", "Example Torrent", StrikeType.Stalled);
    }

    private static ITorrentItemWrapper CreateTorrentMock(
        Func<long>? downloadedBytesFactory = null,
        bool isPrivate = false,
        string hash = "hash",
        string name = "Example Torrent",
        double completionPercentage = 50,
        string size = "100 MB")
    {
        var torrent = Substitute.For<ITorrentItemWrapper>();
        torrent.Hash.Returns(hash);
        torrent.Name.Returns(name);
        torrent.IsPrivate.Returns(isPrivate);
        torrent.CompletionPercentage.Returns(completionPercentage);
        torrent.Size.Returns(ByteSize.Parse(size).Bytes);
        torrent.DownloadedBytes.Returns(callInfo => downloadedBytesFactory?.Invoke() ?? 0);
        torrent.DownloadSpeed.Returns(0);
        torrent.Eta.Returns(7200);
        return torrent;
    }

    private static StallRule CreateStallRule(
        string name,
        bool resetOnProgress,
        int maxStrikes,
        string? minimumProgress = null,
        bool deletePrivateTorrentsFromClient = false,
        bool changeCategory = false)
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
            DeletePrivateTorrentsFromClient = deletePrivateTorrentsFromClient,
            ChangeCategory = changeCategory,
        };
    }

    private static SlowRule CreateSlowRule(
        string name,
        bool resetOnProgress,
        int maxStrikes,
        string? minSpeed = null,
        double maxTimeHours = 1,
        bool deletePrivateTorrentsFromClient = false,
        bool changeCategory = false)
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
            DeletePrivateTorrentsFromClient = deletePrivateTorrentsFromClient,
            ChangeCategory = changeCategory,
        };
    }

    [Fact]
    public async Task EvaluateStallRulesAsync_WhenNoRuleMatches_ShouldReturnDeleteFromClientFalse()
    {
        var ruleManager = Substitute.For<IQueueRuleManager>();
        var striker = Substitute.For<IStriker>();
        var logger = Substitute.For<ILogger<QueueRuleEvaluator>>();
        var context = CreateInMemoryEventsContext();

        var evaluator = new QueueRuleEvaluator(ruleManager, striker, context, logger);

        ruleManager
            .GetMatchingStallRule(Arg.Any<ITorrentItemWrapper>())
            .Returns((StallRule?)null);

        var torrent = CreateTorrentMock();

        var result = await evaluator.EvaluateStallRulesAsync(torrent);

        result.ShouldRemove.ShouldBeFalse();
        result.Reason.ShouldBe(DeleteReason.None);
        result.DeleteFromClient.ShouldBeFalse();
    }

    [Fact]
    public async Task EvaluateStallRulesAsync_WhenRuleMatchesButNoRemoval_ShouldReturnDeleteFromClientFalse()
    {
        var ruleManager = Substitute.For<IQueueRuleManager>();
        var striker = Substitute.For<IStriker>();
        var logger = Substitute.For<ILogger<QueueRuleEvaluator>>();
        var context = CreateInMemoryEventsContext();

        var evaluator = new QueueRuleEvaluator(ruleManager, striker, context, logger);

        var stallRule = CreateStallRule("Test Rule", resetOnProgress: false, maxStrikes: 3, deletePrivateTorrentsFromClient: true);

        ruleManager
            .GetMatchingStallRule(Arg.Any<ITorrentItemWrapper>())
            .Returns(stallRule);

        striker
            .StrikeAndCheckLimit(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ushort>(), StrikeType.Stalled, Arg.Any<long?>())
            .Returns(false);

        var torrent = CreateTorrentMock();

        var result = await evaluator.EvaluateStallRulesAsync(torrent);

        result.ShouldRemove.ShouldBeFalse();
        result.Reason.ShouldBe(DeleteReason.None);
        result.DeleteFromClient.ShouldBeFalse();
    }

    [Fact]
    public async Task EvaluateStallRulesAsync_WhenRuleMatchesAndRemovesWithDeleteFromClientTrue_ShouldReturnTrue()
    {
        var ruleManager = Substitute.For<IQueueRuleManager>();
        var striker = Substitute.For<IStriker>();
        var logger = Substitute.For<ILogger<QueueRuleEvaluator>>();
        var context = CreateInMemoryEventsContext();

        var evaluator = new QueueRuleEvaluator(ruleManager, striker, context, logger);

        var stallRule = CreateStallRule("Delete True Rule", resetOnProgress: false, maxStrikes: 3, deletePrivateTorrentsFromClient: true);

        ruleManager
            .GetMatchingStallRule(Arg.Any<ITorrentItemWrapper>())
            .Returns(stallRule);

        striker
            .StrikeAndCheckLimit(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ushort>(), StrikeType.Stalled, Arg.Any<long?>())
            .Returns(true);

        var torrent = CreateTorrentMock();

        var result = await evaluator.EvaluateStallRulesAsync(torrent);

        result.ShouldRemove.ShouldBeTrue();
        result.Reason.ShouldBe(DeleteReason.Stalled);
        result.DeleteFromClient.ShouldBeTrue();
    }

    [Fact]
    public async Task EvaluateStallRulesAsync_WhenRuleMatchesAndRemovesWithDeleteFromClientFalse_ShouldReturnFalse()
    {
        var ruleManager = Substitute.For<IQueueRuleManager>();
        var striker = Substitute.For<IStriker>();
        var logger = Substitute.For<ILogger<QueueRuleEvaluator>>();
        var context = CreateInMemoryEventsContext();

        var evaluator = new QueueRuleEvaluator(ruleManager, striker, context, logger);

        var stallRule = CreateStallRule("Delete False Rule", resetOnProgress: false, maxStrikes: 3, deletePrivateTorrentsFromClient: false);

        ruleManager
            .GetMatchingStallRule(Arg.Any<ITorrentItemWrapper>())
            .Returns(stallRule);

        striker
            .StrikeAndCheckLimit(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ushort>(), StrikeType.Stalled, Arg.Any<long?>())
            .Returns(true);

        var torrent = CreateTorrentMock();

        var result = await evaluator.EvaluateStallRulesAsync(torrent);

        result.ShouldRemove.ShouldBeTrue();
        result.Reason.ShouldBe(DeleteReason.Stalled);
        result.DeleteFromClient.ShouldBeFalse();
    }

    [Fact]
    public async Task EvaluateSlowRulesAsync_WhenNoRuleMatches_ShouldReturnDeleteFromClientFalse()
    {
        var ruleManager = Substitute.For<IQueueRuleManager>();
        var striker = Substitute.For<IStriker>();
        var logger = Substitute.For<ILogger<QueueRuleEvaluator>>();
        var context = CreateInMemoryEventsContext();

        var evaluator = new QueueRuleEvaluator(ruleManager, striker, context, logger);

        ruleManager
            .GetMatchingSlowRule(Arg.Any<ITorrentItemWrapper>())
            .Returns((SlowRule?)null);

        var torrent = CreateTorrentMock();

        var result = await evaluator.EvaluateSlowRulesAsync(torrent);

        result.ShouldRemove.ShouldBeFalse();
        result.Reason.ShouldBe(DeleteReason.None);
        result.DeleteFromClient.ShouldBeFalse();
    }

    [Fact]
    public async Task EvaluateSlowRulesAsync_WhenRuleMatchesAndRemovesWithDeleteFromClientTrue_ShouldReturnTrue()
    {
        var ruleManager = Substitute.For<IQueueRuleManager>();
        var striker = Substitute.For<IStriker>();
        var logger = Substitute.For<ILogger<QueueRuleEvaluator>>();
        var context = CreateInMemoryEventsContext();

        var evaluator = new QueueRuleEvaluator(ruleManager, striker, context, logger);

        var slowRule = CreateSlowRule("Slow Delete True", resetOnProgress: false, maxStrikes: 3, maxTimeHours: 1, deletePrivateTorrentsFromClient: true);

        ruleManager
            .GetMatchingSlowRule(Arg.Any<ITorrentItemWrapper>())
            .Returns(slowRule);

        striker
            .StrikeAndCheckLimit(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ushort>(), StrikeType.SlowTime, Arg.Any<long?>())
            .Returns(true);

        var torrent = CreateTorrentMock();

        var result = await evaluator.EvaluateSlowRulesAsync(torrent);

        result.ShouldRemove.ShouldBeTrue();
        result.Reason.ShouldBe(DeleteReason.SlowTime);
        result.DeleteFromClient.ShouldBeTrue();
    }

    [Fact]
    public async Task EvaluateSlowRulesAsync_WhenRuleMatchesAndRemovesWithDeleteFromClientFalse_ShouldReturnFalse()
    {
        var ruleManager = Substitute.For<IQueueRuleManager>();
        var striker = Substitute.For<IStriker>();
        var logger = Substitute.For<ILogger<QueueRuleEvaluator>>();
        var context = CreateInMemoryEventsContext();

        var evaluator = new QueueRuleEvaluator(ruleManager, striker, context, logger);

        var slowRule = CreateSlowRule("Slow Delete False", resetOnProgress: false, maxStrikes: 3, maxTimeHours: 1, deletePrivateTorrentsFromClient: false);

        ruleManager
            .GetMatchingSlowRule(Arg.Any<ITorrentItemWrapper>())
            .Returns(slowRule);

        striker
            .StrikeAndCheckLimit(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ushort>(), StrikeType.SlowTime, Arg.Any<long?>())
            .Returns(true);

        var torrent = CreateTorrentMock();

        var result = await evaluator.EvaluateSlowRulesAsync(torrent);

        result.ShouldRemove.ShouldBeTrue();
        result.Reason.ShouldBe(DeleteReason.SlowTime);
        result.DeleteFromClient.ShouldBeFalse();
    }

    [Fact]
    public async Task EvaluateSlowRulesAsync_SpeedBasedRuleWithDeleteFromClientTrue_ShouldReturnTrue()
    {
        var ruleManager = Substitute.For<IQueueRuleManager>();
        var striker = Substitute.For<IStriker>();
        var logger = Substitute.For<ILogger<QueueRuleEvaluator>>();
        var context = CreateInMemoryEventsContext();

        var evaluator = new QueueRuleEvaluator(ruleManager, striker, context, logger);

        var slowRule = CreateSlowRule(
            "Speed Delete True",
            resetOnProgress: false,
            maxStrikes: 3,
            minSpeed: "5 MB",
            maxTimeHours: 0,
            deletePrivateTorrentsFromClient: true);

        ruleManager
            .GetMatchingSlowRule(Arg.Any<ITorrentItemWrapper>())
            .Returns(slowRule);

        striker
            .StrikeAndCheckLimit(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ushort>(), StrikeType.SlowSpeed, Arg.Any<long?>())
            .Returns(true);

        var torrent = CreateTorrentMock();
        torrent.DownloadSpeed.Returns(ByteSize.Parse("1 MB").Bytes);

        var result = await evaluator.EvaluateSlowRulesAsync(torrent);

        result.ShouldRemove.ShouldBeTrue();
        result.Reason.ShouldBe(DeleteReason.SlowSpeed);
        result.DeleteFromClient.ShouldBeTrue();
    }

    [Fact]
    public async Task EvaluateSlowRulesAsync_WhenRuleMatchesButNoRemoval_ShouldReturnDeleteFromClientFalse()
    {
        var ruleManager = Substitute.For<IQueueRuleManager>();
        var striker = Substitute.For<IStriker>();
        var logger = Substitute.For<ILogger<QueueRuleEvaluator>>();
        var context = CreateInMemoryEventsContext();

        var evaluator = new QueueRuleEvaluator(ruleManager, striker, context, logger);

        var slowRule = CreateSlowRule("Test Slow Rule", resetOnProgress: false, maxStrikes: 3, maxTimeHours: 1, deletePrivateTorrentsFromClient: true);

        ruleManager
            .GetMatchingSlowRule(Arg.Any<ITorrentItemWrapper>())
            .Returns(slowRule);

        striker
            .StrikeAndCheckLimit(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ushort>(), StrikeType.SlowTime, Arg.Any<long?>())
            .Returns(false);

        var torrent = CreateTorrentMock();

        var result = await evaluator.EvaluateSlowRulesAsync(torrent);

        result.ShouldRemove.ShouldBeFalse();
        result.Reason.ShouldBe(DeleteReason.None);
        result.DeleteFromClient.ShouldBeFalse();
    }

    #region ChangeCategory Tests

    [Fact]
    public async Task EvaluateStallRulesAsync_WhenRuleMatchesWithChangeCategory_ShouldReturnChangeCategoryTrueAndDeleteFromClientFalse()
    {
        var ruleManager = Substitute.For<IQueueRuleManager>();
        var striker = Substitute.For<IStriker>();
        var logger = Substitute.For<ILogger<QueueRuleEvaluator>>();
        var context = CreateInMemoryEventsContext();

        var evaluator = new QueueRuleEvaluator(ruleManager, striker, context, logger);

        var stallRule = CreateStallRule("Stall Change Category", resetOnProgress: false, maxStrikes: 3, deletePrivateTorrentsFromClient: true, changeCategory: true);

        ruleManager
            .GetMatchingStallRule(Arg.Any<ITorrentItemWrapper>())
            .Returns(stallRule);

        striker
            .StrikeAndCheckLimit(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ushort>(), StrikeType.Stalled, Arg.Any<long?>())
            .Returns(true);

        var torrent = CreateTorrentMock();

        var result = await evaluator.EvaluateStallRulesAsync(torrent);

        result.ShouldRemove.ShouldBeTrue();
        result.Reason.ShouldBe(DeleteReason.Stalled);
        result.ChangeCategory.ShouldBeTrue();
        result.DeleteFromClient.ShouldBeFalse();
    }

    [Fact]
    public async Task EvaluateStallRulesAsync_WhenRuleMatchesWithoutChangeCategory_ShouldReturnChangeCategoryFalse()
    {
        var ruleManager = Substitute.For<IQueueRuleManager>();
        var striker = Substitute.For<IStriker>();
        var logger = Substitute.For<ILogger<QueueRuleEvaluator>>();
        var context = CreateInMemoryEventsContext();

        var evaluator = new QueueRuleEvaluator(ruleManager, striker, context, logger);

        var stallRule = CreateStallRule("Stall Default", resetOnProgress: false, maxStrikes: 3, deletePrivateTorrentsFromClient: false, changeCategory: false);

        ruleManager
            .GetMatchingStallRule(Arg.Any<ITorrentItemWrapper>())
            .Returns(stallRule);

        striker
            .StrikeAndCheckLimit(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ushort>(), StrikeType.Stalled, Arg.Any<long?>())
            .Returns(true);

        var torrent = CreateTorrentMock();

        var result = await evaluator.EvaluateStallRulesAsync(torrent);

        result.ShouldRemove.ShouldBeTrue();
        result.ChangeCategory.ShouldBeFalse();
    }

    [Fact]
    public async Task EvaluateSlowRulesAsync_WhenSpeedRuleMatchesWithChangeCategory_ShouldReturnChangeCategoryTrueAndDeleteFromClientFalse()
    {
        var ruleManager = Substitute.For<IQueueRuleManager>();
        var striker = Substitute.For<IStriker>();
        var logger = Substitute.For<ILogger<QueueRuleEvaluator>>();
        var context = CreateInMemoryEventsContext();

        var evaluator = new QueueRuleEvaluator(ruleManager, striker, context, logger);

        var slowRule = CreateSlowRule(
            "Slow Speed Change Category",
            resetOnProgress: false,
            maxStrikes: 3,
            minSpeed: "5 MB",
            maxTimeHours: 0,
            deletePrivateTorrentsFromClient: true,
            changeCategory: true);

        ruleManager
            .GetMatchingSlowRule(Arg.Any<ITorrentItemWrapper>())
            .Returns(slowRule);

        striker
            .StrikeAndCheckLimit(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ushort>(), StrikeType.SlowSpeed, Arg.Any<long?>())
            .Returns(true);

        var torrent = CreateTorrentMock();
        torrent.DownloadSpeed.Returns(ByteSize.Parse("1 MB").Bytes);

        var result = await evaluator.EvaluateSlowRulesAsync(torrent);

        result.ShouldRemove.ShouldBeTrue();
        result.Reason.ShouldBe(DeleteReason.SlowSpeed);
        result.ChangeCategory.ShouldBeTrue();
        result.DeleteFromClient.ShouldBeFalse();
    }

    [Fact]
    public async Task EvaluateSlowRulesAsync_WhenTimeRuleMatchesWithChangeCategory_ShouldReturnChangeCategoryTrueAndDeleteFromClientFalse()
    {
        var ruleManager = Substitute.For<IQueueRuleManager>();
        var striker = Substitute.For<IStriker>();
        var logger = Substitute.For<ILogger<QueueRuleEvaluator>>();
        var context = CreateInMemoryEventsContext();

        var evaluator = new QueueRuleEvaluator(ruleManager, striker, context, logger);

        var slowRule = CreateSlowRule(
            "Slow Time Change Category",
            resetOnProgress: false,
            maxStrikes: 3,
            maxTimeHours: 1,
            deletePrivateTorrentsFromClient: true,
            changeCategory: true);

        ruleManager
            .GetMatchingSlowRule(Arg.Any<ITorrentItemWrapper>())
            .Returns(slowRule);

        striker
            .StrikeAndCheckLimit(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ushort>(), StrikeType.SlowTime, Arg.Any<long?>())
            .Returns(true);

        var torrent = CreateTorrentMock();

        var result = await evaluator.EvaluateSlowRulesAsync(torrent);

        result.ShouldRemove.ShouldBeTrue();
        result.Reason.ShouldBe(DeleteReason.SlowTime);
        result.ChangeCategory.ShouldBeTrue();
        result.DeleteFromClient.ShouldBeFalse();
    }

    #endregion
}
