using Cleanuparr.Domain.Entities;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Infrastructure.Services;
using Cleanuparr.Infrastructure.Tests.TestHelpers;
using Cleanuparr.Persistence.Models.Configuration.QueueCleaner;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Services;

public class QueueRuleManagerTests
{
    [Fact]
    public void GetMatchingStallRule_NoRules_ReturnsNull()
    {
        // Arrange
        var logger = Substitute.For<ILogger<QueueRuleManager>>();
        var ruleManager = new QueueRuleManager(logger);

        ContextProvider.Set(nameof(StallRule), new List<StallRule>());

        var torrent = CreateTorrentMock();

        // Act
        var result = ruleManager.GetMatchingStallRule(torrent);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void GetMatchingStallRule_OneMatch_ReturnsRule()
    {
        // Arrange
        var logger = Substitute.For<ILogger<QueueRuleManager>>();
        var ruleManager = new QueueRuleManager(logger);

        var stallRule = CreateStallRule("Test Rule", enabled: true, privacyType: TorrentPrivacyType.Public, minCompletion: 0, maxCompletion: 100);
        ContextProvider.Set(nameof(StallRule), new List<StallRule> { stallRule });

        var torrent = CreateTorrentMock(isPrivate: false, completionPercentage: 50);

        // Act
        var result = ruleManager.GetMatchingStallRule(torrent);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe(stallRule.Id);
        result.Name.ShouldBe("Test Rule");
    }

    [Fact]
    public void GetMatchingStallRule_MultipleMatches_ReturnsNull_LogsWarning()
    {
        // Arrange
        var logger = Substitute.For<ILogger<QueueRuleManager>>();
        var ruleManager = new QueueRuleManager(logger);

        var stallRule1 = CreateStallRule("Rule 1", enabled: true, privacyType: TorrentPrivacyType.Public, minCompletion: 0, maxCompletion: 100);
        var stallRule2 = CreateStallRule("Rule 2", enabled: true, privacyType: TorrentPrivacyType.Public, minCompletion: 0, maxCompletion: 100);
        ContextProvider.Set(nameof(StallRule), new List<StallRule> { stallRule1, stallRule2 });

        var torrent = CreateTorrentMock(isPrivate: false, completionPercentage: 50);

        // Act
        var result = ruleManager.GetMatchingStallRule(torrent);

        // Assert
        result.ShouldBeNull();
        logger.ReceivedLogContaining(LogLevel.Warning, "multiple");
    }

    [Fact]
    public void GetMatchingStallRule_DisabledRule_ReturnsNull()
    {
        // Arrange
        var logger = Substitute.For<ILogger<QueueRuleManager>>();
        var ruleManager = new QueueRuleManager(logger);

        var stallRule = CreateStallRule("Disabled Rule", enabled: false, privacyType: TorrentPrivacyType.Public, minCompletion: 0, maxCompletion: 100);
        ContextProvider.Set(nameof(StallRule), new List<StallRule> { stallRule });

        var torrent = CreateTorrentMock(isPrivate: false, completionPercentage: 50);

        // Act
        var result = ruleManager.GetMatchingStallRule(torrent);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void GetMatchingStallRule_PrivacyTypeMismatch_Public_ReturnsNull()
    {
        // Arrange
        var logger = Substitute.For<ILogger<QueueRuleManager>>();
        var ruleManager = new QueueRuleManager(logger);

        var stallRule = CreateStallRule("Public Rule", enabled: true, privacyType: TorrentPrivacyType.Public, minCompletion: 0, maxCompletion: 100);
        ContextProvider.Set(nameof(StallRule), new List<StallRule> { stallRule });

        var torrent = CreateTorrentMock(isPrivate: true, completionPercentage: 50); // Private torrent

        // Act
        var result = ruleManager.GetMatchingStallRule(torrent);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void GetMatchingStallRule_PrivacyTypeMismatch_Private_ReturnsNull()
    {
        // Arrange
        var logger = Substitute.For<ILogger<QueueRuleManager>>();
        var ruleManager = new QueueRuleManager(logger);

        var stallRule = CreateStallRule("Private Rule", enabled: true, privacyType: TorrentPrivacyType.Private, minCompletion: 0, maxCompletion: 100);
        ContextProvider.Set(nameof(StallRule), new List<StallRule> { stallRule });

        var torrent = CreateTorrentMock(isPrivate: false, completionPercentage: 50); // Public torrent

        // Act
        var result = ruleManager.GetMatchingStallRule(torrent);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void GetMatchingStallRule_PrivacyTypeBoth_MatchesPublic()
    {
        // Arrange
        var logger = Substitute.For<ILogger<QueueRuleManager>>();
        var ruleManager = new QueueRuleManager(logger);

        var stallRule = CreateStallRule("Both Rule", enabled: true, privacyType: TorrentPrivacyType.Both, minCompletion: 0, maxCompletion: 100);
        ContextProvider.Set(nameof(StallRule), new List<StallRule> { stallRule });

        var torrent = CreateTorrentMock(isPrivate: false, completionPercentage: 50);

        // Act
        var result = ruleManager.GetMatchingStallRule(torrent);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe(stallRule.Id);
    }

    [Fact]
    public void GetMatchingStallRule_PrivacyTypeBoth_MatchesPrivate()
    {
        // Arrange
        var logger = Substitute.For<ILogger<QueueRuleManager>>();
        var ruleManager = new QueueRuleManager(logger);

        var stallRule = CreateStallRule("Both Rule", enabled: true, privacyType: TorrentPrivacyType.Both, minCompletion: 0, maxCompletion: 100);
        ContextProvider.Set(nameof(StallRule), new List<StallRule> { stallRule });

        var torrent = CreateTorrentMock(isPrivate: true, completionPercentage: 50);

        // Act
        var result = ruleManager.GetMatchingStallRule(torrent);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe(stallRule.Id);
    }

    [Fact]
    public void GetMatchingStallRule_CompletionPercentageBelowMin_ReturnsNull()
    {
        // Arrange
        var logger = Substitute.For<ILogger<QueueRuleManager>>();
        var ruleManager = new QueueRuleManager(logger);

        var stallRule = CreateStallRule("Rule 20-80", enabled: true, privacyType: TorrentPrivacyType.Public, minCompletion: 20, maxCompletion: 80);
        ContextProvider.Set(nameof(StallRule), new List<StallRule> { stallRule });

        var torrent = CreateTorrentMock(isPrivate: false, completionPercentage: 10); // Below 20%

        // Act
        var result = ruleManager.GetMatchingStallRule(torrent);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void GetMatchingStallRule_CompletionPercentageAboveMax_ReturnsNull()
    {
        // Arrange
        var logger = Substitute.For<ILogger<QueueRuleManager>>();
        var ruleManager = new QueueRuleManager(logger);

        var stallRule = CreateStallRule("Rule 20-80", enabled: true, privacyType: TorrentPrivacyType.Public, minCompletion: 20, maxCompletion: 80);
        ContextProvider.Set(nameof(StallRule), new List<StallRule> { stallRule });

        var torrent = CreateTorrentMock(isPrivate: false, completionPercentage: 90); // Above 80%

        // Act
        var result = ruleManager.GetMatchingStallRule(torrent);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void GetMatchingStallRule_CompletionPercentageAtMinBoundary_Matches()
    {
        // Arrange
        var logger = Substitute.For<ILogger<QueueRuleManager>>();
        var ruleManager = new QueueRuleManager(logger);

        var stallRule = CreateStallRule("Rule 20-80", enabled: true, privacyType: TorrentPrivacyType.Public, minCompletion: 20, maxCompletion: 80);
        ContextProvider.Set(nameof(StallRule), new List<StallRule> { stallRule });

        var torrent = CreateTorrentMock(isPrivate: false, completionPercentage: 20.1); // Just above 20%

        // Act
        var result = ruleManager.GetMatchingStallRule(torrent);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe(stallRule.Id);
    }

    [Fact]
    public void GetMatchingStallRule_CompletionPercentageAtMaxBoundary_Matches()
    {
        // Arrange
        var logger = Substitute.For<ILogger<QueueRuleManager>>();
        var ruleManager = new QueueRuleManager(logger);

        var stallRule = CreateStallRule("Rule 20-80", enabled: true, privacyType: TorrentPrivacyType.Public, minCompletion: 20, maxCompletion: 80);
        ContextProvider.Set(nameof(StallRule), new List<StallRule> { stallRule });

        var torrent = CreateTorrentMock(isPrivate: false, completionPercentage: 80); // Exactly at 80%

        // Act
        var result = ruleManager.GetMatchingStallRule(torrent);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe(stallRule.Id);
    }

    [Fact]
    public void GetMatchingSlowRule_NoRules_ReturnsNull()
    {
        // Arrange
        var logger = Substitute.For<ILogger<QueueRuleManager>>();
        var ruleManager = new QueueRuleManager(logger);

        ContextProvider.Set(nameof(SlowRule), new List<SlowRule>());

        var torrent = CreateTorrentMock();

        // Act
        var result = ruleManager.GetMatchingSlowRule(torrent);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void GetMatchingSlowRule_OneMatch_ReturnsRule()
    {
        // Arrange
        var logger = Substitute.For<ILogger<QueueRuleManager>>();
        var ruleManager = new QueueRuleManager(logger);

        var slowRule = CreateSlowRule("Slow Rule", enabled: true, privacyType: TorrentPrivacyType.Public, minCompletion: 0, maxCompletion: 100);
        ContextProvider.Set(nameof(SlowRule), new List<SlowRule> { slowRule });

        var torrent = CreateTorrentMock(isPrivate: false, completionPercentage: 50);

        // Act
        var result = ruleManager.GetMatchingSlowRule(torrent);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe(slowRule.Id);
        result.Name.ShouldBe("Slow Rule");
    }

    [Fact]
    public void GetMatchingSlowRule_MultipleMatches_ReturnsNull_LogsWarning()
    {
        // Arrange
        var logger = Substitute.For<ILogger<QueueRuleManager>>();
        var ruleManager = new QueueRuleManager(logger);

        var slowRule1 = CreateSlowRule("Slow 1", enabled: true, privacyType: TorrentPrivacyType.Public, minCompletion: 0, maxCompletion: 100);
        var slowRule2 = CreateSlowRule("Slow 2", enabled: true, privacyType: TorrentPrivacyType.Public, minCompletion: 0, maxCompletion: 100);
        ContextProvider.Set(nameof(SlowRule), new List<SlowRule> { slowRule1, slowRule2 });

        var torrent = CreateTorrentMock(isPrivate: false, completionPercentage: 50);

        // Act
        var result = ruleManager.GetMatchingSlowRule(torrent);

        // Assert
        result.ShouldBeNull();
        logger.ReceivedLogContaining(LogLevel.Warning, "multiple");
    }

    [Fact]
    public void GetMatchingSlowRule_FileSizeAboveIgnoreThreshold_ReturnsNull()
    {
        // Arrange
        var logger = Substitute.For<ILogger<QueueRuleManager>>();
        var ruleManager = new QueueRuleManager(logger);

        var slowRule = CreateSlowRule("Size Limited", enabled: true, privacyType: TorrentPrivacyType.Public, minCompletion: 0, maxCompletion: 100, ignoreAboveSize: "50 MB");
        ContextProvider.Set(nameof(SlowRule), new List<SlowRule> { slowRule });

        var torrent = CreateTorrentMock(isPrivate: false, completionPercentage: 50, size: "100 MB"); // Torrent is 100 MB, above 50 MB threshold

        // Act
        var result = ruleManager.GetMatchingSlowRule(torrent);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void GetMatchingSlowRule_FileSizeBelowIgnoreThreshold_Matches()
    {
        // Arrange
        var logger = Substitute.For<ILogger<QueueRuleManager>>();
        var ruleManager = new QueueRuleManager(logger);

        var slowRule = CreateSlowRule("Size Limited", enabled: true, privacyType: TorrentPrivacyType.Public, minCompletion: 0, maxCompletion: 100, ignoreAboveSize: "50 MB");
        ContextProvider.Set(nameof(SlowRule), new List<SlowRule> { slowRule });

        var torrent = CreateTorrentMock(isPrivate: false, completionPercentage: 50, size: "30 MB"); // Torrent is 30 MB, below 50 MB threshold

        // Act
        var result = ruleManager.GetMatchingSlowRule(torrent);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe(slowRule.Id);
    }

    [Fact]
    public void GetMatchingSlowRule_NoIgnoreSizeSet_Matches()
    {
        // Arrange
        var logger = Substitute.For<ILogger<QueueRuleManager>>();
        var ruleManager = new QueueRuleManager(logger);

        var slowRule = CreateSlowRule("No Size Limit", enabled: true, privacyType: TorrentPrivacyType.Public, minCompletion: 0, maxCompletion: 100, ignoreAboveSize: string.Empty);
        ContextProvider.Set(nameof(SlowRule), new List<SlowRule> { slowRule });

        var torrent = CreateTorrentMock(isPrivate: false, completionPercentage: 50, size: "1 GB"); // Any size should match

        // Act
        var result = ruleManager.GetMatchingSlowRule(torrent);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe(slowRule.Id);
    }

    private static ITorrentItemWrapper CreateTorrentMock(
        bool isPrivate = false,
        double completionPercentage = 50,
        string size = "100 MB")
    {
        var torrent = Substitute.For<ITorrentItemWrapper>();
        torrent.Hash.Returns("test-hash");
        torrent.Name.Returns("Test Torrent");
        torrent.IsPrivate.Returns(isPrivate);
        torrent.CompletionPercentage.Returns(completionPercentage);
        torrent.Size.Returns(ByteSize.Parse(size).Bytes);
        torrent.DownloadedBytes.Returns(0);
        torrent.DownloadSpeed.Returns(0);
        torrent.Eta.Returns(3600);
        return torrent;
    }

    private static StallRule CreateStallRule(
        string name,
        bool enabled,
        TorrentPrivacyType privacyType,
        ushort minCompletion,
        ushort maxCompletion)
    {
        return new StallRule
        {
            Id = Guid.NewGuid(),
            QueueCleanerConfigId = Guid.NewGuid(),
            Name = name,
            Enabled = enabled,
            MaxStrikes = 3,
            PrivacyType = privacyType,
            MinCompletionPercentage = minCompletion,
            MaxCompletionPercentage = maxCompletion,
            ResetStrikesOnProgress = false,
            MinimumProgress = null,
            DeletePrivateTorrentsFromClient = false,
        };
    }

    private static SlowRule CreateSlowRule(
        string name,
        bool enabled,
        TorrentPrivacyType privacyType,
        ushort minCompletion,
        ushort maxCompletion,
        string? ignoreAboveSize = null)
    {
        return new SlowRule
        {
            Id = Guid.NewGuid(),
            QueueCleanerConfigId = Guid.NewGuid(),
            Name = name,
            Enabled = enabled,
            MaxStrikes = 3,
            PrivacyType = privacyType,
            MinCompletionPercentage = minCompletion,
            MaxCompletionPercentage = maxCompletion,
            ResetStrikesOnProgress = false,
            MaxTimeHours = 1,
            MinSpeed = "1 MB",
            IgnoreAboveSize = ignoreAboveSize ?? string.Empty,
            DeletePrivateTorrentsFromClient = false,
        };
    }
}
