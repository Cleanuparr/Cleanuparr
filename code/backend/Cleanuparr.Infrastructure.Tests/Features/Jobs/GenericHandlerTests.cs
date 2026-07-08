using Cleanuparr.Domain.Entities.Arr;
using Cleanuparr.Domain.Entities.Arr.Queue;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Events.Interfaces;
using Cleanuparr.Infrastructure.Features.Arr.Interfaces;
using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Infrastructure.Features.DownloadClient;
using Cleanuparr.Infrastructure.Features.DownloadRemover.Models;
using Cleanuparr.Infrastructure.Features.Jobs;
using Cleanuparr.Infrastructure.Tests.Features.Jobs.TestHelpers;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration;
using Cleanuparr.Persistence.Models.Events;
using Cleanuparr.Persistence.Models.Configuration.Arr;
using Cleanuparr.Persistence.Models.Configuration.DownloadCleaner;
using Cleanuparr.Persistence.Models.Configuration.General;
using Cleanuparr.Persistence.Models.Configuration.MalwareBlocker;
using Cleanuparr.Persistence.Models.Configuration.QueueCleaner;
using MassTransit;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.Jobs;

public class GenericHandlerTests : IClassFixture<JobHandlerFixture>
{
    private readonly JobHandlerFixture _fixture;
    private readonly TestHandler _handler;

    public GenericHandlerTests(JobHandlerFixture fixture)
    {
        _fixture = fixture;
        _fixture.RecreateDataContext();
        _fixture.ResetMocks();
        _handler = new TestHandler(
            Substitute.For<ILogger<GenericHandler>>(),
            _fixture.DataContext,
            _fixture.Cache,
            _fixture.MessageBus,
            _fixture.ArrClientFactory,
            _fixture.ArrQueueIterator,
            _fixture.DownloadServiceFactory,
            _fixture.EventPublisher);
    }

    #region GetRecordSearchItem

    [Fact]
    public void GetRecordSearchItem_SonarrSingleEpisode_ReturnsEpisodeSeriesSearchItem()
    {
        // Arrange
        var record = NewRecord(seriesId: 10, episodeId: 99);

        // Act
        var item = _handler.PublicGetRecordSearchItem(InstanceType.Sonarr, 4f, record, isPack: false);

        // Assert
        var seriesItem = item.ShouldBeOfType<SeriesSearchItem>();
        seriesItem.Id.ShouldBe(99);
        seriesItem.SeriesId.ShouldBe(10);
        seriesItem.SearchType.ShouldBe(SeriesSearchType.Episode);
    }

    [Fact]
    public void GetRecordSearchItem_SonarrPack_ReturnsSeasonSeriesSearchItem()
    {
        // Arrange
        var record = NewRecord(seriesId: 10, seasonNumber: 3);

        // Act
        var item = _handler.PublicGetRecordSearchItem(InstanceType.Sonarr, 4f, record, isPack: true);

        // Assert
        var seriesItem = item.ShouldBeOfType<SeriesSearchItem>();
        seriesItem.Id.ShouldBe(3);
        seriesItem.SeriesId.ShouldBe(10);
        seriesItem.SearchType.ShouldBe(SeriesSearchType.Season);
    }

    [Fact]
    public void GetRecordSearchItem_Radarr_ReturnsMovieIdSearchItem()
    {
        // Arrange
        var record = NewRecord(movieId: 77);

        // Act
        var item = _handler.PublicGetRecordSearchItem(InstanceType.Radarr, 4f, record);

        // Assert
        item.ShouldBeOfType<SearchItem>();
        item.Id.ShouldBe(77);
    }

    [Fact]
    public void GetRecordSearchItem_Lidarr_ReturnsAlbumIdSearchItem()
    {
        // Arrange
        var record = NewRecord(albumId: 55);

        // Act
        var item = _handler.PublicGetRecordSearchItem(InstanceType.Lidarr, 1f, record);

        // Assert
        item.Id.ShouldBe(55);
    }

