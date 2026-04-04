using System.Net;
using Cleanuparr.Domain.Entities.Arr.Queue;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Events;
using Cleanuparr.Infrastructure.Features.Arr.Interfaces;
using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Infrastructure.Features.DownloadRemover;
using Cleanuparr.Infrastructure.Features.DownloadRemover.Models;
using Cleanuparr.Infrastructure.Features.ItemStriker;
using Cleanuparr.Infrastructure.Features.Notifications;
using Cleanuparr.Infrastructure.Hubs;
using Cleanuparr.Infrastructure.Interceptors;
using Cleanuparr.Infrastructure.Tests.Features.Jobs.TestHelpers;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration.Arr;
using Data.Models.Arr;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.DownloadRemover;

public class QueueItemRemoverTests : IDisposable
{
    private readonly Mock<ILogger<QueueItemRemover>> _loggerMock;
    private readonly MemoryCache _memoryCache;
    private readonly Mock<IArrClientFactory> _arrClientFactoryMock;
    private readonly Mock<IArrClient> _arrClientMock;
    private readonly EventPublisher _eventPublisher;
    private readonly EventsContext _eventsContext;
    private readonly DataContext _dataContext;
    private readonly QueueItemRemover _queueItemRemover;
    private readonly Guid _jobRunId;

    public QueueItemRemoverTests()
    {
        _loggerMock = new Mock<ILogger<QueueItemRemover>>();
        _memoryCache = new MemoryCache(Options.Create(new MemoryCacheOptions()));
        _arrClientFactoryMock = new Mock<IArrClientFactory>();
        _arrClientMock = new Mock<IArrClient>();

        _arrClientFactoryMock
            .Setup(f => f.GetClient(It.IsAny<InstanceType>(), It.IsAny<float>()))
            .Returns(_arrClientMock.Object);

        // Create real EventPublisher with mocked dependencies
        _eventsContext = TestEventsContextFactory.Create();

        // Create a JobRun so event FK constraints are satisfied when events are saved
        _jobRunId = Guid.NewGuid();
        _eventsContext.JobRuns.Add(new Persistence.Models.State.JobRun { Id = _jobRunId, Type = JobType.QueueCleaner });
        _eventsContext.SaveChanges();
        ContextProvider.SetJobRunId(_jobRunId);

        var hubContextMock = new Mock<IHubContext<AppHub>>();
        var clientsMock = new Mock<IHubClients>();
        clientsMock.Setup(c => c.All).Returns(Mock.Of<IClientProxy>());
        hubContextMock.Setup(h => h.Clients).Returns(clientsMock.Object);

        var dryRunInterceptorMock = new Mock<IDryRunInterceptor>();
        dryRunInterceptorMock.Setup(d => d.IsDryRunEnabled()).ReturnsAsync(false);
        // Setup interceptor for other uses (e.g., ArrClient deletion)
        dryRunInterceptorMock
            .Setup(d => d.InterceptAsync(It.IsAny<Delegate>(), It.IsAny<object[]>()))
            .Returns(Task.CompletedTask);

        _eventPublisher = new EventPublisher(
            _eventsContext,
            hubContextMock.Object,
            Mock.Of<ILogger<EventPublisher>>(),
            Mock.Of<INotificationPublisher>(),
            dryRunInterceptorMock.Object);

        // Create in-memory DataContext with seeded SeekerConfig
        _dataContext = TestDataContextFactory.Create();

        _queueItemRemover = new QueueItemRemover(
            _loggerMock.Object,
            _memoryCache,
            _arrClientFactoryMock.Object,
            _eventPublisher,
            _eventsContext,
            _dataContext
        );

        // Clear static RecurringHashes before each test
        Striker.RecurringHashes.Clear();
    }

    public void Dispose()
    {
        _memoryCache.Dispose();
        _eventsContext.Dispose();
        _dataContext.Dispose();
        Striker.RecurringHashes.Clear();
    }

