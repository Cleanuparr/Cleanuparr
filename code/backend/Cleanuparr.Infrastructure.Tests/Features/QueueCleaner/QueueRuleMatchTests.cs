using Cleanuparr.Domain.Entities;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Persistence.Models.Configuration.QueueCleaner;
using Moq;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.QueueCleaner;

public class QueueRuleMatchTests
{
    [Fact]
    public void StallRule_WithNonZeroMinCompletion_ShouldExcludeLowerBoundary()
    {
        var rule = new StallRule
        {
            Name = "Stall",
            Enabled = true,
            PrivacyType = TorrentPrivacyType.Public,
            MaxStrikes = 3,
            MinCompletionPercentage = 20,
            MaxCompletionPercentage = 100,
        };

        var torrentAtBoundary = CreateTorrent(isPrivate: false, completionPercentage: 20);
        var torrentAboveBoundary = CreateTorrent(isPrivate: false, completionPercentage: 20.1);

        Assert.False(rule.MatchesTorrent(torrentAtBoundary.Object));
        Assert.True(rule.MatchesTorrent(torrentAboveBoundary.Object));
    }

    [Fact]
    public void StallRule_WithZeroMinCompletion_ShouldIncludeZero()
    {
        var rule = new StallRule
        {
            Name = "Zero",
            Enabled = true,
            PrivacyType = TorrentPrivacyType.Public,
            MaxStrikes = 3,
            MinCompletionPercentage = 0,
            MaxCompletionPercentage = 20,
        };

        var zeroTorrent = CreateTorrent(isPrivate: false, completionPercentage: 0);
        var midTorrent = CreateTorrent(isPrivate: false, completionPercentage: 10);

        Assert.True(rule.MatchesTorrent(zeroTorrent.Object));
        Assert.True(rule.MatchesTorrent(midTorrent.Object));
    }

    [Fact]
    public void SlowRule_WithNonZeroMinCompletion_ShouldExcludeLowerBoundary()
    {
        var rule = new SlowRule
        {
            Name = "Slow",
            Enabled = true,
            PrivacyType = TorrentPrivacyType.Public,
            MaxStrikes = 3,
            MinCompletionPercentage = 40,
            MaxCompletionPercentage = 90,
            MaxTimeHours = 1,
            MinSpeed = string.Empty,
        };

        var torrentAtBoundary = CreateTorrent(isPrivate: false, completionPercentage: 40);
        var torrentAboveBoundary = CreateTorrent(isPrivate: false, completionPercentage: 40.5);

        Assert.False(rule.MatchesTorrent(torrentAtBoundary.Object));
        Assert.True(rule.MatchesTorrent(torrentAboveBoundary.Object));
    }

    private static Mock<ITorrentInfo> CreateTorrent(bool isPrivate, double completionPercentage)
    {
        var torrent = new Mock<ITorrentInfo>();
        torrent.SetupGet(t => t.IsPrivate).Returns(isPrivate);
        torrent.SetupGet(t => t.CompletionPercentage).Returns(completionPercentage);
        torrent.SetupGet(t => t.Size).Returns(0L);
        torrent.SetupGet(t => t.Trackers).Returns(Array.Empty<string>());
        return torrent;
    }
}
