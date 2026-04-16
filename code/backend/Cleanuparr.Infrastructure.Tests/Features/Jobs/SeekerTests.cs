using Cleanuparr.Domain.Entities.Arr;
using Cleanuparr.Domain.Entities.Arr.Queue;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Events.Interfaces;
using Cleanuparr.Infrastructure.Features.Arr.Interfaces;
using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Infrastructure.Interceptors;
using Cleanuparr.Infrastructure.Tests.Features.Jobs.TestHelpers;
using Cleanuparr.Persistence.Models.Configuration.Arr;
using Cleanuparr.Persistence.Models.Configuration.Seeker;
using Cleanuparr.Persistence.Models.State;
using Cleanuparr.Infrastructure.Hubs;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;
using SeekerJob = Cleanuparr.Infrastructure.Features.Jobs.Seeker;

namespace Cleanuparr.Infrastructure.Tests.Features.Jobs;

[Collection(JobHandlerCollection.Name)]
public class SeekerTests : IDisposable
{
    private readonly JobHandlerFixture _fixture;
    private readonly ILogger<SeekerJob> _logger;
    private readonly IRadarrClient _radarrClient;
    private readonly ISonarrClient _sonarrClient;
    private readonly IDryRunInterceptor _dryRunInterceptor;
    private readonly IHostingEnvironment _hostingEnvironment;
    private readonly IHubContext<AppHub> _hubContext;

