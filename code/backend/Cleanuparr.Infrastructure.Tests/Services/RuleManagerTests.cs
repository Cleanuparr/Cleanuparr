using Cleanuparr.Domain.Entities;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Infrastructure.Services;
using Cleanuparr.Persistence.Models.Configuration.QueueCleaner;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Services;

public class RuleManagerTests
{
    [Fact]
    public void GetMatchingStallRule_NoRules_ReturnsNull()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<RuleManager>>();
        var ruleManager = new RuleManager(loggerMock.Object);

        ContextProvider.Set(nameof(StallRule), new List<StallRule>());

        var torrentMock = CreateTorrentMock();

        // Act
        var result = ruleManager.GetMatchingStallRule(torrentMock.Object);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetMatchingStallRule_OneMatch_ReturnsRule()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<RuleManager>>();
        var ruleManager = new RuleManager(loggerMock.Object);

        var stallRule = CreateStallRule("Test Rule", enabled: true, privacyType: TorrentPrivacyType.Public, minCompletion: 0, maxCompletion: 100);
        ContextProvider.Set(nameof(StallRule), new List<StallRule> { stallRule });

        var torrentMock = CreateTorrentMock(isPrivate: false, completionPercentage: 50);

        // Act
        var result = ruleManager.GetMatchingStallRule(torrentMock.Object);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(stallRule.Id, result.Id);
        Assert.Equal("Test Rule", result.Name);
    }

    [Fact]
    public void GetMatchingStallRule_MultipleMatches_ReturnsNull_LogsWarning()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<RuleManager>>();
        var ruleManager = new RuleManager(loggerMock.Object);

        var stallRule1 = CreateStallRule("Rule 1", enabled: true, privacyType: TorrentPrivacyType.Public, minCompletion: 0, maxCompletion: 100);
        var stallRule2 = CreateStallRule("Rule 2", enabled: true, privacyType: TorrentPrivacyType.Public, minCompletion: 0, maxCompletion: 100);
        ContextProvider.Set(nameof(StallRule), new List<StallRule> { stallRule1, stallRule2 });

        var torrentMock = CreateTorrentMock(isPrivate: false, completionPercentage: 50);

        // Act
        var result = ruleManager.GetMatchingStallRule(torrentMock.Object);

        // Assert
        Assert.Null(result);
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("multiple")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void GetMatchingStallRule_DisabledRule_ReturnsNull()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<RuleManager>>();
        var ruleManager = new RuleManager(loggerMock.Object);

        var stallRule = CreateStallRule("Disabled Rule", enabled: false, privacyType: TorrentPrivacyType.Public, minCompletion: 0, maxCompletion: 100);
        ContextProvider.Set(nameof(StallRule), new List<StallRule> { stallRule });

        var torrentMock = CreateTorrentMock(isPrivate: false, completionPercentage: 50);

        // Act
        var result = ruleManager.GetMatchingStallRule(torrentMock.Object);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetMatchingStallRule_PrivacyTypeMismatch_Public_ReturnsNull()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<RuleManager>>();
        var ruleManager = new RuleManager(loggerMock.Object);

        var stallRule = CreateStallRule("Public Rule", enabled: true, privacyType: TorrentPrivacyType.Public, minCompletion: 0, maxCompletion: 100);
        ContextProvider.Set(nameof(StallRule), new List<StallRule> { stallRule });

        var torrentMock = CreateTorrentMock(isPrivate: true, completionPercentage: 50); // Private torrent

        // Act
        var result = ruleManager.GetMatchingStallRule(torrentMock.Object);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetMatchingStallRule_PrivacyTypeMismatch_Private_ReturnsNull()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<RuleManager>>();
        var ruleManager = new RuleManager(loggerMock.Object);

        var stallRule = CreateStallRule("Private Rule", enabled: true, privacyType: TorrentPrivacyType.Private, minCompletion: 0, maxCompletion: 100);
        ContextProvider.Set(nameof(StallRule), new List<StallRule> { stallRule });

        var torrentMock = CreateTorrentMock(isPrivate: false, completionPercentage: 50); // Public torrent

        // Act
        var result = ruleManager.GetMatchingStallRule(torrentMock.Object);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetMatchingStallRule_PrivacyTypeBoth_MatchesPublic()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<RuleManager>>();
        var ruleManager = new RuleManager(loggerMock.Object);

        var stallRule = CreateStallRule("Both Rule", enabled: true, privacyType: TorrentPrivacyType.Both, minCompletion: 0, maxCompletion: 100);
        ContextProvider.Set(nameof(StallRule), new List<StallRule> { stallRule });

        var torrentMock = CreateTorrentMock(isPrivate: false, completionPercentage: 50);

        // Act
        var result = ruleManager.GetMatchingStallRule(torrentMock.Object);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(stallRule.Id, result.Id);
    }

    [Fact]
    public void GetMatchingStallRule_PrivacyTypeBoth_MatchesPrivate()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<RuleManager>>();
        var ruleManager = new RuleManager(loggerMock.Object);

        var stallRule = CreateStallRule("Both Rule", enabled: true, privacyType: TorrentPrivacyType.Both, minCompletion: 0, maxCompletion: 100);
        ContextProvider.Set(nameof(StallRule), new List<StallRule> { stallRule });

        var torrentMock = CreateTorrentMock(isPrivate: true, completionPercentage: 50);

        // Act
        var result = ruleManager.GetMatchingStallRule(torrentMock.Object);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(stallRule.Id, result.Id);
    }

    [Fact]
    public void GetMatchingStallRule_CompletionPercentageBelowMin_ReturnsNull()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<RuleManager>>();
        var ruleManager = new RuleManager(loggerMock.Object);

        var stallRule = CreateStallRule("Rule 20-80", enabled: true, privacyType: TorrentPrivacyType.Public, minCompletion: 20, maxCompletion: 80);
        ContextProvider.Set(nameof(StallRule), new List<StallRule> { stallRule });

        var torrentMock = CreateTorrentMock(isPrivate: false, completionPercentage: 10); // Below 20%

        // Act
        var result = ruleManager.GetMatchingStallRule(torrentMock.Object);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetMatchingStallRule_CompletionPercentageAboveMax_ReturnsNull()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<RuleManager>>();
        var ruleManager = new RuleManager(loggerMock.Object);

        var stallRule = CreateStallRule("Rule 20-80", enabled: true, privacyType: TorrentPrivacyType.Public, minCompletion: 20, maxCompletion: 80);
        ContextProvider.Set(nameof(StallRule), new List<StallRule> { stallRule });

        var torrentMock = CreateTorrentMock(isPrivate: false, completionPercentage: 90); // Above 80%

        // Act
        var result = ruleManager.GetMatchingStallRule(torrentMock.Object);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetMatchingStallRule_CompletionPercentageAtMinBoundary_Matches()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<RuleManager>>();
        var ruleManager = new RuleManager(loggerMock.Object);

        var stallRule = CreateStallRule("Rule 20-80", enabled: true, privacyType: TorrentPrivacyType.Public, minCompletion: 20, maxCompletion: 80);
        ContextProvider.Set(nameof(StallRule), new List<StallRule> { stallRule });

        var torrentMock = CreateTorrentMock(isPrivate: false, completionPercentage: 20.1); // Just above 20%

        // Act
        var result = ruleManager.GetMatchingStallRule(torrentMock.Object);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(stallRule.Id, result.Id);
    }

    [Fact]
    public void GetMatchingStallRule_CompletionPercentageAtMaxBoundary_Matches()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<RuleManager>>();
        var ruleManager = new RuleManager(loggerMock.Object);

        var stallRule = CreateStallRule("Rule 20-80", enabled: true, privacyType: TorrentPrivacyType.Public, minCompletion: 20, maxCompletion: 80);
        ContextProvider.Set(nameof(StallRule), new List<StallRule> { stallRule });

        var torrentMock = CreateTorrentMock(isPrivate: false, completionPercentage: 80); // Exactly at 80%

        // Act
        var result = ruleManager.GetMatchingStallRule(torrentMock.Object);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(stallRule.Id, result.Id);
    }

    [Fact]
    public void GetMatchingSlowRule_NoRules_ReturnsNull()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<RuleManager>>();
        var ruleManager = new RuleManager(loggerMock.Object);

        ContextProvider.Set(nameof(SlowRule), new List<SlowRule>());

        var torrentMock = CreateTorrentMock();

        // Act
        var result = ruleManager.GetMatchingSlowRule(torrentMock.Object);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetMatchingSlowRule_OneMatch_ReturnsRule()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<RuleManager>>();
        var ruleManager = new RuleManager(loggerMock.Object);

        var slowRule = CreateSlowRule("Slow Rule", enabled: true, privacyType: TorrentPrivacyType.Public, minCompletion: 0, maxCompletion: 100);
        ContextProvider.Set(nameof(SlowRule), new List<SlowRule> { slowRule });

        var torrentMock = CreateTorrentMock(isPrivate: false, completionPercentage: 50);

        // Act
        var result = ruleManager.GetMatchingSlowRule(torrentMock.Object);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(slowRule.Id, result.Id);
        Assert.Equal("Slow Rule", result.Name);
    }

    [Fact]
    public void GetMatchingSlowRule_MultipleMatches_ReturnsNull_LogsWarning()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<RuleManager>>();
        var ruleManager = new RuleManager(loggerMock.Object);

        var slowRule1 = CreateSlowRule("Slow 1", enabled: true, privacyType: TorrentPrivacyType.Public, minCompletion: 0, maxCompletion: 100);
        var slowRule2 = CreateSlowRule("Slow 2", enabled: true, privacyType: TorrentPrivacyType.Public, minCompletion: 0, maxCompletion: 100);
        ContextProvider.Set(nameof(SlowRule), new List<SlowRule> { slowRule1, slowRule2 });

        var torrentMock = CreateTorrentMock(isPrivate: false, completionPercentage: 50);

        // Act
        var result = ruleManager.GetMatchingSlowRule(torrentMock.Object);

        // Assert
        Assert.Null(result);
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("multiple")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void GetMatchingSlowRule_FileSizeAboveIgnoreThreshold_ReturnsNull()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<RuleManager>>();
        var ruleManager = new RuleManager(loggerMock.Object);

        var slowRule = CreateSlowRule("Size Limited", enabled: true, privacyType: TorrentPrivacyType.Public, minCompletion: 0, maxCompletion: 100, ignoreAboveSize: "50 MB");
        ContextProvider.Set(nameof(SlowRule), new List<SlowRule> { slowRule });

        var torrentMock = CreateTorrentMock(isPrivate: false, completionPercentage: 50, size: "100 MB"); // Torrent is 100 MB, above 50 MB threshold

        // Act
        var result = ruleManager.GetMatchingSlowRule(torrentMock.Object);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetMatchingSlowRule_FileSizeBelowIgnoreThreshold_Matches()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<RuleManager>>();
        var ruleManager = new RuleManager(loggerMock.Object);

        var slowRule = CreateSlowRule("Size Limited", enabled: true, privacyType: TorrentPrivacyType.Public, minCompletion: 0, maxCompletion: 100, ignoreAboveSize: "50 MB");
        ContextProvider.Set(nameof(SlowRule), new List<SlowRule> { slowRule });

        var torrentMock = CreateTorrentMock(isPrivate: false, completionPercentage: 50, size: "30 MB"); // Torrent is 30 MB, below 50 MB threshold

        // Act
        var result = ruleManager.GetMatchingSlowRule(torrentMock.Object);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(slowRule.Id, result.Id);
    }

    [Fact]
    public void GetMatchingSlowRule_NoIgnoreSizeSet_Matches()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<RuleManager>>();
        var ruleManager = new RuleManager(loggerMock.Object);

        var slowRule = CreateSlowRule("No Size Limit", enabled: true, privacyType: TorrentPrivacyType.Public, minCompletion: 0, maxCompletion: 100, ignoreAboveSize: string.Empty);
        ContextProvider.Set(nameof(SlowRule), new List<SlowRule> { slowRule });

        var torrentMock = CreateTorrentMock(isPrivate: false, completionPercentage: 50, size: "1 GB"); // Any size should match

        // Act
        var result = ruleManager.GetMatchingSlowRule(torrentMock.Object);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(slowRule.Id, result.Id);
    }

    private static Mock<ITorrentItem> CreateTorrentMock(
        bool isPrivate = false,
        double completionPercentage = 50,
        string size = "100 MB")
    {
        var torrentMock = new Mock<ITorrentItem>();
        torrentMock.SetupGet(t => t.Hash).Returns("test-hash");
        torrentMock.SetupGet(t => t.Name).Returns("Test Torrent");
        torrentMock.SetupGet(t => t.IsPrivate).Returns(isPrivate);
        torrentMock.SetupGet(t => t.CompletionPercentage).Returns(completionPercentage);
        torrentMock.SetupGet(t => t.Size).Returns(ByteSize.Parse(size).Bytes);
        torrentMock.SetupGet(t => t.Trackers).Returns(Array.Empty<string>());
        torrentMock.SetupGet(t => t.DownloadedBytes).Returns(0);
        torrentMock.SetupGet(t => t.DownloadSpeed).Returns(0);
        torrentMock.SetupGet(t => t.Eta).Returns(3600);
        return torrentMock;
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
