using System.Net;
using Cleanuparr.Domain.Entities.Arr;
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
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.DownloadRemover;

public class QueueItemRemoverTests : IDisposable
{
    private readonly ILogger<QueueItemRemover> _logger;
    private readonly MemoryCache _memoryCache;
    private readonly IArrClientFactory _arrClientFactory;
    private readonly IArrClient _arrClient;
    private readonly EventPublisher _eventPublisher;
    private readonly EventsContext _eventsContext;
    private readonly DataContext _dataContext;
    private readonly QueueItemRemover _queueItemRemover;
    private readonly Guid _jobRunId;

    public QueueItemRemoverTests()
    {
        _logger = Substitute.For<ILogger<QueueItemRemover>>();
        _memoryCache = new MemoryCache(Options.Create(new MemoryCacheOptions()));
        _arrClientFactory = Substitute.For<IArrClientFactory>();
        _arrClient = Substitute.For<IArrClient>();

        _arrClientFactory
            .GetClient(Arg.Any<InstanceType>(), Arg.Any<float>())
            .Returns(_arrClient);

        // Create real EventPublisher with mocked dependencies
        _eventsContext = TestEventsContextFactory.Create();

        // Create a JobRun so event FK constraints are satisfied when events are saved
        _jobRunId = Guid.NewGuid();
        _eventsContext.JobRuns.Add(new Persistence.Models.State.JobRun { Id = _jobRunId, Type = JobType.QueueCleaner });
        _eventsContext.SaveChanges();
        ContextProvider.SetJobRunId(_jobRunId);

        var hubContext = Substitute.For<IHubContext<AppHub>>();
        var clients = Substitute.For<IHubClients>();
        clients.All.Returns(Substitute.For<IClientProxy>());
        hubContext.Clients.Returns(clients);

        var dryRunInterceptor = Substitute.For<IDryRunInterceptor>();
        dryRunInterceptor.IsDryRunEnabled().Returns(false);
        // Setup interceptor for other uses (e.g., ArrClient deletion)
        dryRunInterceptor
            .InterceptAsync(Arg.Any<Delegate>(), Arg.Any<object[]>())
            .Returns(Task.CompletedTask);

        _eventPublisher = new EventPublisher(
            _eventsContext,
            hubContext,
            Substitute.For<ILogger<EventPublisher>>(),
            Substitute.For<INotificationPublisher>(),
            dryRunInterceptor);

        // Create in-memory DataContext with seeded SeekerConfig
        _dataContext = TestDataContextFactory.Create();

        _queueItemRemover = new QueueItemRemover(
            _logger,
            _memoryCache,
            _arrClientFactory,
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

        // Act
        await _queueItemRemover.RemoveQueueItemAsync(request);

        // Assert
        await _arrClient.Received(1).DeleteQueueItemAsync(
            request.Instance,
            request.Record,
            request.RemoveFromClient,
            request.DeleteReason);
    }

    [Fact]
    public async Task RemoveQueueItemAsync_Success_AddsSearchQueueItem()
    {
        // Arrange
        var request = CreateRemoveRequest();

        // Act
        await _queueItemRemover.RemoveQueueItemAsync(request);

        // Assert
        var queueItems = await _dataContext.SearchQueue.ToListAsync();
        queueItems.Count.ShouldBe(1);
        queueItems[0].ArrInstanceId.ShouldBe(request.Instance.Id);
        queueItems[0].ItemId.ShouldBe(request.SearchItem.Id);
        queueItems[0].Title.ShouldBe(request.Record.Title);
    }

    [Fact]
    public async Task RemoveQueueItemAsync_Success_ClearsDownloadMarkedForRemovalCache()
    {
        // Arrange
        var request = CreateRemoveRequest();
        var cacheKey = $"remove_{request.Record.DownloadId.ToLowerInvariant()}_{request.Instance.Url}";
        _memoryCache.Set(cacheKey, true);

        // Act
        await _queueItemRemover.RemoveQueueItemAsync(request);

        // Assert
        _memoryCache.TryGetValue(cacheKey, out _).ShouldBeFalse();
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

        // Act
        await _queueItemRemover.RemoveQueueItemAsync(request);

        // Assert
        _arrClientFactory.Received(1).GetClient(instanceType, Arg.Any<float>());
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

        // Act
        await _queueItemRemover.RemoveQueueItemAsync(request);

        // Assert
        var queueItems = await _dataContext.SearchQueue.ToListAsync();
        queueItems.ShouldBeEmpty();
    }

    [Fact]
    public async Task RemoveQueueItemAsync_WhenHashIsRecurring_RemovesHashFromRecurring()
    {
        // Arrange
        var request = CreateRemoveRequest();
        var hash = request.Record.DownloadId.ToLowerInvariant();
        Striker.RecurringHashes.TryAdd(hash, null);

        // Act
        await _queueItemRemover.RemoveQueueItemAsync(request);

        // Assert
        Striker.RecurringHashes.ContainsKey(hash).ShouldBeFalse();
    }

    [Fact]
    public async Task RemoveQueueItemAsync_WhenHashIsNotRecurring_AddsSearchQueueItem()
    {
        // Arrange
        var request = CreateRemoveRequest();

        // Act
        await _queueItemRemover.RemoveQueueItemAsync(request);

        // Assert
        var queueItems = await _dataContext.SearchQueue.ToListAsync();
        queueItems.Count.ShouldBe(1);
    }

    #endregion

    #region RemoveQueueItemAsync - SkipSearch Tests

    [Fact]
    public async Task RemoveQueueItemAsync_WhenSkipSearch_DoesNotAddSearchQueueItem()
    {
        // Arrange
        var request = CreateRemoveRequest(skipSearch: true);

        // Act
        await _queueItemRemover.RemoveQueueItemAsync(request);

        // Assert
        var queueItems = await _dataContext.SearchQueue.ToListAsync();
        queueItems.ShouldBeEmpty();
    }

    [Fact]
    public async Task RemoveQueueItemAsync_WhenSkipSearch_AndHashIsNotRecurring_DoesNotModifyRecurringHashes()
    {
        // Arrange
        var request = CreateRemoveRequest(skipSearch: true);
        var hash = request.Record.DownloadId.ToLowerInvariant();

        // Act
        await _queueItemRemover.RemoveQueueItemAsync(request);

        // Assert - hash was never in recurring, should still not be there
        Striker.RecurringHashes.ContainsKey(hash).ShouldBeFalse();
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

        // Act
        await _queueItemRemover.RemoveQueueItemAsync(request);

        // Assert
        var queueItems = await _dataContext.SearchQueue.ToListAsync();
        queueItems.ShouldBeEmpty();
    }

    #endregion

    #region RemoveQueueItemAsync - HTTP Error Tests

    [Fact]
    public async Task RemoveQueueItemAsync_WhenNotFoundError_ThrowsWithItemAlreadyDeletedMessage()
    {
        // Arrange
        var request = CreateRemoveRequest();

        _arrClient
            .DeleteQueueItemAsync(
                Arg.Any<ArrInstance>(),
                Arg.Any<QueueRecord>(),
                Arg.Any<bool>(),
                Arg.Any<DeleteReason>())
            .ThrowsAsync(new HttpRequestException("Not found", null, HttpStatusCode.NotFound));

        // Act & Assert
        var exception = await Should.ThrowAsync<Exception>(
            () => _queueItemRemover.RemoveQueueItemAsync(request));

        exception.Message.ShouldContain("might have already been deleted");
        exception.Message.ShouldContain(request.Instance.ArrConfig.Type.ToString());
    }

    [Fact]
    public async Task RemoveQueueItemAsync_WhenNotFoundError_ClearsCacheInFinally()
    {
        // Arrange
        var request = CreateRemoveRequest();
        var cacheKey = $"remove_{request.Record.DownloadId.ToLowerInvariant()}_{request.Instance.Url}";
        _memoryCache.Set(cacheKey, true);

        _arrClient
            .DeleteQueueItemAsync(
                Arg.Any<ArrInstance>(),
                Arg.Any<QueueRecord>(),
                Arg.Any<bool>(),
                Arg.Any<DeleteReason>())
            .ThrowsAsync(new HttpRequestException("Not found", null, HttpStatusCode.NotFound));

        // Act & Assert
        await Should.ThrowAsync<Exception>(
            () => _queueItemRemover.RemoveQueueItemAsync(request));

        // Cache should be cleared in finally block
        _memoryCache.TryGetValue(cacheKey, out _).ShouldBeFalse();
    }

    [Fact]
    public async Task RemoveQueueItemAsync_WhenOtherHttpError_Rethrows()
    {
        // Arrange
        var request = CreateRemoveRequest();
        var originalException = new HttpRequestException("Server error", null, HttpStatusCode.InternalServerError);

        _arrClient
            .DeleteQueueItemAsync(
                Arg.Any<ArrInstance>(),
                Arg.Any<QueueRecord>(),
                Arg.Any<bool>(),
                Arg.Any<DeleteReason>())
            .ThrowsAsync(originalException);

        // Act & Assert
        var exception = await Should.ThrowAsync<HttpRequestException>(
            () => _queueItemRemover.RemoveQueueItemAsync(request));

        exception.ShouldBeSameAs(originalException);
    }

    [Fact]
    public async Task RemoveQueueItemAsync_WhenNonHttpError_Rethrows()
    {
        // Arrange
        var request = CreateRemoveRequest();
        var originalException = new InvalidOperationException("Some other error");

        _arrClient
            .DeleteQueueItemAsync(
                Arg.Any<ArrInstance>(),
                Arg.Any<QueueRecord>(),
                Arg.Any<bool>(),
                Arg.Any<DeleteReason>())
            .ThrowsAsync(originalException);

        // Act & Assert
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => _queueItemRemover.RemoveQueueItemAsync(request));

        exception.ShouldBeSameAs(originalException);
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

        // Act
        await _queueItemRemover.RemoveQueueItemAsync(request);

        // Assert
        await _arrClient.Received(1).DeleteQueueItemAsync(
            Arg.Any<ArrInstance>(),
            Arg.Any<QueueRecord>(),
            Arg.Any<bool>(),
            deleteReason);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RemoveQueueItemAsync_PassesCorrectRemoveFromClientFlag(bool removeFromClient)
    {
        // Arrange
        var request = CreateRemoveRequest(removeFromClient: removeFromClient);

        // Act
        await _queueItemRemover.RemoveQueueItemAsync(request);

        // Assert
        await _arrClient.Received(1).DeleteQueueItemAsync(
            Arg.Any<ArrInstance>(),
            Arg.Any<QueueRecord>(),
            removeFromClient,
            Arg.Any<DeleteReason>());
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