    [Fact]
    public void GetRecordSearchItem_Readarr_ReturnsBookIdSearchItem()
    {
        // Arrange
        var record = NewRecord(bookId: 42);

        // Act
        var item = _handler.PublicGetRecordSearchItem(InstanceType.Readarr, 1f, record);

        // Assert
        item.Id.ShouldBe(42);
    }

    [Fact]
    public void GetRecordSearchItem_WhisparrV2SingleEpisode_ReturnsSeriesSearchItem()
    {
        // Arrange
        var record = NewRecord(seriesId: 5, episodeId: 13);

        // Act
        var item = _handler.PublicGetRecordSearchItem(InstanceType.Whisparr, version: 2f, record, isPack: false);

        // Assert
        var seriesItem = item.ShouldBeOfType<SeriesSearchItem>();
        seriesItem.Id.ShouldBe(13);
        seriesItem.SeriesId.ShouldBe(5);
        seriesItem.SearchType.ShouldBe(SeriesSearchType.Episode);
    }

    [Fact]
    public void GetRecordSearchItem_WhisparrV2Pack_ReturnsSeasonSearchItem()
    {
        // Arrange
        var record = NewRecord(seriesId: 5, seasonNumber: 2);

        // Act
        var item = _handler.PublicGetRecordSearchItem(InstanceType.Whisparr, version: 2f, record, isPack: true);

        // Assert
        var seriesItem = item.ShouldBeOfType<SeriesSearchItem>();
        seriesItem.Id.ShouldBe(2);
        seriesItem.SeriesId.ShouldBe(5);
        seriesItem.SearchType.ShouldBe(SeriesSearchType.Season);
    }

    [Fact]
    public void GetRecordSearchItem_WhisparrV3_ReturnsMovieIdSearchItem()
    {
        // Arrange
        var record = NewRecord(movieId: 88);

        // Act
        var item = _handler.PublicGetRecordSearchItem(InstanceType.Whisparr, version: 3f, record);

        // Assert
        item.ShouldBeOfType<SearchItem>();
        item.Id.ShouldBe(88);
    }

    #endregion

    #region ProcessArrConfigAsync

    [Fact]
    public async Task ProcessArrConfigAsync_NoEnabledInstances_SkipsProcessing()
    {
        // Arrange
        var config = new ArrConfig
        {
            Type = InstanceType.Sonarr,
            Instances = new List<ArrInstance>
            {
                new ArrInstance { Name = "n", Url = new Uri("http://x"), ApiKey = "k", Enabled = false },
            },
        };

        // Act
        await _handler.PublicProcessArrConfigAsync(config);

        // Assert
        _handler.ProcessInstanceCalls.ShouldBeEmpty();
    }

    [Fact]
    public async Task ProcessArrConfigAsync_EnabledInstances_ProcessesEach()
    {
        // Arrange
        var a = new ArrInstance { Name = "a", Url = new Uri("http://a"), ApiKey = "k", Enabled = true };
        var b = new ArrInstance { Name = "b", Url = new Uri("http://b"), ApiKey = "k", Enabled = true };
        var disabled = new ArrInstance { Name = "c", Url = new Uri("http://c"), ApiKey = "k", Enabled = false };
        var config = new ArrConfig
        {
            Type = InstanceType.Sonarr,
            Instances = new List<ArrInstance> { a, b, disabled },
        };

        // Act
        await _handler.PublicProcessArrConfigAsync(config);

        // Assert
        _handler.ProcessInstanceCalls.Select(i => i.Name).ShouldBe(new[] { "a", "b" });
    }