    #region RemoveQueueItemAsync - Success Tests

    [Fact]
    public async Task RemoveQueueItemAsync_Success_DeletesQueueItem()
    {
        // Arrange
        var request = CreateRemoveRequest();

        _arrClientMock
            .Setup(c => c.DeleteQueueItemAsync(
                It.IsAny<ArrInstance>(),
                It.IsAny<QueueRecord>(),
                It.IsAny<bool>(),
                It.IsAny<DeleteReason>()))
            .Returns(Task.CompletedTask);

        // Act
        await _queueItemRemover.RemoveQueueItemAsync(request);

        // Assert
        _arrClientMock.Verify(c => c.DeleteQueueItemAsync(
            request.Instance,
            request.Record,
            request.RemoveFromClient,
            request.DeleteReason), Times.Once);
    }

    [Fact]
    public async Task RemoveQueueItemAsync_Success_AddsSearchQueueItem()
    {
        // Arrange
        var request = CreateRemoveRequest();

        _arrClientMock
            .Setup(c => c.DeleteQueueItemAsync(
                It.IsAny<ArrInstance>(),
                It.IsAny<QueueRecord>(),
                It.IsAny<bool>(),
                It.IsAny<DeleteReason>()))
            .Returns(Task.CompletedTask);

        // Act
        await _queueItemRemover.RemoveQueueItemAsync(request);

        // Assert
        var queueItems = await _dataContext.SearchQueue.ToListAsync();
        Assert.Single(queueItems);
        Assert.Equal(request.Instance.Id, queueItems[0].ArrInstanceId);
        Assert.Equal(request.SearchItem.Id, queueItems[0].ItemId);
        Assert.Equal(request.Record.Title, queueItems[0].Title);
    }

    [Fact]
    public async Task RemoveQueueItemAsync_Success_ClearsDownloadMarkedForRemovalCache()
    {
        // Arrange
        var request = CreateRemoveRequest();
        var cacheKey = $"remove_{request.Record.DownloadId.ToLowerInvariant()}_{request.Instance.Url}";
        _memoryCache.Set(cacheKey, true);

        _arrClientMock
            .Setup(c => c.DeleteQueueItemAsync(
                It.IsAny<ArrInstance>(),
                It.IsAny<QueueRecord>(),
                It.IsAny<bool>(),
                It.IsAny<DeleteReason>()))
            .Returns(Task.CompletedTask);

        // Act
        await _queueItemRemover.RemoveQueueItemAsync(request);

        // Assert
        Assert.False(_memoryCache.TryGetValue(cacheKey, out _));
    }

    [Theory]
    [InlineData(InstanceType.Sonarr)]
    [InlineData(InstanceType.Radarr)]
    [InlineData(InstanceType.Lidarr)]
    [InlineData(InstanceType.Readarr)]
    [InlineData(InstanceType.Whisparr)]
    public async Task RemoveQueueItemAsync_UsesCorrectClientForInstanceType(InstanceType instanceType)
    {
        // Arrange
        var request = CreateRemoveRequest(instanceType);

        _arrClientMock
            .Setup(c => c.DeleteQueueItemAsync(
                It.IsAny<ArrInstance>(),
                It.IsAny<QueueRecord>(),
                It.IsAny<bool>(),
                It.IsAny<DeleteReason>()))
            .Returns(Task.CompletedTask);

        // Act
        await _queueItemRemover.RemoveQueueItemAsync(request);

        // Assert
        _arrClientFactoryMock.Verify(f => f.GetClient(instanceType, It.IsAny<float>()), Times.Once);
    }

    #endregion

    #region RemoveQueueItemAsync - Recurring Hash Tests

