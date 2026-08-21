using Cleanuparr.Infrastructure.Features.LazyLibrarian;
using System.Net;
using Cleanuparr.Domain.Entities.Arr;
using Cleanuparr.Domain.Entities.LazyLibrarian;
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
using Cleanuparr.Persistence.Models.State;
using Cleanuparr.Infrastructure.Tests.Features.Jobs.TestHelpers;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration.Arr;
using Cleanuparr.Persistence.Providers;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Cleanuparr.Infrastructure.Tests.TestHelpers;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Cleanuparr.Infrastructure.Tests.Features.Jobs.Integration;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.DownloadRemover;

[Collection(IntegrationTestCollection.Name)]
public class QueueItemRemoverTests : IDisposable
{
    private readonly ILogger<QueueItemRemover> _logger;
    private readonly MemoryCache _memoryCache;
    private readonly IArrClientFactory _arrClientFactory;
    private readonly IArrClient _arrClient;
    private readonly EventPublisher _eventPublisher;
    private readonly EventsContext _eventsContext;
    private readonly DataContext _dataContext;
    private readonly ILazyLibrarianService _lazyLibrarianService = Substitute.For<ILazyLibrarianService>();
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
        _eventsContext.JobRuns.Add(new Cleanuparr.Persistence.Models.State.JobRun { Id = _jobRunId, Type = JobType.QueueCleaner });
        _eventsContext.SaveChanges();
        ContextProvider.SetJobRunId(_jobRunId);

        var hubContext = Substitute.For<IHubContext<AppHub>>();
        var clients = Substitute.For<IHubClients>();
        clients.All.Returns(Substitute.For<IClientProxy>());
        hubContext.Clients.Returns(clients);

        var dryRunInterceptor = Substitute.For<IDryRunInterceptor>();
        dryRunInterceptor.IsDryRunEnabled().Returns(false);
        dryRunInterceptor
            .InterceptAsync(Arg.Any<Func<Task>>(), Arg.Any<string?>())
            .ReturnsForAnyArgs(Task.CompletedTask);

        _eventPublisher = new EventPublisher(
            _eventsContext,
            hubContext,
            Substitute.For<ILogger<EventPublisher>>(),
            Substitute.For<INotificationPublisher>(),
            dryRunInterceptor,
            new SqliteDatabaseProvider());

        // Create in-memory DataContext with seeded SeekerConfig
        _dataContext = TestDataContextFactory.Create();

        _queueItemRemover = new QueueItemRemover(
            _logger,
            _memoryCache,
            _arrClientFactory,
            _eventPublisher,
            _eventsContext,
            _dataContext,
            _lazyLibrarianService
        );

