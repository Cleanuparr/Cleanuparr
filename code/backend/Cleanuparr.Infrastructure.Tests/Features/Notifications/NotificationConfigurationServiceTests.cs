using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Notifications;
using Cleanuparr.Infrastructure.Tests.TestHelpers;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration.Notification;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.Notifications;

public class NotificationConfigurationServiceTests : IDisposable
{
    private readonly DataContext _context;
    private readonly ILogger<NotificationConfigurationService> _logger;
    private readonly NotificationConfigurationService _service;

    public NotificationConfigurationServiceTests()
    {
        var options = new DbContextOptionsBuilder<DataContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new DataContext(options);
        _logger = Substitute.For<ILogger<NotificationConfigurationService>>();
        _service = new NotificationConfigurationService(_context, _logger);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    #region GetActiveProvidersAsync Tests

    [Fact]
    public async Task GetActiveProvidersAsync_NoProviders_ReturnsEmptyList()
    {
        // Act
        var result = await _service.GetActiveProvidersAsync();

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetActiveProvidersAsync_WithEnabledProvider_ReturnsProvider()
    {
        // Arrange
        var config = CreateNotifiarrConfig("Test Provider", isEnabled: true);
        _context.Set<NotificationConfig>().Add(config);
        await _context.SaveChangesAsync();
        await _service.InvalidateCacheAsync();

        // Act
        var result = await _service.GetActiveProvidersAsync();

        // Assert
        result.ShouldHaveSingleItem();
        result[0].Name.ShouldBe("Test Provider");
    }

    [Fact]
    public async Task GetActiveProvidersAsync_WithDisabledProvider_ReturnsEmptyList()
    {
        // Arrange
        var config = CreateNotifiarrConfig("Disabled Provider", isEnabled: false);
        _context.Set<NotificationConfig>().Add(config);
        await _context.SaveChangesAsync();
        await _service.InvalidateCacheAsync();

        // Act
        var result = await _service.GetActiveProvidersAsync();

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetActiveProvidersAsync_CachesResults()
    {
        // Arrange
        var config = CreateNotifiarrConfig("Test Provider", isEnabled: true);
        _context.Set<NotificationConfig>().Add(config);
        await _context.SaveChangesAsync();
        await _service.InvalidateCacheAsync();

        // Act - Call twice
        var result1 = await _service.GetActiveProvidersAsync();
        var result2 = await _service.GetActiveProvidersAsync();

        // Assert - Both calls should return same data
        result1.ShouldHaveSingleItem();
        result2.ShouldHaveSingleItem();
        result1[0].Id.ShouldBe(result2[0].Id);
    }

    [Fact]
    public async Task GetActiveProvidersAsync_WithMixedProviders_ReturnsOnlyEnabled()
    {
        // Arrange
        var enabledConfig = CreateNotifiarrConfig("Enabled", isEnabled: true);
        var disabledConfig = CreateNotifiarrConfig("Disabled", isEnabled: false);
        _context.Set<NotificationConfig>().AddRange(enabledConfig, disabledConfig);
        await _context.SaveChangesAsync();
        await _service.InvalidateCacheAsync();

        // Act
        var result = await _service.GetActiveProvidersAsync();

        // Assert
        result.ShouldHaveSingleItem();
        result[0].Name.ShouldBe("Enabled");
    }

    #endregion

    #region GetProvidersForEventAsync Tests

    [Fact]
    public async Task GetProvidersForEventAsync_NoMatchingProviders_ReturnsEmptyList()
    {
        // Arrange
        var config = CreateNotifiarrConfig("Test", isEnabled: true, onStalledStrike: false);
        _context.Set<NotificationConfig>().Add(config);
        await _context.SaveChangesAsync();
        await _service.InvalidateCacheAsync();

        // Act
        var result = await _service.GetProvidersForEventAsync(NotificationEventType.StalledStrike);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetProvidersForEventAsync_WithMatchingProvider_ReturnsProvider()
    {
        // Arrange
        var config = CreateNotifiarrConfig("Test", isEnabled: true, onStalledStrike: true);
        _context.Set<NotificationConfig>().Add(config);
        await _context.SaveChangesAsync();
        await _service.InvalidateCacheAsync();

        // Act
        var result = await _service.GetProvidersForEventAsync(NotificationEventType.StalledStrike);

        // Assert
        result.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task GetProvidersForEventAsync_TestEvent_AlwaysReturnsEnabledProviders()
    {
        // Arrange
        var config = CreateNotifiarrConfig("Test", isEnabled: true, onStalledStrike: false);
        _context.Set<NotificationConfig>().Add(config);
        await _context.SaveChangesAsync();
        await _service.InvalidateCacheAsync();

        // Act
        var result = await _service.GetProvidersForEventAsync(NotificationEventType.Test);

        // Assert
        result.ShouldHaveSingleItem();
    }

    [Theory]
    [InlineData(NotificationEventType.FailedImportStrike)]
    [InlineData(NotificationEventType.StalledStrike)]
    [InlineData(NotificationEventType.SlowSpeedStrike)]
    [InlineData(NotificationEventType.SlowTimeStrike)]
    [InlineData(NotificationEventType.QueueItemDeleted)]
    [InlineData(NotificationEventType.DownloadCleaned)]
    [InlineData(NotificationEventType.CategoryChanged)]
    [InlineData(NotificationEventType.SearchTriggered)]
    [InlineData(NotificationEventType.SearchItemGrabbed)]
    public async Task GetProvidersForEventAsync_ReturnsProviderForCorrectEvents(NotificationEventType eventType)
    {
        // Arrange
        bool onFailedImport = eventType == NotificationEventType.FailedImportStrike;
        bool onStalled = eventType == NotificationEventType.StalledStrike;
        bool onSlow = eventType is NotificationEventType.SlowSpeedStrike or NotificationEventType.SlowTimeStrike;
        bool onDeleted = eventType == NotificationEventType.QueueItemDeleted;
        bool onCleaned = eventType == NotificationEventType.DownloadCleaned;
        bool onCategory = eventType == NotificationEventType.CategoryChanged;
        bool onSearchTriggered = eventType == NotificationEventType.SearchTriggered;
        bool onSearchItemGrabbed = eventType == NotificationEventType.SearchItemGrabbed;

        var config = new NotificationConfig
        {
            Id = Guid.NewGuid(),
            Name = "Test Provider",
            Type = NotificationProviderType.Notifiarr,
            IsEnabled = true,
            OnFailedImportStrike = onFailedImport,
            OnStalledStrike = onStalled,
            OnSlowStrike = onSlow,
            OnQueueItemDeleted = onDeleted,
            OnDownloadCleaned = onCleaned,
            OnCategoryChanged = onCategory,
            OnSearchTriggered = onSearchTriggered,
            OnSearchItemGrabbed = onSearchItemGrabbed,
            NotifiarrConfiguration = new NotifiarrConfig
            {
                Id = Guid.NewGuid(),
                ApiKey = "testapikey1234567890",
                ChannelId = "123456789012345678"
            }
        };
        _context.Set<NotificationConfig>().Add(config);
        await _context.SaveChangesAsync();
        await _service.InvalidateCacheAsync();

        // Act
        var result = await _service.GetProvidersForEventAsync(eventType);

        // Assert
        result.ShouldHaveSingleItem();
    }

    #endregion

    #region GetProviderByIdAsync Tests

    [Fact]
    public async Task GetProviderByIdAsync_ProviderExists_ReturnsProvider()
    {
        // Arrange
        var config = CreateNotifiarrConfig("Test", isEnabled: true);
        _context.Set<NotificationConfig>().Add(config);
        await _context.SaveChangesAsync();
        await _service.InvalidateCacheAsync();

        // Act
        var result = await _service.GetProviderByIdAsync(config.Id);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe(config.Id);
        result.Name.ShouldBe("Test");
    }

    [Fact]
    public async Task GetProviderByIdAsync_ProviderDoesNotExist_ReturnsNull()
    {
        // Act
        var result = await _service.GetProviderByIdAsync(Guid.NewGuid());

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetProviderByIdAsync_DisabledProvider_ReturnsNull()
    {
        // Arrange
        var config = CreateNotifiarrConfig("Disabled", isEnabled: false);
        _context.Set<NotificationConfig>().Add(config);
        await _context.SaveChangesAsync();
        await _service.InvalidateCacheAsync();

        // Act
        var result = await _service.GetProviderByIdAsync(config.Id);

        // Assert
        result.ShouldBeNull();
    }

    #endregion

    #region InvalidateCacheAsync Tests

    [Fact]
    public async Task InvalidateCacheAsync_RefreshesDataOnNextCall()
    {
        // Arrange
        var config1 = CreateNotifiarrConfig("Provider 1", isEnabled: true);
        _context.Set<NotificationConfig>().Add(config1);
        await _context.SaveChangesAsync();
        await _service.InvalidateCacheAsync();

        // First call to populate cache
        var result1 = await _service.GetActiveProvidersAsync();
        result1.ShouldHaveSingleItem();

        // Add another provider
        var config2 = CreateNotifiarrConfig("Provider 2", isEnabled: true);
        _context.Set<NotificationConfig>().Add(config2);
        await _context.SaveChangesAsync();

        // Without invalidation, should return cached result
        var result2 = await _service.GetActiveProvidersAsync();
        result2.ShouldHaveSingleItem();

        // After invalidation, should return updated result
        await _service.InvalidateCacheAsync();
        var result3 = await _service.GetActiveProvidersAsync();
        result3.Count.ShouldBe(2);
    }

    [Fact]
    public async Task InvalidateCacheAsync_LogsDebugMessage()
    {
        // Act
        await _service.InvalidateCacheAsync();

        // Assert
        _logger.ReceivedLogContaining(LogLevel.Debug, "cache invalidated");
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task GetProvidersForEventAsync_UnknownEventType_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var config = CreateNotifiarrConfig("Test", isEnabled: true);
        _context.Set<NotificationConfig>().Add(config);
        await _context.SaveChangesAsync();
        await _service.InvalidateCacheAsync();

        var unknownEventType = (NotificationEventType)999;

        // Act & Assert
        await Should.ThrowAsync<ArgumentOutOfRangeException>(
            () => _service.GetProvidersForEventAsync(unknownEventType));
    }

    [Fact]
    public async Task GetActiveProvidersAsync_DatabaseError_ReturnsEmptyListAndLogsError()
    {
        // Arrange - dispose context to simulate database error
        var options = new DbContextOptionsBuilder<DataContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var disposedContext = new DataContext(options);
        var logger = Substitute.For<ILogger<NotificationConfigurationService>>();
        var service = new NotificationConfigurationService(disposedContext, logger);

        await disposedContext.DisposeAsync();

        // Act
        var result = await service.GetActiveProvidersAsync();

        // Assert
        result.ShouldBeEmpty();
        logger.ReceivedLogContaining(LogLevel.Error, "Failed to load notification providers");
    }

    #endregion

    #region Provider Type Mapping Tests


    [Theory]
    [MemberData(nameof(NotificationProviderTypes))]
    public async Task GetActiveProvidersAsync_MapsProviderTypeCorrectly(NotificationProviderType providerType)
    {
        // Arrange
        var config = CreateConfigForType(providerType, "Test Provider", isEnabled: true);
        _context.Set<NotificationConfig>().Add(config);
        await _context.SaveChangesAsync();
        await _service.InvalidateCacheAsync();

        // Act
        var result = await _service.GetActiveProvidersAsync();

        // Assert
        result.ShouldHaveSingleItem();
        result[0].Type.ShouldBe(providerType);
        result[0].Name.ShouldBe("Test Provider");
        result[0].Configuration.ShouldNotBeNull();
    }

    [Theory]
    [MemberData(nameof(NotificationProviderTypes))]
    public async Task GetProvidersForEventAsync_ReturnsProviderForAllTypes(NotificationProviderType providerType)
    {
        // Arrange
        var config = CreateConfigForType(providerType, "Test", isEnabled: true);
        _context.Set<NotificationConfig>().Add(config);
        await _context.SaveChangesAsync();
        await _service.InvalidateCacheAsync();

        // Act
        var result = await _service.GetProvidersForEventAsync(NotificationEventType.StalledStrike);

        // Assert
        result.ShouldHaveSingleItem();
        result[0].Type.ShouldBe(providerType);
    }

    #endregion

    #region Helper Methods

    private static NotificationConfig CreateConfigForType(
        NotificationProviderType providerType,
        string name,
        bool isEnabled)
    {
        return providerType switch
        {
            NotificationProviderType.Notifiarr => CreateNotifiarrConfig(name, isEnabled),
            NotificationProviderType.Apprise => CreateAppriseConfig(name, isEnabled),
            NotificationProviderType.Ntfy => CreateNtfyConfig(name, isEnabled),
            NotificationProviderType.Pushover => CreatePushoverConfig(name, isEnabled),
            NotificationProviderType.Telegram => CreateTelegramConfig(name, isEnabled),
            NotificationProviderType.Discord => CreateDiscordConfig(name, isEnabled),
            NotificationProviderType.Gotify => CreateGotifyConfig(name, isEnabled),
            _ => throw new ArgumentOutOfRangeException(nameof(providerType))
        };
    }

    private static NotificationConfig CreateNotifiarrConfig(
        string name,
        bool isEnabled,
        bool onStalledStrike = true,
        bool onFailedImport = true,
        bool onSlow = true,
        bool onDeleted = true,
        bool onCleaned = true,
        bool onCategory = true)
    {
        return new NotificationConfig
        {
            Id = Guid.NewGuid(),
            Name = name,
            Type = NotificationProviderType.Notifiarr,
            IsEnabled = isEnabled,
            OnStalledStrike = onStalledStrike,
            OnFailedImportStrike = onFailedImport,
            OnSlowStrike = onSlow,
            OnQueueItemDeleted = onDeleted,
            OnDownloadCleaned = onCleaned,
            OnCategoryChanged = onCategory,
            NotifiarrConfiguration = new NotifiarrConfig
            {
                Id = Guid.NewGuid(),
                ApiKey = "testapikey1234567890",
                ChannelId = "123456789012345678"
            }
        };
    }

    private static NotificationConfig CreateAppriseConfig(string name, bool isEnabled)
    {
        return new NotificationConfig
        {
            Id = Guid.NewGuid(),
            Name = name,
            Type = NotificationProviderType.Apprise,
            IsEnabled = isEnabled,
            OnStalledStrike = true,
            OnFailedImportStrike = true,
            OnSlowStrike = true,
            OnQueueItemDeleted = true,
            OnDownloadCleaned = true,
            OnCategoryChanged = true,
            AppriseConfiguration = new AppriseConfig
            {
                Id = Guid.NewGuid(),
                Url = "http://localhost:8000",
                Key = "testkey"
            }
        };
    }

    private static NotificationConfig CreateNtfyConfig(string name, bool isEnabled)
    {
        return new NotificationConfig
        {
            Id = Guid.NewGuid(),
            Name = name,
            Type = NotificationProviderType.Ntfy,
            IsEnabled = isEnabled,
            OnStalledStrike = true,
            OnFailedImportStrike = true,
            OnSlowStrike = true,
            OnQueueItemDeleted = true,
            OnDownloadCleaned = true,
            OnCategoryChanged = true,
            NtfyConfiguration = new NtfyConfig
            {
                Id = Guid.NewGuid(),
                ServerUrl = "https://ntfy.sh",
                Topics = ["test-topic"]
            }
        };
    }

    private static NotificationConfig CreatePushoverConfig(string name, bool isEnabled)
    {
        return new NotificationConfig
        {
            Id = Guid.NewGuid(),
            Name = name,
            Type = NotificationProviderType.Pushover,
            IsEnabled = isEnabled,
            OnStalledStrike = true,
            OnFailedImportStrike = true,
            OnSlowStrike = true,
            OnQueueItemDeleted = true,
            OnDownloadCleaned = true,
            OnCategoryChanged = true,
            PushoverConfiguration = new PushoverConfig
            {
                Id = Guid.NewGuid(),
                ApiToken = "test_api_token_1234567890abcd",
                UserKey = "test_user_key_1234567890abcde"
            }
        };
    }

    private static NotificationConfig CreateTelegramConfig(string name, bool isEnabled)
    {
        return new NotificationConfig
        {
            Id = Guid.NewGuid(),
            Name = name,
            Type = NotificationProviderType.Telegram,
            IsEnabled = isEnabled,
            OnStalledStrike = true,
            OnFailedImportStrike = true,
            OnSlowStrike = true,
            OnQueueItemDeleted = true,
            OnDownloadCleaned = true,
            OnCategoryChanged = true,
            TelegramConfiguration = new TelegramConfig()
            {
                Id = Guid.NewGuid(),
                BotToken = "test_bot_token_1234567890abcd",
                ChatId =  "1234567890",
                TopicId = "-1234567890",
                SendSilently = true
            }
        };
    }

    private static NotificationConfig CreateDiscordConfig(string name, bool isEnabled)
    {
        return new NotificationConfig
        {
            Id = Guid.NewGuid(),
            Name = name,
            Type = NotificationProviderType.Discord,
            IsEnabled = isEnabled,
            OnStalledStrike = true,
            OnFailedImportStrike = true,
            OnSlowStrike = true,
            OnQueueItemDeleted = true,
            OnDownloadCleaned = true,
            OnCategoryChanged = true,
            DiscordConfiguration = new DiscordConfig
            {
                Id = Guid.NewGuid(),
                WebhookUrl = "http://localhost:8000",
                AvatarUrl =  "https://example.com/avatar.png",
                Username = "test_username",
            }
        };
    }

    private static NotificationConfig CreateGotifyConfig(string name, bool isEnabled)
    {
        return new NotificationConfig
        {
            Id = Guid.NewGuid(),
            Name = name,
            Type = NotificationProviderType.Gotify,
            IsEnabled = isEnabled,
            OnStalledStrike = true,
            OnFailedImportStrike = true,
            OnSlowStrike = true,
            OnQueueItemDeleted = true,
            OnDownloadCleaned = true,
            OnCategoryChanged = true,
            GotifyConfiguration = new GotifyConfig
            {
                Id = Guid.NewGuid(),
                ServerUrl = "http://localhost:8000",
                ApplicationToken =  "test_application_token",
            }
        };
    }

    #endregion

    public static IEnumerable<object[]> NotificationProviderTypes =>
    [
        ..Enum.GetValues<NotificationProviderType>()
            .Cast<Object>()
            .Select(x => new[] { x })
            .ToList()
    ];
}