    [Fact]
    public async Task RemoveQueueItemAsync_WhenHashIsRecurring_DoesNotAddSearchQueueItem()
    {
        // Arrange
        var request = CreateRemoveRequest();
        var hash = request.Record.DownloadId.ToLowerInvariant();
        Striker.RecurringHashes.TryAdd(hash, null);

        _arrClientMock
            .Setup(c => c.DeleteQueueItemAsync(
                It.IsAny<ArrInstance>(),
                It.IsAny<QueueRecord>(),
                It.IsAny<bool>(),
                It.IsAny<DeleteReason>()))
            .Returns(Task.CompletedTask);

        // Act
        await _queueItemRemover.RemoveQueueItemAsync(request);

        // Assert
        var queueItems = await _dataContext.SearchQueue.ToListAsync();
        Assert.Empty(queueItems);
    }

    [Fact]
    public async Task RemoveQueueItemAsync_WhenHashIsRecurring_RemovesHashFromRecurring()
    {
        // Arrange
        var request = CreateRemoveRequest();
        var hash = request.Record.DownloadId.ToLowerInvariant();
        Striker.RecurringHashes.TryAdd(hash, null);

        _arrClientMock
            .Setup(c => c.DeleteQueueItemAsync(
                It.IsAny<ArrInstance>(),
                It.IsAny<QueueRecord>(),
                It.IsAny<bool>(),
                It.IsAny<DeleteReason>()))
            .Returns(Task.CompletedTask);

        // Act
        await _queueItemRemover.RemoveQueueItemAsync(request);

        // Assert
        Assert.False(Striker.RecurringHashes.ContainsKey(hash));
    }

    [Fact]
    public async Task RemoveQueueItemAsync_WhenHashIsNotRecurring_AddsSearchQueueItem()
    {
        // Arrange
        var request = CreateRemoveRequest();

        _arrClientMock
            .Setup(c => c.DeleteQueueItemAsync(
                It.IsAny<ArrInstance>(),
                It.IsAny<QueueRecord>(),
                It.IsAny<bool>(),
                It.IsAny<DeleteReason>()))
            .Returns(Task.CompletedTask);

        // Act
        await _queueItemRemover.RemoveQueueItemAsync(request);

        // Assert
        var queueItems = await _dataContext.SearchQueue.ToListAsync();
        Assert.Single(queueItems);
    }

    #endregion

    #region RemoveQueueItemAsync - SkipSearch Tests

    [Fact]
    public async Task RemoveQueueItemAsync_WhenSkipSearch_DoesNotAddSearchQueueItem()
    {
        // Arrange
        var request = CreateRemoveRequest(skipSearch: true);

        _arrClientMock
            .Setup(c => c.DeleteQueueItemAsync(
                It.IsAny<ArrInstance>(),
                It.IsAny<QueueRecord>(),
                It.IsAny<bool>(),
                It.IsAny<DeleteReason>()))
            .Returns(Task.CompletedTask);

        // Act
        await _queueItemRemover.RemoveQueueItemAsync(request);

        // Assert
        var queueItems = await _dataContext.SearchQueue.ToListAsync();
        Assert.Empty(queueItems);
    }

    [Fact]
    public async Task RemoveQueueItemAsync_WhenSkipSearch_AndHashIsNotRecurring_DoesNotModifyRecurringHashes()
    {
        // Arrange
        var request = CreateRemoveRequest(skipSearch: true);
        var hash = request.Record.DownloadId.ToLowerInvariant();

        _arrClientMock
            .Setup(c => c.DeleteQueueItemAsync(
                It.IsAny<ArrInstance>(),
                It.IsAny<QueueRecord>(),
                It.IsAny<bool>(),
                It.IsAny<DeleteReason>()))
            .Returns(Task.CompletedTask);

        // Act
        await _queueItemRemover.RemoveQueueItemAsync(request);

        // Assert - hash was never in recurring, should still not be there
        Assert.False(Striker.RecurringHashes.ContainsKey(hash));
    }

    #endregion

    #region RemoveQueueItemAsync - SearchEnabled Tests