    [Fact]
    public async Task ProcessArrConfigAsync_InstanceThrowsAndThrowOnFailureTrue_Rethrows()
    {
        // Arrange
        _handler.ProcessInstanceBehavior = _ => throw new InvalidOperationException("boom");
        var config = new ArrConfig
        {
            Type = InstanceType.Sonarr,
            Instances = new List<ArrInstance>
            {
                new ArrInstance { Name = "boom", Url = new Uri("http://x"), ApiKey = "k", Enabled = true },
            },
        };

        // Act / Assert
        await Should.ThrowAsync<InvalidOperationException>(
            () => _handler.PublicProcessArrConfigAsync(config, throwOnFailure: true));
    }

    [Fact]
    public async Task ProcessArrConfigAsync_InstanceThrowsAndThrowOnFailureFalse_LogsAndContinues()
    {
        // Arrange — first instance throws, second succeeds
        _handler.ProcessInstanceBehavior = instance =>
        {
            if (instance.Name == "bad")
            {
                throw new InvalidOperationException("boom");
            }
            return Task.CompletedTask;
        };
        var config = new ArrConfig
        {
            Type = InstanceType.Sonarr,
            Instances = new List<ArrInstance>
            {
                new ArrInstance { Name = "bad", Url = new Uri("http://x"), ApiKey = "k", Enabled = true },
                new ArrInstance { Name = "good", Url = new Uri("http://y"), ApiKey = "k", Enabled = true },
            },
        };

        // Act
        await _handler.PublicProcessArrConfigAsync(config, throwOnFailure: false);

        // Assert
        _handler.ProcessInstanceCalls.Select(i => i.Name).ShouldBe(new[] { "bad", "good" });
    }

    #endregion

    #region PublishQueueItemRemoveRequest

    [Fact]
    public async Task PublishQueueItemRemoveRequest_AlreadyMarked_SkipsPublish()
    {
        // Arrange
        const string key = "remove-key";
        _fixture.Cache.Set(key, true);
        var arrConfig = new ArrConfig { Type = InstanceType.Sonarr, Instances = [] };
        var instance = new ArrInstance
        {
            Name = "s",
            Url = new Uri("http://s"),
            ApiKey = "k",
            ArrConfig = arrConfig,
            Version = 4f,
        };

        // Act
        await _handler.PublicPublishQueueItemRemoveRequest(
            key,
            instance,
            NewRecord(seriesId: 1, episodeId: 2),
            isPack: false,
            removeFromClient: true,
            DeleteReason.FailedImport);

        // Assert
        await _fixture.MessageBus.DidNotReceiveWithAnyArgs().Publish(default(object)!, default(CancellationToken));
    }

    [Fact]
    public async Task PublishQueueItemRemoveRequest_Sonarr_PublishesSeriesSearchItemMessage()
    {
        // Arrange
        var arrConfig = new ArrConfig { Type = InstanceType.Sonarr, Instances = [] };
        var instance = new ArrInstance
        {
            Name = "s",
            Url = new Uri("http://s"),
            ApiKey = "k",
            ArrConfig = arrConfig,
            Version = 4f,
        };
        var record = NewRecord(seriesId: 1, episodeId: 2);

        // Act
        await _handler.PublicPublishQueueItemRemoveRequest(
            "k1", instance, record, isPack: false, removeFromClient: true, DeleteReason.FailedImport);

        // Assert
        await _fixture.MessageBus.Received(1)
            .Publish(Arg.Any<QueueItemRemoveRequest<SeriesSearchItem>>(), Arg.Any<CancellationToken>());
        await _fixture.EventPublisher.Received(1).PublishAsync(
            EventType.DownloadMarkedForDeletion, Arg.Any<string>(), Arg.Any<EventSeverity>(),
            Arg.Any<Action<AppEvent>?>(), Arg.Any<Guid?>(), Arg.Any<Guid?>(), Arg.Any<bool?>());
    }

