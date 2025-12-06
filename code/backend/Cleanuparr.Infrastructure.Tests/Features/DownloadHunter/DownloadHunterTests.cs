using Cleanuparr.Domain.Entities.Arr.Queue;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Arr.Interfaces;
using Cleanuparr.Infrastructure.Features.DownloadHunter.Models;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration.Arr;
using Cleanuparr.Persistence.Models.Configuration.General;
using Cleanuparr.Shared.Helpers;
using Data.Models.Arr;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.DownloadHunter;

public class DownloadHunterTests : IDisposable
{
    private readonly DataContext _dataContext;
    private readonly Mock<IArrClientFactory> _arrClientFactoryMock;
    private readonly Mock<IArrClient> _arrClientMock;
    private readonly FakeTimeProvider _fakeTimeProvider;
    private readonly Infrastructure.Features.DownloadHunter.DownloadHunter _downloadHunter;
    private readonly SqliteConnection _connection;

    public DownloadHunterTests()
    {
        // Use SQLite in-memory with shared connection to support complex types
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<DataContext>()
            .UseSqlite(_connection)
            .Options;

        _dataContext = new DataContext(options);
        _dataContext.Database.EnsureCreated();

        _arrClientFactoryMock = new Mock<IArrClientFactory>();
        _arrClientMock = new Mock<IArrClient>();
        _fakeTimeProvider = new FakeTimeProvider();

        _arrClientFactoryMock
            .Setup(f => f.GetClient(It.IsAny<InstanceType>()))
            .Returns(_arrClientMock.Object);

        _downloadHunter = new Infrastructure.Features.DownloadHunter.DownloadHunter(
            _dataContext,
            _arrClientFactoryMock.Object,
            _fakeTimeProvider
        );
    }

    public void Dispose()
    {
        _dataContext.Dispose();
        _connection.Dispose();
    }

    #region HuntDownloadsAsync - Search Disabled Tests

    [Fact]
    public async Task HuntDownloadsAsync_WhenSearchDisabled_DoesNotCallArrClient()
    {
        // Arrange
        await SetupGeneralConfig(searchEnabled: false);
        var request = CreateHuntRequest();

        // Act
        await _downloadHunter.HuntDownloadsAsync(request);

        // Assert
        _arrClientFactoryMock.Verify(f => f.GetClient(It.IsAny<InstanceType>()), Times.Never);
        _arrClientMock.Verify(c => c.SearchItemsAsync(It.IsAny<ArrInstance>(), It.IsAny<HashSet<SearchItem>>()), Times.Never);
    }

    [Fact]
    public async Task HuntDownloadsAsync_WhenSearchDisabled_ReturnsImmediately()
    {
        // Arrange
        await SetupGeneralConfig(searchEnabled: false);
        var request = CreateHuntRequest();

        // Act
        var task = _downloadHunter.HuntDownloadsAsync(request);

        // Assert - Should complete without needing to advance time
        var completedTask = await Task.WhenAny(task, Task.Delay(100));
        Assert.Same(task, completedTask);
    }

    #endregion

    #region HuntDownloadsAsync - Search Enabled Tests

    [Fact]
    public async Task HuntDownloadsAsync_WhenSearchEnabled_CallsArrClientFactory()
    {
        // Arrange
        await SetupGeneralConfig(searchEnabled: true, searchDelay: Constants.MinSearchDelaySeconds);
        var request = CreateHuntRequest();

        // Act - Start the task and advance time
        var task = _downloadHunter.HuntDownloadsAsync(request);
        _fakeTimeProvider.Advance(TimeSpan.FromSeconds(Constants.MinSearchDelaySeconds));
        await task;

        // Assert
        _arrClientFactoryMock.Verify(f => f.GetClient(request.InstanceType), Times.Once);
    }

    [Fact]
    public async Task HuntDownloadsAsync_WhenSearchEnabled_CallsSearchItemsAsync()
    {
        // Arrange
        await SetupGeneralConfig(searchEnabled: true, searchDelay: Constants.MinSearchDelaySeconds);
        var request = CreateHuntRequest();

        // Act
        var task = _downloadHunter.HuntDownloadsAsync(request);
        _fakeTimeProvider.Advance(TimeSpan.FromSeconds(Constants.MinSearchDelaySeconds));
        await task;

        // Assert
        _arrClientMock.Verify(
            c => c.SearchItemsAsync(
                request.Instance,
                It.Is<HashSet<SearchItem>>(s => s.Contains(request.SearchItem))),
            Times.Once);
    }

    [Theory]
    [InlineData(InstanceType.Sonarr)]
    [InlineData(InstanceType.Radarr)]
    [InlineData(InstanceType.Lidarr)]
    [InlineData(InstanceType.Readarr)]
    [InlineData(InstanceType.Whisparr)]
    public async Task HuntDownloadsAsync_UsesCorrectInstanceType(InstanceType instanceType)
    {
        // Arrange
        await SetupGeneralConfig(searchEnabled: true, searchDelay: Constants.MinSearchDelaySeconds);
        var request = CreateHuntRequest(instanceType);

        // Act
        var task = _downloadHunter.HuntDownloadsAsync(request);
        _fakeTimeProvider.Advance(TimeSpan.FromSeconds(Constants.MinSearchDelaySeconds));
        await task;

        // Assert
        _arrClientFactoryMock.Verify(f => f.GetClient(instanceType), Times.Once);
    }

