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
}