    [Fact]
    public async Task RemoveQueueItemAsync_WhenSearchDisabled_DoesNotAddSearchQueueItem()
    {
        // Arrange
        var seekerConfig = await _dataContext.SeekerConfigs.FirstAsync();
        seekerConfig.SearchEnabled = false;
        await _dataContext.SaveChangesAsync();

        var request = CreateRemoveRequest();

        _arrClientMock
            .Setup(c => c.DeleteQueueItemAsync(
                It.IsAny<ArrInstance>(),
                It.IsAny<QueueRecord>(),
                It.IsAny<bool>(),
                It.IsAny<DeleteReason>()))
            .Returns(Task.CompletedTask);

        // Act
        await _queueItemRemover.RemoveQueueItemAsync(request);

        // Assert
        var queueItems = await _dataContext.SearchQueue.ToListAsync();
        Assert.Empty(queueItems);
    }

    #endregion

    #region RemoveQueueItemAsync - HTTP Error Tests

    [Fact]
    public async Task RemoveQueueItemAsync_WhenNotFoundError_ThrowsWithItemAlreadyDeletedMessage()
    {
        // Arrange
        var request = CreateRemoveRequest();

        _arrClientMock
            .Setup(c => c.DeleteQueueItemAsync(
                It.IsAny<ArrInstance>(),
                It.IsAny<QueueRecord>(),
                It.IsAny<bool>(),
                It.IsAny<DeleteReason>()))
            .ThrowsAsync(new HttpRequestException("Not found", null, HttpStatusCode.NotFound));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<Exception>(
            () => _queueItemRemover.RemoveQueueItemAsync(request));

        Assert.Contains("might have already been deleted", exception.Message);
        Assert.Contains(request.Instance.ArrConfig.Type.ToString(), exception.Message);
    }

    [Fact]
    public async Task RemoveQueueItemAsync_WhenNotFoundError_ClearsCacheInFinally()
    {
        // Arrange
        var request = CreateRemoveRequest();
        var cacheKey = $"remove_{request.Record.DownloadId.ToLowerInvariant()}_{request.Instance.Url}";
        _memoryCache.Set(cacheKey, true);

        _arrClientMock
            .Setup(c => c.DeleteQueueItemAsync(
                It.IsAny<ArrInstance>(),
                It.IsAny<QueueRecord>(),
                It.IsAny<bool>(),
                It.IsAny<DeleteReason>()))
            .ThrowsAsync(new HttpRequestException("Not found", null, HttpStatusCode.NotFound));

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(
            () => _queueItemRemover.RemoveQueueItemAsync(request));

        // Cache should be cleared in finally block
        Assert.False(_memoryCache.TryGetValue(cacheKey, out _));
    }

    [Fact]
    public async Task RemoveQueueItemAsync_WhenOtherHttpError_Rethrows()
    {
        // Arrange
        var request = CreateRemoveRequest();
        var originalException = new HttpRequestException("Server error", null, HttpStatusCode.InternalServerError);

        _arrClientMock
            .Setup(c => c.DeleteQueueItemAsync(
                It.IsAny<ArrInstance>(),
                It.IsAny<QueueRecord>(),
                It.IsAny<bool>(),
                It.IsAny<DeleteReason>()))
            .ThrowsAsync(originalException);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<HttpRequestException>(
            () => _queueItemRemover.RemoveQueueItemAsync(request));

        Assert.Same(originalException, exception);
    }

    [Fact]
    public async Task RemoveQueueItemAsync_WhenNonHttpError_Rethrows()
    {
        // Arrange
        var request = CreateRemoveRequest();
        var originalException = new InvalidOperationException("Some other error");

        _arrClientMock
            .Setup(c => c.DeleteQueueItemAsync(
                It.IsAny<ArrInstance>(),
                It.IsAny<QueueRecord>(),
                It.IsAny<bool>(),
                It.IsAny<DeleteReason>()))
            .ThrowsAsync(originalException);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _queueItemRemover.RemoveQueueItemAsync(request));

