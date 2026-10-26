using Cleanuparr.Api.Features.Seeker.Contracts.Responses;
using Cleanuparr.Api.Features.Seeker.Controllers;
using Cleanuparr.Api.Tests.Features.Seeker.TestHelpers;
using Cleanuparr.Api.Tests.TestHelpers;
using Cleanuparr.Domain.Entities.Arr;
using Cleanuparr.Domain.Entities.Arr.Queue;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Arr.Interfaces;
using Cleanuparr.Infrastructure.Features.Seeker;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration.Arr;
using Cleanuparr.Persistence.Models.Configuration.Seeker;
using Cleanuparr.Persistence.Models.State;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;

namespace Cleanuparr.Api.Tests.Features.Seeker;

public class SeekerPreviewControllerTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 1, 10, 12, 0, 0, TimeSpan.Zero);

    private readonly DataContext _dataContext;
    private readonly EventsContext _eventsContext;
    private readonly IRadarrClient _radarrClient;
    private readonly ISonarrClient _sonarrClient;
    private readonly IWhisparrV3Client _whisparrV3Client;
    private readonly IArrClientFactory _arrClientFactory;
    private readonly IArrQueueReader _arrQueueReader;
    private readonly SeekerPreviewController _controller;

    public SeekerPreviewControllerTests()
    {
        _dataContext = SeekerTestDataFactory.CreateDataContext();
        _eventsContext = SeekerTestDataFactory.CreateEventsContext();
        _radarrClient = Substitute.For<IRadarrClient>();
        _sonarrClient = Substitute.For<ISonarrClient>();
        _whisparrV3Client = Substitute.For<IWhisparrV3Client>();
        _arrClientFactory = Substitute.For<IArrClientFactory>();
        _arrQueueReader = Substitute.For<IArrQueueReader>();
        FakeTimeProvider timeProvider = new(Now);

        _arrClientFactory.GetClient(Arg.Any<InstanceType>(), Arg.Any<float>()).Returns(Substitute.For<IArrClient>());
        _arrQueueReader.ReadAllAsync(Arg.Any<IArrClient>(), Arg.Any<ArrInstance>()).Returns([]);
        _radarrClient.GetAllTagsAsync(Arg.Any<ArrInstance>()).Returns([]);
        _radarrClient.GetQualityProfilesAsync(Arg.Any<ArrInstance>()).Returns([]);
        _radarrClient
            .GetRecentlyGrabbedMovieIdsAsync(Arg.Any<ArrInstance>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(new HashSet<long>());
        _radarrClient
            .GetCutoffUnmetMoviesAsync(Arg.Any<ArrInstance>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns([]);

        SeekerUnitEvaluator evaluator = new(
            NullLogger<SeekerUnitEvaluator>.Instance,
            _eventsContext,
            _radarrClient,
            _sonarrClient,
            _whisparrV3Client,
            timeProvider);

        _controller = new SeekerPreviewController(
            NullLogger<SeekerPreviewController>.Instance,
            _dataContext,
            _eventsContext,
            _arrClientFactory,
            _arrQueueReader,
            evaluator,
            timeProvider);
        ConfigControllerTestDataFactory.ConfigureProblemDetails(_controller);
    }

    public void Dispose()
    {
        _dataContext.Dispose();
        _eventsContext.Dispose();
        GC.SuppressFinalize(this);
    }

    private static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(IEnumerable<T> items)
    {
        foreach (T item in items)
        {
            yield return item;
        }

        await Task.CompletedTask;
    }

    private static SearchableMovie Movie(long id, string status = "released")
    {
        return new SearchableMovie { Id = id, Title = $"Movie {id}", Status = status, Monitored = true, Tags = [] };
    }

    private void SetupMovies(params SearchableMovie[] movies)
    {
        List<SearchableMovie> library = movies.ToList();
        _radarrClient.StreamAllMoviesAsync(Arg.Any<ArrInstance>(), Arg.Any<CancellationToken>())
            .Returns(_ => ToAsyncEnumerable(library));
        _radarrClient.GetMissingMoviesAsync(Arg.Any<ArrInstance>(), true, Arg.Any<CancellationToken>())
            .Returns(library.Where(m => !m.HasFile).ToList());
        _radarrClient.GetMissingMoviesAsync(Arg.Any<ArrInstance>(), false, Arg.Any<CancellationToken>())
            .Returns([]);
    }

    private static SearchableEpisode Episode(long id, long seriesId, int season, int number)
    {
        return new SearchableEpisode
        {
            Id = id,
            SeriesId = seriesId,
            SeasonNumber = season,
            EpisodeNumber = number,
            Monitored = true,
            AirDateUtc = Now.AddDays(-30),
        };
    }

    private ArrInstance AddSonarrWithSeries(SelectionStrategy strategy, long seriesId, params SearchableEpisode[] missing)
    {
        SeekerConfig config = _dataContext.SeekerConfigs.First();
        config.SelectionStrategy = strategy;
        ArrInstance sonarr = SeekerTestDataFactory.AddSonarrInstance(_dataContext);
        _dataContext.SeekerInstanceConfigs.Add(new SeekerInstanceConfig
        {
            ArrInstanceId = sonarr.Id,
            Enabled = true,
            CurrentCycleId = Guid.NewGuid(),
        });
        _dataContext.SaveChanges();

        SearchableSeries series = new()
        {
            Id = seriesId,
            Title = $"Series {seriesId}",
            Status = "continuing",
            Monitored = true,
            Tags = [],
            SeriesType = "standard",
        };
        _sonarrClient.GetAllTagsAsync(Arg.Any<ArrInstance>()).Returns([]);
        _sonarrClient.GetQualityProfilesAsync(Arg.Any<ArrInstance>()).Returns([]);
        _sonarrClient.StreamAllSeriesAsync(Arg.Any<ArrInstance>(), Arg.Any<CancellationToken>())
            .Returns(_ => ToAsyncEnumerable([series]));
        _sonarrClient.GetMissingEpisodesAsync(Arg.Any<ArrInstance>(), true, Arg.Any<CancellationToken>())
            .Returns(missing.ToList());
        _sonarrClient.GetMissingEpisodesAsync(Arg.Any<ArrInstance>(), false, Arg.Any<CancellationToken>())
            .Returns([]);
        _sonarrClient.GetCutoffUnmetEpisodesAsync(Arg.Any<ArrInstance>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns([]);
        _sonarrClient
            .GetRecentlyGrabbedEpisodesAsync(Arg.Any<ArrInstance>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(new HashSet<(long SeriesId, long SeasonNumber, long EpisodeId)>());
        return sonarr;
    }

    private ArrInstance AddRadarr(Guid? currentCycleId = null, int minCycleTimeDays = 7, bool restUnitsEnabled = false)
    {
        ArrInstance radarr = SeekerTestDataFactory.AddRadarrInstance(_dataContext);
        _dataContext.SeekerInstanceConfigs.Add(new SeekerInstanceConfig
        {
            ArrInstanceId = radarr.Id,
            Enabled = true,
            CurrentCycleId = currentCycleId ?? Guid.NewGuid(),
            MinCycleTimeDays = minCycleTimeDays,
            RestUnitsEnabled = restUnitsEnabled,
        });
        _dataContext.SaveChanges();
        return radarr;
    }

    private void AddCycle(Guid id, Guid arrInstanceId, DateTimeOffset startedAt, int searchedUnits, bool isDryRun = false)
    {
        _eventsContext.SeekerCycles.Add(new SeekerCycle
        {
            Id = id,
            ArrInstanceId = arrInstanceId,
            StartedAt = startedAt,
            SearchedUnits = searchedUnits,
            IsDryRun = isDryRun,
        });
        _eventsContext.SaveChanges();
    }

    private void AddHistory(Guid arrInstanceId, Guid cycleId, long movieId, bool isDryRun = false)
    {
        _eventsContext.SeekerHistory.Add(new SeekerHistory
        {
            ArrInstanceId = arrInstanceId,
            ExternalItemId = movieId,
            ItemType = InstanceType.Radarr,
            CycleId = cycleId,
            LastSearchedAt = Now.AddDays(-1),
            ItemTitle = $"Movie {movieId}",
            IsDryRun = isDryRun,
        });
        _eventsContext.SaveChanges();
    }

    private static SeekerPreviewResponse Preview(IActionResult result) =>
        result.ShouldBeOfType<OkObjectResult>().Value.ShouldBeOfType<SeekerPreviewResponse>();

    private static int StatusOf(IActionResult result) =>
        result.ShouldBeOfType<ObjectResult>().StatusCode.ShouldNotBeNull();

    [Fact]
    public async Task GetPreview_UnknownInstance_ReturnsNotFound()
    {
        IActionResult result = await _controller.GetPreview(Guid.NewGuid(), CancellationToken.None);

        StatusOf(result).ShouldBe(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task GetPreview_UnsupportedInstanceType_ReturnsBadRequest()
    {
        ArrInstance lidarr = SeekerTestDataFactory.AddLidarrInstance(_dataContext);
        _dataContext.SeekerInstanceConfigs.Add(new SeekerInstanceConfig { ArrInstanceId = lidarr.Id, Enabled = true });
        await _dataContext.SaveChangesAsync();

        IActionResult result = await _controller.GetPreview(lidarr.Id, CancellationToken.None);

        StatusOf(result).ShouldBe(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task GetPreview_Radarr_ReturnsNextUpExcludedAndFunnel()
    {
        ArrInstance radarr = AddRadarr();
        SetupMovies(Movie(1), Movie(2), Movie(3, status: "announced"));

        SeekerPreviewResponse response = Preview(await _controller.GetPreview(radarr.Id, CancellationToken.None));

        response.NextUp.Select(u => u.Name).ShouldBe(["Movie 1", "Movie 2"], ignoreOrder: true);
        response.NextUp.ShouldAllBe(u => u.Reason == SeekerSearchReason.Missing);
        SeekerPreviewExclusionResponse excluded = response.Excluded.ShouldHaveSingleItem();
        excluded.Name.ShouldBe("Movie 3");
        excluded.Reason.ShouldBe(SeekerExclusionReason.NotReleased);
        response.CycleWaitingUntil.ShouldBeNull();
        response.Funnel.LibraryItems.ShouldBe(3);
        response.Funnel.ReleasedItems.ShouldBe(2);
        response.Funnel.EligibleUnits.ShouldBe(2);
        response.Funnel.RemainingUnits.ShouldBe(2);
        response.Funnel.SearchedUnits.ShouldBe(0);
        response.Funnel.CountedAt.ShouldBe(Now);
    }

    [Fact]
    public async Task GetPreview_RestingMovie_MapsTheRestFieldsAndCountsItInTheFunnel()
    {
        Guid cycleId = Guid.NewGuid();
        ArrInstance radarr = AddRadarr(cycleId, restUnitsEnabled: true);
        AddCycle(cycleId, radarr.Id, Now.AddDays(-1), searchedUnits: 1);
        _eventsContext.SeekerHistory.Add(new SeekerHistory
        {
            ArrInstanceId = radarr.Id,
            ExternalItemId = 1,
            ItemType = InstanceType.Radarr,
            CycleId = Guid.NewGuid(),
            LastSearchedAt = Now.AddDays(-2),
            ItemTitle = "Movie 1",
            ConsecutiveMisses = 4,
        });
        _eventsContext.SaveChanges();
        SetupMovies(Movie(1), Movie(2));

        SeekerPreviewResponse response = Preview(await _controller.GetPreview(radarr.Id, CancellationToken.None));

        SeekerPreviewExclusionResponse excluded = response.Excluded.ShouldHaveSingleItem();
        excluded.Name.ShouldBe("Movie 1");
        excluded.Reason.ShouldBe(SeekerExclusionReason.Resting);
        excluded.ConsecutiveMisses.ShouldBe(4);
        excluded.RestCyclesLeft.ShouldBe(2);
        excluded.RestUntil.ShouldBe(Now.AddDays(-2).AddDays(30));
        response.NextUp.ShouldHaveSingleItem().Name.ShouldBe("Movie 2");
        response.Funnel.RestingUnits.ShouldBe(1);
        response.Funnel.SearchedUnits.ShouldBe(0);
        response.Funnel.RemainingUnits.ShouldBe(1);
    }

    [Fact]
    public async Task GetPreview_NonRestingExclusion_LeavesTheRestFieldsNull()
    {
        ArrInstance radarr = AddRadarr();
        SetupMovies(Movie(1), Movie(3, status: "announced"));

        SeekerPreviewResponse response = Preview(await _controller.GetPreview(radarr.Id, CancellationToken.None));

        SeekerPreviewExclusionResponse excluded = response.Excluded.ShouldHaveSingleItem();
        excluded.ConsecutiveMisses.ShouldBeNull();
        excluded.RestCyclesLeft.ShouldBeNull();
        excluded.RestUntil.ShouldBeNull();
    }

    [Fact]
    public async Task GetPreview_QueuedMovie_LeavesItOutOfNextUp()
    {
        ArrInstance radarr = AddRadarr();
        SetupMovies(Movie(1), Movie(2));
        _arrQueueReader.ReadAllAsync(Arg.Any<IArrClient>(), Arg.Any<ArrInstance>()).Returns(
        [
            new QueueRecord { Id = 1, Title = "Movie 1 Download", DownloadId = "hash", Protocol = "torrent", SizeLeft = 1, MovieId = 1, TrackedDownloadState = "downloading" },
        ]);

        SeekerPreviewResponse response = Preview(await _controller.GetPreview(radarr.Id, CancellationToken.None));

        response.NextUp.ShouldHaveSingleItem().Name.ShouldBe("Movie 2");
        response.Excluded.ShouldHaveSingleItem().Reason.ShouldBe(SeekerExclusionReason.InQueue);
        response.Funnel.QueuedUnits.ShouldBe(1);
    }

    [Fact]
    public async Task GetPreview_CycleSearchedEverythingBeforeMinCycleTime_WaitsAndListsNothingNextUp()
    {
        Guid cycleId = Guid.NewGuid();
        ArrInstance radarr = AddRadarr(cycleId, minCycleTimeDays: 7);
        DateTimeOffset startedAt = Now.AddDays(-2);
        AddCycle(cycleId, radarr.Id, startedAt, searchedUnits: 2);
        AddHistory(radarr.Id, cycleId, 1);
        AddHistory(radarr.Id, cycleId, 2);
        SetupMovies(Movie(1), Movie(2));

        SeekerPreviewResponse response = Preview(await _controller.GetPreview(radarr.Id, CancellationToken.None));

        response.CycleWaitingUntil.ShouldBe(startedAt.AddDays(7));
        response.NextUp.ShouldBeEmpty();
        response.Excluded.ShouldAllBe(e => e.Reason == SeekerExclusionReason.SearchedThisCycle);
        response.Excluded.Count.ShouldBe(2);
    }

    [Fact]
    public async Task GetPreview_CycleSearchedEverythingAfterMinCycleTime_PreviewsTheNextCycle()
    {
        Guid cycleId = Guid.NewGuid();
        ArrInstance radarr = AddRadarr(cycleId, minCycleTimeDays: 7);
        AddCycle(cycleId, radarr.Id, Now.AddDays(-10), searchedUnits: 2);
        AddHistory(radarr.Id, cycleId, 1);
        AddHistory(radarr.Id, cycleId, 2);
        SetupMovies(Movie(1), Movie(2));

        SeekerPreviewResponse response = Preview(await _controller.GetPreview(radarr.Id, CancellationToken.None));

        response.CycleWaitingUntil.ShouldBeNull();
        response.NextUp.Count.ShouldBe(2);
        response.Excluded.ShouldBeEmpty();
        response.Funnel.RemainingUnits.ShouldBe(2);
        response.Funnel.SearchedUnits.ShouldBe(0);
    }

    [Fact]
    public async Task GetPreview_UnsearchedCycleWithEveryMovieResting_WaitsForMinCycleTime()
    {
        Guid cycleId = Guid.NewGuid();
        ArrInstance radarr = AddRadarr(cycleId, minCycleTimeDays: 7, restUnitsEnabled: true);
        DateTimeOffset startedAt = Now.AddDays(-2);
        AddCycle(cycleId, radarr.Id, startedAt, searchedUnits: 0);
        _eventsContext.SeekerHistory.Add(new SeekerHistory
        {
            ArrInstanceId = radarr.Id,
            ExternalItemId = 1,
            ItemType = InstanceType.Radarr,
            CycleId = Guid.NewGuid(),
            LastSearchedAt = Now.AddDays(-3),
            ItemTitle = "Movie 1",
            ConsecutiveMisses = 4,
        });
        _eventsContext.SaveChanges();
        SetupMovies(Movie(1));

        SeekerPreviewResponse response = Preview(await _controller.GetPreview(radarr.Id, CancellationToken.None));

        response.CycleWaitingUntil.ShouldBe(startedAt.AddDays(7));
        response.NextUp.ShouldBeEmpty();
        response.Excluded.ShouldHaveSingleItem().Reason.ShouldBe(SeekerExclusionReason.Resting);
    }

    [Fact]
    public async Task GetPreview_RestEndsBeforeMinCycleTime_WaitsForTheRestEnd()
    {
        Guid cycleId = Guid.NewGuid();
        ArrInstance radarr = AddRadarr(cycleId, minCycleTimeDays: 60, restUnitsEnabled: true);
        DateTimeOffset startedAt = Now.AddDays(-2);
        AddCycle(cycleId, radarr.Id, startedAt, searchedUnits: 0);
        _eventsContext.SeekerHistory.Add(new SeekerHistory
        {
            ArrInstanceId = radarr.Id,
            ExternalItemId = 1,
            ItemType = InstanceType.Radarr,
            CycleId = Guid.NewGuid(),
            LastSearchedAt = Now.AddDays(-3),
            ItemTitle = "Movie 1",
            ConsecutiveMisses = 4,
        });
        _eventsContext.SaveChanges();
        SetupMovies(Movie(1));

        SeekerPreviewResponse response = Preview(await _controller.GetPreview(radarr.Id, CancellationToken.None));

        DateTimeOffset restUntil = response.Excluded.ShouldHaveSingleItem().RestUntil.ShouldNotBeNull();
        restUntil.ShouldBeLessThan(startedAt.AddDays(60));
        response.CycleWaitingUntil.ShouldBe(restUntil);
    }

    [Fact]
    public async Task GetPreview_RestStartedThisCycleEndsBeforeMinCycleTime_WaitsForMinCycleTime()
    {
        Guid cycleId = Guid.NewGuid();
        ArrInstance radarr = AddRadarr(cycleId, minCycleTimeDays: 60, restUnitsEnabled: true);
        DateTimeOffset startedAt = Now.AddDays(-2);
        AddCycle(cycleId, radarr.Id, startedAt, searchedUnits: 1);
        _eventsContext.SeekerHistory.Add(new SeekerHistory
        {
            ArrInstanceId = radarr.Id,
            ExternalItemId = 1,
            ItemType = InstanceType.Radarr,
            CycleId = cycleId,
            LastSearchedAt = Now.AddDays(-1),
            ItemTitle = "Movie 1",
            ConsecutiveMisses = 4,
        });
        _eventsContext.SaveChanges();
        SetupMovies(Movie(1));

        SeekerPreviewResponse response = Preview(await _controller.GetPreview(radarr.Id, CancellationToken.None));

        response.Excluded.ShouldHaveSingleItem().RestUntil.ShouldNotBeNull().ShouldBeLessThan(startedAt.AddDays(60));
        response.CycleWaitingUntil.ShouldBe(startedAt.AddDays(60));
    }

    [Fact]
    public async Task GetPreview_UnsearchedCycleWithNothingLeftOrResting_DoesNotWait()
    {
        Guid cycleId = Guid.NewGuid();
        ArrInstance radarr = AddRadarr(cycleId, minCycleTimeDays: 7);
        AddCycle(cycleId, radarr.Id, Now.AddDays(-2), searchedUnits: 0);
        SetupMovies(Movie(1, status: "announced"));

        SeekerPreviewResponse response = Preview(await _controller.GetPreview(radarr.Id, CancellationToken.None));

        response.CycleWaitingUntil.ShouldBeNull();
        response.NextUp.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetPreview_DryRun_ReadsTheNewestDryCyclesHistory()
    {
        (await _dataContext.GeneralConfigs.FirstAsync()).DryRun = true;
        await _dataContext.SaveChangesAsync();

        Guid liveCycleId = Guid.NewGuid();
        ArrInstance radarr = AddRadarr(liveCycleId);
        Guid oldDryCycleId = Guid.NewGuid();
        Guid newDryCycleId = Guid.NewGuid();
        AddCycle(liveCycleId, radarr.Id, Now.AddDays(-20), searchedUnits: 1);
        AddCycle(oldDryCycleId, radarr.Id, Now.AddDays(-5), searchedUnits: 1, isDryRun: true);
        AddCycle(newDryCycleId, radarr.Id, Now.AddDays(-1), searchedUnits: 1, isDryRun: true);
        AddHistory(radarr.Id, liveCycleId, 1);
        AddHistory(radarr.Id, oldDryCycleId, 3, isDryRun: true);
        AddHistory(radarr.Id, newDryCycleId, 2, isDryRun: true);
        SetupMovies(Movie(1), Movie(2), Movie(3));

        SeekerPreviewResponse response = Preview(await _controller.GetPreview(radarr.Id, CancellationToken.None));

        response.Excluded.ShouldHaveSingleItem().Name.ShouldBe("Movie 2");
        response.NextUp.Select(u => u.Name).ShouldBe(["Movie 1", "Movie 3"], ignoreOrder: true);
    }

    [Fact]
    public async Task GetPreview_WritesNothing()
    {
        Guid cycleId = Guid.NewGuid();
        ArrInstance radarr = AddRadarr(cycleId);
        AddCycle(cycleId, radarr.Id, Now.AddDays(-1), searchedUnits: 1);
        AddHistory(radarr.Id, cycleId, 1);
        SetupMovies(Movie(1), Movie(2));

        Preview(await _controller.GetPreview(radarr.Id, CancellationToken.None));

        _eventsContext.ChangeTracker.Clear();
        (await _eventsContext.SeekerCycles.CountAsync()).ShouldBe(1);
        (await _eventsContext.SeekerHistory.CountAsync()).ShouldBe(1);
        SeekerCycle cycle = await _eventsContext.SeekerCycles.SingleAsync();
        cycle.FunnelCountedAt.ShouldBeNull();
        cycle.SearchedUnits.ShouldBe(1);
    }

    [Fact]
    public async Task GetPreview_NoCycleYet_CreatesNoCycle()
    {
        ArrInstance radarr = AddRadarr();
        SetupMovies(Movie(1));

        SeekerPreviewResponse response = Preview(await _controller.GetPreview(radarr.Id, CancellationToken.None));

        response.NextUp.ShouldHaveSingleItem();
        (await _eventsContext.SeekerCycles.CountAsync()).ShouldBe(0);
        (await _eventsContext.SeekerHistory.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task GetPreview_QueueReadThrows_ReturnsBadGateway()
    {
        ArrInstance radarr = AddRadarr();
        SetupMovies(Movie(1));
        _arrQueueReader.ReadAllAsync(Arg.Any<IArrClient>(), Arg.Any<ArrInstance>())
            .ThrowsAsync(new HttpRequestException("radarr is down"));

        IActionResult result = await _controller.GetPreview(radarr.Id, CancellationToken.None);

        StatusOf(result).ShouldBe(StatusCodes.Status502BadGateway);
    }

    [Fact]
    public async Task GetPreview_RadarrWithoutWantedEndpoints_ReturnsBadGateway()
    {
        ArrInstance radarr = AddRadarr();
        SetupMovies(Movie(1));
        _radarrClient.GetMissingMoviesAsync(Arg.Any<ArrInstance>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Not found", null, System.Net.HttpStatusCode.NotFound));

        IActionResult result = await _controller.GetPreview(radarr.Id, CancellationToken.None);

        StatusOf(result).ShouldBe(StatusCodes.Status502BadGateway);
    }

    [Fact]
    public async Task GetPreview_SonarrSeriesMissingInTwoSeasons_FlagsRandomPickUnderOldestFirst()
    {
        ArrInstance sonarr = AddSonarrWithSeries(
            SelectionStrategy.OldestSearchFirst, 100, Episode(501, 100, 1, 1), Episode(502, 100, 2, 1));

        SeekerPreviewResponse response = Preview(await _controller.GetPreview(sonarr.Id, CancellationToken.None));

        response.NextUp.ShouldHaveSingleItem();
        response.RandomPick.ShouldBeTrue();
    }

    [Fact]
    public async Task GetPreview_SonarrSeriesMissingInOneSeason_DoesNotFlagRandomPickUnderOldestFirst()
    {
        ArrInstance sonarr = AddSonarrWithSeries(
            SelectionStrategy.OldestSearchFirst, 100, Episode(501, 100, 1, 1), Episode(502, 100, 1, 2));

        SeekerPreviewResponse response = Preview(await _controller.GetPreview(sonarr.Id, CancellationToken.None));

        response.NextUp.ShouldHaveSingleItem();
        response.RandomPick.ShouldBeFalse();
    }

    [Fact]
    public async Task GetPreview_TiedMoviesUnderOldestFirst_FlagsRandomPick()
    {
        _dataContext.SeekerConfigs.First().SelectionStrategy = SelectionStrategy.OldestSearchFirst;
        ArrInstance radarr = AddRadarr();
        SetupMovies(Movie(1), Movie(2));

        SeekerPreviewResponse response = Preview(await _controller.GetPreview(radarr.Id, CancellationToken.None));

        response.NextUp.ShouldNotBeEmpty();
        response.RandomPick.ShouldBeTrue();
    }

    [Fact]
    public async Task GetPreview_QueueReadCancelled_Rethrows()
    {
        ArrInstance radarr = AddRadarr();
        SetupMovies(Movie(1));
        _arrQueueReader.ReadAllAsync(Arg.Any<IArrClient>(), Arg.Any<ArrInstance>())
            .ThrowsAsync(new OperationCanceledException());

        await Should.ThrowAsync<OperationCanceledException>(
            () => _controller.GetPreview(radarr.Id, CancellationToken.None));
    }
}
