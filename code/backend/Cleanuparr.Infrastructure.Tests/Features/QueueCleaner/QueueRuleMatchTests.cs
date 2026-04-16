using Cleanuparr.Domain.Entities;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Persistence.Models.Configuration.QueueCleaner;
using NSubstitute;
using Shouldly;
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

        rule.MatchesTorrent(torrentAtBoundary).ShouldBeFalse();
        rule.MatchesTorrent(torrentAboveBoundary).ShouldBeTrue();
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

        rule.MatchesTorrent(zeroTorrent).ShouldBeTrue();
        rule.MatchesTorrent(midTorrent).ShouldBeTrue();
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

        rule.MatchesTorrent(torrentAtBoundary).ShouldBeFalse();
        rule.MatchesTorrent(torrentAboveBoundary).ShouldBeTrue();
    }

    [Fact]
    public void StallRule_PrivacyType_Both_MatchesPublic()
    {
        var rule = new StallRule
        {
            Name = "Both Rule",
            Enabled = true,
            PrivacyType = TorrentPrivacyType.Both,
            MaxStrikes = 3,
            MinCompletionPercentage = 0,
            MaxCompletionPercentage = 100,
        };

        var publicTorrent = CreateTorrent(isPrivate: false, completionPercentage: 50);

        rule.MatchesTorrent(publicTorrent).ShouldBeTrue();
    }

    [Fact]
    public void StallRule_PrivacyType_Both_MatchesPrivate()
    {
        var rule = new StallRule
        {
            Name = "Both Rule",
            Enabled = true,
            PrivacyType = TorrentPrivacyType.Both,
            MaxStrikes = 3,
            MinCompletionPercentage = 0,
            MaxCompletionPercentage = 100,
        };

        var privateTorrent = CreateTorrent(isPrivate: true, completionPercentage: 50);

        rule.MatchesTorrent(privateTorrent).ShouldBeTrue();
    }

    [Fact]
    public void SlowRule_PrivacyType_Both_MatchesPublic()
    {
        var rule = new SlowRule
        {
            Name = "Both Rule",
            Enabled = true,
            PrivacyType = TorrentPrivacyType.Both,
            MaxStrikes = 3,
            MinCompletionPercentage = 0,
            MaxCompletionPercentage = 100,
            MaxTimeHours = 1,
            MinSpeed = string.Empty,
        };

        var publicTorrent = CreateTorrent(isPrivate: false, completionPercentage: 50);

        rule.MatchesTorrent(publicTorrent).ShouldBeTrue();
    }

    [Fact]
    public void SlowRule_PrivacyType_Both_MatchesPrivate()
    {
        var rule = new SlowRule
        {
            Name = "Both Rule",
            Enabled = true,
            PrivacyType = TorrentPrivacyType.Both,
            MaxStrikes = 3,
            MinCompletionPercentage = 0,
            MaxCompletionPercentage = 100,
            MaxTimeHours = 1,
            MinSpeed = string.Empty,
        };

        var privateTorrent = CreateTorrent(isPrivate: true, completionPercentage: 50);

        rule.MatchesTorrent(privateTorrent).ShouldBeTrue();
    }

    [Fact]
    public void StallRule_CompletionPercentage_AtMaxBoundary_Matches()
    {
        var rule = new StallRule
        {
            Name = "Max Boundary",
            Enabled = true,
            PrivacyType = TorrentPrivacyType.Public,
            MaxStrikes = 3,
            MinCompletionPercentage = 20,
            MaxCompletionPercentage = 80,
        };

        var torrentAtMax = CreateTorrent(isPrivate: false, completionPercentage: 80);

        rule.MatchesTorrent(torrentAtMax).ShouldBeTrue();
    }

    [Fact]
    public void StallRule_CompletionPercentage_BelowMin_DoesNotMatch()
    {
        var rule = new StallRule
        {
            Name = "Below Min",
            Enabled = true,
            PrivacyType = TorrentPrivacyType.Public,
            MaxStrikes = 3,
            MinCompletionPercentage = 20,
            MaxCompletionPercentage = 80,
        };

        var torrentBelowMin = CreateTorrent(isPrivate: false, completionPercentage: 15);

        rule.MatchesTorrent(torrentBelowMin).ShouldBeFalse();
    }

    [Fact]
    public void StallRule_CompletionPercentage_AboveMax_DoesNotMatch()
    {
        var rule = new StallRule
        {
            Name = "Above Max",
            Enabled = true,
            PrivacyType = TorrentPrivacyType.Public,
            MaxStrikes = 3,
            MinCompletionPercentage = 20,
            MaxCompletionPercentage = 80,
        };

        var torrentAboveMax = CreateTorrent(isPrivate: false, completionPercentage: 85);

        rule.MatchesTorrent(torrentAboveMax).ShouldBeFalse();
    }

    [Fact]
    public void SlowRule_CompletionPercentage_AtMaxBoundary_Matches()
    {
        var rule = new SlowRule
        {
            Name = "Max Boundary",
            Enabled = true,
            PrivacyType = TorrentPrivacyType.Public,
            MaxStrikes = 3,
            MinCompletionPercentage = 30,
            MaxCompletionPercentage = 70,
            MaxTimeHours = 1,
            MinSpeed = string.Empty,
        };

        var torrentAtMax = CreateTorrent(isPrivate: false, completionPercentage: 70);

        rule.MatchesTorrent(torrentAtMax).ShouldBeTrue();
    }

    [Fact]
    public void SlowRule_CompletionPercentage_BelowMin_DoesNotMatch()
    {
        var rule = new SlowRule
        {
            Name = "Below Min",
            Enabled = true,
            PrivacyType = TorrentPrivacyType.Public,
            MaxStrikes = 3,
            MinCompletionPercentage = 30,
            MaxCompletionPercentage = 70,
            MaxTimeHours = 1,
            MinSpeed = string.Empty,
        };

        var torrentBelowMin = CreateTorrent(isPrivate: false, completionPercentage: 25);

        rule.MatchesTorrent(torrentBelowMin).ShouldBeFalse();
    }

    [Fact]
    public void SlowRule_CompletionPercentage_AboveMax_DoesNotMatch()
    {
        var rule = new SlowRule
        {
            Name = "Above Max",
            Enabled = true,
            PrivacyType = TorrentPrivacyType.Public,
            MaxStrikes = 3,
            MinCompletionPercentage = 30,
            MaxCompletionPercentage = 70,
            MaxTimeHours = 1,
            MinSpeed = string.Empty,
        };

        var torrentAboveMax = CreateTorrent(isPrivate: false, completionPercentage: 75);

        rule.MatchesTorrent(torrentAboveMax).ShouldBeFalse();
    }

    [Fact]
    public void SlowRule_IgnoreAboveSize_TorrentTooLarge_DoesNotMatch()
    {
        var rule = new SlowRule
        {
            Name = "Size Limited",
            Enabled = true,
            PrivacyType = TorrentPrivacyType.Public,
            MaxStrikes = 3,
            MinCompletionPercentage = 0,
            MaxCompletionPercentage = 100,
            MaxTimeHours = 1,
            MinSpeed = string.Empty,
            IgnoreAboveSize = "50 GB",
        };

        var largeTorrent = CreateTorrent(isPrivate: false, completionPercentage: 50, size: "100 GB");

        rule.MatchesTorrent(largeTorrent).ShouldBeFalse();
    }

    [Fact]
    public void SlowRule_IgnoreAboveSize_TorrentSizeOk_Matches()
    {
        var rule = new SlowRule
        {
            Name = "Size Limited",
            Enabled = true,
            PrivacyType = TorrentPrivacyType.Public,
            MaxStrikes = 3,
            MinCompletionPercentage = 0,
            MaxCompletionPercentage = 100,
            MaxTimeHours = 1,
            MinSpeed = string.Empty,
            IgnoreAboveSize = "50 GB",
        };

        var smallTorrent = CreateTorrent(isPrivate: false, completionPercentage: 50, size: "30 GB");

        rule.MatchesTorrent(smallTorrent).ShouldBeTrue();
    }

    [Fact]
    public void SlowRule_NoIgnoreSizeSet_AllSizesMatch()
    {
        var rule = new SlowRule
        {
            Name = "No Size Limit",
            Enabled = true,
            PrivacyType = TorrentPrivacyType.Public,
            MaxStrikes = 3,
            MinCompletionPercentage = 0,
            MaxCompletionPercentage = 100,
            MaxTimeHours = 1,
            MinSpeed = string.Empty,
            IgnoreAboveSize = string.Empty,
        };

        var hugeTorrent = CreateTorrent(isPrivate: false, completionPercentage: 50, size: "500 GB");

        rule.MatchesTorrent(hugeTorrent).ShouldBeTrue();
    }

    [Fact]
    public void StallRule_PrivacyType_Public_DoesNotMatchPrivate()
    {
        var rule = new StallRule
        {
            Name = "Public Only",
            Enabled = true,
            PrivacyType = TorrentPrivacyType.Public,
            MaxStrikes = 3,
            MinCompletionPercentage = 0,
            MaxCompletionPercentage = 100,
        };

        var privateTorrent = CreateTorrent(isPrivate: true, completionPercentage: 50);

        rule.MatchesTorrent(privateTorrent).ShouldBeFalse();
    }

    [Fact]
    public void StallRule_PrivacyType_Private_DoesNotMatchPublic()
    {
        var rule = new StallRule
        {
            Name = "Private Only",
            Enabled = true,
            PrivacyType = TorrentPrivacyType.Private,
            MaxStrikes = 3,
            MinCompletionPercentage = 0,
            MaxCompletionPercentage = 100,
        };

        var publicTorrent = CreateTorrent(isPrivate: false, completionPercentage: 50);

        rule.MatchesTorrent(publicTorrent).ShouldBeFalse();
    }

    [Fact]
    public void SlowRule_PrivacyType_Public_DoesNotMatchPrivate()
    {
        var rule = new SlowRule
        {
            Name = "Public Only",
            Enabled = true,
            PrivacyType = TorrentPrivacyType.Public,
            MaxStrikes = 3,
            MinCompletionPercentage = 0,
            MaxCompletionPercentage = 100,
            MaxTimeHours = 1,
            MinSpeed = string.Empty,
        };

        var privateTorrent = CreateTorrent(isPrivate: true, completionPercentage: 50);

        rule.MatchesTorrent(privateTorrent).ShouldBeFalse();
    }

    [Fact]
    public void SlowRule_PrivacyType_Private_DoesNotMatchPublic()
    {
        var rule = new SlowRule
        {
            Name = "Private Only",
            Enabled = true,
            PrivacyType = TorrentPrivacyType.Private,
            MaxStrikes = 3,
            MinCompletionPercentage = 0,
            MaxCompletionPercentage = 100,
            MaxTimeHours = 1,
            MinSpeed = string.Empty,
        };

        var publicTorrent = CreateTorrent(isPrivate: false, completionPercentage: 50);

        rule.MatchesTorrent(publicTorrent).ShouldBeFalse();
    }

    private static ITorrentItemWrapper CreateTorrent(bool isPrivate, double completionPercentage, string size = "10 GB")
    {
        var torrent = Substitute.For<ITorrentItemWrapper>();
        torrent.IsPrivate.Returns(isPrivate);
        torrent.CompletionPercentage.Returns(completionPercentage);
        torrent.Size.Returns(ByteSize.Parse(size).Bytes);
        return torrent;
    }
}