        Assert.Same(originalException, exception);
    }

    #endregion

    #region RemoveQueueItemAsync - Delete Reason Tests

    [Theory]
    [InlineData(DeleteReason.Stalled)]
    [InlineData(DeleteReason.FailedImport)]
    [InlineData(DeleteReason.SlowSpeed)]
    [InlineData(DeleteReason.SlowTime)]
    [InlineData(DeleteReason.DownloadingMetadata)]
    public async Task RemoveQueueItemAsync_PassesCorrectDeleteReason(DeleteReason deleteReason)
    {
        // Arrange
        var request = CreateRemoveRequest(deleteReason: deleteReason);

        _arrClientMock
            .Setup(c => c.DeleteQueueItemAsync(
                It.IsAny<ArrInstance>(),
                It.IsAny<QueueRecord>(),
                It.IsAny<bool>(),
                It.IsAny<DeleteReason>()))
            .Returns(Task.CompletedTask);

        // Act
        await _queueItemRemover.RemoveQueueItemAsync(request);

        // Assert
        _arrClientMock.Verify(c => c.DeleteQueueItemAsync(
            It.IsAny<ArrInstance>(),
            It.IsAny<QueueRecord>(),
            It.IsAny<bool>(),
            deleteReason), Times.Once);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RemoveQueueItemAsync_PassesCorrectRemoveFromClientFlag(bool removeFromClient)
    {
        // Arrange
        var request = CreateRemoveRequest(removeFromClient: removeFromClient);

        _arrClientMock
            .Setup(c => c.DeleteQueueItemAsync(
                It.IsAny<ArrInstance>(),
                It.IsAny<QueueRecord>(),
                It.IsAny<bool>(),
                It.IsAny<DeleteReason>()))
            .Returns(Task.CompletedTask);

        // Act
        await _queueItemRemover.RemoveQueueItemAsync(request);

        // Assert
        _arrClientMock.Verify(c => c.DeleteQueueItemAsync(
            It.IsAny<ArrInstance>(),
            It.IsAny<QueueRecord>(),
            removeFromClient,
            It.IsAny<DeleteReason>()), Times.Once);
    }

    #endregion

    #region Helper Methods

    private QueueItemRemoveRequest<SearchItem> CreateRemoveRequest(
        InstanceType instanceType = InstanceType.Sonarr,
        bool removeFromClient = true,
        DeleteReason deleteReason = DeleteReason.Stalled,
        bool skipSearch = false)
    {
        // Use an ArrInstance that exists in the DB to satisfy FK constraint on SearchQueueItem
        var instance = GetOrCreateArrInstance(instanceType);

        return new QueueItemRemoveRequest<SearchItem>
        {
            Instance = instance,
            SearchItem = new SearchItem { Id = 123 },
            Record = CreateQueueRecord(),
            RemoveFromClient = removeFromClient,
            DeleteReason = deleteReason,
            SkipSearch = skipSearch,
            JobRunId = _jobRunId
        };
    }

    private ArrInstance GetOrCreateArrInstance(InstanceType instanceType)
    {
        return instanceType switch
        {
            InstanceType.Sonarr => TestDataContextFactory.AddSonarrInstance(_dataContext),
            InstanceType.Radarr => TestDataContextFactory.AddRadarrInstance(_dataContext),
            InstanceType.Lidarr => TestDataContextFactory.AddLidarrInstance(_dataContext),
            InstanceType.Readarr => TestDataContextFactory.AddReadarrInstance(_dataContext),
            InstanceType.Whisparr => TestDataContextFactory.AddWhisparrInstance(_dataContext),
            _ => TestDataContextFactory.AddSonarrInstance(_dataContext),
        };
    }

    private static QueueRecord CreateQueueRecord()
    {
        return new QueueRecord
        {
            Id = 1,
            Title = "Test Record",
            Protocol = "torrent",
            DownloadId = "ABC123DEF456"
        };
    }

    #endregion
}