    [Fact]
    public async Task PublishQueueItemRemoveRequest_Radarr_PublishesGenericSearchItemMessage()
    {
        // Arrange
        var arrConfig = new ArrConfig { Type = InstanceType.Radarr, Instances = [] };
        var instance = new ArrInstance
        {
            Name = "r",
            Url = new Uri("http://r"),
            ApiKey = "k",
            ArrConfig = arrConfig,
            Version = 4f,
        };
        var record = NewRecord(movieId: 9);

        // Act
        await _handler.PublicPublishQueueItemRemoveRequest(
            "k1", instance, record, isPack: false, removeFromClient: false, DeleteReason.Stalled);

        // Assert
        await _fixture.MessageBus.Received(1)
            .Publish(Arg.Any<QueueItemRemoveRequest<SearchItem>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PublishQueueItemRemoveRequest_WhisparrV2_PublishesSeriesSearchItemMessage()
    {
        // Arrange
        var arrConfig = new ArrConfig { Type = InstanceType.Whisparr, Instances = [] };
        var instance = new ArrInstance
        {
            Name = "w2",
            Url = new Uri("http://w"),
            ApiKey = "k",
            ArrConfig = arrConfig,
            Version = 2f,
        };
        var record = NewRecord(seriesId: 1, episodeId: 2);

        // Act
        await _handler.PublicPublishQueueItemRemoveRequest(
            "k1", instance, record, isPack: false, removeFromClient: true, DeleteReason.FailedImport);

        // Assert
        await _fixture.MessageBus.Received(1)
            .Publish(Arg.Any<QueueItemRemoveRequest<SeriesSearchItem>>(), Arg.Any<CancellationToken>());
    }

    #endregion

    #region GetInitializedDownloadServicesAsync

    [Fact]
    public async Task GetInitializedDownloadServicesAsync_NoClientsInContext_ReturnsEmpty()
    {
        // Arrange
        ContextProvider.Set(nameof(DownloadClientConfig), new List<DownloadClientConfig>());

        // Act
        var services = await _handler.PublicGetInitializedDownloadServicesAsync();

        // Assert
        services.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetInitializedDownloadServicesAsync_AllSucceed_ReturnsAll()
    {
        // Arrange
        var clientA = new DownloadClientConfig
        {
            Id = Guid.NewGuid(),
            Name = "a",
            Type = DownloadClientType.Torrent,
            TypeName = DownloadClientTypeName.qBittorrent,
        };
        var clientB = new DownloadClientConfig
        {
            Id = Guid.NewGuid(),
            Name = "b",
            Type = DownloadClientType.Torrent,
            TypeName = DownloadClientTypeName.Deluge,
        };
        ContextProvider.Set(nameof(DownloadClientConfig), new List<DownloadClientConfig> { clientA, clientB });

        var serviceA = Substitute.For<IDownloadService>();
        serviceA.LoginAsync().Returns(Task.CompletedTask);
        var serviceB = Substitute.For<IDownloadService>();
        serviceB.LoginAsync().Returns(Task.CompletedTask);
        _fixture.DownloadServiceFactory.GetDownloadService(clientA).Returns(serviceA);
        _fixture.DownloadServiceFactory.GetDownloadService(clientB).Returns(serviceB);

        // Act
        var services = await _handler.PublicGetInitializedDownloadServicesAsync();

        // Assert
        services.Count.ShouldBe(2);
    }

    [Fact]
    public async Task GetInitializedDownloadServicesAsync_LoginFailureForOne_SkipsThatOne()
    {
        // Arrange
        var clientA = new DownloadClientConfig
        {
            Id = Guid.NewGuid(),
            Name = "a",
            Type = DownloadClientType.Torrent,
            TypeName = DownloadClientTypeName.qBittorrent,
        };
        var clientB = new DownloadClientConfig
        {
            Id = Guid.NewGuid(),
            Name = "b",
            Type = DownloadClientType.Torrent,
            TypeName = DownloadClientTypeName.Deluge,
        };
        ContextProvider.Set(nameof(DownloadClientConfig), new List<DownloadClientConfig> { clientA, clientB });

        var serviceA = Substitute.For<IDownloadService>();
        serviceA.LoginAsync().Returns(Task.FromException(new InvalidOperationException("login failed")));
        var serviceB = Substitute.For<IDownloadService>();
        serviceB.LoginAsync().Returns(Task.CompletedTask);
        _fixture.DownloadServiceFactory.GetDownloadService(clientA).Returns(serviceA);
        _fixture.DownloadServiceFactory.GetDownloadService(clientB).Returns(serviceB);

        // Act
        var services = await _handler.PublicGetInitializedDownloadServicesAsync();

        // Assert
        services.Count.ShouldBe(1);
        services[0].ShouldBeSameAs(serviceB);
    }

    #endregion

    #region ExecuteAsync — populates context

    [Fact]
    public async Task ExecuteAsync_PopulatesContextWithAllConfigs()
    {
        // Act
        await _handler.ExecuteAsync();

        // Assert — every key required downstream is captured inside ExecuteInternalAsync,
        // since AsyncLocal writes don't propagate back to the caller
        _handler.ExecuteInternalInvoked.ShouldBeTrue();
        _handler.CapturedGeneralConfig.ShouldNotBeNull();
        _handler.CapturedSonarrConfig!.Type.ShouldBe(InstanceType.Sonarr);
        _handler.CapturedRadarrConfig!.Type.ShouldBe(InstanceType.Radarr);
        _handler.CapturedLidarrConfig!.Type.ShouldBe(InstanceType.Lidarr);
        _handler.CapturedReadarrConfig!.Type.ShouldBe(InstanceType.Readarr);
        _handler.CapturedWhisparrConfig!.Type.ShouldBe(InstanceType.Whisparr);
        _handler.CapturedQueueCleanerConfig.ShouldNotBeNull();
        _handler.CapturedContentBlockerConfig.ShouldNotBeNull();
        _handler.CapturedDownloadCleanerConfig.ShouldNotBeNull();
        _handler.CapturedDownloadClients.ShouldNotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_OnlyEnabledDownloadClientsInContext()
    {
        // Arrange
        TestDataContextFactory.AddDownloadClient(_fixture.DataContext, name: "enabled-1", enabled: true);
        TestDataContextFactory.AddDownloadClient(_fixture.DataContext, name: "disabled-1", typeName: DownloadClientTypeName.Deluge, enabled: false);

        // Act
        await _handler.ExecuteAsync();

        // Assert
        _handler.CapturedDownloadClients.ShouldNotBeNull();
        _handler.CapturedDownloadClients!.Count.ShouldBe(1);
        _handler.CapturedDownloadClients[0].Name.ShouldBe("enabled-1");
    }

    #endregion

    private static QueueRecord NewRecord(
        long id = 1,
        string downloadId = "h",
        long seriesId = 0,
        long episodeId = 0,
        long seasonNumber = 0,
        long movieId = 0,
        long albumId = 0,
        long bookId = 0)
    {
        return new QueueRecord
        {
            Id = id,
            Title = $"item-{id}",
            DownloadId = downloadId,
            Protocol = "torrent",
            SeriesId = seriesId,
            EpisodeId = episodeId,
            SeasonNumber = seasonNumber,
            MovieId = movieId,
            AlbumId = albumId,
            BookId = bookId,
        };
    }

    /// <summary>
    /// Concrete GenericHandler subclass exposing protected members for testing.
    /// </summary>
    private sealed class TestHandler : GenericHandler
    {
        public List<ArrInstance> ProcessInstanceCalls { get; } = [];
        public Func<ArrInstance, Task> ProcessInstanceBehavior { get; set; } = _ => Task.CompletedTask;
        public bool ExecuteInternalInvoked { get; private set; }

        // Snapshot of ContextProvider values, captured inside ExecuteInternalAsync.
        // (AsyncLocal writes from ExecuteAsync don't propagate back to the caller.)
        public GeneralConfig? CapturedGeneralConfig { get; private set; }
        public ArrConfig? CapturedSonarrConfig { get; private set; }
        public ArrConfig? CapturedRadarrConfig { get; private set; }
        public ArrConfig? CapturedLidarrConfig { get; private set; }
        public ArrConfig? CapturedReadarrConfig { get; private set; }
        public ArrConfig? CapturedWhisparrConfig { get; private set; }
        public QueueCleanerConfig? CapturedQueueCleanerConfig { get; private set; }
        public ContentBlockerConfig? CapturedContentBlockerConfig { get; private set; }
        public DownloadCleanerConfig? CapturedDownloadCleanerConfig { get; private set; }
        public List<DownloadClientConfig>? CapturedDownloadClients { get; private set; }

        public TestHandler(
            ILogger<GenericHandler> logger,
            DataContext dataContext,
            IMemoryCache cache,
            IBus messageBus,
            IArrClientFactory arrClientFactory,
            IArrQueueIterator arrQueueIterator,
            IDownloadServiceFactory downloadServiceFactory,
            IEventPublisher eventPublisher)
            : base(logger, dataContext, cache, messageBus, arrClientFactory, arrQueueIterator, downloadServiceFactory, eventPublisher)
        {
        }

        protected override Task ExecuteInternalAsync(CancellationToken cancellationToken = default)
        {
            ExecuteInternalInvoked = true;
            CapturedGeneralConfig = ContextProvider.Get(nameof(GeneralConfig)) as GeneralConfig;
            CapturedSonarrConfig = ContextProvider.Get(nameof(InstanceType.Sonarr)) as ArrConfig;
            CapturedRadarrConfig = ContextProvider.Get(nameof(InstanceType.Radarr)) as ArrConfig;
            CapturedLidarrConfig = ContextProvider.Get(nameof(InstanceType.Lidarr)) as ArrConfig;
            CapturedReadarrConfig = ContextProvider.Get(nameof(InstanceType.Readarr)) as ArrConfig;
            CapturedWhisparrConfig = ContextProvider.Get(nameof(InstanceType.Whisparr)) as ArrConfig;
            CapturedQueueCleanerConfig = ContextProvider.Get(nameof(QueueCleanerConfig)) as QueueCleanerConfig;
            CapturedContentBlockerConfig = ContextProvider.Get(nameof(ContentBlockerConfig)) as ContentBlockerConfig;
            CapturedDownloadCleanerConfig = ContextProvider.Get(nameof(DownloadCleanerConfig)) as DownloadCleanerConfig;
            CapturedDownloadClients = ContextProvider.Get(nameof(DownloadClientConfig)) as List<DownloadClientConfig>;
            return Task.CompletedTask;
        }

        protected override Task ProcessInstanceAsync(ArrInstance instance)
        {
            ProcessInstanceCalls.Add(instance);
            return ProcessInstanceBehavior(instance);
        }

        public SearchItem PublicGetRecordSearchItem(InstanceType type, float version, QueueRecord record, bool isPack = false)
            => GetRecordSearchItem(type, version, record, isPack);

        public Task PublicProcessArrConfigAsync(ArrConfig config, bool throwOnFailure = false)
            => ProcessArrConfigAsync(config, throwOnFailure);

        public Task PublicPublishQueueItemRemoveRequest(
            string key,
            ArrInstance instance,
            QueueRecord record,
            bool isPack,
            bool removeFromClient,
            DeleteReason deleteReason,
            bool skipSearch = false,
            DownloadClientConfig? downloadClient = null,
            bool changeCategory = false)
            => PublishQueueItemRemoveRequest(key, instance, record, isPack, removeFromClient, deleteReason, skipSearch, downloadClient, changeCategory);

        public async Task<IReadOnlyList<IDownloadService>> PublicGetInitializedDownloadServicesAsync()
            => await GetInitializedDownloadServicesAsync();
    }
}