    public SeekerTests(JobHandlerFixture fixture)
    {
        _fixture = fixture;
        _fixture.RecreateDataContext();
        _fixture.ResetMocks();
        _logger = Substitute.For<ILogger<SeekerJob>>();
        _radarrClient = Substitute.For<IRadarrClient>();
        _sonarrClient = Substitute.For<ISonarrClient>();
        _dryRunInterceptor = Substitute.For<IDryRunInterceptor>();
        _hostingEnvironment = Substitute.For<IHostingEnvironment>();
        _hubContext = Substitute.For<IHubContext<AppHub>>();

        // Default: hub context setup
        var mockClients = Substitute.For<IHubClients>();
        var mockClientProxy = Substitute.For<IClientProxy>();
        mockClients.All.Returns(mockClientProxy);
        _hubContext.Clients.Returns(mockClients);

        // Default: development mode (skips jitter)
        _hostingEnvironment.EnvironmentName.Returns("Development");

        // Default: dry run disabled
        _dryRunInterceptor.IsDryRunEnabled().Returns(false);

        // Default: GetAllTagsAsync returns empty list
        _radarrClient
            .GetAllTagsAsync(Arg.Any<ArrInstance>())
            .Returns([]);
        _sonarrClient
            .GetAllTagsAsync(Arg.Any<ArrInstance>())
            .Returns([]);

        // Default: PublishSearchTriggered returns a Guid
        _fixture.EventPublisher
            .PublishSearchTriggered(
                Arg.Any<string>(),
                Arg.Any<SeekerSearchType>(),
                Arg.Any<SeekerSearchReason>(),
                Arg.Any<Guid?>())
            .Returns(Guid.NewGuid());
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    private SeekerJob CreateSut()
    {
        return new SeekerJob(
            _logger,
            _fixture.DataContext,
            _radarrClient,
            _sonarrClient,
            _fixture.ArrClientFactory,
            _fixture.ArrQueueIterator,
            _fixture.EventPublisher,
            _dryRunInterceptor,
            _hostingEnvironment,
            _fixture.TimeProvider,
            _hubContext
        );
    }

    #region ExecuteAsync Tests

    [Fact]
    public async Task ExecuteAsync_WhenSearchDisabled_ReturnsEarly()
    {
        // Arrange — disable search in the seeded config
        var config = await _fixture.DataContext.SeekerConfigs.FirstAsync();
        config.SearchEnabled = false;
        await _fixture.DataContext.SaveChangesAsync();

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert — no search triggered, no arr client interaction
        _fixture.ArrClientFactory.DidNotReceive()
            .GetClient(Arg.Any<InstanceType>(), Arg.Any<float>());
        await _fixture.EventPublisher.DidNotReceive()
            .PublishSearchTriggered(
                Arg.Any<string>(),
                Arg.Any<SeekerSearchType>(),
                Arg.Any<SeekerSearchReason>(),
                Arg.Any<Guid?>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenProactiveSearchDisabled_SkipsProactiveSearch()
    {
        // Arrange — search enabled but proactive disabled, no queue items
        var config = await _fixture.DataContext.SeekerConfigs.FirstAsync();
        config.SearchEnabled = true;
        config.ProactiveSearchEnabled = false;
        await _fixture.DataContext.SaveChangesAsync();

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert — no arr client interaction (no replacement items, proactive disabled)
        _fixture.ArrClientFactory.DidNotReceive()
            .GetClient(Arg.Any<InstanceType>(), Arg.Any<float>());
        await _fixture.EventPublisher.DidNotReceive()
            .PublishSearchTriggered(
                Arg.Any<string>(),
                Arg.Any<SeekerSearchType>(),
                Arg.Any<SeekerSearchReason>(),
                Arg.Any<Guid?>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenReplacementItemExists_ProcessesReplacementFirst()
    {
        // Arrange
        var radarrInstance = TestDataContextFactory.AddRadarrInstance(_fixture.DataContext);

        _fixture.DataContext.SearchQueue.Add(new SearchQueueItem
        {
            ArrInstanceId = radarrInstance.Id,
            ArrInstance = radarrInstance,
            ItemId = 42,
            Title = "Test Movie",
            CreatedAt = DateTime.UtcNow
        });
        await _fixture.DataContext.SaveChangesAsync();

        var mockArrClient = Substitute.For<IArrClient>();
        mockArrClient
            .SearchItemAsync(Arg.Any<ArrInstance>(), Arg.Any<SearchItem>())
            .Returns(100L);

        _fixture.ArrClientFactory
            .GetClient(InstanceType.Radarr, Arg.Any<float>())
            .Returns(mockArrClient);

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert — search was triggered for the replacement item
        await mockArrClient.Received(1)
            .SearchItemAsync(radarrInstance, Arg.Any<SearchItem>());

        await _fixture.EventPublisher.Received(1)
            .PublishSearchTriggered(
                "Test Movie",
                SeekerSearchType.Replacement,
                SeekerSearchReason.Replacement,
                Arg.Any<Guid?>());

        // Replacement item should be removed from the queue
        var remaining = await _fixture.DataContext.SearchQueue.CountAsync();
        Assert.Equal(0, remaining);
    }

    [Fact]
    public async Task ExecuteAsync_WhenDryRunEnabled_DoesNotRemoveFromSearchQueue()
    {
        // Arrange
        _dryRunInterceptor.IsDryRunEnabled().Returns(true);

        var radarrInstance = TestDataContextFactory.AddRadarrInstance(_fixture.DataContext);

        _fixture.DataContext.SearchQueue.Add(new SearchQueueItem
        {
            ArrInstanceId = radarrInstance.Id,
            ArrInstance = radarrInstance,
            ItemId = 42,
            Title = "Test Movie",
            CreatedAt = DateTime.UtcNow
        });
        await _fixture.DataContext.SaveChangesAsync();

        var mockArrClient = Substitute.For<IArrClient>();
        mockArrClient
            .SearchItemAsync(Arg.Any<ArrInstance>(), Arg.Any<SearchItem>())
            .Returns(100L);

        _fixture.ArrClientFactory
            .GetClient(InstanceType.Radarr, Arg.Any<float>())
            .Returns(mockArrClient);

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert — search was triggered but item stays in queue
        await mockArrClient.Received(1)
            .SearchItemAsync(radarrInstance, Arg.Any<SearchItem>());

        var remaining = await _fixture.DataContext.SearchQueue.CountAsync();
        Assert.Equal(1, remaining);
    }

    [Fact]
    public async Task ExecuteAsync_WhenActiveDownloadLimitReached_SkipsInstance()
    {
        // Arrange — enable proactive search with a Radarr instance
        var config = await _fixture.DataContext.SeekerConfigs.FirstAsync();
        config.SearchEnabled = true;
        config.ProactiveSearchEnabled = true;
        await _fixture.DataContext.SaveChangesAsync();

        var radarrInstance = TestDataContextFactory.AddRadarrInstance(_fixture.DataContext);

        // Add a SeekerInstanceConfig with ActiveDownloadLimit = 2
        _fixture.DataContext.SeekerInstanceConfigs.Add(new SeekerInstanceConfig
        {
            ArrInstanceId = radarrInstance.Id,
            ArrInstance = radarrInstance,
            Enabled = true,
            ActiveDownloadLimit = 2
        });
        await _fixture.DataContext.SaveChangesAsync();

        var mockArrClient = Substitute.For<IArrClient>();

        // Return 2 queue items with SizeLeft > 0 (actively downloading), which meets the limit
        QueueRecord[] activeDownloads =
        [
            new() { Id = 1, Title = "Download 1", DownloadId = "hash1", Protocol = "torrent", SizeLeft = 1000, MovieId = 10, TrackedDownloadState = "downloading" },
            new() { Id = 2, Title = "Download 2", DownloadId = "hash2", Protocol = "torrent", SizeLeft = 2000, MovieId = 20, TrackedDownloadState = "downloading" }
        ];
        _fixture.ArrQueueIterator
            .Iterate(mockArrClient, Arg.Any<ArrInstance>(), Arg.Any<Func<IReadOnlyList<QueueRecord>, Task>>())
            .Returns(ci => ci.ArgAt<Func<IReadOnlyList<QueueRecord>, Task>>(2)(activeDownloads));

        _fixture.ArrClientFactory
            .GetClient(InstanceType.Radarr, Arg.Any<float>())
            .Returns(mockArrClient);

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert — no search triggered because active downloads >= limit
        await mockArrClient.DidNotReceive()
            .SearchItemAsync(Arg.Any<ArrInstance>(), Arg.Any<SearchItem>());

        await _fixture.EventPublisher.DidNotReceive()
            .PublishSearchTriggered(
                Arg.Any<string>(),
                SeekerSearchType.Proactive,
                Arg.Any<SeekerSearchReason>(),
                Arg.Any<Guid?>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenActiveDownloadLimitNotReached_BecauseSameDownloadId_DoesNotSkip()
    {
        // Arrange — season pack: 2 queue records share the same DownloadId, so it's 1 unique download
        var config = await _fixture.DataContext.SeekerConfigs.FirstAsync();
        config.SearchEnabled = true;
        config.ProactiveSearchEnabled = true;
        await _fixture.DataContext.SaveChangesAsync();

        var radarrInstance = TestDataContextFactory.AddRadarrInstance(_fixture.DataContext);

        _fixture.DataContext.SeekerInstanceConfigs.Add(new SeekerInstanceConfig
        {
            ArrInstanceId = radarrInstance.Id,
            ArrInstance = radarrInstance,
            Enabled = true,
            ActiveDownloadLimit = 2
        });
        await _fixture.DataContext.SaveChangesAsync();

        var mockArrClient = Substitute.For<IArrClient>();

        // 2 queue records with the same DownloadId (season pack) — only 1 unique download
        QueueRecord[] activeDownloads =
        [
            new() { Id = 1, Title = "Episode 1", DownloadId = "same-hash", Protocol = "torrent", SizeLeft = 1000, MovieId = 10, TrackedDownloadState = "downloading" },
            new() { Id = 2, Title = "Episode 2", DownloadId = "same-hash", Protocol = "torrent", SizeLeft = 2000, MovieId = 20, TrackedDownloadState = "downloading" }
        ];
        _fixture.ArrQueueIterator
            .Iterate(mockArrClient, Arg.Any<ArrInstance>(), Arg.Any<Func<IReadOnlyList<QueueRecord>, Task>>())
            .Returns(ci => ci.ArgAt<Func<IReadOnlyList<QueueRecord>, Task>>(2)(activeDownloads));

        _fixture.ArrClientFactory
            .GetClient(InstanceType.Radarr, Arg.Any<float>())
            .Returns(mockArrClient);

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert — search should NOT be skipped because only 1 unique download (< limit of 2)
        // The cycle completes (no eligible items) but the point is it wasn't blocked by the limit
        var instanceConfig = await _fixture.DataContext.SeekerInstanceConfigs.FirstAsync();
        Assert.NotNull(instanceConfig.LastProcessedAt);
    }

    [Fact]
    public async Task ExecuteAsync_Radarr_ExcludesMoviesAlreadyInQueue()
    {
        // Arrange — proactive search enabled with 3 movies, one already in queue
        var config = await _fixture.DataContext.SeekerConfigs.FirstAsync();
        config.SearchEnabled = true;
        config.ProactiveSearchEnabled = true;
        await _fixture.DataContext.SaveChangesAsync();

        var radarrInstance = TestDataContextFactory.AddRadarrInstance(_fixture.DataContext);

        _fixture.DataContext.SeekerInstanceConfigs.Add(new SeekerInstanceConfig
        {
            ArrInstanceId = radarrInstance.Id,
            ArrInstance = radarrInstance,
            Enabled = true
        });
        await _fixture.DataContext.SaveChangesAsync();

        var mockArrClient = Substitute.For<IArrClient>();

        // Movie 2 is already in the download queue
        QueueRecord[] queuedRecords =
        [
            new() { Id = 1, Title = "Movie 2 Download", DownloadId = "hash1", Protocol = "torrent", SizeLeft = 1000, MovieId = 2, TrackedDownloadState = "downloading" }
        ];
        _fixture.ArrQueueIterator
            .Iterate(mockArrClient, Arg.Any<ArrInstance>(), Arg.Any<Func<IReadOnlyList<QueueRecord>, Task>>())
            .Returns(ci => ci.ArgAt<Func<IReadOnlyList<QueueRecord>, Task>>(2)(queuedRecords));

        _radarrClient
            .GetAllMoviesAsync(radarrInstance)
            .Returns(
            [
                new SearchableMovie { Id = 1, Title = "Movie 1", Status = "released", Monitored = true, Tags = [] },
                new SearchableMovie { Id = 2, Title = "Movie 2", Status = "released", Monitored = true, Tags = [] },
                new SearchableMovie { Id = 3, Title = "Movie 3", Status = "released", Monitored = true, Tags = [] }
            ]);

        HashSet<SearchItem>? capturedSearchItems = null;
        mockArrClient
            .SearchItemAsync(radarrInstance, Arg.Any<SearchItem>())
            .Returns(ci =>
            {
                capturedSearchItems = [ci.ArgAt<SearchItem>(1)];
                return 100L;
            });

        _fixture.ArrClientFactory
            .GetClient(InstanceType.Radarr, Arg.Any<float>())
            .Returns(mockArrClient);

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert — search was triggered, but NOT for movie 2
        await mockArrClient.Received(1)
            .SearchItemAsync(radarrInstance, Arg.Any<SearchItem>());

        Assert.NotNull(capturedSearchItems);
        Assert.DoesNotContain(capturedSearchItems, item => item.Id == 2);
    }

    [Fact]
    public async Task ExecuteAsync_Radarr_DoesNotExcludeImportFailedItems()
    {
        // Arrange — movie in queue with importFailed state should still be searchable
        var config = await _fixture.DataContext.SeekerConfigs.FirstAsync();
        config.SearchEnabled = true;
        config.ProactiveSearchEnabled = true;
        await _fixture.DataContext.SaveChangesAsync();

        var radarrInstance = TestDataContextFactory.AddRadarrInstance(_fixture.DataContext);

        _fixture.DataContext.SeekerInstanceConfigs.Add(new SeekerInstanceConfig
        {
            ArrInstanceId = radarrInstance.Id,
            ArrInstance = radarrInstance,
            Enabled = true
        });
        await _fixture.DataContext.SaveChangesAsync();

        var mockArrClient = Substitute.For<IArrClient>();

        // Movie 1 is in queue but with importFailed state — should NOT be excluded
        QueueRecord[] queuedRecords =
        [
            new() { Id = 1, Title = "Movie 1 Download", DownloadId = "hash1", Protocol = "torrent", SizeLeft = 0, MovieId = 1, TrackedDownloadState = "importFailed" }
        ];
        _fixture.ArrQueueIterator
            .Iterate(mockArrClient, Arg.Any<ArrInstance>(), Arg.Any<Func<IReadOnlyList<QueueRecord>, Task>>())
            .Returns(ci => ci.ArgAt<Func<IReadOnlyList<QueueRecord>, Task>>(2)(queuedRecords));

        _radarrClient
            .GetAllMoviesAsync(radarrInstance)
            .Returns(
            [
                new SearchableMovie { Id = 1, Title = "Movie 1", Status = "released", Monitored = true, Tags = [] }
            ]);

        mockArrClient
            .SearchItemAsync(radarrInstance, Arg.Any<SearchItem>())
            .Returns(100L);

        _fixture.ArrClientFactory
            .GetClient(InstanceType.Radarr, Arg.Any<float>())
            .Returns(mockArrClient);

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert — search was triggered for movie 1 (importFailed does not exclude)
        await mockArrClient.Received(1)
            .SearchItemAsync(radarrInstance, Arg.Any<SearchItem>());
    }

    [Fact]
    public async Task ExecuteAsync_Sonarr_ExcludesSeasonsAlreadyInQueue()
    {
        // Arrange — series with 2 seasons, season 1 in queue
        var config = await _fixture.DataContext.SeekerConfigs.FirstAsync();
        config.SearchEnabled = true;
        config.ProactiveSearchEnabled = true;
        await _fixture.DataContext.SaveChangesAsync();

        var sonarrInstance = TestDataContextFactory.AddSonarrInstance(_fixture.DataContext);

        _fixture.DataContext.SeekerInstanceConfigs.Add(new SeekerInstanceConfig
        {
            ArrInstanceId = sonarrInstance.Id,
            ArrInstance = sonarrInstance,
            Enabled = true
        });
        await _fixture.DataContext.SaveChangesAsync();

        var mockArrClient = Substitute.For<IArrClient>();

        // Season 1 of series 10 is in the queue
        QueueRecord[] queuedRecords =
        [
            new() { Id = 1, Title = "Series Episode", DownloadId = "hash1", Protocol = "torrent", SizeLeft = 1000, SeriesId = 10, SeasonNumber = 1, TrackedDownloadState = "downloading" }
        ];
        _fixture.ArrQueueIterator
            .Iterate(mockArrClient, Arg.Any<ArrInstance>(), Arg.Any<Func<IReadOnlyList<QueueRecord>, Task>>())
            .Returns(ci => ci.ArgAt<Func<IReadOnlyList<QueueRecord>, Task>>(2)(queuedRecords));

        _sonarrClient
            .GetAllSeriesAsync(Arg.Any<ArrInstance>())
            .Returns(
            [
                new SearchableSeries { Id = 10, Title = "Test Series", Status = "continuing", Monitored = true, Tags = [], Statistics = new SeriesStatistics { EpisodeCount = 20, EpisodeFileCount = 10 } }
            ]);

        // Use dates relative to FakeTimeProvider (defaults to Jan 1, 2000)
        var pastDate = _fixture.TimeProvider.GetUtcNow().UtcDateTime.AddDays(-30);
        _sonarrClient
            .GetEpisodesAsync(Arg.Any<ArrInstance>(), 10)
            .Returns(
            [
                new SearchableEpisode { Id = 100, SeasonNumber = 1, EpisodeNumber = 1, Monitored = true, AirDateUtc = pastDate, HasFile = false },
                new SearchableEpisode { Id = 101, SeasonNumber = 2, EpisodeNumber = 1, Monitored = true, AirDateUtc = pastDate, HasFile = false }
            ]);

        SeriesSearchItem? capturedSearchItem = null;
        mockArrClient
            .SearchItemAsync(Arg.Any<ArrInstance>(), Arg.Any<SearchItem>())
            .Returns(ci =>
            {
                capturedSearchItem = ci.ArgAt<SearchItem>(1) as SeriesSearchItem;
                return 100L;
            });

        _fixture.ArrClientFactory
            .GetClient(InstanceType.Sonarr, Arg.Any<float>())
            .Returns(mockArrClient);

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert — season 2 was searched (season 1 excluded because it's in queue)
        await mockArrClient.Received(1)
            .SearchItemAsync(Arg.Any<ArrInstance>(), Arg.Any<SearchItem>());

        Assert.NotNull(capturedSearchItem);
        Assert.Equal(2, capturedSearchItem.Id); // Season 2
        Assert.Equal(10, capturedSearchItem.SeriesId);
    }

    [Fact]
    public async Task ExecuteAsync_QueueFetchFails_ProceedsWithoutFiltering()
    {
        // Arrange — queue fetch throws, but search should still proceed
        var config = await _fixture.DataContext.SeekerConfigs.FirstAsync();
        config.SearchEnabled = true;
        config.ProactiveSearchEnabled = true;
        await _fixture.DataContext.SaveChangesAsync();

        var radarrInstance = TestDataContextFactory.AddRadarrInstance(_fixture.DataContext);

        _fixture.DataContext.SeekerInstanceConfigs.Add(new SeekerInstanceConfig
        {
            ArrInstanceId = radarrInstance.Id,
            ArrInstance = radarrInstance,
            Enabled = true
        });
        await _fixture.DataContext.SaveChangesAsync();

        var mockArrClient = Substitute.For<IArrClient>();

        // Queue fetch fails
        _fixture.ArrQueueIterator
            .Iterate(mockArrClient, Arg.Any<ArrInstance>(), Arg.Any<Func<IReadOnlyList<QueueRecord>, Task>>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        _radarrClient
            .GetAllMoviesAsync(radarrInstance)
            .Returns(
            [
                new SearchableMovie { Id = 1, Title = "Movie 1", Status = "released", Monitored = true, Tags = [] }
            ]);

        mockArrClient
            .SearchItemAsync(radarrInstance, Arg.Any<SearchItem>())
            .Returns(100L);

        _fixture.ArrClientFactory
            .GetClient(InstanceType.Radarr, Arg.Any<float>())
            .Returns(mockArrClient);

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert — search still proceeded despite queue fetch failure
        await mockArrClient.Received(1)
            .SearchItemAsync(radarrInstance, Arg.Any<SearchItem>());
    }

    #endregion

    #region Radarr Proactive Search Filters

    [Fact]
    public async Task ExecuteAsync_Radarr_MonitoredOnlyTrue_ExcludesUnmonitoredMovies()
    {
        // Arrange — MonitoredOnly is true by default
        var config = await _fixture.DataContext.SeekerConfigs.FirstAsync();
        config.SearchEnabled = true;
        config.ProactiveSearchEnabled = true;
        await _fixture.DataContext.SaveChangesAsync();

        var radarrInstance = TestDataContextFactory.AddRadarrInstance(_fixture.DataContext);

        _fixture.DataContext.SeekerInstanceConfigs.Add(new SeekerInstanceConfig
        {
            ArrInstanceId = radarrInstance.Id,
            ArrInstance = radarrInstance,
            Enabled = true,
            MonitoredOnly = true
        });
        await _fixture.DataContext.SaveChangesAsync();

        var mockArrClient = Substitute.For<IArrClient>();

        _fixture.ArrQueueIterator
            .Iterate(mockArrClient, Arg.Any<ArrInstance>(), Arg.Any<Func<IReadOnlyList<QueueRecord>, Task>>())
            .Returns(Task.CompletedTask);

        _radarrClient
            .GetAllMoviesAsync(radarrInstance)
            .Returns(
            [
                new SearchableMovie { Id = 1, Title = "Monitored Movie", Status = "released", Monitored = true, Tags = [] },
                new SearchableMovie { Id = 2, Title = "Unmonitored Movie", Status = "released", Monitored = false, Tags = [] }
            ]);

        HashSet<SearchItem>? capturedSearchItems = null;
        mockArrClient
            .SearchItemAsync(radarrInstance, Arg.Any<SearchItem>())
            .Returns(ci =>
            {
                capturedSearchItems = [ci.ArgAt<SearchItem>(1)];
                return 100L;
            });

        _fixture.ArrClientFactory
            .GetClient(InstanceType.Radarr, Arg.Any<float>())
            .Returns(mockArrClient);

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert — only monitored movie searched
        Assert.NotNull(capturedSearchItems);
        Assert.DoesNotContain(capturedSearchItems, item => item.Id == 2);
    }

    [Fact]
    public async Task ExecuteAsync_Radarr_SkipTags_ExcludesMoviesWithMatchingTags()
    {
        // Arrange
        var config = await _fixture.DataContext.SeekerConfigs.FirstAsync();
        config.SearchEnabled = true;
        config.ProactiveSearchEnabled = true;
        await _fixture.DataContext.SaveChangesAsync();

        var radarrInstance = TestDataContextFactory.AddRadarrInstance(_fixture.DataContext);

        _fixture.DataContext.SeekerInstanceConfigs.Add(new SeekerInstanceConfig
        {
            ArrInstanceId = radarrInstance.Id,
            ArrInstance = radarrInstance,
            Enabled = true,
            MonitoredOnly = false,
            SkipTags = ["no-search"]
        });
        await _fixture.DataContext.SaveChangesAsync();

        var mockArrClient = Substitute.For<IArrClient>();

        _fixture.ArrQueueIterator
            .Iterate(mockArrClient, Arg.Any<ArrInstance>(), Arg.Any<Func<IReadOnlyList<QueueRecord>, Task>>())
            .Returns(Task.CompletedTask);

        _radarrClient
            .GetAllMoviesAsync(radarrInstance)
            .Returns(
            [
                new SearchableMovie { Id = 1, Title = "Normal Movie", Status = "released", Monitored = true, Tags = [1] },
                new SearchableMovie { Id = 2, Title = "Skipped Movie", Status = "released", Monitored = true, Tags = [2, 1] }
            ]);

        _radarrClient
            .GetAllTagsAsync(radarrInstance)
            .Returns(
            [
                new Tag { Id = 1, Label = "movies" },
                new Tag { Id = 2, Label = "no-search" }
            ]);

        HashSet<SearchItem>? capturedSearchItems = null;
        mockArrClient
            .SearchItemAsync(radarrInstance, Arg.Any<SearchItem>())
            .Returns(ci =>
            {
                capturedSearchItems = [ci.ArgAt<SearchItem>(1)];
                return 100L;
            });

        _fixture.ArrClientFactory
            .GetClient(InstanceType.Radarr, Arg.Any<float>())
            .Returns(mockArrClient);

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert — movie with skip tag excluded
        Assert.NotNull(capturedSearchItems);
        Assert.DoesNotContain(capturedSearchItems, item => item.Id == 2);
        Assert.Contains(capturedSearchItems, item => item.Id == 1);
    }

    [Fact]
    public async Task ExecuteAsync_Radarr_UseCutoff_SkipsCutoffMetMovies()
    {
        // Arrange — enable cutoff filtering: only movies with QualityCutoffNotMet should be searched
        var config = await _fixture.DataContext.SeekerConfigs.FirstAsync();
        config.SearchEnabled = true;
        config.ProactiveSearchEnabled = true;
        await _fixture.DataContext.SaveChangesAsync();

        var radarrInstance = TestDataContextFactory.AddRadarrInstance(_fixture.DataContext);

        _fixture.DataContext.SeekerInstanceConfigs.Add(new SeekerInstanceConfig
        {
            ArrInstanceId = radarrInstance.Id,
            ArrInstance = radarrInstance,
            Enabled = true,
            MonitoredOnly = false,
            UseCutoff = true
        });
        await _fixture.DataContext.SaveChangesAsync();

        var mockArrClient = Substitute.For<IArrClient>();

        _fixture.ArrQueueIterator
            .Iterate(mockArrClient, Arg.Any<ArrInstance>(), Arg.Any<Func<IReadOnlyList<QueueRecord>, Task>>())
            .Returns(Task.CompletedTask);

        _radarrClient
            .GetAllMoviesAsync(radarrInstance)
            .Returns(
            [
                new SearchableMovie { Id = 1, Title = "Missing Movie", Status = "released", Monitored = true, HasFile = false, Tags = [] },
                new SearchableMovie { Id = 2, Title = "Cutoff Met", Status = "released", Monitored = true, HasFile = true, MovieFile = new MovieFileInfo { Id = 200, QualityCutoffNotMet = false }, Tags = [] },
                new SearchableMovie { Id = 3, Title = "Cutoff Not Met", Status = "released", Monitored = true, HasFile = true, MovieFile = new MovieFileInfo { Id = 300, QualityCutoffNotMet = true }, Tags = [] }
            ]);

        HashSet<SearchItem>? capturedSearchItems = null;
        mockArrClient
            .SearchItemAsync(radarrInstance, Arg.Any<SearchItem>())
            .Returns(ci =>
            {
                capturedSearchItems = [ci.ArgAt<SearchItem>(1)];
                return 100L;
            });

        _fixture.ArrClientFactory
            .GetClient(InstanceType.Radarr, Arg.Any<float>())
            .Returns(mockArrClient);

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert — movie with cutoff met should be excluded; missing + cutoff not met should be eligible
        Assert.NotNull(capturedSearchItems);
        Assert.DoesNotContain(capturedSearchItems, item => item.Id == 2);
    }

    [Fact]
    public async Task ExecuteAsync_Radarr_CycleComplete_StartsNewCycle()
    {
        // Arrange — all candidate movies are already in search history for current cycle
        var config = await _fixture.DataContext.SeekerConfigs.FirstAsync();
        config.SearchEnabled = true;
        config.ProactiveSearchEnabled = true;
        await _fixture.DataContext.SaveChangesAsync();

        var radarrInstance = TestDataContextFactory.AddRadarrInstance(_fixture.DataContext);
        var currentCycleId = Guid.NewGuid();
        var now = _fixture.TimeProvider.GetUtcNow().UtcDateTime;

        _fixture.DataContext.SeekerInstanceConfigs.Add(new SeekerInstanceConfig
        {
            ArrInstanceId = radarrInstance.Id,
            ArrInstance = radarrInstance,
            Enabled = true,
            MonitoredOnly = false,
            CurrentCycleId = currentCycleId
        });

        // Add history entries for both movies in the current cycle
        // Use dates relative to FakeTimeProvider and far enough back to exceed default MinCycleTimeDays
        _fixture.DataContext.SeekerHistory.Add(new SeekerHistory
        {
            ArrInstanceId = radarrInstance.Id,
            ExternalItemId = 1,
            ItemType = InstanceType.Radarr,
            CycleId = currentCycleId,
            LastSearchedAt = now.AddDays(-10),
            ItemTitle = "Movie 1"
        });
        _fixture.DataContext.SeekerHistory.Add(new SeekerHistory
        {
            ArrInstanceId = radarrInstance.Id,
            ExternalItemId = 2,
            ItemType = InstanceType.Radarr,
            CycleId = currentCycleId,
            LastSearchedAt = now.AddDays(-10),
            ItemTitle = "Movie 2"
        });
        await _fixture.DataContext.SaveChangesAsync();

        var mockArrClient = Substitute.For<IArrClient>();

        _fixture.ArrQueueIterator
            .Iterate(mockArrClient, Arg.Any<ArrInstance>(), Arg.Any<Func<IReadOnlyList<QueueRecord>, Task>>())
            .Returns(Task.CompletedTask);

        _radarrClient
            .GetAllMoviesAsync(radarrInstance)
            .Returns(
            [
                new SearchableMovie { Id = 1, Title = "Movie 1", Status = "released", Monitored = true, Tags = [] },
                new SearchableMovie { Id = 2, Title = "Movie 2", Status = "released", Monitored = true, Tags = [] }
            ]);

        mockArrClient
            .SearchItemAsync(radarrInstance, Arg.Any<SearchItem>())
            .Returns(100L);

        _fixture.ArrClientFactory
            .GetClient(InstanceType.Radarr, Arg.Any<float>())
            .Returns(mockArrClient);

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert — search was triggered (new cycle started) and the CycleId changed
        await mockArrClient.Received(1)
            .SearchItemAsync(radarrInstance, Arg.Any<SearchItem>());

        var instanceConfig = await _fixture.DataContext.SeekerInstanceConfigs
            .FirstAsync(s => s.ArrInstanceId == radarrInstance.Id);
        Assert.NotEqual(currentCycleId, instanceConfig.CurrentCycleId);
    }

    #endregion

    #region Round-Robin

    [Fact]
    public async Task ExecuteAsync_RoundRobin_SelectsOldestProcessedInstance()
    {
        // Arrange — two Radarr instances, round-robin should pick the oldest processed one
        var config = await _fixture.DataContext.SeekerConfigs.FirstAsync();
        config.SearchEnabled = true;
        config.ProactiveSearchEnabled = true;
        config.UseRoundRobin = true;
        await _fixture.DataContext.SaveChangesAsync();

        var radarrInstance1 = TestDataContextFactory.AddRadarrInstance(_fixture.DataContext, "http://radarr1:7878");
        var radarrInstance2 = TestDataContextFactory.AddRadarrInstance(_fixture.DataContext, "http://radarr2:7878");

        // Instance 1 was processed recently, instance 2 was processed long ago
        _fixture.DataContext.SeekerInstanceConfigs.Add(new SeekerInstanceConfig
        {
            ArrInstanceId = radarrInstance1.Id,
            ArrInstance = radarrInstance1,
            Enabled = true,
            MonitoredOnly = false,
            LastProcessedAt = DateTime.UtcNow
        });
        _fixture.DataContext.SeekerInstanceConfigs.Add(new SeekerInstanceConfig
        {
            ArrInstanceId = radarrInstance2.Id,
            ArrInstance = radarrInstance2,
            Enabled = true,
            MonitoredOnly = false,
            LastProcessedAt = DateTime.UtcNow.AddHours(-24)
        });
        await _fixture.DataContext.SaveChangesAsync();

        var mockArrClient = Substitute.For<IArrClient>();

        _fixture.ArrQueueIterator
            .Iterate(mockArrClient, Arg.Any<ArrInstance>(), Arg.Any<Func<IReadOnlyList<QueueRecord>, Task>>())
            .Returns(Task.CompletedTask);

        // Return movies for both instances — only instance 2 should be called
        _radarrClient
            .GetAllMoviesAsync(Arg.Is<ArrInstance>(a => a.Id == radarrInstance2.Id))
            .Returns([new SearchableMovie { Id = 1, Title = "Movie 1", Status = "released", Monitored = true, Tags = [] }]);

        mockArrClient
            .SearchItemAsync(Arg.Any<ArrInstance>(), Arg.Any<SearchItem>())
            .Returns(100L);

        _fixture.ArrClientFactory
            .GetClient(InstanceType.Radarr, Arg.Any<float>())
            .Returns(mockArrClient);

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert — instance 2 (oldest) was processed, verified by GetAllMoviesAsync being called for it
        await _radarrClient.Received(1)
            .GetAllMoviesAsync(Arg.Is<ArrInstance>(a => a.Id == radarrInstance2.Id));
        await _radarrClient.DidNotReceive()
            .GetAllMoviesAsync(Arg.Is<ArrInstance>(a => a.Id == radarrInstance1.Id));
    }

    #endregion

    #region Replacement Edge Cases

    [Fact]
    public async Task ExecuteAsync_ReplacementItem_MissingArrInstance_RemovesFromQueue()
    {
        // Arrange — replacement item references an instance that no longer exists
        var config = await _fixture.DataContext.SeekerConfigs.FirstAsync();
        config.SearchEnabled = true;
        await _fixture.DataContext.SaveChangesAsync();

        // Add a valid instance just so we can create the queue item with its ID, then detach it
        var radarrInstance = TestDataContextFactory.AddRadarrInstance(_fixture.DataContext);
        var instanceId = radarrInstance.Id;

        _fixture.DataContext.SearchQueue.Add(new SearchQueueItem
        {
            ArrInstanceId = instanceId,
            ArrInstance = radarrInstance,
            ItemId = 42,
            Title = "Orphaned Movie",
            CreatedAt = DateTime.UtcNow
        });
        await _fixture.DataContext.SaveChangesAsync();

        // Now remove the arr instance to simulate deletion
        _fixture.DataContext.ArrInstances.Remove(radarrInstance);
        await _fixture.DataContext.SaveChangesAsync();

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert — queue item should be cleaned up
        var remaining = await _fixture.DataContext.SearchQueue.CountAsync();
        Assert.Equal(0, remaining);

        // No search should have been triggered
        await _fixture.EventPublisher.DidNotReceive()
            .PublishSearchTriggered(
                Arg.Any<string>(),
                Arg.Any<SeekerSearchType>(),
                Arg.Any<SeekerSearchReason>(),
                Arg.Any<Guid?>());
    }

    #endregion

    #region MinCycleTimeDays

    [Fact]
    public async Task ExecuteAsync_Radarr_CycleComplete_WaitsForMinCycleTime()
    {
        // Arrange — all items searched but MinCycleTimeDays has not elapsed
        var config = await _fixture.DataContext.SeekerConfigs.FirstAsync();
        config.SearchEnabled = true;
        config.ProactiveSearchEnabled = true;
        await _fixture.DataContext.SaveChangesAsync();

        var radarrInstance = TestDataContextFactory.AddRadarrInstance(_fixture.DataContext);
        var currentCycleId = Guid.NewGuid();
        var now = _fixture.TimeProvider.GetUtcNow().UtcDateTime;

        _fixture.DataContext.SeekerInstanceConfigs.Add(new SeekerInstanceConfig
        {
            ArrInstanceId = radarrInstance.Id,
            ArrInstance = radarrInstance,
            Enabled = true,
            MonitoredOnly = false,
            CurrentCycleId = currentCycleId,
            MinCycleTimeDays = 7,
            TotalEligibleItems = 2
        });

        // Cycle started 2 days ago — within the 7-day minimum
        _fixture.DataContext.SeekerHistory.Add(new SeekerHistory
        {
            ArrInstanceId = radarrInstance.Id,
            ExternalItemId = 1,
            ItemType = InstanceType.Radarr,
            CycleId = currentCycleId,
            LastSearchedAt = now.AddDays(-2),
            ItemTitle = "Movie 1"
        });
        _fixture.DataContext.SeekerHistory.Add(new SeekerHistory
        {
            ArrInstanceId = radarrInstance.Id,
            ExternalItemId = 2,
            ItemType = InstanceType.Radarr,
            CycleId = currentCycleId,
            LastSearchedAt = now.AddDays(-1),
            ItemTitle = "Movie 2"
        });
        await _fixture.DataContext.SaveChangesAsync();

        var mockArrClient = Substitute.For<IArrClient>();

        _fixture.ArrQueueIterator
            .Iterate(mockArrClient, Arg.Any<ArrInstance>(), Arg.Any<Func<IReadOnlyList<QueueRecord>, Task>>())
            .Returns(Task.CompletedTask);

        _radarrClient
            .GetAllMoviesAsync(radarrInstance)
            .Returns(
            [
                new SearchableMovie { Id = 1, Title = "Movie 1", Status = "released", Monitored = true, Tags = [] },
                new SearchableMovie { Id = 2, Title = "Movie 2", Status = "released", Monitored = true, Tags = [] }
            ]);

        _fixture.ArrClientFactory
            .GetClient(InstanceType.Radarr, Arg.Any<float>())
            .Returns(mockArrClient);

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert — no search triggered, cycle not reset
        await mockArrClient.DidNotReceive()
            .SearchItemAsync(radarrInstance, Arg.Any<SearchItem>());

        var instanceConfig = await _fixture.DataContext.SeekerInstanceConfigs
            .FirstAsync(s => s.ArrInstanceId == radarrInstance.Id);
        Assert.Equal(currentCycleId, instanceConfig.CurrentCycleId);
    }

    [Fact]
    public async Task ExecuteAsync_Radarr_CycleComplete_RestartsAfterMinCycleTimeElapsed()
    {
        // Arrange — all items searched and MinCycleTimeDays has elapsed
        var config = await _fixture.DataContext.SeekerConfigs.FirstAsync();
        config.SearchEnabled = true;
        config.ProactiveSearchEnabled = true;
        await _fixture.DataContext.SaveChangesAsync();

        var radarrInstance = TestDataContextFactory.AddRadarrInstance(_fixture.DataContext);
        var currentCycleId = Guid.NewGuid();
        var now = _fixture.TimeProvider.GetUtcNow().UtcDateTime;

        _fixture.DataContext.SeekerInstanceConfigs.Add(new SeekerInstanceConfig
        {
            ArrInstanceId = radarrInstance.Id,
            ArrInstance = radarrInstance,
            Enabled = true,
            MonitoredOnly = false,
            CurrentCycleId = currentCycleId,
            MinCycleTimeDays = 7,
            TotalEligibleItems = 2
        });

        // Cycle started 10 days ago — beyond the 7-day minimum
        _fixture.DataContext.SeekerHistory.Add(new SeekerHistory
        {
            ArrInstanceId = radarrInstance.Id,
            ExternalItemId = 1,
            ItemType = InstanceType.Radarr,
            CycleId = currentCycleId,
            LastSearchedAt = now.AddDays(-10),
            ItemTitle = "Movie 1"
        });
        _fixture.DataContext.SeekerHistory.Add(new SeekerHistory
        {
            ArrInstanceId = radarrInstance.Id,
            ExternalItemId = 2,
            ItemType = InstanceType.Radarr,
            CycleId = currentCycleId,
            LastSearchedAt = now.AddDays(-8),
            ItemTitle = "Movie 2"
        });
        await _fixture.DataContext.SaveChangesAsync();

        var mockArrClient = Substitute.For<IArrClient>();

        _fixture.ArrQueueIterator
            .Iterate(mockArrClient, Arg.Any<ArrInstance>(), Arg.Any<Func<IReadOnlyList<QueueRecord>, Task>>())
            .Returns(Task.CompletedTask);

        _radarrClient
            .GetAllMoviesAsync(radarrInstance)
            .Returns(
            [
                new SearchableMovie { Id = 1, Title = "Movie 1", Status = "released", Monitored = true, Tags = [] },
                new SearchableMovie { Id = 2, Title = "Movie 2", Status = "released", Monitored = true, Tags = [] }
            ]);

        mockArrClient
            .SearchItemAsync(radarrInstance, Arg.Any<SearchItem>())
            .Returns(100L);

        _fixture.ArrClientFactory
            .GetClient(InstanceType.Radarr, Arg.Any<float>())
            .Returns(mockArrClient);

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert — search was triggered, cycle was reset
        await mockArrClient.Received(1)
            .SearchItemAsync(radarrInstance, Arg.Any<SearchItem>());

        var instanceConfig = await _fixture.DataContext.SeekerInstanceConfigs
            .FirstAsync(s => s.ArrInstanceId == radarrInstance.Id);
        Assert.NotEqual(currentCycleId, instanceConfig.CurrentCycleId);
    }

    [Fact]
    public async Task ExecuteAsync_Radarr_CycleComplete_NoCycleHistory_StartsNewCycle()
    {
        // Arrange — cycle complete but no history (cycleStartedAt is null), should not block
        var config = await _fixture.DataContext.SeekerConfigs.FirstAsync();
        config.SearchEnabled = true;
        config.ProactiveSearchEnabled = true;
        await _fixture.DataContext.SaveChangesAsync();

        var radarrInstance = TestDataContextFactory.AddRadarrInstance(_fixture.DataContext);
        var currentCycleId = Guid.NewGuid();

        _fixture.DataContext.SeekerInstanceConfigs.Add(new SeekerInstanceConfig
        {
            ArrInstanceId = radarrInstance.Id,
            ArrInstance = radarrInstance,
            Enabled = true,
            MonitoredOnly = false,
            CurrentCycleId = currentCycleId,
            MinCycleTimeDays = 30
        });

        // History uses a DIFFERENT CycleId — current cycle has no history entries
        var oldCycleId = Guid.NewGuid();
        _fixture.DataContext.SeekerHistory.Add(new SeekerHistory
        {
            ArrInstanceId = radarrInstance.Id,
            ExternalItemId = 1,
            ItemType = InstanceType.Radarr,
            CycleId = oldCycleId,
            LastSearchedAt = DateTime.UtcNow.AddDays(-60),
            ItemTitle = "Movie 1"
        });
        await _fixture.DataContext.SaveChangesAsync();

        var mockArrClient = Substitute.For<IArrClient>();

        _fixture.ArrQueueIterator
            .Iterate(mockArrClient, Arg.Any<ArrInstance>(), Arg.Any<Func<IReadOnlyList<QueueRecord>, Task>>())
            .Returns(Task.CompletedTask);

        _radarrClient
            .GetAllMoviesAsync(radarrInstance)
            .Returns(
            [
                new SearchableMovie { Id = 1, Title = "Movie 1", Status = "released", Monitored = true, Tags = [] }
            ]);

        mockArrClient
            .SearchItemAsync(radarrInstance, Arg.Any<SearchItem>())
            .Returns(100L);

        _fixture.ArrClientFactory
            .GetClient(InstanceType.Radarr, Arg.Any<float>())
            .Returns(mockArrClient);

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert — search was triggered (item not in current cycle, so it's selected directly)
        await mockArrClient.Received(1)
            .SearchItemAsync(radarrInstance, Arg.Any<SearchItem>());
    }

    [Fact]
    public async Task ExecuteAsync_Sonarr_CycleComplete_WaitsForMinCycleTime()
    {
        // Arrange — all series seasons searched but MinCycleTimeDays not elapsed
        var config = await _fixture.DataContext.SeekerConfigs.FirstAsync();
        config.SearchEnabled = true;
        config.ProactiveSearchEnabled = true;
        await _fixture.DataContext.SaveChangesAsync();

        var sonarrInstance = TestDataContextFactory.AddSonarrInstance(_fixture.DataContext);
        var currentCycleId = Guid.NewGuid();
        var now = _fixture.TimeProvider.GetUtcNow().UtcDateTime;

        _fixture.DataContext.SeekerInstanceConfigs.Add(new SeekerInstanceConfig
        {
            ArrInstanceId = sonarrInstance.Id,
            ArrInstance = sonarrInstance,
            Enabled = true,
            MonitoredOnly = false,
            CurrentCycleId = currentCycleId,
            MinCycleTimeDays = 7,
            TotalEligibleItems = 1
        });

        // Series history — season already searched in current cycle (started 2 days ago)
        _fixture.DataContext.SeekerHistory.Add(new SeekerHistory
        {
            ArrInstanceId = sonarrInstance.Id,
            ExternalItemId = 10,
            ItemType = InstanceType.Sonarr,
            SeasonNumber = 1,
            CycleId = currentCycleId,
            LastSearchedAt = now.AddDays(-2),
            ItemTitle = "Test Series"
        });
        await _fixture.DataContext.SaveChangesAsync();

        var mockArrClient = Substitute.For<IArrClient>();

        _fixture.ArrQueueIterator
            .Iterate(mockArrClient, Arg.Any<ArrInstance>(), Arg.Any<Func<IReadOnlyList<QueueRecord>, Task>>())
            .Returns(Task.CompletedTask);

        _sonarrClient
            .GetAllSeriesAsync(sonarrInstance)
            .Returns(
            [
                new SearchableSeries { Id = 10, Title = "Test Series", Status = "continuing", Monitored = true, Tags = [], Statistics = new SeriesStatistics { EpisodeCount = 10, EpisodeFileCount = 5 } }
            ]);

        var pastDate = now.AddDays(-30);
        _sonarrClient
            .GetEpisodesAsync(Arg.Any<ArrInstance>(), 10)
            .Returns(
            [
                new SearchableEpisode { Id = 100, SeasonNumber = 1, EpisodeNumber = 1, Monitored = true, HasFile = false, AirDateUtc = pastDate }
            ]);

        _fixture.ArrClientFactory
            .GetClient(InstanceType.Sonarr, Arg.Any<float>())
            .Returns(mockArrClient);

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert — no search triggered, cycle not reset
        await mockArrClient.DidNotReceive()
            .SearchItemAsync(sonarrInstance, Arg.Any<SearchItem>());

        var instanceConfig = await _fixture.DataContext.SeekerInstanceConfigs
            .FirstAsync(s => s.ArrInstanceId == sonarrInstance.Id);
        Assert.Equal(currentCycleId, instanceConfig.CurrentCycleId);
    }

    [Fact]
    public async Task ExecuteAsync_Sonarr_CycleComplete_RestartsAfterMinCycleTimeElapsed()
    {
        // Arrange — all series seasons searched and MinCycleTimeDays has elapsed
        var config = await _fixture.DataContext.SeekerConfigs.FirstAsync();
        config.SearchEnabled = true;
        config.ProactiveSearchEnabled = true;
        await _fixture.DataContext.SaveChangesAsync();

        var sonarrInstance = TestDataContextFactory.AddSonarrInstance(_fixture.DataContext);
        var currentCycleId = Guid.NewGuid();
        var now = _fixture.TimeProvider.GetUtcNow().UtcDateTime;

        _fixture.DataContext.SeekerInstanceConfigs.Add(new SeekerInstanceConfig
        {
            ArrInstanceId = sonarrInstance.Id,
            ArrInstance = sonarrInstance,
            Enabled = true,
            MonitoredOnly = false,
            CurrentCycleId = currentCycleId,
            MinCycleTimeDays = 7,
            TotalEligibleItems = 1
        });

        // Series history — season already searched in current cycle (started 10 days ago)
        _fixture.DataContext.SeekerHistory.Add(new SeekerHistory
        {
            ArrInstanceId = sonarrInstance.Id,
            ExternalItemId = 10,
            ItemType = InstanceType.Sonarr,
            SeasonNumber = 1,
            CycleId = currentCycleId,
            LastSearchedAt = now.AddDays(-10),
            ItemTitle = "Test Series"
        });
        await _fixture.DataContext.SaveChangesAsync();

        var mockArrClient = Substitute.For<IArrClient>();

        _fixture.ArrQueueIterator
            .Iterate(mockArrClient, Arg.Any<ArrInstance>(), Arg.Any<Func<IReadOnlyList<QueueRecord>, Task>>())
            .Returns(Task.CompletedTask);

        _sonarrClient
            .GetAllSeriesAsync(sonarrInstance)
            .Returns(
            [
                new SearchableSeries { Id = 10, Title = "Test Series", Status = "continuing", Monitored = true, Tags = [], Statistics = new SeriesStatistics { EpisodeCount = 10, EpisodeFileCount = 5 } }
            ]);

        var pastDate = now.AddDays(-30);
        _sonarrClient
            .GetEpisodesAsync(Arg.Any<ArrInstance>(), 10)
            .Returns(
            [
                new SearchableEpisode { Id = 100, SeasonNumber = 1, EpisodeNumber = 1, Monitored = true, HasFile = false, AirDateUtc = pastDate }
            ]);

        mockArrClient
            .SearchItemAsync(sonarrInstance, Arg.Any<SearchItem>())
            .Returns(100L);

        _fixture.ArrClientFactory
            .GetClient(InstanceType.Sonarr, Arg.Any<float>())
            .Returns(mockArrClient);

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert — search was triggered, cycle was reset
        await mockArrClient.Received(1)
            .SearchItemAsync(sonarrInstance, Arg.Any<SearchItem>());

        var instanceConfig = await _fixture.DataContext.SeekerInstanceConfigs
            .FirstAsync(s => s.ArrInstanceId == sonarrInstance.Id);
        Assert.NotEqual(currentCycleId, instanceConfig.CurrentCycleId);
    }

    [Fact]
    public async Task ExecuteAsync_RoundRobin_SkipsInstanceWaitingForMinCycleTime()
    {
        // Arrange — two Radarr instances: one waiting for MinCycleTimeDays, the other has work to do.
        // Round-robin tries instances in order of oldest LastProcessedAt.
        // Instance A (oldest) is cycle-complete and waiting — no search triggered, moves to instance B.
        var config = await _fixture.DataContext.SeekerConfigs.FirstAsync();
        config.SearchEnabled = true;
        config.ProactiveSearchEnabled = true;
        config.UseRoundRobin = true;
        await _fixture.DataContext.SaveChangesAsync();

        var now = _fixture.TimeProvider.GetUtcNow().UtcDateTime;

        // Instance A: cycle complete, waiting for MinCycleTimeDays (oldest LastProcessedAt — tried first)
        var instanceA = TestDataContextFactory.AddRadarrInstance(_fixture.DataContext, "http://radarr-a:7878");
        var cycleIdA = Guid.NewGuid();
        _fixture.DataContext.SeekerInstanceConfigs.Add(new SeekerInstanceConfig
        {
            ArrInstanceId = instanceA.Id,
            ArrInstance = instanceA,
            Enabled = true,
            MonitoredOnly = false,
            CurrentCycleId = cycleIdA,
            MinCycleTimeDays = 30,
            TotalEligibleItems = 1,
            LastProcessedAt = now.AddDays(-5) // Oldest — round-robin tries this first
        });
        _fixture.DataContext.SeekerHistory.Add(new SeekerHistory
        {
            ArrInstanceId = instanceA.Id,
            ExternalItemId = 1,
            ItemType = InstanceType.Radarr,
            CycleId = cycleIdA,
            LastSearchedAt = now.AddDays(-2), // Cycle started 2 days ago, MinCycleTimeDays=30
            ItemTitle = "Movie A"
        });

        // Instance B: has work to do (newer LastProcessedAt)
        var instanceB = TestDataContextFactory.AddRadarrInstance(_fixture.DataContext, "http://radarr-b:7878");
        var cycleIdB = Guid.NewGuid();
        _fixture.DataContext.SeekerInstanceConfigs.Add(new SeekerInstanceConfig
        {
            ArrInstanceId = instanceB.Id,
            ArrInstance = instanceB,
            Enabled = true,
            MonitoredOnly = false,
            CurrentCycleId = cycleIdB,
            MinCycleTimeDays = 5,
            TotalEligibleItems = 1,
            LastProcessedAt = now.AddDays(-1)
        });
        // No history for instance B — it hasn't searched anything yet

        await _fixture.DataContext.SaveChangesAsync();

        var mockArrClient = Substitute.For<IArrClient>();

        _fixture.ArrQueueIterator
            .Iterate(mockArrClient, Arg.Any<ArrInstance>(), Arg.Any<Func<IReadOnlyList<QueueRecord>, Task>>())
            .Returns(Task.CompletedTask);

        // Instance A: return the movie that was already searched in its cycle
        _radarrClient
            .GetAllMoviesAsync(instanceA)
            .Returns(
            [
                new SearchableMovie { Id = 1, Title = "Movie A", Status = "released", Monitored = true, Tags = [] }
            ]);

        _radarrClient
            .GetAllMoviesAsync(instanceB)
            .Returns(
            [
                new SearchableMovie { Id = 10, Title = "Movie B", Status = "released", Monitored = true, Tags = [] }
            ]);

        mockArrClient
            .SearchItemAsync(Arg.Any<ArrInstance>(), Arg.Any<SearchItem>())
            .Returns(100L);

        _fixture.ArrClientFactory
            .GetClient(InstanceType.Radarr, Arg.Any<float>())
            .Returns(mockArrClient);

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert — Instance A was checked (library fetched) but no search triggered
        await _radarrClient.Received(1)
            .GetAllMoviesAsync(instanceA);
        // Instance B was processed and searched
        await _radarrClient.Received(1)
            .GetAllMoviesAsync(instanceB);
        // Search was only triggered for instance B, not instance A
        await mockArrClient.DidNotReceive()
            .SearchItemAsync(instanceA, Arg.Any<SearchItem>());
        await mockArrClient.Received(1)
            .SearchItemAsync(instanceB, Arg.Any<SearchItem>());
    }

    [Fact]
    public async Task ExecuteAsync_Radarr_NewItemAdded_SearchedDespiteCycleComplete()
    {
        // Arrange — cycle was complete (2 items searched), but a new item was added to the library.
        // The new item should be searched immediately without waiting for MinCycleTimeDays.
        var config = await _fixture.DataContext.SeekerConfigs.FirstAsync();
        config.SearchEnabled = true;
        config.ProactiveSearchEnabled = true;
        await _fixture.DataContext.SaveChangesAsync();

        var radarrInstance = TestDataContextFactory.AddRadarrInstance(_fixture.DataContext);
        var currentCycleId = Guid.NewGuid();
        var now = _fixture.TimeProvider.GetUtcNow().UtcDateTime;

        _fixture.DataContext.SeekerInstanceConfigs.Add(new SeekerInstanceConfig
        {
            ArrInstanceId = radarrInstance.Id,
            ArrInstance = radarrInstance,
            Enabled = true,
            MonitoredOnly = false,
            CurrentCycleId = currentCycleId,
            MinCycleTimeDays = 30,
            TotalEligibleItems = 2 // Stale value from previous run
        });

        // History: 2 items searched in current cycle
        _fixture.DataContext.SeekerHistory.Add(new SeekerHistory
        {
            ArrInstanceId = radarrInstance.Id,
            ExternalItemId = 1,
            ItemType = InstanceType.Radarr,
            CycleId = currentCycleId,
            LastSearchedAt = now.AddDays(-2),
            ItemTitle = "Movie 1"
        });
        _fixture.DataContext.SeekerHistory.Add(new SeekerHistory
        {
            ArrInstanceId = radarrInstance.Id,
            ExternalItemId = 2,
            ItemType = InstanceType.Radarr,
            CycleId = currentCycleId,
            LastSearchedAt = now.AddDays(-1),
            ItemTitle = "Movie 2"
        });
        await _fixture.DataContext.SaveChangesAsync();

        var mockArrClient = Substitute.For<IArrClient>();

        _fixture.ArrQueueIterator
            .Iterate(mockArrClient, Arg.Any<ArrInstance>(), Arg.Any<Func<IReadOnlyList<QueueRecord>, Task>>())
            .Returns(Task.CompletedTask);

        // Library now has 3 items — the 3rd was newly added
        _radarrClient
            .GetAllMoviesAsync(radarrInstance)
            .Returns(
            [
                new SearchableMovie { Id = 1, Title = "Movie 1", Status = "released", Monitored = true, Tags = [] },
                new SearchableMovie { Id = 2, Title = "Movie 2", Status = "released", Monitored = true, Tags = [] },
                new SearchableMovie { Id = 3, Title = "Movie 3 (New)", Status = "released", Monitored = true, Tags = [] }
            ]);

        mockArrClient
            .SearchItemAsync(radarrInstance, Arg.Any<SearchItem>())
            .Returns(100L);

        _fixture.ArrClientFactory
            .GetClient(InstanceType.Radarr, Arg.Any<float>())
            .Returns(mockArrClient);

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert — search was triggered for the new item (cycle is NOT considered complete)
        await mockArrClient.Received(1)
            .SearchItemAsync(radarrInstance, Arg.Any<SearchItem>());

        // Cycle ID should NOT have changed (cycle is not complete — there's still a new item)
        var instanceConfig = await _fixture.DataContext.SeekerInstanceConfigs
            .FirstAsync(s => s.ArrInstanceId == radarrInstance.Id);
        Assert.Equal(currentCycleId, instanceConfig.CurrentCycleId);
    }

    [Fact]
    public async Task ExecuteAsync_Radarr_ItemSwapped_SearchesNewItem()
    {
        // Arrange — cycle was complete (2 items searched), but one item was removed and a new one added.
        // Total count is the same, but the library has changed. The new item should be searched.
        var config = await _fixture.DataContext.SeekerConfigs.FirstAsync();
        config.SearchEnabled = true;
        config.ProactiveSearchEnabled = true;
        await _fixture.DataContext.SaveChangesAsync();

        var radarrInstance = TestDataContextFactory.AddRadarrInstance(_fixture.DataContext);
        var currentCycleId = Guid.NewGuid();
        var now = _fixture.TimeProvider.GetUtcNow().UtcDateTime;

        _fixture.DataContext.SeekerInstanceConfigs.Add(new SeekerInstanceConfig
        {
            ArrInstanceId = radarrInstance.Id,
            ArrInstance = radarrInstance,
            Enabled = true,
            MonitoredOnly = false,
            CurrentCycleId = currentCycleId,
            MinCycleTimeDays = 30,
            TotalEligibleItems = 2 // Stale value from previous run
        });

        // History: items 1 and 2 were searched
        _fixture.DataContext.SeekerHistory.Add(new SeekerHistory
        {
            ArrInstanceId = radarrInstance.Id,
            ExternalItemId = 1,
            ItemType = InstanceType.Radarr,
            CycleId = currentCycleId,
            LastSearchedAt = now.AddDays(-2),
            ItemTitle = "Movie 1"
        });
        _fixture.DataContext.SeekerHistory.Add(new SeekerHistory
        {
            ArrInstanceId = radarrInstance.Id,
            ExternalItemId = 2,
            ItemType = InstanceType.Radarr,
            CycleId = currentCycleId,
            LastSearchedAt = now.AddDays(-1),
            ItemTitle = "Movie 2 (Removed)"
        });
        await _fixture.DataContext.SaveChangesAsync();

        var mockArrClient = Substitute.For<IArrClient>();

        _fixture.ArrQueueIterator
            .Iterate(mockArrClient, Arg.Any<ArrInstance>(), Arg.Any<Func<IReadOnlyList<QueueRecord>, Task>>())
            .Returns(Task.CompletedTask);

        // Library: item 2 was removed, item 3 was added (same total count of 2)
        _radarrClient
            .GetAllMoviesAsync(radarrInstance)
            .Returns(
            [
                new SearchableMovie { Id = 1, Title = "Movie 1", Status = "released", Monitored = true, Tags = [] },
                new SearchableMovie { Id = 3, Title = "Movie 3 (New)", Status = "released", Monitored = true, Tags = [] }
            ]);

        mockArrClient
            .SearchItemAsync(radarrInstance, Arg.Any<SearchItem>())
            .Returns(100L);

        _fixture.ArrClientFactory
            .GetClient(InstanceType.Radarr, Arg.Any<float>())
            .Returns(mockArrClient);

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert — search was triggered for the new item
        await mockArrClient.Received(1)
            .SearchItemAsync(radarrInstance, Arg.Any<SearchItem>());

        // Cycle ID should NOT have changed (the new item hasn't been searched yet)
        var instanceConfig = await _fixture.DataContext.SeekerInstanceConfigs
            .FirstAsync(s => s.ArrInstanceId == radarrInstance.Id);
        Assert.Equal(currentCycleId, instanceConfig.CurrentCycleId);
    }

    #endregion

    #region Post-Release Grace Period Tests

    [Fact]
    public async Task ExecuteAsync_Radarr_GracePeriod_ExcludesRecentlyReleasedMovies()
    {
        // Arrange — grace period of 6 hours, one movie released 2 hours ago (within grace), one released 10 hours ago (past grace)
        var config = await _fixture.DataContext.SeekerConfigs.FirstAsync();
        config.SearchEnabled = true;
        config.ProactiveSearchEnabled = true;
        config.PostReleaseGraceHours = 6;
        await _fixture.DataContext.SaveChangesAsync();

        var radarrInstance = TestDataContextFactory.AddRadarrInstance(_fixture.DataContext);

        _fixture.DataContext.SeekerInstanceConfigs.Add(new SeekerInstanceConfig
        {
            ArrInstanceId = radarrInstance.Id,
            ArrInstance = radarrInstance,
            Enabled = true
        });
        await _fixture.DataContext.SaveChangesAsync();

        var mockArrClient = Substitute.For<IArrClient>();

        _fixture.ArrQueueIterator
            .Iterate(mockArrClient, Arg.Any<ArrInstance>(), Arg.Any<Func<IReadOnlyList<QueueRecord>, Task>>())
            .Returns(ci => ci.ArgAt<Func<IReadOnlyList<QueueRecord>, Task>>(2)([]));

        var now = _fixture.TimeProvider.GetUtcNow().UtcDateTime;
        _radarrClient
            .GetAllMoviesAsync(radarrInstance)
            .Returns(
            [
                new SearchableMovie { Id = 1, Title = "Recent Movie", Status = "released", Monitored = true, Tags = [], DigitalRelease = now.AddHours(-2) },
                new SearchableMovie { Id = 2, Title = "Old Movie", Status = "released", Monitored = true, Tags = [], DigitalRelease = now.AddHours(-10) }
            ]);

        HashSet<SearchItem>? capturedSearchItems = null;
        mockArrClient
            .SearchItemAsync(radarrInstance, Arg.Any<SearchItem>())
            .Returns(ci =>
            {
                capturedSearchItems = [ci.ArgAt<SearchItem>(1)];
                return 100L;
            });

        _fixture.ArrClientFactory
            .GetClient(InstanceType.Radarr, Arg.Any<float>())
            .Returns(mockArrClient);

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert — only movie 2 (past grace) should be searched
        await mockArrClient.Received(1)
            .SearchItemAsync(radarrInstance, Arg.Any<SearchItem>());

        Assert.NotNull(capturedSearchItems);
        Assert.Single(capturedSearchItems);
        Assert.Contains(capturedSearchItems, item => item.Id == 2);
        Assert.DoesNotContain(capturedSearchItems, item => item.Id == 1);
    }

    [Fact]
    public async Task ExecuteAsync_Radarr_GracePeriodZero_DoesNotFilterMovies()
    {
        // Arrange — grace period of 0 (disabled)
        var config = await _fixture.DataContext.SeekerConfigs.FirstAsync();
        config.SearchEnabled = true;
        config.ProactiveSearchEnabled = true;
        config.PostReleaseGraceHours = 0;
        await _fixture.DataContext.SaveChangesAsync();

        var radarrInstance = TestDataContextFactory.AddRadarrInstance(_fixture.DataContext);

        _fixture.DataContext.SeekerInstanceConfigs.Add(new SeekerInstanceConfig
        {
            ArrInstanceId = radarrInstance.Id,
            ArrInstance = radarrInstance,
            Enabled = true
        });
        await _fixture.DataContext.SaveChangesAsync();

        var mockArrClient = Substitute.For<IArrClient>();

        _fixture.ArrQueueIterator
            .Iterate(mockArrClient, Arg.Any<ArrInstance>(), Arg.Any<Func<IReadOnlyList<QueueRecord>, Task>>())
            .Returns(ci => ci.ArgAt<Func<IReadOnlyList<QueueRecord>, Task>>(2)([]));

        var now = _fixture.TimeProvider.GetUtcNow().UtcDateTime;
        _radarrClient
            .GetAllMoviesAsync(radarrInstance)
            .Returns(
            [
                new SearchableMovie { Id = 1, Title = "Just Released", Status = "released", Monitored = true, Tags = [], DigitalRelease = now.AddMinutes(-5) }
            ]);

        mockArrClient
            .SearchItemAsync(radarrInstance, Arg.Any<SearchItem>())
            .Returns(100L);

        _fixture.ArrClientFactory
            .GetClient(InstanceType.Radarr, Arg.Any<float>())
            .Returns(mockArrClient);

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert — movie should be searched (grace period disabled)
        await mockArrClient.Received(1)
            .SearchItemAsync(radarrInstance, Arg.Any<SearchItem>());
    }

    [Fact]
    public async Task ExecuteAsync_Radarr_GracePeriod_NoReleaseDates_TreatsAsReleased()
    {
        // Arrange — movie with no release date info should not be filtered by grace period
        var config = await _fixture.DataContext.SeekerConfigs.FirstAsync();
        config.SearchEnabled = true;
        config.ProactiveSearchEnabled = true;
        config.PostReleaseGraceHours = 6;
        await _fixture.DataContext.SaveChangesAsync();

        var radarrInstance = TestDataContextFactory.AddRadarrInstance(_fixture.DataContext);

        _fixture.DataContext.SeekerInstanceConfigs.Add(new SeekerInstanceConfig
        {
            ArrInstanceId = radarrInstance.Id,
            ArrInstance = radarrInstance,
            Enabled = true
        });
        await _fixture.DataContext.SaveChangesAsync();

        var mockArrClient = Substitute.For<IArrClient>();

        _fixture.ArrQueueIterator
            .Iterate(mockArrClient, Arg.Any<ArrInstance>(), Arg.Any<Func<IReadOnlyList<QueueRecord>, Task>>())
            .Returns(ci => ci.ArgAt<Func<IReadOnlyList<QueueRecord>, Task>>(2)([]));

        _radarrClient
            .GetAllMoviesAsync(radarrInstance)
            .Returns(
            [
                new SearchableMovie { Id = 1, Title = "No Dates Movie", Status = "released", Monitored = true, Tags = [] }
            ]);

        mockArrClient
            .SearchItemAsync(radarrInstance, Arg.Any<SearchItem>())
            .Returns(100L);

        _fixture.ArrClientFactory
            .GetClient(InstanceType.Radarr, Arg.Any<float>())
            .Returns(mockArrClient);

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert — movie should be searched (no dates = treated as released)
        await mockArrClient.Received(1)
            .SearchItemAsync(radarrInstance, Arg.Any<SearchItem>());
    }

    [Fact]
    public async Task ExecuteAsync_Sonarr_GracePeriod_ExcludesRecentlyAiredEpisodes()
    {
        // Arrange — grace period of 6 hours, one episode aired 2 hours ago (within grace), one aired 10 hours ago (past grace)
        var config = await _fixture.DataContext.SeekerConfigs.FirstAsync();
        config.SearchEnabled = true;
        config.ProactiveSearchEnabled = true;
        config.PostReleaseGraceHours = 6;
        await _fixture.DataContext.SaveChangesAsync();

        var sonarrInstance = TestDataContextFactory.AddSonarrInstance(_fixture.DataContext);

        _fixture.DataContext.SeekerInstanceConfigs.Add(new SeekerInstanceConfig
        {
            ArrInstanceId = sonarrInstance.Id,
            ArrInstance = sonarrInstance,
            Enabled = true
        });
        await _fixture.DataContext.SaveChangesAsync();

        var mockArrClient = Substitute.For<IArrClient>();

        _fixture.ArrQueueIterator
            .Iterate(mockArrClient, Arg.Any<ArrInstance>(), Arg.Any<Func<IReadOnlyList<QueueRecord>, Task>>())
            .Returns(ci => ci.ArgAt<Func<IReadOnlyList<QueueRecord>, Task>>(2)([]));

        _sonarrClient
            .GetAllSeriesAsync(Arg.Any<ArrInstance>())
            .Returns(
            [
                new SearchableSeries { Id = 10, Title = "Test Series", Status = "continuing", Monitored = true, Tags = [], Statistics = new SeriesStatistics { EpisodeCount = 10, EpisodeFileCount = 8 } }
            ]);

        var now = _fixture.TimeProvider.GetUtcNow().UtcDateTime;
        _sonarrClient
            .GetEpisodesAsync(Arg.Any<ArrInstance>(), 10)
            .Returns(
            [
                new SearchableEpisode { Id = 100, SeasonNumber = 1, EpisodeNumber = 1, Monitored = true, AirDateUtc = now.AddHours(-2), HasFile = false },
                new SearchableEpisode { Id = 101, SeasonNumber = 2, EpisodeNumber = 1, Monitored = true, AirDateUtc = now.AddHours(-10), HasFile = false }
            ]);

        SeriesSearchItem? capturedSearchItem = null;
        mockArrClient
            .SearchItemAsync(Arg.Any<ArrInstance>(), Arg.Any<SearchItem>())
            .Returns(ci =>
            {
                capturedSearchItem = ci.ArgAt<SearchItem>(1) as SeriesSearchItem;
                return 100L;
            });

        _fixture.ArrClientFactory
            .GetClient(InstanceType.Sonarr, Arg.Any<float>())
            .Returns(mockArrClient);

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert — only season 2 should be searched (season 1's episode is within grace period)
        await mockArrClient.Received(1)
            .SearchItemAsync(Arg.Any<ArrInstance>(), Arg.Any<SearchItem>());

        Assert.NotNull(capturedSearchItem);
        Assert.Equal(2, capturedSearchItem.Id); // Season 2
    }

    [Fact]
    public async Task ExecuteAsync_Radarr_GracePeriod_UsesReleaseDateFallbackOrder()
    {
        // Arrange — movie with only PhysicalRelease (no DigitalRelease), within grace period
        var config = await _fixture.DataContext.SeekerConfigs.FirstAsync();
        config.SearchEnabled = true;
        config.ProactiveSearchEnabled = true;
        config.PostReleaseGraceHours = 6;
        await _fixture.DataContext.SaveChangesAsync();

        var radarrInstance = TestDataContextFactory.AddRadarrInstance(_fixture.DataContext);

        _fixture.DataContext.SeekerInstanceConfigs.Add(new SeekerInstanceConfig
        {
            ArrInstanceId = radarrInstance.Id,
            ArrInstance = radarrInstance,
            Enabled = true
        });
        await _fixture.DataContext.SaveChangesAsync();

        var mockArrClient = Substitute.For<IArrClient>();

        _fixture.ArrQueueIterator
            .Iterate(mockArrClient, Arg.Any<ArrInstance>(), Arg.Any<Func<IReadOnlyList<QueueRecord>, Task>>())
            .Returns(ci => ci.ArgAt<Func<IReadOnlyList<QueueRecord>, Task>>(2)([]));

        var now = _fixture.TimeProvider.GetUtcNow().UtcDateTime;
        _radarrClient
            .GetAllMoviesAsync(radarrInstance)
            .Returns(
            [
                // DigitalRelease is null, PhysicalRelease is 2h ago (within grace)
                new SearchableMovie { Id = 1, Title = "Physical Only", Status = "released", Monitored = true, Tags = [], PhysicalRelease = now.AddHours(-2) },
                // DigitalRelease is 10h ago (past grace), PhysicalRelease is 2h ago — DigitalRelease takes precedence
                new SearchableMovie { Id = 2, Title = "Digital First", Status = "released", Monitored = true, Tags = [], DigitalRelease = now.AddHours(-10), PhysicalRelease = now.AddHours(-2) }
            ]);

        HashSet<SearchItem>? capturedSearchItems = null;
        mockArrClient
            .SearchItemAsync(radarrInstance, Arg.Any<SearchItem>())
            .Returns(ci =>
            {
                capturedSearchItems = [ci.ArgAt<SearchItem>(1)];
                return 100L;
            });

        _fixture.ArrClientFactory
            .GetClient(InstanceType.Radarr, Arg.Any<float>())
            .Returns(mockArrClient);

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert — movie 1 excluded (PhysicalRelease within grace), movie 2 included (DigitalRelease past grace)
        await mockArrClient.Received(1)
            .SearchItemAsync(radarrInstance, Arg.Any<SearchItem>());

        Assert.NotNull(capturedSearchItems);
        Assert.Single(capturedSearchItems);
        Assert.Contains(capturedSearchItems, item => item.Id == 2);
        Assert.DoesNotContain(capturedSearchItems, item => item.Id == 1);
    }

    #endregion
}
