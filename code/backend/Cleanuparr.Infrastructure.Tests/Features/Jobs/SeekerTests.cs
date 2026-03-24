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
using Data.Models.Arr;
using Cleanuparr.Infrastructure.Hubs;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using SeekerJob = Cleanuparr.Infrastructure.Features.Jobs.Seeker;

namespace Cleanuparr.Infrastructure.Tests.Features.Jobs;

[Collection(JobHandlerCollection.Name)]
public class SeekerTests : IDisposable
{
    private readonly JobHandlerFixture _fixture;
    private readonly Mock<ILogger<SeekerJob>> _logger;
    private readonly Mock<IRadarrClient> _radarrClient;
    private readonly Mock<ISonarrClient> _sonarrClient;
    private readonly Mock<IDryRunInterceptor> _dryRunInterceptor;
    private readonly Mock<IHostingEnvironment> _hostingEnvironment;
    private readonly Mock<IHubContext<AppHub>> _hubContext;

    public SeekerTests(JobHandlerFixture fixture)
    {
        _fixture = fixture;
        _fixture.RecreateDataContext();
        _fixture.ResetMocks();
        _logger = new Mock<ILogger<SeekerJob>>();
        _radarrClient = new Mock<IRadarrClient>();
        _sonarrClient = new Mock<ISonarrClient>();
        _dryRunInterceptor = new Mock<IDryRunInterceptor>();
        _hostingEnvironment = new Mock<IHostingEnvironment>();
        _hubContext = new Mock<IHubContext<AppHub>>();

        // Default: hub context setup
        var mockClients = new Mock<IHubClients>();
        var mockClientProxy = new Mock<IClientProxy>();
        mockClients.Setup(c => c.All).Returns(mockClientProxy.Object);
        _hubContext.Setup(h => h.Clients).Returns(mockClients.Object);

        // Default: development mode (skips jitter)
        _hostingEnvironment.Setup(x => x.EnvironmentName).Returns("Development");

        // Default: dry run disabled
        _dryRunInterceptor.Setup(x => x.IsDryRunEnabled()).ReturnsAsync(false);

        // Default: PublishSearchTriggered returns a Guid
        _fixture.EventPublisher
            .Setup(x => x.PublishSearchTriggered(
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<SeekerSearchType>(),
                It.IsAny<Guid?>()))
            .ReturnsAsync(Guid.NewGuid());
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    private SeekerJob CreateSut()
    {
        return new SeekerJob(
            _logger.Object,
            _fixture.DataContext,
            _radarrClient.Object,
            _sonarrClient.Object,
            _fixture.ArrClientFactory.Object,
            _fixture.ArrQueueIterator.Object,
            _fixture.EventPublisher.Object,
            _dryRunInterceptor.Object,
            _hostingEnvironment.Object,
            _fixture.TimeProvider,
            _hubContext.Object
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
        _fixture.ArrClientFactory.Verify(
            x => x.GetClient(It.IsAny<InstanceType>(), It.IsAny<float>()),
            Times.Never);
        _fixture.EventPublisher.Verify(
            x => x.PublishSearchTriggered(
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<SeekerSearchType>(),
                It.IsAny<Guid?>()),
            Times.Never);
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
        _fixture.ArrClientFactory.Verify(
            x => x.GetClient(It.IsAny<InstanceType>(), It.IsAny<float>()),
            Times.Never);
        _fixture.EventPublisher.Verify(
            x => x.PublishSearchTriggered(
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<SeekerSearchType>(),
                It.IsAny<Guid?>()),
            Times.Never);
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

        var mockArrClient = new Mock<IArrClient>();
        mockArrClient
            .Setup(x => x.SearchItemsAsync(It.IsAny<ArrInstance>(), It.IsAny<HashSet<SearchItem>>()))
            .ReturnsAsync([100L]);

        _fixture.ArrClientFactory
            .Setup(x => x.GetClient(InstanceType.Radarr, It.IsAny<float>()))
            .Returns(mockArrClient.Object);

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert — search was triggered for the replacement item
        mockArrClient.Verify(
            x => x.SearchItemsAsync(radarrInstance, It.IsAny<HashSet<SearchItem>>()),
            Times.Once);

        _fixture.EventPublisher.Verify(
            x => x.PublishSearchTriggered(
                radarrInstance.Name,
                1,
                It.Is<IEnumerable<string>>(items => items.Contains("Test Movie")),
                SeekerSearchType.Replacement,
                It.IsAny<Guid?>()),
            Times.Once);

        // Replacement item should be removed from the queue
        var remaining = await _fixture.DataContext.SearchQueue.CountAsync();
        Assert.Equal(0, remaining);
    }

    [Fact]
    public async Task ExecuteAsync_WhenDryRunEnabled_DoesNotRemoveFromSearchQueue()
    {
        // Arrange
        _dryRunInterceptor.Setup(x => x.IsDryRunEnabled()).ReturnsAsync(true);

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

        var mockArrClient = new Mock<IArrClient>();
        mockArrClient
            .Setup(x => x.SearchItemsAsync(It.IsAny<ArrInstance>(), It.IsAny<HashSet<SearchItem>>()))
            .ReturnsAsync([100L]);

        _fixture.ArrClientFactory
            .Setup(x => x.GetClient(InstanceType.Radarr, It.IsAny<float>()))
            .Returns(mockArrClient.Object);

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert — search was triggered but item stays in queue
        mockArrClient.Verify(
            x => x.SearchItemsAsync(radarrInstance, It.IsAny<HashSet<SearchItem>>()),
            Times.Once);

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

        var mockArrClient = new Mock<IArrClient>();

        // Return 2 queue items with SizeLeft > 0 (actively downloading), which meets the limit
        QueueRecord[] activeDownloads =
        [
            new() { Id = 1, Title = "Download 1", DownloadId = "hash1", Protocol = "torrent", SizeLeft = 1000, MovieId = 10, TrackedDownloadState = "downloading" },
            new() { Id = 2, Title = "Download 2", DownloadId = "hash2", Protocol = "torrent", SizeLeft = 2000, MovieId = 20, TrackedDownloadState = "downloading" }
        ];
        _fixture.ArrQueueIterator
            .Setup(x => x.Iterate(mockArrClient.Object, It.IsAny<ArrInstance>(), It.IsAny<Func<IReadOnlyList<QueueRecord>, Task>>()))
            .Returns<IArrClient, ArrInstance, Func<IReadOnlyList<QueueRecord>, Task>>((_, _, action) => action(activeDownloads));

        _fixture.ArrClientFactory
            .Setup(x => x.GetClient(InstanceType.Radarr, It.IsAny<float>()))
            .Returns(mockArrClient.Object);

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert — no search triggered because active downloads >= limit
        mockArrClient.Verify(
            x => x.SearchItemsAsync(It.IsAny<ArrInstance>(), It.IsAny<HashSet<SearchItem>>()),
            Times.Never);

        _fixture.EventPublisher.Verify(
            x => x.PublishSearchTriggered(
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<IEnumerable<string>>(),
                SeekerSearchType.Proactive,
                It.IsAny<Guid?>()),
            Times.Never);
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

        var mockArrClient = new Mock<IArrClient>();

        // Movie 2 is already in the download queue
        QueueRecord[] queuedRecords =
        [
            new() { Id = 1, Title = "Movie 2 Download", DownloadId = "hash1", Protocol = "torrent", SizeLeft = 1000, MovieId = 2, TrackedDownloadState = "downloading" }
        ];
        _fixture.ArrQueueIterator
            .Setup(x => x.Iterate(mockArrClient.Object, It.IsAny<ArrInstance>(), It.IsAny<Func<IReadOnlyList<QueueRecord>, Task>>()))
            .Returns<IArrClient, ArrInstance, Func<IReadOnlyList<QueueRecord>, Task>>((_, _, action) => action(queuedRecords));

        _radarrClient
            .Setup(x => x.GetAllMoviesAsync(radarrInstance))
            .ReturnsAsync(
            [
                new SearchableMovie { Id = 1, Title = "Movie 1", Status = "released", Monitored = true, Tags = [] },
                new SearchableMovie { Id = 2, Title = "Movie 2", Status = "released", Monitored = true, Tags = [] },
                new SearchableMovie { Id = 3, Title = "Movie 3", Status = "released", Monitored = true, Tags = [] }
            ]);

        HashSet<SearchItem>? capturedSearchItems = null;
        mockArrClient
            .Setup(x => x.SearchItemsAsync(radarrInstance, It.IsAny<HashSet<SearchItem>>()))
            .Callback<ArrInstance, HashSet<SearchItem>>((_, items) => capturedSearchItems = items)
            .ReturnsAsync([100L]);

        _fixture.ArrClientFactory
            .Setup(x => x.GetClient(InstanceType.Radarr, It.IsAny<float>()))
            .Returns(mockArrClient.Object);

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert — search was triggered, but NOT for movie 2
        mockArrClient.Verify(
            x => x.SearchItemsAsync(radarrInstance, It.IsAny<HashSet<SearchItem>>()),
            Times.Once);

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

        var mockArrClient = new Mock<IArrClient>();

        // Movie 1 is in queue but with importFailed state — should NOT be excluded
        QueueRecord[] queuedRecords =
        [
            new() { Id = 1, Title = "Movie 1 Download", DownloadId = "hash1", Protocol = "torrent", SizeLeft = 0, MovieId = 1, TrackedDownloadState = "importFailed" }
        ];
        _fixture.ArrQueueIterator
            .Setup(x => x.Iterate(mockArrClient.Object, It.IsAny<ArrInstance>(), It.IsAny<Func<IReadOnlyList<QueueRecord>, Task>>()))
            .Returns<IArrClient, ArrInstance, Func<IReadOnlyList<QueueRecord>, Task>>((_, _, action) => action(queuedRecords));

        _radarrClient
            .Setup(x => x.GetAllMoviesAsync(radarrInstance))
            .ReturnsAsync(
            [
                new SearchableMovie { Id = 1, Title = "Movie 1", Status = "released", Monitored = true, Tags = [] }
            ]);

        mockArrClient
            .Setup(x => x.SearchItemsAsync(radarrInstance, It.IsAny<HashSet<SearchItem>>()))
            .ReturnsAsync([100L]);

        _fixture.ArrClientFactory
            .Setup(x => x.GetClient(InstanceType.Radarr, It.IsAny<float>()))
            .Returns(mockArrClient.Object);

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert — search was triggered for movie 1 (importFailed does not exclude)
        mockArrClient.Verify(
            x => x.SearchItemsAsync(radarrInstance, It.IsAny<HashSet<SearchItem>>()),
            Times.Once);
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

        var mockArrClient = new Mock<IArrClient>();

        // Season 1 of series 10 is in the queue
        QueueRecord[] queuedRecords =
        [
            new() { Id = 1, Title = "Series Episode", DownloadId = "hash1", Protocol = "torrent", SizeLeft = 1000, SeriesId = 10, SeasonNumber = 1, TrackedDownloadState = "downloading" }
        ];
        _fixture.ArrQueueIterator
            .Setup(x => x.Iterate(mockArrClient.Object, It.IsAny<ArrInstance>(), It.IsAny<Func<IReadOnlyList<QueueRecord>, Task>>()))
            .Returns<IArrClient, ArrInstance, Func<IReadOnlyList<QueueRecord>, Task>>((_, _, action) => action(queuedRecords));

        _sonarrClient
            .Setup(x => x.GetAllSeriesAsync(It.IsAny<ArrInstance>()))
            .ReturnsAsync(
            [
                new SearchableSeries { Id = 10, Title = "Test Series", Status = "continuing", Monitored = true, Tags = [], Statistics = new SeriesStatistics { EpisodeCount = 20, EpisodeFileCount = 10 } }
            ]);

        // Use dates relative to FakeTimeProvider (defaults to Jan 1, 2000)
        var pastDate = _fixture.TimeProvider.GetUtcNow().UtcDateTime.AddDays(-30);
        _sonarrClient
            .Setup(x => x.GetEpisodesAsync(It.IsAny<ArrInstance>(), 10))
            .ReturnsAsync(
            [
                new SearchableEpisode { Id = 100, SeasonNumber = 1, EpisodeNumber = 1, Monitored = true, AirDateUtc = pastDate, HasFile = false },
                new SearchableEpisode { Id = 101, SeasonNumber = 2, EpisodeNumber = 1, Monitored = true, AirDateUtc = pastDate, HasFile = false }
            ]);

        SeriesSearchItem? capturedSearchItem = null;
        mockArrClient
            .Setup(x => x.SearchItemsAsync(It.IsAny<ArrInstance>(), It.IsAny<HashSet<SearchItem>>()))
            .Callback<ArrInstance, HashSet<SearchItem>>((_, items) => capturedSearchItem = items.OfType<SeriesSearchItem>().FirstOrDefault())
            .ReturnsAsync([100L]);

        _fixture.ArrClientFactory
            .Setup(x => x.GetClient(InstanceType.Sonarr, It.IsAny<float>()))
            .Returns(mockArrClient.Object);

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert — season 2 was searched (season 1 excluded because it's in queue)
        mockArrClient.Verify(
            x => x.SearchItemsAsync(It.IsAny<ArrInstance>(), It.IsAny<HashSet<SearchItem>>()),
            Times.Once);

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

        var mockArrClient = new Mock<IArrClient>();

        // Queue fetch fails
        _fixture.ArrQueueIterator
            .Setup(x => x.Iterate(mockArrClient.Object, It.IsAny<ArrInstance>(), It.IsAny<Func<IReadOnlyList<QueueRecord>, Task>>()))
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        _radarrClient
            .Setup(x => x.GetAllMoviesAsync(radarrInstance))
            .ReturnsAsync(
            [
                new SearchableMovie { Id = 1, Title = "Movie 1", Status = "released", Monitored = true, Tags = [] }
            ]);

        mockArrClient
            .Setup(x => x.SearchItemsAsync(radarrInstance, It.IsAny<HashSet<SearchItem>>()))
            .ReturnsAsync([100L]);

        _fixture.ArrClientFactory
            .Setup(x => x.GetClient(InstanceType.Radarr, It.IsAny<float>()))
            .Returns(mockArrClient.Object);

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert — search still proceeded despite queue fetch failure
        mockArrClient.Verify(
            x => x.SearchItemsAsync(radarrInstance, It.IsAny<HashSet<SearchItem>>()),
            Times.Once);
    }

    #endregion

    #region Radarr Proactive Search Filters

    [Fact]
    public async Task ExecuteAsync_Radarr_MonitoredOnlyTrue_ExcludesUnmonitoredMovies()
    {
        // Arrange — MonitoredOnly is true by default in seed data
        var config = await _fixture.DataContext.SeekerConfigs.FirstAsync();
        config.SearchEnabled = true;
        config.ProactiveSearchEnabled = true;
        config.MonitoredOnly = true;
        await _fixture.DataContext.SaveChangesAsync();

        var radarrInstance = TestDataContextFactory.AddRadarrInstance(_fixture.DataContext);

        _fixture.DataContext.SeekerInstanceConfigs.Add(new SeekerInstanceConfig
        {
            ArrInstanceId = radarrInstance.Id,
            ArrInstance = radarrInstance,
            Enabled = true
        });
        await _fixture.DataContext.SaveChangesAsync();

        var mockArrClient = new Mock<IArrClient>();

        _fixture.ArrQueueIterator
            .Setup(x => x.Iterate(mockArrClient.Object, It.IsAny<ArrInstance>(), It.IsAny<Func<IReadOnlyList<QueueRecord>, Task>>()))
            .Returns(Task.CompletedTask);

        _radarrClient
            .Setup(x => x.GetAllMoviesAsync(radarrInstance))
            .ReturnsAsync(
            [
                new SearchableMovie { Id = 1, Title = "Monitored Movie", Status = "released", Monitored = true, Tags = [] },
                new SearchableMovie { Id = 2, Title = "Unmonitored Movie", Status = "released", Monitored = false, Tags = [] }
            ]);

        HashSet<SearchItem>? capturedSearchItems = null;
        mockArrClient
            .Setup(x => x.SearchItemsAsync(radarrInstance, It.IsAny<HashSet<SearchItem>>()))
            .Callback<ArrInstance, HashSet<SearchItem>>((_, items) => capturedSearchItems = items)
            .ReturnsAsync([100L]);

        _fixture.ArrClientFactory
            .Setup(x => x.GetClient(InstanceType.Radarr, It.IsAny<float>()))
            .Returns(mockArrClient.Object);

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
        config.MonitoredOnly = false;
        await _fixture.DataContext.SaveChangesAsync();

        var radarrInstance = TestDataContextFactory.AddRadarrInstance(_fixture.DataContext);

        _fixture.DataContext.SeekerInstanceConfigs.Add(new SeekerInstanceConfig
        {
            ArrInstanceId = radarrInstance.Id,
            ArrInstance = radarrInstance,
            Enabled = true,
            SkipTags = ["no-search"]
        });
        await _fixture.DataContext.SaveChangesAsync();

        var mockArrClient = new Mock<IArrClient>();

        _fixture.ArrQueueIterator
            .Setup(x => x.Iterate(mockArrClient.Object, It.IsAny<ArrInstance>(), It.IsAny<Func<IReadOnlyList<QueueRecord>, Task>>()))
            .Returns(Task.CompletedTask);

        _radarrClient
            .Setup(x => x.GetAllMoviesAsync(radarrInstance))
            .ReturnsAsync(
            [
                new SearchableMovie { Id = 1, Title = "Normal Movie", Status = "released", Monitored = true, Tags = ["movies"] },
                new SearchableMovie { Id = 2, Title = "Skipped Movie", Status = "released", Monitored = true, Tags = ["no-search", "movies"] }
            ]);

        HashSet<SearchItem>? capturedSearchItems = null;
        mockArrClient
            .Setup(x => x.SearchItemsAsync(radarrInstance, It.IsAny<HashSet<SearchItem>>()))
            .Callback<ArrInstance, HashSet<SearchItem>>((_, items) => capturedSearchItems = items)
            .ReturnsAsync([100L]);

        _fixture.ArrClientFactory
            .Setup(x => x.GetClient(InstanceType.Radarr, It.IsAny<float>()))
            .Returns(mockArrClient.Object);

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
        config.MonitoredOnly = false;
        config.UseCutoff = true;
        await _fixture.DataContext.SaveChangesAsync();

        var radarrInstance = TestDataContextFactory.AddRadarrInstance(_fixture.DataContext);

        _fixture.DataContext.SeekerInstanceConfigs.Add(new SeekerInstanceConfig
        {
            ArrInstanceId = radarrInstance.Id,
            ArrInstance = radarrInstance,
            Enabled = true
        });
        await _fixture.DataContext.SaveChangesAsync();

        var mockArrClient = new Mock<IArrClient>();

        _fixture.ArrQueueIterator
            .Setup(x => x.Iterate(mockArrClient.Object, It.IsAny<ArrInstance>(), It.IsAny<Func<IReadOnlyList<QueueRecord>, Task>>()))
            .Returns(Task.CompletedTask);

        _radarrClient
            .Setup(x => x.GetAllMoviesAsync(radarrInstance))
            .ReturnsAsync(
            [
                new SearchableMovie { Id = 1, Title = "Missing Movie", Status = "released", Monitored = true, HasFile = false, Tags = [] },
                new SearchableMovie { Id = 2, Title = "Cutoff Met", Status = "released", Monitored = true, HasFile = true, MovieFile = new MovieFileInfo { Id = 200, QualityCutoffNotMet = false }, Tags = [] },
                new SearchableMovie { Id = 3, Title = "Cutoff Not Met", Status = "released", Monitored = true, HasFile = true, MovieFile = new MovieFileInfo { Id = 300, QualityCutoffNotMet = true }, Tags = [] }
            ]);

        HashSet<SearchItem>? capturedSearchItems = null;
        mockArrClient
            .Setup(x => x.SearchItemsAsync(radarrInstance, It.IsAny<HashSet<SearchItem>>()))
            .Callback<ArrInstance, HashSet<SearchItem>>((_, items) => capturedSearchItems = items)
            .ReturnsAsync([100L]);

        _fixture.ArrClientFactory
            .Setup(x => x.GetClient(InstanceType.Radarr, It.IsAny<float>()))
            .Returns(mockArrClient.Object);

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
        config.MonitoredOnly = false;
        await _fixture.DataContext.SaveChangesAsync();

        var radarrInstance = TestDataContextFactory.AddRadarrInstance(_fixture.DataContext);
        var currentCycleId = Guid.NewGuid();
        var now = _fixture.TimeProvider.GetUtcNow().UtcDateTime;

        _fixture.DataContext.SeekerInstanceConfigs.Add(new SeekerInstanceConfig
        {
            ArrInstanceId = radarrInstance.Id,
            ArrInstance = radarrInstance,
            Enabled = true,
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

        var mockArrClient = new Mock<IArrClient>();

        _fixture.ArrQueueIterator
            .Setup(x => x.Iterate(mockArrClient.Object, It.IsAny<ArrInstance>(), It.IsAny<Func<IReadOnlyList<QueueRecord>, Task>>()))
            .Returns(Task.CompletedTask);

        _radarrClient
            .Setup(x => x.GetAllMoviesAsync(radarrInstance))
            .ReturnsAsync(
            [
                new SearchableMovie { Id = 1, Title = "Movie 1", Status = "released", Monitored = true, Tags = [] },
                new SearchableMovie { Id = 2, Title = "Movie 2", Status = "released", Monitored = true, Tags = [] }
            ]);

        mockArrClient
            .Setup(x => x.SearchItemsAsync(radarrInstance, It.IsAny<HashSet<SearchItem>>()))
            .ReturnsAsync([100L]);

        _fixture.ArrClientFactory
            .Setup(x => x.GetClient(InstanceType.Radarr, It.IsAny<float>()))
            .Returns(mockArrClient.Object);

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert — search was triggered (new cycle started) and the CycleId changed
        mockArrClient.Verify(
            x => x.SearchItemsAsync(radarrInstance, It.IsAny<HashSet<SearchItem>>()),
            Times.Once);

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
        config.MonitoredOnly = false;
        await _fixture.DataContext.SaveChangesAsync();

        var radarrInstance1 = TestDataContextFactory.AddRadarrInstance(_fixture.DataContext, "http://radarr1:7878");
        var radarrInstance2 = TestDataContextFactory.AddRadarrInstance(_fixture.DataContext, "http://radarr2:7878");

        // Instance 1 was processed recently, instance 2 was processed long ago
        _fixture.DataContext.SeekerInstanceConfigs.Add(new SeekerInstanceConfig
        {
            ArrInstanceId = radarrInstance1.Id,
            ArrInstance = radarrInstance1,
            Enabled = true,
            LastProcessedAt = DateTime.UtcNow
        });
        _fixture.DataContext.SeekerInstanceConfigs.Add(new SeekerInstanceConfig
        {
            ArrInstanceId = radarrInstance2.Id,
            ArrInstance = radarrInstance2,
            Enabled = true,
            LastProcessedAt = DateTime.UtcNow.AddHours(-24)
        });
        await _fixture.DataContext.SaveChangesAsync();

        var mockArrClient = new Mock<IArrClient>();

        _fixture.ArrQueueIterator
            .Setup(x => x.Iterate(mockArrClient.Object, It.IsAny<ArrInstance>(), It.IsAny<Func<IReadOnlyList<QueueRecord>, Task>>()))
            .Returns(Task.CompletedTask);

        // Return movies for both instances — only instance 2 should be called
        _radarrClient
            .Setup(x => x.GetAllMoviesAsync(It.Is<ArrInstance>(a => a.Id == radarrInstance2.Id)))
            .ReturnsAsync([new SearchableMovie { Id = 1, Title = "Movie 1", Status = "released", Monitored = true, Tags = [] }]);

        mockArrClient
            .Setup(x => x.SearchItemsAsync(It.IsAny<ArrInstance>(), It.IsAny<HashSet<SearchItem>>()))
            .ReturnsAsync([100L]);

        _fixture.ArrClientFactory
            .Setup(x => x.GetClient(InstanceType.Radarr, It.IsAny<float>()))
            .Returns(mockArrClient.Object);

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert — instance 2 (oldest) was processed, verified by GetAllMoviesAsync being called for it
        _radarrClient.Verify(
            x => x.GetAllMoviesAsync(It.Is<ArrInstance>(a => a.Id == radarrInstance2.Id)),
            Times.Once);
        _radarrClient.Verify(
            x => x.GetAllMoviesAsync(It.Is<ArrInstance>(a => a.Id == radarrInstance1.Id)),
            Times.Never);
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
        _fixture.EventPublisher.Verify(
            x => x.PublishSearchTriggered(
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<SeekerSearchType>(),
                It.IsAny<Guid?>()),
            Times.Never);
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
        config.MonitoredOnly = false;
        await _fixture.DataContext.SaveChangesAsync();

        var radarrInstance = TestDataContextFactory.AddRadarrInstance(_fixture.DataContext);
        var currentCycleId = Guid.NewGuid();
        var now = _fixture.TimeProvider.GetUtcNow().UtcDateTime;

        _fixture.DataContext.SeekerInstanceConfigs.Add(new SeekerInstanceConfig
        {
            ArrInstanceId = radarrInstance.Id,
            ArrInstance = radarrInstance,
            Enabled = true,
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

        var mockArrClient = new Mock<IArrClient>();

        _fixture.ArrQueueIterator
            .Setup(x => x.Iterate(mockArrClient.Object, It.IsAny<ArrInstance>(), It.IsAny<Func<IReadOnlyList<QueueRecord>, Task>>()))
            .Returns(Task.CompletedTask);

        _radarrClient
            .Setup(x => x.GetAllMoviesAsync(radarrInstance))
            .ReturnsAsync(
            [
                new SearchableMovie { Id = 1, Title = "Movie 1", Status = "released", Monitored = true, Tags = [] },
                new SearchableMovie { Id = 2, Title = "Movie 2", Status = "released", Monitored = true, Tags = [] }
            ]);

        _fixture.ArrClientFactory
            .Setup(x => x.GetClient(InstanceType.Radarr, It.IsAny<float>()))
            .Returns(mockArrClient.Object);

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert — no search triggered, cycle not reset
        mockArrClient.Verify(
            x => x.SearchItemsAsync(radarrInstance, It.IsAny<HashSet<SearchItem>>()),
            Times.Never);

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
        config.MonitoredOnly = false;
        await _fixture.DataContext.SaveChangesAsync();

        var radarrInstance = TestDataContextFactory.AddRadarrInstance(_fixture.DataContext);
        var currentCycleId = Guid.NewGuid();
        var now = _fixture.TimeProvider.GetUtcNow().UtcDateTime;

        _fixture.DataContext.SeekerInstanceConfigs.Add(new SeekerInstanceConfig
        {
            ArrInstanceId = radarrInstance.Id,
            ArrInstance = radarrInstance,
            Enabled = true,
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

        var mockArrClient = new Mock<IArrClient>();

        _fixture.ArrQueueIterator
            .Setup(x => x.Iterate(mockArrClient.Object, It.IsAny<ArrInstance>(), It.IsAny<Func<IReadOnlyList<QueueRecord>, Task>>()))
            .Returns(Task.CompletedTask);

        _radarrClient
            .Setup(x => x.GetAllMoviesAsync(radarrInstance))
            .ReturnsAsync(
            [
                new SearchableMovie { Id = 1, Title = "Movie 1", Status = "released", Monitored = true, Tags = [] },
                new SearchableMovie { Id = 2, Title = "Movie 2", Status = "released", Monitored = true, Tags = [] }
            ]);

        mockArrClient
            .Setup(x => x.SearchItemsAsync(radarrInstance, It.IsAny<HashSet<SearchItem>>()))
            .ReturnsAsync([100L]);

        _fixture.ArrClientFactory
            .Setup(x => x.GetClient(InstanceType.Radarr, It.IsAny<float>()))
            .Returns(mockArrClient.Object);

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert — search was triggered, cycle was reset
        mockArrClient.Verify(
            x => x.SearchItemsAsync(radarrInstance, It.IsAny<HashSet<SearchItem>>()),
            Times.Once);

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
        config.MonitoredOnly = false;
        await _fixture.DataContext.SaveChangesAsync();

        var radarrInstance = TestDataContextFactory.AddRadarrInstance(_fixture.DataContext);
        var currentCycleId = Guid.NewGuid();

        _fixture.DataContext.SeekerInstanceConfigs.Add(new SeekerInstanceConfig
        {
            ArrInstanceId = radarrInstance.Id,
            ArrInstance = radarrInstance,
            Enabled = true,
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

        var mockArrClient = new Mock<IArrClient>();

        _fixture.ArrQueueIterator
            .Setup(x => x.Iterate(mockArrClient.Object, It.IsAny<ArrInstance>(), It.IsAny<Func<IReadOnlyList<QueueRecord>, Task>>()))
            .Returns(Task.CompletedTask);

        _radarrClient
            .Setup(x => x.GetAllMoviesAsync(radarrInstance))
            .ReturnsAsync(
            [
                new SearchableMovie { Id = 1, Title = "Movie 1", Status = "released", Monitored = true, Tags = [] }
            ]);

        mockArrClient
            .Setup(x => x.SearchItemsAsync(radarrInstance, It.IsAny<HashSet<SearchItem>>()))
            .ReturnsAsync([100L]);

        _fixture.ArrClientFactory
            .Setup(x => x.GetClient(InstanceType.Radarr, It.IsAny<float>()))
            .Returns(mockArrClient.Object);

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert — search was triggered (item not in current cycle, so it's selected directly)
        mockArrClient.Verify(
            x => x.SearchItemsAsync(radarrInstance, It.IsAny<HashSet<SearchItem>>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_Sonarr_CycleComplete_WaitsForMinCycleTime()
    {
        // Arrange — all series seasons searched but MinCycleTimeDays not elapsed
        var config = await _fixture.DataContext.SeekerConfigs.FirstAsync();
        config.SearchEnabled = true;
        config.ProactiveSearchEnabled = true;
        config.MonitoredOnly = false;
        await _fixture.DataContext.SaveChangesAsync();

        var sonarrInstance = TestDataContextFactory.AddSonarrInstance(_fixture.DataContext);
        var currentCycleId = Guid.NewGuid();
        var now = _fixture.TimeProvider.GetUtcNow().UtcDateTime;

        _fixture.DataContext.SeekerInstanceConfigs.Add(new SeekerInstanceConfig
        {
            ArrInstanceId = sonarrInstance.Id,
            ArrInstance = sonarrInstance,
            Enabled = true,
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

        var mockArrClient = new Mock<IArrClient>();

        _fixture.ArrQueueIterator
            .Setup(x => x.Iterate(mockArrClient.Object, It.IsAny<ArrInstance>(), It.IsAny<Func<IReadOnlyList<QueueRecord>, Task>>()))
            .Returns(Task.CompletedTask);

        _sonarrClient
            .Setup(x => x.GetAllSeriesAsync(sonarrInstance))
            .ReturnsAsync(
            [
                new SearchableSeries { Id = 10, Title = "Test Series", Status = "continuing", Monitored = true, Tags = [], Statistics = new SeriesStatistics { EpisodeCount = 10, EpisodeFileCount = 5 } }
            ]);

        var pastDate = now.AddDays(-30);
        _sonarrClient
            .Setup(x => x.GetEpisodesAsync(It.IsAny<ArrInstance>(), 10))
            .ReturnsAsync(
            [
                new SearchableEpisode { Id = 100, SeasonNumber = 1, EpisodeNumber = 1, Monitored = true, HasFile = false, AirDateUtc = pastDate }
            ]);

        _fixture.ArrClientFactory
            .Setup(x => x.GetClient(InstanceType.Sonarr, It.IsAny<float>()))
            .Returns(mockArrClient.Object);

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert — no search triggered, cycle not reset
        mockArrClient.Verify(
            x => x.SearchItemsAsync(sonarrInstance, It.IsAny<HashSet<SearchItem>>()),
            Times.Never);

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
        config.MonitoredOnly = false;
        await _fixture.DataContext.SaveChangesAsync();

        var sonarrInstance = TestDataContextFactory.AddSonarrInstance(_fixture.DataContext);
        var currentCycleId = Guid.NewGuid();
        var now = _fixture.TimeProvider.GetUtcNow().UtcDateTime;

        _fixture.DataContext.SeekerInstanceConfigs.Add(new SeekerInstanceConfig
        {
            ArrInstanceId = sonarrInstance.Id,
            ArrInstance = sonarrInstance,
            Enabled = true,
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

        var mockArrClient = new Mock<IArrClient>();

        _fixture.ArrQueueIterator
            .Setup(x => x.Iterate(mockArrClient.Object, It.IsAny<ArrInstance>(), It.IsAny<Func<IReadOnlyList<QueueRecord>, Task>>()))
            .Returns(Task.CompletedTask);

        _sonarrClient
            .Setup(x => x.GetAllSeriesAsync(sonarrInstance))
            .ReturnsAsync(
            [
                new SearchableSeries { Id = 10, Title = "Test Series", Status = "continuing", Monitored = true, Tags = [], Statistics = new SeriesStatistics { EpisodeCount = 10, EpisodeFileCount = 5 } }
            ]);

        var pastDate = now.AddDays(-30);
        _sonarrClient
            .Setup(x => x.GetEpisodesAsync(It.IsAny<ArrInstance>(), 10))
            .ReturnsAsync(
            [
                new SearchableEpisode { Id = 100, SeasonNumber = 1, EpisodeNumber = 1, Monitored = true, HasFile = false, AirDateUtc = pastDate }
            ]);

        mockArrClient
            .Setup(x => x.SearchItemsAsync(sonarrInstance, It.IsAny<HashSet<SearchItem>>()))
            .ReturnsAsync([100L]);

        _fixture.ArrClientFactory
            .Setup(x => x.GetClient(InstanceType.Sonarr, It.IsAny<float>()))
            .Returns(mockArrClient.Object);

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert — search was triggered, cycle was reset
        mockArrClient.Verify(
            x => x.SearchItemsAsync(sonarrInstance, It.IsAny<HashSet<SearchItem>>()),
            Times.Once);

        var instanceConfig = await _fixture.DataContext.SeekerInstanceConfigs
            .FirstAsync(s => s.ArrInstanceId == sonarrInstance.Id);
        Assert.NotEqual(currentCycleId, instanceConfig.CurrentCycleId);
    }

    [Fact]
    public async Task ExecuteAsync_RoundRobin_SkipsInstanceWaitingForMinCycleTime()
    {
        // Arrange — two Radarr instances: one waiting for MinCycleTimeDays, the other has work to do
        var config = await _fixture.DataContext.SeekerConfigs.FirstAsync();
        config.SearchEnabled = true;
        config.ProactiveSearchEnabled = true;
        config.UseRoundRobin = true;
        config.MonitoredOnly = false;
        await _fixture.DataContext.SaveChangesAsync();

        var now = _fixture.TimeProvider.GetUtcNow().UtcDateTime;

        // Instance A: cycle complete, waiting for MinCycleTimeDays (oldest LastProcessedAt — would be picked first)
        var instanceA = TestDataContextFactory.AddRadarrInstance(_fixture.DataContext, "http://radarr-a:7878");
        var cycleIdA = Guid.NewGuid();
        _fixture.DataContext.SeekerInstanceConfigs.Add(new SeekerInstanceConfig
        {
            ArrInstanceId = instanceA.Id,
            ArrInstance = instanceA,
            Enabled = true,
            CurrentCycleId = cycleIdA,
            MinCycleTimeDays = 30,
            TotalEligibleItems = 1,
            LastProcessedAt = now.AddDays(-5) // Oldest — round-robin would pick this first
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
            CurrentCycleId = cycleIdB,
            MinCycleTimeDays = 5,
            TotalEligibleItems = 1,
            LastProcessedAt = now.AddDays(-1)
        });
        // No history for instance B — it hasn't searched anything yet

        await _fixture.DataContext.SaveChangesAsync();

        var mockArrClient = new Mock<IArrClient>();

        _fixture.ArrQueueIterator
            .Setup(x => x.Iterate(mockArrClient.Object, It.IsAny<ArrInstance>(), It.IsAny<Func<IReadOnlyList<QueueRecord>, Task>>()))
            .Returns(Task.CompletedTask);

        _radarrClient
            .Setup(x => x.GetAllMoviesAsync(instanceB))
            .ReturnsAsync(
            [
                new SearchableMovie { Id = 10, Title = "Movie B", Status = "released", Monitored = true, Tags = [] }
            ]);

        mockArrClient
            .Setup(x => x.SearchItemsAsync(instanceB, It.IsAny<HashSet<SearchItem>>()))
            .ReturnsAsync([100L]);

        _fixture.ArrClientFactory
            .Setup(x => x.GetClient(InstanceType.Radarr, It.IsAny<float>()))
            .Returns(mockArrClient.Object);

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert — Instance B was processed (not A which was waiting)
        _radarrClient.Verify(
            x => x.GetAllMoviesAsync(instanceB),
            Times.Once);
        _radarrClient.Verify(
            x => x.GetAllMoviesAsync(instanceA),
            Times.Never);
    }

    #endregion
}
