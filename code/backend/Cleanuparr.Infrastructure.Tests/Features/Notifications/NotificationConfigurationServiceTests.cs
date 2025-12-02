using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Notifications;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration.Notification;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.Notifications;

public class NotificationConfigurationServiceTests : IDisposable
{
    private readonly DataContext _context;
    private readonly Mock<ILogger<NotificationConfigurationService>> _loggerMock;
    private readonly NotificationConfigurationService _service;

    public NotificationConfigurationServiceTests()
    {
        var options = new DbContextOptionsBuilder<DataContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new DataContext(options);
        _loggerMock = new Mock<ILogger<NotificationConfigurationService>>();
        _service = new NotificationConfigurationService(_context, _loggerMock.Object);
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
        Assert.Empty(result);
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
        Assert.Single(result);
        Assert.Equal("Test Provider", result[0].Name);
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
        Assert.Empty(result);
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
        Assert.Single(result1);
        Assert.Single(result2);
        Assert.Equal(result1[0].Id, result2[0].Id);
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
        Assert.Single(result);
        Assert.Equal("Enabled", result[0].Name);
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
        Assert.Empty(result);
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
        Assert.Single(result);
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
        Assert.Single(result);
    }

    [Theory]
    [InlineData(NotificationEventType.FailedImportStrike, true, false, false, false, false, false)]
    [InlineData(NotificationEventType.StalledStrike, false, true, false, false, false, false)]
    [InlineData(NotificationEventType.SlowSpeedStrike, false, false, true, false, false, false)]
    [InlineData(NotificationEventType.SlowTimeStrike, false, false, true, false, false, false)]
    [InlineData(NotificationEventType.QueueItemDeleted, false, false, false, true, false, false)]
    [InlineData(NotificationEventType.DownloadCleaned, false, false, false, false, true, false)]
    [InlineData(NotificationEventType.CategoryChanged, false, false, false, false, false, true)]
    public async Task GetProvidersForEventAsync_ReturnsProviderForCorrectEvents(
        NotificationEventType eventType,
        bool onFailedImport, bool onStalled, bool onSlow,
        bool onDeleted, bool onCleaned, bool onCategory)
    {
        // Arrange
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
        Assert.Single(result);
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
        Assert.NotNull(result);
        Assert.Equal(config.Id, result.Id);
        Assert.Equal("Test", result.Name);
    }

    [Fact]
    public async Task GetProviderByIdAsync_ProviderDoesNotExist_ReturnsNull()
    {
        // Act
        var result = await _service.GetProviderByIdAsync(Guid.NewGuid());

        // Assert
        Assert.Null(result);
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
        Assert.Null(result);
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
        Assert.Single(result1);

        // Add another provider
        var config2 = CreateNotifiarrConfig("Provider 2", isEnabled: true);
        _context.Set<NotificationConfig>().Add(config2);
        await _context.SaveChangesAsync();

        // Without invalidation, should return cached result
        var result2 = await _service.GetActiveProvidersAsync();
        Assert.Single(result2);

        // After invalidation, should return updated result
        await _service.InvalidateCacheAsync();
        var result3 = await _service.GetActiveProvidersAsync();
        Assert.Equal(2, result3.Count);
    }

    [Fact]
    public async Task InvalidateCacheAsync_LogsDebugMessage()
    {
        // Act
        await _service.InvalidateCacheAsync();

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("cache invalidated")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region Helper Methods

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

    #endregion
}