        // Clear static RecurringHashes before each test
        Striker.RecurringHashes.Clear();
    }

    #region LazyLibrarian

    private LazyLibrarianQueueItem CreateBookItem(BookLibrary library = BookLibrary.EBook) => new()
    {
        DownloadId = "HASH1",
        Title = "A Book",
        Books = [new LazyLibrarianBookRef { BookId = "OL7353617M", Library = library }],
        Source = LazyLibrarianSource.QBittorrent,
        Origin = LazyLibrarianOrigin.New,
    };

    private QueueItemRemoveRequest CreateLazyLibrarianRequest(bool removedFromClient, LazyLibrarianQueueItem? item = null) => new()
    {
        Instance = TestDataContextFactory.AddLazyLibrarianInstance(_dataContext),
        Target = new LazyLibrarianRemovalTarget
        {
            Item = item ?? CreateBookItem(),
            RemovedFromClient = removedFromClient,
        },
        DeleteReason = DeleteReason.Stalled,
        JobRunId = _jobRunId,
    };

    private void StubProgress(int progress)
    {
        _lazyLibrarianService
            .GetDownloadProgressAsync(Arg.Any<ArrInstance>(), Arg.Any<LazyLibrarianQueueItem>())
            .Returns(new LazyLibrarianDownloadProgress { Progress = progress });
    }

    [Fact]
    public async Task RemoveQueueItemAsync_LazyLibrarian_ResetsTheBook()
    {
        // Arrange
        StubProgress(-1);
        QueueItemRemoveRequest request = CreateLazyLibrarianRequest(removedFromClient: true);

        // Act
        await _queueItemRemover.RemoveQueueItemAsync(request);

        // Assert
        await _lazyLibrarianService.Received(1).ResetItemAsync(Arg.Any<ArrInstance>(), Arg.Any<LazyLibrarianQueueItem>());
    }

    [Fact]
    public async Task RemoveQueueItemAsync_LazyLibrarian_KeepsTheDownloadWhenTheResetFails()
    {
        // Arrange: a failed reset leaves the snatch, so the download is not removed yet.
        _eventsContext.DownloadItems.Add(new DownloadItem
        {
            DownloadId = "hash1",
            Title = "A Book",
            IsMarkedForRemoval = true,
        });
        await _eventsContext.SaveChangesAsync();

        StubProgress(-1);
        _lazyLibrarianService
            .ResetItemAsync(Arg.Any<ArrInstance>(), Arg.Any<LazyLibrarianQueueItem>())
            .Returns(Task.FromException(new Exception("lazylibrarian is down")));
        QueueItemRemoveRequest request = CreateLazyLibrarianRequest(removedFromClient: true);

        // Act
        await Should.ThrowAsync<Exception>(() => _queueItemRemover.RemoveQueueItemAsync(request));

        // Assert
        DownloadItem item = await _eventsContext.DownloadItems.AsNoTracking()
            .FirstAsync(x => x.DownloadId == "hash1");
        item.IsRemoved.ShouldBeFalse();
    }

    [Fact]
    public async Task RemoveQueueItemAsync_LazyLibrarian_KeepsEveryBookSharingTheDownload()
    {
        // Arrange: one torrent backs an ebook row and an audiobook row, and both have to come back.
        StubProgress(-1);
        LazyLibrarianQueueItem item = CreateBookItem() with
        {
            Books =
            [
                new LazyLibrarianBookRef { BookId = "OL1W", Library = BookLibrary.EBook },
                new LazyLibrarianBookRef { BookId = "OL2W", Library = BookLibrary.AudioBook },
            ],
        };
        QueueItemRemoveRequest request = CreateLazyLibrarianRequest(removedFromClient: true, item);

        // Act
        await _queueItemRemover.RemoveQueueItemAsync(request);

        // Assert
        await _lazyLibrarianService.Received(1)
            .ResetItemAsync(Arg.Any<ArrInstance>(), Arg.Is<LazyLibrarianQueueItem>(x => x.Books.Count == 2));
        await _lazyLibrarianService.Received(1)
            .TriggerSearchAsync(Arg.Any<ArrInstance>(), Arg.Is<LazyLibrarianQueueItem>(x => x.Books.Count == 2));
    }

    [Fact]
    public async Task RemoveQueueItemAsync_LazyLibrarian_ClearsTheSnatchBeforeSearching()
    {
        // Arrange: progress -1 means LazyLibrarian just marked the row aborted.
        StubProgress(-1);
        QueueItemRemoveRequest request = CreateLazyLibrarianRequest(removedFromClient: true);

        // Act
        await _queueItemRemover.RemoveQueueItemAsync(request);

        // Assert
        Received.InOrder(() =>
        {
            _lazyLibrarianService.GetDownloadProgressAsync(Arg.Any<ArrInstance>(), Arg.Any<LazyLibrarianQueueItem>());
            _lazyLibrarianService.ResetItemAsync(Arg.Any<ArrInstance>(), Arg.Any<LazyLibrarianQueueItem>());
            _lazyLibrarianService.TriggerSearchAsync(Arg.Any<ArrInstance>(), Arg.Any<LazyLibrarianQueueItem>());
        });
    }

    [Theory]
    [InlineData(0)]
    [InlineData(50)]
    [InlineData(100)]
    [InlineData(-2)]
    public async Task RemoveQueueItemAsync_LazyLibrarian_DoesNotSearchWhileTheSnatchStands(int progress)
    {
        // Arrange: anything but -1 leaves the wanted row snatched, and every search command skips such a book.
        StubProgress(progress);
        QueueItemRemoveRequest request = CreateLazyLibrarianRequest(removedFromClient: true);

        // Act
        await _queueItemRemover.RemoveQueueItemAsync(request);

        // Assert
        await _lazyLibrarianService.DidNotReceive().TriggerSearchAsync(Arg.Any<ArrInstance>(), Arg.Any<LazyLibrarianQueueItem>());
    }

    [Fact]
    public async Task RemoveQueueItemAsync_LazyLibrarian_DoesNotSearchWhenTheTorrentStaysInTheClient()
    {
        // Arrange
        QueueItemRemoveRequest request = CreateLazyLibrarianRequest(removedFromClient: false);

        // Act
        await _queueItemRemover.RemoveQueueItemAsync(request);

        // Assert
        await _lazyLibrarianService.DidNotReceive().GetDownloadProgressAsync(Arg.Any<ArrInstance>(), Arg.Any<LazyLibrarianQueueItem>());
        await _lazyLibrarianService.DidNotReceive().TriggerSearchAsync(Arg.Any<ArrInstance>(), Arg.Any<LazyLibrarianQueueItem>());
    }

    [Fact]
    public async Task RemoveQueueItemAsync_LazyLibrarian_DoesNotSearchWhenNoProgressIsReported()
    {
        // Arrange
        _lazyLibrarianService
            .GetDownloadProgressAsync(Arg.Any<ArrInstance>(), Arg.Any<LazyLibrarianQueueItem>())
            .Returns((LazyLibrarianDownloadProgress?)null);
        QueueItemRemoveRequest request = CreateLazyLibrarianRequest(removedFromClient: true);

        // Act
        await _queueItemRemover.RemoveQueueItemAsync(request);

        // Assert
        await _lazyLibrarianService.DidNotReceive().TriggerSearchAsync(Arg.Any<ArrInstance>(), Arg.Any<LazyLibrarianQueueItem>());
    }

    [Fact]
    public async Task RemoveQueueItemAsync_LazyLibrarian_DoesNotSearchWhenSearchIsDisabled()
    {
        // Arrange
        StubProgress(-1);
        QueueItemRemoveRequest request = CreateLazyLibrarianRequest(removedFromClient: true);

        var seekerConfig = await _dataContext.SeekerConfigs.FirstAsync();
        seekerConfig.SearchEnabled = false;
        await _dataContext.SaveChangesAsync();

        // Act
        await _queueItemRemover.RemoveQueueItemAsync(request);

        // Assert
        await _lazyLibrarianService.DidNotReceive().TriggerSearchAsync(Arg.Any<ArrInstance>(), Arg.Any<LazyLibrarianQueueItem>());
    }

    [Fact]
    public async Task RemoveQueueItemAsync_LazyLibrarian_DoesNotQueueASeekerSearch()
    {
        // Arrange: the search happens here, so the Seeker must stay out of it.
        StubProgress(-1);
        QueueItemRemoveRequest request = CreateLazyLibrarianRequest(removedFromClient: true);

        // Act
        await _queueItemRemover.RemoveQueueItemAsync(request);

        // Assert
        (await _eventsContext.SearchQueue.ToListAsync()).ShouldBeEmpty();
    }

    #endregion

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
            request.ArrTarget().Record,
            request.ArrTarget().RemoveFromClient,
            request.ArrTarget().ChangeCategory,
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
        var queueItems = await _eventsContext.SearchQueue.ToListAsync();
        queueItems.Count.ShouldBe(1);
        queueItems[0].ArrInstanceId.ShouldBe(request.Instance.Id);
        queueItems[0].ItemId.ShouldBe(request.ArrTarget().SearchItem.Id);
        queueItems[0].Title.ShouldBe(request.ArrTarget().Record.Title);
    }

    [Fact]
    public async Task RemoveQueueItemAsync_Success_ClearsDownloadMarkedForRemovalCache()
    {
        // Arrange
        var request = CreateRemoveRequest();
        var cacheKey = $"remove_{request.ArrTarget().Record.DownloadId.ToLowerInvariant()}_{request.Instance.Url}";
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
        var hash = request.ArrTarget().Record.DownloadId.ToLowerInvariant();
        Striker.RecurringHashes.TryAdd(hash, null);

        // Act
        await _queueItemRemover.RemoveQueueItemAsync(request);

        // Assert
        var queueItems = await _eventsContext.SearchQueue.ToListAsync();
        queueItems.ShouldBeEmpty();
    }

    [Fact]
    public async Task RemoveQueueItemAsync_WhenHashIsRecurring_RemovesHashFromRecurring()
    {
        // Arrange
        var request = CreateRemoveRequest();
        var hash = request.ArrTarget().Record.DownloadId.ToLowerInvariant();
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
        var queueItems = await _eventsContext.SearchQueue.ToListAsync();
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
        var queueItems = await _eventsContext.SearchQueue.ToListAsync();
        queueItems.ShouldBeEmpty();
    }

    [Fact]
    public async Task RemoveQueueItemAsync_WhenSkipSearch_AndHashIsNotRecurring_DoesNotModifyRecurringHashes()
    {
        // Arrange
        var request = CreateRemoveRequest(skipSearch: true);
        var hash = request.ArrTarget().Record.DownloadId.ToLowerInvariant();

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
        var queueItems = await _eventsContext.SearchQueue.ToListAsync();
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
        var cacheKey = $"remove_{request.ArrTarget().Record.DownloadId.ToLowerInvariant()}_{request.Instance.Url}";
        _memoryCache.Set(cacheKey, true);

        _arrClient
            .DeleteQueueItemAsync(
                Arg.Any<ArrInstance>(),
                Arg.Any<QueueRecord>(),
                Arg.Any<bool>(),
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
            Arg.Is<bool>(x => x == removeFromClient),
            Arg.Any<bool>(),
            Arg.Any<DeleteReason>());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RemoveQueueItemAsync_PassesCorrectChangeCategoryFlag(bool changeCategory)
    {
        // Arrange
        var request = CreateRemoveRequest(changeCategory: changeCategory);

        // Act
        await _queueItemRemover.RemoveQueueItemAsync(request);

        // Assert
        await _arrClient.Received(1).DeleteQueueItemAsync(
            Arg.Any<ArrInstance>(),
            Arg.Any<QueueRecord>(),
            Arg.Any<bool>(),
            Arg.Is<bool>(x => x == changeCategory),
            Arg.Any<DeleteReason>());
    }

    #endregion

    #region Helper Methods

    private QueueItemRemoveRequest CreateRemoveRequest(
        InstanceType instanceType = InstanceType.Sonarr,
        bool removeFromClient = true,
        DeleteReason deleteReason = DeleteReason.Stalled,
        bool skipSearch = false,
        bool changeCategory = false)
    {
        // Use an ArrInstance that exists in the DB to satisfy FK constraint on SearchQueueItem
        var instance = GetOrCreateArrInstance(instanceType);

        return new QueueItemRemoveRequest
        {
            Instance = instance,
            Target = new ArrRemovalTarget
            {
                Record = CreateQueueRecord(),
                SearchItem = new SearchItem { Id = 123 },
                RemoveFromClient = removeFromClient,
                ChangeCategory = changeCategory,
            },
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
            InstanceType.LazyLibrarian => TestDataContextFactory.AddLazyLibrarianInstance(_dataContext),
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