    #endregion

    #region HuntDownloadsAsync - Delay Tests

    [Fact]
    public async Task HuntDownloadsAsync_WaitsForConfiguredDelay()
    {
        // Arrange
        const ushort configuredDelay = 120;
        await SetupGeneralConfig(searchEnabled: true, searchDelay: configuredDelay);
        var request = CreateHuntRequest();

        // Act
        var task = _downloadHunter.HuntDownloadsAsync(request);

        // Assert - Task should not complete before advancing time
        Assert.False(task.IsCompleted);

        // Advance partial time - should still not complete
        _fakeTimeProvider.Advance(TimeSpan.FromSeconds(configuredDelay - 1));
        await Task.Delay(10); // Give the task a chance to complete if it would
        Assert.False(task.IsCompleted);

        // Advance remaining time - should now complete
        _fakeTimeProvider.Advance(TimeSpan.FromSeconds(1));
        await task;
        Assert.True(task.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task HuntDownloadsAsync_WhenDelayBelowMinimum_UsesDefaultDelay()
    {
        // Arrange - Set delay below minimum (simulating manual DB edit)
        const ushort belowMinDelay = 10; // Below MinSearchDelaySeconds (60)
        await SetupGeneralConfig(searchEnabled: true, searchDelay: belowMinDelay);
        var request = CreateHuntRequest();

        // Act
        var task = _downloadHunter.HuntDownloadsAsync(request);

        // Advance by the below-min value - should NOT complete because it should use default
        _fakeTimeProvider.Advance(TimeSpan.FromSeconds(belowMinDelay));
        await Task.Delay(10);
        Assert.False(task.IsCompleted);

        // Advance to default delay - should now complete
        _fakeTimeProvider.Advance(TimeSpan.FromSeconds(Constants.DefaultSearchDelaySeconds - belowMinDelay));
        await task;
        Assert.True(task.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task HuntDownloadsAsync_WhenDelayIsZero_UsesDefaultDelay()
    {
        // Arrange
        await SetupGeneralConfig(searchEnabled: true, searchDelay: 0);
        var request = CreateHuntRequest();

        // Act
        var task = _downloadHunter.HuntDownloadsAsync(request);

        // Assert - Should not complete immediately
        Assert.False(task.IsCompleted);

        // Advance to default delay
        _fakeTimeProvider.Advance(TimeSpan.FromSeconds(Constants.DefaultSearchDelaySeconds));
        await task;
        Assert.True(task.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task HuntDownloadsAsync_WhenDelayAtMinimum_UsesConfiguredDelay()
    {
        // Arrange - Set delay exactly at minimum
        await SetupGeneralConfig(searchEnabled: true, searchDelay: Constants.MinSearchDelaySeconds);
        var request = CreateHuntRequest();

        // Act
        var task = _downloadHunter.HuntDownloadsAsync(request);

        // Advance by minimum - should complete
        _fakeTimeProvider.Advance(TimeSpan.FromSeconds(Constants.MinSearchDelaySeconds));
        await task;
        Assert.True(task.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task HuntDownloadsAsync_WhenDelayAboveMinimum_UsesConfiguredDelay()
    {
        // Arrange - Set delay above minimum
        const ushort aboveMinDelay = 180;
        await SetupGeneralConfig(searchEnabled: true, searchDelay: aboveMinDelay);
        var request = CreateHuntRequest();

        // Act
        var task = _downloadHunter.HuntDownloadsAsync(request);

        // Advance by minimum - should NOT complete yet
        _fakeTimeProvider.Advance(TimeSpan.FromSeconds(Constants.MinSearchDelaySeconds));
        await Task.Delay(10);
        Assert.False(task.IsCompleted);

        // Advance remaining time
        _fakeTimeProvider.Advance(TimeSpan.FromSeconds(aboveMinDelay - Constants.MinSearchDelaySeconds));
        await task;
        Assert.True(task.IsCompletedSuccessfully);
    }

    #endregion

    #region Helper Methods

    private async Task SetupGeneralConfig(bool searchEnabled, ushort searchDelay = Constants.DefaultSearchDelaySeconds)
    {
        var generalConfig = new GeneralConfig
        {
            SearchEnabled = searchEnabled,
            SearchDelay = searchDelay
        };

        _dataContext.GeneralConfigs.Add(generalConfig);
        await _dataContext.SaveChangesAsync();
    }

    private static DownloadHuntRequest<SearchItem> CreateHuntRequest(InstanceType instanceType = InstanceType.Sonarr)
    {
        return new DownloadHuntRequest<SearchItem>
        {
            InstanceType = instanceType,
            Instance = CreateArrInstance(),
            SearchItem = new SearchItem { Id = 123 },
            Record = CreateQueueRecord()
        };
    }

    private static ArrInstance CreateArrInstance()
    {
        return new ArrInstance
        {
            Name = "Test Instance",
            Url = new Uri("http://arr.local"),
            ApiKey = "test-api-key"
        };
    }

    private static QueueRecord CreateQueueRecord()
    {
        return new QueueRecord
        {
            Id = 1,
            Title = "Test Record",
            Protocol = "torrent",
            DownloadId = "ABC123"
        };
    }

    #endregion
}
