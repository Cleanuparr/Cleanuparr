using Cleanuparr.Infrastructure.Features.LazyLibrarian;
using Cleanuparr.Domain.Entities;
using Cleanuparr.Domain.Entities.Arr.Queue;
using Cleanuparr.Domain.Entities.HealthCheck;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Events.Interfaces;
using Cleanuparr.Infrastructure.Features.Arr.Interfaces;
using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Infrastructure.Features.DownloadClient;
using Cleanuparr.Infrastructure.Features.Files;
using Cleanuparr.Infrastructure.Features.ItemStriker;
using Cleanuparr.Infrastructure.Features.Jobs;
using Cleanuparr.Infrastructure.Features.MalwareBlocker;
using Cleanuparr.Infrastructure.Http;
using Cleanuparr.Infrastructure.Interceptors;
using Cleanuparr.Infrastructure.Services;
using Cleanuparr.Infrastructure.Services.Interfaces;
using Cleanuparr.Infrastructure.Tests.Features.Jobs.TestHelpers;
using Cleanuparr.Persistence.Models.Configuration;
using Cleanuparr.Persistence.Models.Configuration.Arr;
using Cleanuparr.Persistence.Models.Configuration.DownloadCleaner;
using Cleanuparr.Persistence.Models.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.Jobs.Integration;

[Collection(IntegrationTestCollection.Name)]
public class DownloadCleanerIntegrationTests : IDisposable
{
    private readonly IntegrationTestFixture _fixture;

    public DownloadCleanerIntegrationTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
        _fixture.Reset();
    }

    public void Dispose()
    {
        Striker.RecurringHashes.Clear();
    }

    private DownloadCleaner CreateSut()
    {
        return new DownloadCleaner(
            Substitute.For<ILogger<DownloadCleaner>>(),
            _fixture.DataContext,
            _fixture.Cache,
            _fixture.MessageBus,
            _fixture.ArrClientFactory,
            _fixture.ArrQueueIterator,
            _fixture.DownloadServiceFactory,
            _fixture.EventPublisher,
            _fixture.TimeProvider,
            _fixture.SeedingRulesService,
            _fixture.UnlinkedService,
            _fixture.DeadTorrentService,
            _fixture.OrphanedFilesService,
            _fixture.DryRunInterceptor,
            _fixture.LazyLibrarianService);
    }

    /// <summary>
    /// Creates a mock download service that uses the actual DB config (so seeding rules match by ID).
    /// </summary>
    private static IDownloadService CreateMockDownloadServiceWithDbConfig(DownloadClientConfig dbConfig)
    {
        var mock = Substitute.For<IDownloadService>();
        mock.ClientConfig.Returns(dbConfig);
        mock.LoginAsync().Returns(Task.CompletedTask);
        return mock;
    }

    [Fact]
    public async Task ArrManagedDownloads_AreExcludedFromCleanup()
    {
        // Arrange
        TestDataContextFactory.AddSonarrInstance(_fixture.DataContext);
        var downloadClient = TestDataContextFactory.AddDownloadClient(_fixture.DataContext);
        TestDataContextFactory.AddSeedingRule(_fixture.DataContext);

        string arrManagedHash = "arr_managed_hash_123";
        string orphanedHash = "orphaned_hash_456";

        var arrManagedDownload = CreateMockTorrentItem(arrManagedHash, "Managed.Show.S01E01");
        var orphanedDownload = CreateMockTorrentItem(orphanedHash, "Orphaned.Movie.2024");

        var mockDownloadService = CreateMockDownloadServiceWithDbConfig(downloadClient);
        mockDownloadService.GetSeedingDownloads()
            .Returns([arrManagedDownload, orphanedDownload]);

        _fixture.DownloadServiceFactory.GetDownloadService(Arg.Any<DownloadClientConfig>())
            .Returns(mockDownloadService);

        // Setup arr queue iterator to return the arr-managed hash
        var queueRecord = new QueueRecord
        {
            Id = 1,
            Title = "Managed.Show.S01E01",
            Protocol = "torrent",
            DownloadId = arrManagedHash
        };
        _fixture.SetupArrQueueIterator(queueRecord);

        var sut = CreateSut();

        // Act - advance time past the 10s delay
        var executeTask = sut.ExecuteAsync();
        _fixture.TimeProvider.Advance(TimeSpan.FromSeconds(15));
        await executeTask;

        // Assert: Only the orphaned download should be passed to filter/clean
        mockDownloadService.Received().FilterDownloadsToBeCleanedAsync(
            Arg.Is<List<ITorrentItemWrapper>>(list =>
                list.Count == 1 && list[0].Hash == orphanedHash),
            Arg.Any<List<ISeedingRule>>());
    }

    [Fact]
    public async Task IgnoredDownloads_AreExcludedFromCleanup()
    {
        // Arrange
        TestDataContextFactory.AddSonarrInstance(_fixture.DataContext);
        var downloadClient = TestDataContextFactory.AddDownloadClient(_fixture.DataContext);
        TestDataContextFactory.AddSeedingRule(_fixture.DataContext);

        // Add a download name to the ignored list
        var generalConfig = await _fixture.DataContext.GeneralConfigs.FirstAsync();
        generalConfig.IgnoredDownloads.Add("ignored_download");
        await _fixture.DataContext.SaveChangesAsync();

        var ignoredDownload = CreateMockTorrentItem("some_hash", "ignored_download");
        var normalDownload = CreateMockTorrentItem("normal_hash", "Normal.Movie.2024");

        var mockDownloadService = CreateMockDownloadServiceWithDbConfig(downloadClient);
        mockDownloadService.GetSeedingDownloads()
            .Returns([ignoredDownload, normalDownload]);

        _fixture.DownloadServiceFactory.GetDownloadService(Arg.Any<DownloadClientConfig>())
            .Returns(mockDownloadService);

        // No arr-managed downloads
        _fixture.ArrQueueIterator.Iterate(
                Arg.Any<IArrClient>(), Arg.Any<ArrInstance>(),
                Arg.Any<Func<IReadOnlyList<QueueRecord>, Task>>())
            .Returns(ci =>
            {
                var callback = ci.Arg<Func<IReadOnlyList<QueueRecord>, Task>>();
                return callback(Array.Empty<QueueRecord>());
            });

        var sut = CreateSut();

        // Act
        var executeTask = sut.ExecuteAsync();
        _fixture.TimeProvider.Advance(TimeSpan.FromSeconds(15));
        await executeTask;

        // Assert: Only the non-ignored download should be processed
        mockDownloadService.Received().FilterDownloadsToBeCleanedAsync(
            Arg.Is<List<ITorrentItemWrapper>>(list =>
                list.Count == 1 && list[0].Hash == "normal_hash"),
            Arg.Any<List<ISeedingRule>>());
    }

    [Fact]
    public async Task NoDownloadClients_ExitsEarly()
    {
        // Arrange: No download clients configured (default seed has none)
        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert: No download service interactions, no events
        _fixture.DownloadServiceFactory.DidNotReceive()
            .GetDownloadService(Arg.Any<DownloadClientConfig>());
        var events = await _fixture.EventsContext.Events.ToListAsync();
        events.ShouldBeEmpty();
    }

    [Fact]
    public async Task CleanedDownload_PublishesDownloadCleanedEvent()
    {
        // Arrange
        TestDataContextFactory.AddSonarrInstance(_fixture.DataContext);
        var downloadClient = TestDataContextFactory.AddDownloadClient(_fixture.DataContext);
        TestDataContextFactory.AddSeedingRule(_fixture.DataContext);

        var torrent = CreateMockTorrentItem("cleaned_hash_abc", "Completed.Movie.2024");

        var mockDownloadService = CreateMockDownloadServiceWithDbConfig(downloadClient);
        mockDownloadService.GetSeedingDownloads().Returns([torrent]);
        mockDownloadService.FilterDownloadsToBeCleanedAsync(
            Arg.Any<List<ITorrentItemWrapper>>(), Arg.Any<List<ISeedingRule>>())
            .Returns(ci => ci.Arg<List<ITorrentItemWrapper>>());

        // Configure CleanDownloadsAsync to simulate what real DownloadService does:
        // set ContextProvider keys and call real EventPublisher
        mockDownloadService.CleanDownloadsAsync(
            Arg.Any<List<ITorrentItemWrapper>>(), Arg.Any<List<ISeedingRule>>())
            .Returns(async ci =>
            {
                ContextProvider.Set(ContextProvider.Keys.ItemName, "Completed.Movie.2024");
                ContextProvider.Set(ContextProvider.Keys.Hash, "cleaned_hash_abc");
                ContextProvider.Set(ContextProvider.Keys.DownloadClientUrl, downloadClient.ExternalOrInternalUrl);
                ContextProvider.Set(ContextProvider.Keys.DownloadClientId, downloadClient.Id);
                ContextProvider.Set(ContextProvider.Keys.DownloadClientType, downloadClient.TypeName);
                ContextProvider.Set(ContextProvider.Keys.DownloadClientName, downloadClient.Name);
                await _fixture.EventPublisher.PublishDownloadCleaned(
                    1.5, TimeSpan.FromHours(24), "completed", CleanReason.MaxRatioReached);
            });

        _fixture.DownloadServiceFactory.GetDownloadService(Arg.Any<DownloadClientConfig>())
            .Returns(mockDownloadService);

        // No arr-managed downloads
        _fixture.ArrQueueIterator.Iterate(
                Arg.Any<IArrClient>(), Arg.Any<ArrInstance>(),
                Arg.Any<Func<IReadOnlyList<QueueRecord>, Task>>())
            .Returns(ci =>
            {
                var callback = ci.Arg<Func<IReadOnlyList<QueueRecord>, Task>>();
                return callback(Array.Empty<QueueRecord>());
            });

        var sut = CreateSut();

        // Act
        var executeTask = sut.ExecuteAsync();
        _fixture.TimeProvider.Advance(TimeSpan.FromSeconds(15));
        await executeTask;

        // Assert: Full DownloadCleaned event property verification
        var events = await _fixture.EventsContext.Events.ToListAsync();
        events.Count.ShouldBe(1);

        var cleanedEvent = events.First(e => e.EventType == EventType.DownloadCleaned);
        cleanedEvent.Message.ShouldBe("Cleaned item from download client with reason: MaxRatioReached");
        cleanedEvent.Severity.ShouldBe(EventSeverity.Important);
        cleanedEvent.JobRunId.ShouldBe(_fixture.JobRunId);
        cleanedEvent.ArrInstanceId.ShouldBeNull();
        cleanedEvent.DownloadClientId.ShouldBe(downloadClient.Id);
        cleanedEvent.IsDryRun.ShouldBe(false);
        cleanedEvent.StrikeId.ShouldBeNull();
        cleanedEvent.TrackingId.ShouldBeNull();
        cleanedEvent.SearchStatus.ShouldBeNull();
        cleanedEvent.CompletedAt.ShouldBeNull();
        cleanedEvent.CycleId.ShouldBeNull();
        cleanedEvent.ItemTitle.ShouldBe("Completed.Movie.2024");
        cleanedEvent.ItemHash.ShouldBe("cleaned_hash_abc");
        cleanedEvent.CleanedCategory.ShouldBe("completed");
        cleanedEvent.SeedRatio.ShouldBe(1.5);
        cleanedEvent.SeedingTimeHours.ShouldBe(24.0);
        cleanedEvent.CleanReason.ShouldBe(CleanReason.MaxRatioReached);

        // Assert: Notification sent
        await _fixture.NotificationPublisher.Received(1)
            .NotifyDownloadCleaned(1.5, TimeSpan.FromHours(24), "completed", CleanReason.MaxRatioReached);
    }

    [Fact]
    public async Task UnlinkedDownload_PublishesCategoryChangedEvent()
    {
        // Arrange
        TestDataContextFactory.AddSonarrInstance(_fixture.DataContext);
        var downloadClient = TestDataContextFactory.AddDownloadClient(_fixture.DataContext);
        TestDataContextFactory.AddUnlinkedConfig(_fixture.DataContext,
            enabled: true, targetCategory: "unlinked", categories: ["completed"]);

        var torrent = CreateMockTorrentItem("unlinked_hash_xyz", "NoLinks.Movie.2024", category: "completed");

        var mockDownloadService = CreateMockDownloadServiceWithDbConfig(downloadClient);
        mockDownloadService.GetSeedingDownloads().Returns([torrent]);
        mockDownloadService.FilterDownloadsToChangeCategoryAsync(
            Arg.Any<List<ITorrentItemWrapper>>(), Arg.Any<UnlinkedConfig>())
            .Returns(ci => ci.Arg<List<ITorrentItemWrapper>>());

        // Configure ChangeCategoryForNoHardLinksAsync to simulate what real DownloadService does
        mockDownloadService.ChangeCategoryForNoHardLinksAsync(
            Arg.Any<List<ITorrentItemWrapper>>(), Arg.Any<UnlinkedConfig>())
            .Returns(async ci =>
            {
                ContextProvider.Set(ContextProvider.Keys.ItemName, "NoLinks.Movie.2024");
                ContextProvider.Set(ContextProvider.Keys.Hash, "unlinked_hash_xyz");
                ContextProvider.Set(ContextProvider.Keys.DownloadClientUrl, downloadClient.ExternalOrInternalUrl);
                ContextProvider.Set(ContextProvider.Keys.DownloadClientId, downloadClient.Id);
                ContextProvider.Set(ContextProvider.Keys.DownloadClientType, downloadClient.TypeName);
                ContextProvider.Set(ContextProvider.Keys.DownloadClientName, downloadClient.Name);
                await _fixture.EventPublisher.PublishCategoryChanged("completed", "unlinked");
            });

        _fixture.DownloadServiceFactory.GetDownloadService(Arg.Any<DownloadClientConfig>())
            .Returns(mockDownloadService);

        // No arr-managed downloads
        _fixture.ArrQueueIterator.Iterate(
                Arg.Any<IArrClient>(), Arg.Any<ArrInstance>(),
                Arg.Any<Func<IReadOnlyList<QueueRecord>, Task>>())
            .Returns(ci =>
            {
                var callback = ci.Arg<Func<IReadOnlyList<QueueRecord>, Task>>();
                return callback(Array.Empty<QueueRecord>());
            });

        var sut = CreateSut();

        // Act
        var executeTask = sut.ExecuteAsync();
        _fixture.TimeProvider.Advance(TimeSpan.FromSeconds(15));
        await executeTask;

        // Assert: Full CategoryChanged event property verification
        var events = await _fixture.EventsContext.Events.ToListAsync();
        events.Count.ShouldBe(1);

        var categoryEvent = events.First(e => e.EventType == EventType.CategoryChanged);
        categoryEvent.Message.ShouldBe("Category changed from 'completed' to 'unlinked'");
        categoryEvent.Severity.ShouldBe(EventSeverity.Information);
        categoryEvent.JobRunId.ShouldBe(_fixture.JobRunId);
        categoryEvent.ArrInstanceId.ShouldBeNull();
        categoryEvent.DownloadClientId.ShouldBe(downloadClient.Id);
        categoryEvent.IsDryRun.ShouldBe(false);
        categoryEvent.StrikeId.ShouldBeNull();
        categoryEvent.TrackingId.ShouldBeNull();
        categoryEvent.SearchStatus.ShouldBeNull();
        categoryEvent.CompletedAt.ShouldBeNull();
        categoryEvent.CycleId.ShouldBeNull();
        categoryEvent.ItemTitle.ShouldBe("NoLinks.Movie.2024");
        categoryEvent.ItemHash.ShouldBe("unlinked_hash_xyz");
        categoryEvent.OldCategory.ShouldBe("completed");
        categoryEvent.NewCategory.ShouldBe("unlinked");
        categoryEvent.IsCategoryTag.ShouldBe(false);

        // Assert: Notification sent
        await _fixture.NotificationPublisher.Received(1)
            .NotifyCategoryChanged("completed", "unlinked", false);
    }

    [Fact]
    public async Task StopRule_StopsTheDownloadAndPublishesDownloadStoppedEvent()
    {
        // Arrange
        RecordingDownloadService downloadService = SetupSeedingRuleRun(
            SeedingRuleAction.Stop,
            CreateMockTorrentItem("stop_hash", "Seeding.Movie.2024", category: "completed", ratio: 2.0, seedingHours: 10));

        DownloadCleaner sut = CreateSut();

        // Act
        Task executeTask = sut.ExecuteAsync();
        _fixture.TimeProvider.Advance(TimeSpan.FromSeconds(15));
        await executeTask;

        // Assert: the torrent was stopped and stayed in the client
        downloadService.StoppedHashes.ShouldBe(["stop_hash"]);
        downloadService.DeletedHashes.ShouldBeEmpty();

        List<AppEvent> events = await _fixture.EventsContext.Events.ToListAsync();
        AppEvent stoppedEvent = events.ShouldHaveSingleItem();
        stoppedEvent.EventType.ShouldBe(EventType.DownloadStopped);
        stoppedEvent.Severity.ShouldBe(EventSeverity.Important);
        stoppedEvent.ItemTitle.ShouldBe("Seeding.Movie.2024");
        stoppedEvent.ItemHash.ShouldBe("stop_hash");
        stoppedEvent.CleanedCategory.ShouldBe("completed");
        stoppedEvent.SeedRatio.ShouldBe(2.0);
        stoppedEvent.SeedingTimeHours.ShouldBe(10.0);
        stoppedEvent.CleanReason.ShouldBe(CleanReason.MaxRatioReached);

        await _fixture.NotificationPublisher.Received(1)
            .NotifyDownloadStopped(2.0, TimeSpan.FromHours(10), "completed", CleanReason.MaxRatioReached);
    }

    [Fact]
    public async Task StopRule_SecondRunOverTheStoppedDownload_DoesNothing()
    {
        // Arrange
        ITorrentItemWrapper torrent = CreateMockTorrentItem(
            "stop_hash", "Seeding.Movie.2024", category: "completed", ratio: 2.0, seedingHours: 10);
        RecordingDownloadService downloadService = SetupSeedingRuleRun(SeedingRuleAction.Stop, torrent);

        Task firstRun = CreateSut().ExecuteAsync();
        _fixture.TimeProvider.Advance(TimeSpan.FromSeconds(15));
        await firstRun;

        // The clients keep listing a stopped torrent as seeding.
        torrent.IsStopped.Returns(true);

        // Act
        Task secondRun = CreateSut().ExecuteAsync();
        _fixture.TimeProvider.Advance(TimeSpan.FromSeconds(15));
        await secondRun;

        // Assert: no second stop and no second event
        downloadService.StoppedHashes.ShouldBe(["stop_hash"]);

        List<AppEvent> events = await _fixture.EventsContext.Events.ToListAsync();
        events.ShouldHaveSingleItem();

        await _fixture.NotificationPublisher.Received(1)
            .NotifyDownloadStopped(Arg.Any<double>(), Arg.Any<TimeSpan>(), Arg.Any<string>(), Arg.Any<CleanReason>());
    }

    [Fact]
    public async Task DeleteRule_StillDeletesADownloadThatIsAlreadyStopped()
    {
        // Arrange: Deluge and Transmission list paused but finished torrents as seeding
        RecordingDownloadService downloadService = SetupSeedingRuleRun(
            SeedingRuleAction.Delete,
            CreateMockTorrentItem(
                "paused_hash", "Paused.Movie.2024", category: "completed", ratio: 2.0, seedingHours: 10, isStopped: true));

        DownloadCleaner sut = CreateSut();

        // Act
        Task executeTask = sut.ExecuteAsync();
        _fixture.TimeProvider.Advance(TimeSpan.FromSeconds(15));
        await executeTask;

        // Assert
        downloadService.DeletedHashes.ShouldBe(["paused_hash"]);
        downloadService.StoppedHashes.ShouldBeEmpty();

        List<AppEvent> events = await _fixture.EventsContext.Events.ToListAsync();
        events.ShouldHaveSingleItem().EventType.ShouldBe(EventType.DownloadCleaned);
    }

    [Fact]
    public async Task UnknownRuleAction_LeavesTheDownloadAlone()
    {
        // Arrange
        RecordingDownloadService downloadService = SetupSeedingRuleRun(
            SeedingRuleAction.Delete,
            CreateMockTorrentItem("future_hash", "Future.Movie.2024", category: "completed", ratio: 2.0, seedingHours: 10));

        // Only a newer version can write an action this build does not know.
        await _fixture.DataContext.Database.ExecuteSqlRawAsync(
            """UPDATE "QBitSeedingRules" SET "Action" = 'fromthefuture'""");

        DownloadCleaner sut = CreateSut();

        // Act
        Task executeTask = sut.ExecuteAsync();
        _fixture.TimeProvider.Advance(TimeSpan.FromSeconds(15));
        await executeTask;

        // Assert
        downloadService.DeletedHashes.ShouldBeEmpty();
        downloadService.StoppedHashes.ShouldBeEmpty();

        List<AppEvent> events = await _fixture.EventsContext.Events.ToListAsync();
        events.ShouldBeEmpty();
    }

    /// <summary>
    /// Seeds a client with one seeding rule and a real DownloadService over the given torrents.
    /// The cleanup loop itself runs, rather than a mocked CleanDownloadsAsync.
    /// </summary>
    private RecordingDownloadService SetupSeedingRuleRun(SeedingRuleAction action, params ITorrentItemWrapper[] torrents)
    {
        TestDataContextFactory.AddSonarrInstance(_fixture.DataContext);
        DownloadClientConfig downloadClient = TestDataContextFactory.AddDownloadClient(_fixture.DataContext);
        TestDataContextFactory.AddSeedingRule(_fixture.DataContext, action: action);

        IDynamicHttpClientProvider httpClientProvider = Substitute.For<IDynamicHttpClientProvider>();
        httpClientProvider.CreateClient(Arg.Any<DownloadClientConfig>()).Returns(new HttpClient());

        RecordingDownloadService downloadService = new(
            Substitute.For<ILogger<DownloadService>>(),
            Substitute.For<IFilenameEvaluator>(),
            Substitute.For<IStriker>(),
            _fixture.DryRunInterceptor,
            _fixture.HardLinkFileService,
            httpClientProvider,
            _fixture.EventPublisher,
            _fixture.BlocklistProvider,
            downloadClient,
            Substitute.For<IQueueRuleEvaluator>(),
            new SeedingRuleEvaluator(),
            torrents.ToList());

        _fixture.DryRunInterceptor
            .InterceptAsync(Arg.Any<Func<Task>>(), Arg.Any<string?>())
            .ReturnsForAnyArgs(ci => ci.ArgAt<Func<Task>>(0).Invoke());

        _fixture.DownloadServiceFactory.GetDownloadService(Arg.Any<DownloadClientConfig>())
            .Returns(downloadService);

        // No arr-managed downloads
        _fixture.SetupArrQueueIterator();

        return downloadService;
    }

    /// <summary>
    /// A real DownloadService, so the cleanup loop runs.
    /// The client calls are recorded rather than sent.
    /// </summary>
    private sealed class RecordingDownloadService : DownloadService
    {
        private readonly List<ITorrentItemWrapper> _seedingDownloads;

        public RecordingDownloadService(
            ILogger<DownloadService> logger,
            IFilenameEvaluator filenameEvaluator,
            IStriker striker,
            IDryRunInterceptor dryRunInterceptor,
            IHardLinkFileService hardLinkFileService,
            IDynamicHttpClientProvider httpClientProvider,
            IEventPublisher eventPublisher,
            IBlocklistProvider blocklistProvider,
            DownloadClientConfig downloadClientConfig,
            IQueueRuleEvaluator queueRuleEvaluator,
            ISeedingRuleEvaluator seedingRuleEvaluator,
            List<ITorrentItemWrapper> seedingDownloads
        ) : base(
            logger, filenameEvaluator, striker, dryRunInterceptor, hardLinkFileService, httpClientProvider,
            eventPublisher, blocklistProvider, downloadClientConfig, queueRuleEvaluator, seedingRuleEvaluator)
        {
            _seedingDownloads = seedingDownloads;
        }

        public List<string> DeletedHashes { get; } = [];

        public List<string> StoppedHashes { get; } = [];

        public override Task DeleteDownload(ITorrentItemWrapper torrent, bool deleteSourceFiles)
        {
            DeletedHashes.Add(torrent.Hash);

            return Task.CompletedTask;
        }

        public override Task StopDownload(ITorrentItemWrapper torrent)
        {
            StoppedHashes.Add(torrent.Hash);

            return Task.CompletedTask;
        }

        public override Task<List<ITorrentItemWrapper>> GetSeedingDownloads() => Task.FromResult(_seedingDownloads);

        public override List<ITorrentItemWrapper>? FilterDownloadsToBeCleanedAsync(
            List<ITorrentItemWrapper>? downloads, List<ISeedingRule> seedingRules) => downloads;

        public override List<ITorrentItemWrapper>? FilterDownloadsToChangeCategoryAsync(
            List<ITorrentItemWrapper>? downloads, UnlinkedConfig unlinkedConfig) => [];

        public override Task ChangeCategoryForNoHardLinksAsync(
            List<ITorrentItemWrapper>? downloads, UnlinkedConfig unlinkedConfig) => Task.CompletedTask;

        public override Task<List<ITorrentItemWrapper>> GetAllTorrentsLite() => Task.FromResult(_seedingDownloads);

        public override Task<IReadOnlyList<string>> GetClaimedPathsAsync(IReadOnlyList<ITorrentItemWrapper> torrents) =>
            Task.FromResult<IReadOnlyList<string>>([]);

        public override Task LoginAsync() => Task.CompletedTask;

        public override void Dispose()
        {
        }

        public override Task<HealthCheckResult> HealthCheckAsync() => throw new NotSupportedException();

        public override Task<DownloadCheckResult> ShouldRemoveFromArrQueueAsync(
            string hash, IReadOnlyList<string> ignoredDownloads) => throw new NotSupportedException();

        public override Task ChangeTorrentCategoryAsync(ITorrentItemWrapper torrent, string targetCategory, bool useTag) =>
            throw new NotSupportedException();

        public override Task CreateCategoryAsync(string name) => throw new NotSupportedException();

        public override Task<BlockFilesResult> BlockUnwantedFilesAsync(
            string hash, IReadOnlyList<string> ignoredDownloads) => throw new NotSupportedException();
    }

    private static ITorrentItemWrapper CreateMockTorrentItem(
        string hash,
        string name,
        string? category = null,
        double ratio = 0,
        double seedingHours = 0,
        bool isStopped = false)
    {
        var mock = Substitute.For<ITorrentItemWrapper>();
        mock.Hash.Returns(hash);
        mock.Name.Returns(name);
        mock.Category.Returns(category);
        mock.Ratio.Returns(ratio);
        mock.SeedingTimeSeconds.Returns((long)TimeSpan.FromHours(seedingHours).TotalSeconds);
        mock.IsStopped.Returns(isStopped);
        mock.IsIgnored(Arg.Any<List<string>>()).Returns(ci =>
        {
            var ignoredList = ci.Arg<List<string>>();
            return ignoredList.Contains(name, StringComparer.InvariantCultureIgnoreCase);
        });
        return mock;
    }
}
