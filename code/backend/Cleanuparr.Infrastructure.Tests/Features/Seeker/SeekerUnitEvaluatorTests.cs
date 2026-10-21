using System.Net;
using Cleanuparr.Domain.Entities.Arr;
using Cleanuparr.Domain.Entities.Arr.Queue;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Arr.Interfaces;
using Cleanuparr.Infrastructure.Features.Seeker;
using Cleanuparr.Infrastructure.Tests.Features.Jobs.TestHelpers;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration.Arr;
using Cleanuparr.Persistence.Models.Configuration.Seeker;
using Cleanuparr.Persistence.Models.State;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.Seeker;

public class SeekerUnitEvaluatorTests : IDisposable
{
    private readonly DataContext _dataContext;
    private readonly EventsContext _eventsContext;
    private readonly IRadarrClient _radarrClient;
    private readonly ISonarrClient _sonarrClient;
    private readonly FakeTimeProvider _timeProvider;
    private readonly SeekerUnitEvaluator _sut;
    private readonly SeekerConfig _config = new() { PostReleaseGraceHours = 6 };
    private readonly ArrInstance _radarr;
    private readonly ArrInstance _sonarr;

    public SeekerUnitEvaluatorTests()
    {
        _dataContext = TestDataContextFactory.Create();
        _eventsContext = TestDataContextFactory.CreateEvents();
        _radarrClient = Substitute.For<IRadarrClient>();
        _sonarrClient = Substitute.For<ISonarrClient>();
        _timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 1, 10, 12, 0, 0, TimeSpan.Zero));
        _radarr = TestDataContextFactory.AddRadarrInstance(_dataContext);
        _sonarr = TestDataContextFactory.AddSonarrInstance(_dataContext);

        _radarrClient.GetAllTagsAsync(Arg.Any<ArrInstance>()).Returns([]);
        _sonarrClient.GetAllTagsAsync(Arg.Any<ArrInstance>()).Returns([]);
        _radarrClient.GetQualityProfilesAsync(Arg.Any<ArrInstance>()).Returns([]);
        _sonarrClient.GetQualityProfilesAsync(Arg.Any<ArrInstance>()).Returns([]);
        _radarrClient.GetCutoffUnmetMoviesAsync(Arg.Any<ArrInstance>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns([]);
        _sonarrClient.GetCutoffUnmetEpisodesAsync(Arg.Any<ArrInstance>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns([]);

        _sut = new SeekerUnitEvaluator(
            Substitute.For<ILogger<SeekerUnitEvaluator>>(),
            _eventsContext,
            _radarrClient,
            _sonarrClient,
            _timeProvider);
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

    private static SearchableMovie Movie(
        long id,
        string status = "released",
        bool monitored = true,
        List<long>? tags = null,
        bool hasFile = false,
        int qualityProfileId = 1,
        DateTimeOffset? digitalRelease = null,
        DateTimeOffset? added = null)
    {
        return new SearchableMovie
        {
            Id = id,
            Title = $"Movie {id}",
            Status = status,
            Monitored = monitored,
            Tags = tags ?? [],
            HasFile = hasFile,
            QualityProfileId = qualityProfileId,
            DigitalRelease = digitalRelease,
            Added = added,
        };
    }

    private static SearchableSeries Series(
        long id,
        string status = "continuing",
        bool monitored = true,
        List<long>? tags = null)
    {
        return new SearchableSeries
        {
            Id = id,
            Title = $"Series {id}",
            Status = status,
            Monitored = monitored,
            Tags = tags ?? [],
            SeriesType = "standard",
        };
    }

    private SearchableEpisode Episode(long id, long seriesId, int season = 1, int number = 1, DateTimeOffset? airDate = null, bool noAirDate = false)
    {
        return new SearchableEpisode
        {
            Id = id,
            SeriesId = seriesId,
            SeasonNumber = season,
            EpisodeNumber = number,
            Monitored = true,
            AirDateUtc = noAirDate ? null : airDate ?? _timeProvider.GetUtcNow().AddDays(-30),
        };
    }

    private static SeekerInstanceConfig InstanceConfig(ArrInstance instance, Action<SeekerInstanceConfig>? configure = null)
    {
        SeekerInstanceConfig instanceConfig = new()
        {
            ArrInstanceId = instance.Id,
            ArrInstance = instance,
            Enabled = true,
            MonitoredOnly = true,
        };
        configure?.Invoke(instanceConfig);
        return instanceConfig;
    }

    private static QueueRecord Queued(long movieId = 0, long seriesId = 0, long seasonNumber = 0, long episodeId = 0, string state = "downloading")
    {
        return new QueueRecord
        {
            Id = 1,
            Title = "Download",
            DownloadId = "hash",
            Protocol = "torrent",
            SizeLeft = 1000,
            MovieId = movieId,
            SeriesId = seriesId,
            SeasonNumber = seasonNumber,
            EpisodeId = episodeId,
            TrackedDownloadState = state,
        };
    }

    private void SetupRadarr(List<SearchableMovie> library, List<SearchableMovie>? missing = null)
    {
        List<SearchableMovie> wanted = missing ?? library.Where(m => !m.HasFile).ToList();

        _radarrClient.StreamAllMoviesAsync(Arg.Any<ArrInstance>(), Arg.Any<CancellationToken>())
            .Returns(_ => ToAsyncEnumerable(library));
        _radarrClient.GetMissingMoviesAsync(Arg.Any<ArrInstance>(), true, Arg.Any<CancellationToken>())
            .Returns(wanted.Where(m => m.Monitored).ToList());
        _radarrClient.GetMissingMoviesAsync(Arg.Any<ArrInstance>(), false, Arg.Any<CancellationToken>())
            .Returns(wanted.Where(m => !m.Monitored).ToList());
    }

    private void SetupSonarr(List<SearchableSeries> library, List<SearchableEpisode> missing)
    {
        _sonarrClient.StreamAllSeriesAsync(Arg.Any<ArrInstance>(), Arg.Any<CancellationToken>())
            .Returns(_ => ToAsyncEnumerable(library));
        _sonarrClient.GetMissingEpisodesAsync(Arg.Any<ArrInstance>(), true, Arg.Any<CancellationToken>())
            .Returns(missing.Where(e => e.Monitored).ToList());
        _sonarrClient.GetMissingEpisodesAsync(Arg.Any<ArrInstance>(), false, Arg.Any<CancellationToken>())
            .Returns(missing.Where(e => !e.Monitored).ToList());
    }

    private Task<SeekerEvaluation> EvaluateRadarrAsync(
        SeekerInstanceConfig? instanceConfig = null,
        List<QueueRecord>? queue = null,
        SeekerRecentGrabs? recentGrabs = null,
        List<SeekerHistory>? history = null)
    {
        return _sut.EvaluateAsync(
            _config,
            instanceConfig ?? InstanceConfig(_radarr),
            _radarr,
            queue ?? [],
            recentGrabs ?? new SeekerRecentGrabs(),
            history ?? [],
            CancellationToken.None);
    }

    private Task<SeekerEvaluation> EvaluateSonarrAsync(
        SeekerInstanceConfig? instanceConfig = null,
        List<QueueRecord>? queue = null,
        List<SeekerHistory>? history = null)
    {
        return _sut.EvaluateAsync(
            _config,
            instanceConfig ?? InstanceConfig(_sonarr),
            _sonarr,
            queue ?? [],
            new SeekerRecentGrabs(),
            history ?? [],
            CancellationToken.None);
    }

    private static SeekerHistory History(ArrInstance instance, long itemId, DateTimeOffset searchedAt, int seasonNumber = 0, long episodeId = 0)
    {
        return new SeekerHistory
        {
            ArrInstanceId = instance.Id,
            ExternalItemId = itemId,
            ItemType = instance.ArrConfig.Type,
            SeasonNumber = seasonNumber,
            EpisodeId = episodeId,
            CycleId = Guid.NewGuid(),
            LastSearchedAt = searchedAt,
            ItemTitle = $"Item {itemId}",
        };
    }

    private static SeekerExclusionReason ReasonOf(SeekerEvaluation evaluation, string name)
    {
        return evaluation.Excluded.Single(e => e.Name == name).Reason;
    }

    #region Radarr exclusion reasons

    [Fact]
    public async Task EvaluateAsync_Radarr_StatusNotReleased_ExcludesAsNotReleased()
    {
        // Arrange
        SetupRadarr([Movie(1, status: "announced")]);

        // Act
        SeekerEvaluation evaluation = await EvaluateRadarrAsync();

        // Assert
        ReasonOf(evaluation, "Movie 1").ShouldBe(SeekerExclusionReason.NotReleased);
        evaluation.Units.ShouldBeEmpty();
    }

    [Fact]
    public async Task EvaluateAsync_Radarr_ReleasedInsideGraceWindow_ExcludesAsInGracePeriod()
    {
        // Arrange
        SetupRadarr([Movie(1, digitalRelease: _timeProvider.GetUtcNow().AddHours(-1))]);

        // Act
        SeekerEvaluation evaluation = await EvaluateRadarrAsync();

        // Assert
        ReasonOf(evaluation, "Movie 1").ShouldBe(SeekerExclusionReason.InGracePeriod);
    }

    [Fact]
    public async Task EvaluateAsync_Radarr_UnmonitoredWithMonitoredOnly_ExcludesAsNotMonitored()
    {
        // Arrange
        SetupRadarr([Movie(1, monitored: false)]);

        // Act
        SeekerEvaluation evaluation = await EvaluateRadarrAsync();

        // Assert
        ReasonOf(evaluation, "Movie 1").ShouldBe(SeekerExclusionReason.NotMonitored);
    }

    [Fact]
    public async Task EvaluateAsync_Radarr_UnmonitoredWithoutMonitoredOnly_BecomesUnit()
    {
        // Arrange
        SetupRadarr([Movie(1, monitored: false)]);

        // Act
        SeekerEvaluation evaluation = await EvaluateRadarrAsync(InstanceConfig(_radarr, c => c.MonitoredOnly = false));

        // Assert
        evaluation.Units.ShouldHaveSingleItem().ItemId.ShouldBe(1);
        evaluation.Excluded.ShouldBeEmpty();
    }

    [Fact]
    public async Task EvaluateAsync_Radarr_SkipTag_ExcludesAsSkipTag()
    {
        // Arrange
        _radarrClient.GetAllTagsAsync(Arg.Any<ArrInstance>()).Returns([new Tag { Id = 7, Label = "Skip-Me" }]);
        SetupRadarr([Movie(1, tags: [7])]);

        // Act
        SeekerEvaluation evaluation = await EvaluateRadarrAsync(InstanceConfig(_radarr, c => c.SkipTags = ["skip-me"]));

        // Assert
        ReasonOf(evaluation, "Movie 1").ShouldBe(SeekerExclusionReason.SkipTag);
    }

    [Fact]
    public async Task EvaluateAsync_Radarr_IncludeTagsAndNoMatchingTag_ExcludesAsMissingIncludeTag()
    {
        // Arrange
        _radarrClient.GetAllTagsAsync(Arg.Any<ArrInstance>()).Returns([new Tag { Id = 7, Label = "keep" }]);
        SetupRadarr([Movie(1), Movie(2, tags: [7])]);

        // Act
        SeekerEvaluation evaluation = await EvaluateRadarrAsync(InstanceConfig(_radarr, c => c.IncludeTags = ["keep"]));

        // Assert
        ReasonOf(evaluation, "Movie 1").ShouldBe(SeekerExclusionReason.MissingIncludeTag);
        evaluation.Units.ShouldHaveSingleItem().ItemId.ShouldBe(2);
    }

    [Fact]
    public async Task EvaluateAsync_Radarr_SkipTagAndIncludeTagOnSameMovie_SkipTagWins()
    {
        // Arrange
        _radarrClient.GetAllTagsAsync(Arg.Any<ArrInstance>()).Returns(
        [
            new Tag { Id = 1, Label = "skip" },
            new Tag { Id = 2, Label = "keep" },
        ]);
        SetupRadarr([Movie(1, tags: [1, 2])]);

        // Act
        SeekerEvaluation evaluation = await EvaluateRadarrAsync(InstanceConfig(_radarr, c =>
        {
            c.SkipTags = ["skip"];
            c.IncludeTags = ["keep"];
        }));

        // Assert
        ReasonOf(evaluation, "Movie 1").ShouldBe(SeekerExclusionReason.SkipTag);
    }

    [Fact]
    public async Task EvaluateAsync_Radarr_MovieWithFileAndNothingWanted_ExcludesAsNothingToSearch()
    {
        // Arrange
        SetupRadarr([Movie(1, hasFile: true)]);

        // Act
        SeekerEvaluation evaluation = await EvaluateRadarrAsync();

        // Assert
        ReasonOf(evaluation, "Movie 1").ShouldBe(SeekerExclusionReason.NothingToSearch);
    }

    [Fact]
    public async Task EvaluateAsync_Radarr_BelowCustomFormatCutoffWithUpgradesDisallowed_ExcludesAsUpgradesDisabled()
    {
        // Arrange
        SetupRadarr([Movie(1, hasFile: true, qualityProfileId: 4)]);
        _radarrClient.GetQualityProfilesAsync(Arg.Any<ArrInstance>())
            .Returns([new ArrQualityProfile { Id = 4, Name = "Locked", UpgradeAllowed = false }]);
        _eventsContext.CustomFormatScoreEntries.Add(new CustomFormatScoreEntry
        {
            ArrInstanceId = _radarr.Id,
            ExternalItemId = 1,
            ItemType = InstanceType.Radarr,
            CurrentScore = 5,
            CutoffScore = 10,
        });
        await _eventsContext.SaveChangesAsync();

        // Act
        SeekerEvaluation evaluation = await EvaluateRadarrAsync(InstanceConfig(_radarr, c => c.UseCustomFormatScore = true));

        // Assert
        ReasonOf(evaluation, "Movie 1").ShouldBe(SeekerExclusionReason.UpgradesDisabled);
    }

    [Fact]
    public async Task EvaluateAsync_Radarr_BelowCustomFormatCutoffWithUpgradesAllowed_BecomesUnit()
    {
        // Arrange
        SetupRadarr([Movie(1, hasFile: true, qualityProfileId: 4)]);
        _radarrClient.GetQualityProfilesAsync(Arg.Any<ArrInstance>())
            .Returns([new ArrQualityProfile { Id = 4, Name = "Open", UpgradeAllowed = true }]);
        _eventsContext.CustomFormatScoreEntries.Add(new CustomFormatScoreEntry
        {
            ArrInstanceId = _radarr.Id,
            ExternalItemId = 1,
            ItemType = InstanceType.Radarr,
            CurrentScore = 5,
            CutoffScore = 10,
        });
        await _eventsContext.SaveChangesAsync();

        // Act
        SeekerEvaluation evaluation = await EvaluateRadarrAsync(InstanceConfig(_radarr, c => c.UseCustomFormatScore = true));

        // Assert
        SeekerSearchUnit unit = evaluation.Units.ShouldHaveSingleItem();
        unit.Reasons.ShouldBe([SeekerSearchReason.CustomFormatScoreBelowCutoff]);
    }

    [Theory]
    [InlineData("downloading")]
    [InlineData("importing")]
    [InlineData("importPending")]
    [InlineData("importBlocked")]
    public async Task EvaluateAsync_Radarr_ActiveQueueState_ExcludesAsInQueue(string state)
    {
        // Arrange
        SetupRadarr([Movie(1)]);

        // Act
        SeekerEvaluation evaluation = await EvaluateRadarrAsync(queue: [Queued(movieId: 1, state: state)]);

        // Assert
        ReasonOf(evaluation, "Movie 1").ShouldBe(SeekerExclusionReason.InQueue);
    }

    [Fact]
    public async Task EvaluateAsync_Radarr_ImportFailedQueueState_StaysSearchable()
    {
        // Arrange
        SetupRadarr([Movie(1)]);

        // Act
        SeekerEvaluation evaluation = await EvaluateRadarrAsync(queue: [Queued(movieId: 1, state: "importFailed")]);

        // Assert
        evaluation.Remaining.ShouldHaveSingleItem().ItemId.ShouldBe(1);
        evaluation.QueuedUnits.ShouldBe(0);
    }

    [Fact]
    public async Task EvaluateAsync_Radarr_RecentlyGrabbed_ExcludesAsRecentlyGrabbed()
    {
        // Arrange
        SetupRadarr([Movie(1)]);

        // Act
        SeekerEvaluation evaluation = await EvaluateRadarrAsync(recentGrabs: new SeekerRecentGrabs { MovieIds = [1] });

        // Assert
        ReasonOf(evaluation, "Movie 1").ShouldBe(SeekerExclusionReason.RecentlyGrabbed);
    }

    [Fact]
    public async Task EvaluateAsync_Radarr_QueuedAndRecentlyGrabbed_QueueWins()
    {
        // Arrange
        SetupRadarr([Movie(1)]);

        // Act
        SeekerEvaluation evaluation = await EvaluateRadarrAsync(
            queue: [Queued(movieId: 1)],
            recentGrabs: new SeekerRecentGrabs { MovieIds = [1] });

        // Assert
        ReasonOf(evaluation, "Movie 1").ShouldBe(SeekerExclusionReason.InQueue);
        evaluation.QueuedUnits.ShouldBe(1);
        evaluation.RecentlyGrabbedUnits.ShouldBe(0);
    }

    [Fact]
    public async Task EvaluateAsync_Radarr_SearchedInCurrentCycle_ExcludesAsSearchedThisCycle()
    {
        // Arrange
        SetupRadarr([Movie(1)]);

        // Act
        SeekerEvaluation evaluation = await EvaluateRadarrAsync(history: [History(_radarr, 1, _timeProvider.GetUtcNow().AddDays(-1))]);

        // Assert
        ReasonOf(evaluation, "Movie 1").ShouldBe(SeekerExclusionReason.SearchedThisCycle);
        evaluation.Remaining.ShouldBeEmpty();
    }

    [Fact]
    public async Task EvaluateAsync_Radarr_FirstMatchingStageWins()
    {
        // Arrange
        _radarrClient.GetAllTagsAsync(Arg.Any<ArrInstance>()).Returns([new Tag { Id = 1, Label = "skip" }]);
        SetupRadarr([Movie(1, status: "announced", monitored: false, tags: [1])]);

        // Act
        SeekerEvaluation evaluation = await EvaluateRadarrAsync(InstanceConfig(_radarr, c => c.SkipTags = ["skip"]));

        // Assert
        evaluation.Excluded.ShouldHaveSingleItem().Reason.ShouldBe(SeekerExclusionReason.NotReleased);
    }

    #endregion

    #region Sonarr exclusion reasons

    [Fact]
    public async Task EvaluateAsync_Sonarr_StatusUpcoming_ExcludesSeriesAsNotReleased()
    {
        // Arrange
        SetupSonarr([Series(1, status: "upcoming")], [Episode(10, 1)]);

        // Act
        SeekerEvaluation evaluation = await EvaluateSonarrAsync();

        // Assert
        ReasonOf(evaluation, "Series 1").ShouldBe(SeekerExclusionReason.NotReleased);
        evaluation.Units.ShouldBeEmpty();
    }

    [Fact]
    public async Task EvaluateAsync_Sonarr_UnmonitoredSeries_ExcludesAsNotMonitored()
    {
        // Arrange
        SetupSonarr([Series(1, monitored: false)], [Episode(10, 1)]);

        // Act
        SeekerEvaluation evaluation = await EvaluateSonarrAsync();

        // Assert
        ReasonOf(evaluation, "Series 1").ShouldBe(SeekerExclusionReason.NotMonitored);
    }

    [Fact]
    public async Task EvaluateAsync_Sonarr_SkipTag_ExcludesSeriesAsSkipTag()
    {
        // Arrange
        _sonarrClient.GetAllTagsAsync(Arg.Any<ArrInstance>()).Returns([new Tag { Id = 3, Label = "skip" }]);
        SetupSonarr([Series(1, tags: [3])], [Episode(10, 1)]);

        // Act
        SeekerEvaluation evaluation = await EvaluateSonarrAsync(InstanceConfig(_sonarr, c => c.SkipTags = ["skip"]));

        // Assert
        ReasonOf(evaluation, "Series 1").ShouldBe(SeekerExclusionReason.SkipTag);
    }

    [Fact]
    public async Task EvaluateAsync_Sonarr_EpisodeAiredInsideGraceWindow_ExcludesAsInGracePeriod()
    {
        // Arrange
        SetupSonarr([Series(1)], [Episode(10, 1, airDate: _timeProvider.GetUtcNow().AddHours(-1))]);

        // Act
        SeekerEvaluation evaluation = await EvaluateSonarrAsync();

        // Assert
        ReasonOf(evaluation, "Series 1 S01E01").ShouldBe(SeekerExclusionReason.InGracePeriod);
        evaluation.Units.ShouldBeEmpty();
    }

    [Fact]
    public async Task EvaluateAsync_Sonarr_EpisodeWithoutAirDate_ExcludesAsNotReleased()
    {
        // Arrange
        SetupSonarr([Series(1)], [Episode(10, 1, noAirDate: true)]);

        // Act
        SeekerEvaluation evaluation = await EvaluateSonarrAsync();

        // Assert
        ReasonOf(evaluation, "Series 1 S01E01").ShouldBe(SeekerExclusionReason.NotReleased);
    }

    [Fact]
    public async Task EvaluateAsync_Sonarr_SeriesWithoutWantedEpisodes_ExcludesAsNothingToSearch()
    {
        // Arrange
        SetupSonarr([Series(1)], []);

        // Act
        SeekerEvaluation evaluation = await EvaluateSonarrAsync();

        // Assert
        ReasonOf(evaluation, "Series 1").ShouldBe(SeekerExclusionReason.NothingToSearch);
    }

    [Fact]
    public async Task EvaluateAsync_Sonarr_SeasonInQueue_ExcludesSeasonUnitAsInQueue()
    {
        // Arrange
        SetupSonarr([Series(1)], [Episode(10, 1), Episode(11, 1, number: 2)]);

        // Act
        SeekerEvaluation evaluation = await EvaluateSonarrAsync(queue: [Queued(seriesId: 1, seasonNumber: 1)]);

        // Assert
        ReasonOf(evaluation, "Series 1 S01").ShouldBe(SeekerExclusionReason.InQueue);
        evaluation.QueuedUnits.ShouldBe(1);
    }

    [Fact]
    public async Task EvaluateAsync_Sonarr_SeasonSearchedInCurrentCycle_ExcludesAsSearchedThisCycle()
    {
        // Arrange
        SetupSonarr([Series(1)], [Episode(10, 1)]);

        // Act
        SeekerEvaluation evaluation = await EvaluateSonarrAsync(
            history: [History(_sonarr, 1, _timeProvider.GetUtcNow().AddDays(-1), seasonNumber: 1)]);

        // Assert
        ReasonOf(evaluation, "Series 1 S01").ShouldBe(SeekerExclusionReason.SearchedThisCycle);
    }

    #endregion

    #region Stage counts

    [Fact]
    public async Task EvaluateAsync_Radarr_CountsEveryStageOfTheFunnel()
    {
        // Arrange
        _radarrClient.GetAllTagsAsync(Arg.Any<ArrInstance>()).Returns([new Tag { Id = 1, Label = "skip" }]);
        SetupRadarr(
        [
            Movie(1, status: "announced"),
            Movie(2, digitalRelease: _timeProvider.GetUtcNow().AddHours(-1)),
            Movie(3, monitored: false),
            Movie(4, tags: [1]),
            Movie(5),
            Movie(6),
        ]);

        // Act
        SeekerEvaluation evaluation = await EvaluateRadarrAsync(
            InstanceConfig(_radarr, c => c.SkipTags = ["skip"]),
            queue: [Queued(movieId: 5)],
            recentGrabs: new SeekerRecentGrabs { MovieIds = [6] });

        // Assert
        evaluation.AllLibraryIds.Count.ShouldBe(6);
        evaluation.ReleasedItems.ShouldBe(4);
        evaluation.MonitoredItems.ShouldBe(3);
        evaluation.TagFilteredItems.ShouldBe(2);
        evaluation.Units.Count.ShouldBe(2);
        evaluation.QueuedUnits.ShouldBe(1);
        evaluation.RecentlyGrabbedUnits.ShouldBe(1);
        evaluation.Remaining.ShouldBeEmpty();
    }

    [Fact]
    public async Task EvaluateAsync_Sonarr_CountsEveryStageOfTheFunnel()
    {
        // Arrange
        _sonarrClient.GetAllTagsAsync(Arg.Any<ArrInstance>()).Returns([new Tag { Id = 1, Label = "skip" }]);
        SetupSonarr(
            [
                Series(1, status: "upcoming"),
                Series(2, monitored: false),
                Series(3, tags: [1]),
                Series(4),
                Series(5),
            ],
            [Episode(40, 4), Episode(50, 5)]);

        // Act
        SeekerEvaluation evaluation = await EvaluateSonarrAsync(
            InstanceConfig(_sonarr, c => c.SkipTags = ["skip"]),
            queue: [Queued(seriesId: 4, seasonNumber: 1)]);

        // Assert
        evaluation.AllLibraryIds.Count.ShouldBe(5);
        evaluation.ReleasedItems.ShouldBe(4);
        evaluation.MonitoredItems.ShouldBe(3);
        evaluation.TagFilteredItems.ShouldBe(2);
        evaluation.Units.Count.ShouldBe(2);
        evaluation.QueuedUnits.ShouldBe(1);
        evaluation.RecentlyGrabbedUnits.ShouldBe(0);
    }

    #endregion

    #region SelectNext

    [Fact]
    public async Task SelectNext_MoreRemainingThanRequested_ReturnsExactlyTheRequestedCount()
    {
        // Arrange
        SetupRadarr([Movie(1), Movie(2), Movie(3), Movie(4), Movie(5)]);
        SeekerEvaluation evaluation = await EvaluateRadarrAsync();

        // Act
        List<SeekerSearchCandidate> candidates = SeekerUnitEvaluator.SelectNext(evaluation, SelectionStrategy.Random, 3);

        // Assert
        candidates.Count.ShouldBe(3);
        candidates.Select(c => c.ItemId).Distinct().Count().ShouldBe(3);
    }

    [Fact]
    public async Task SelectNext_FewerRemainingThanRequested_ReturnsAllRemaining()
    {
        // Arrange
        SetupRadarr([Movie(1), Movie(2)]);
        SeekerEvaluation evaluation = await EvaluateRadarrAsync();

        // Act
        List<SeekerSearchCandidate> candidates = SeekerUnitEvaluator.SelectNext(evaluation, SelectionStrategy.Random, 10);

        // Assert
        candidates.Count.ShouldBe(2);
    }

    [Fact]
    public async Task SelectNext_BlockedAndSearchedUnits_AreNeverPicked()
    {
        // Arrange
        SetupRadarr([Movie(1), Movie(2), Movie(3)]);
        SeekerEvaluation evaluation = await EvaluateRadarrAsync(
            queue: [Queued(movieId: 1)],
            history: [History(_radarr, 2, _timeProvider.GetUtcNow().AddDays(-1))]);

        // Act
        List<SeekerSearchCandidate> candidates = SeekerUnitEvaluator.SelectNext(evaluation, SelectionStrategy.Random, 10);

        // Assert
        candidates.ShouldHaveSingleItem().ItemId.ShouldBe(3);
    }

    [Fact]
    public async Task SelectNext_Sonarr_ReturnsAtMostOneUnitPerSeries()
    {
        // Arrange
        SetupSonarr([Series(1), Series(2)],
        [
            Episode(10, 1, season: 1),
            Episode(11, 1, season: 2),
            Episode(12, 1, season: 3),
            Episode(20, 2, season: 1),
        ]);
        SeekerEvaluation evaluation = await EvaluateSonarrAsync();

        // Act
        List<SeekerSearchCandidate> candidates = SeekerUnitEvaluator.SelectNext(evaluation, SelectionStrategy.Random, 10);

        // Assert
        evaluation.Remaining.Count.ShouldBe(4);
        candidates.Count.ShouldBe(2);
        candidates.Select(c => c.ItemId).OrderBy(id => id).ShouldBe([1L, 2L]);
    }

    [Fact]
    public async Task SelectNext_NewestFirst_OrdersByDateAdded()
    {
        // Arrange
        DateTimeOffset now = _timeProvider.GetUtcNow();
        SetupRadarr(
        [
            Movie(1, added: now.AddDays(-30)),
            Movie(2, added: now.AddDays(-1)),
            Movie(3, added: now.AddDays(-10)),
        ]);
        SeekerEvaluation evaluation = await EvaluateRadarrAsync();

        // Act
        List<SeekerSearchCandidate> candidates = SeekerUnitEvaluator.SelectNext(evaluation, SelectionStrategy.NewestFirst, 3);

        // Assert
        candidates.Select(c => c.ItemId).ShouldBe([2L, 3L, 1L]);
    }

    [Fact]
    public async Task SelectNext_OldestSearchFirst_PicksTheNeverSearchedSeriesBeforeThePartlySearchedOne()
    {
        // Arrange
        SetupSonarr([Series(1), Series(2)],
        [
            Episode(10, 1, season: 1),
            Episode(11, 1, season: 2),
            Episode(20, 2, season: 1),
        ]);
        SeekerEvaluation evaluation = await EvaluateSonarrAsync(
            history: [History(_sonarr, 1, _timeProvider.GetUtcNow().AddDays(-2), seasonNumber: 1)]);

        // Act
        List<SeekerSearchCandidate> candidates = SeekerUnitEvaluator.SelectNext(evaluation, SelectionStrategy.OldestSearchFirst, 2);

        // Assert
        candidates.Select(c => c.ItemId).ShouldBe([2L, 1L]);
        candidates[1].SeasonNumber.ShouldBe(2);
    }

    #endregion

    #region WithoutCycleHistory

    [Fact]
    public async Task WithoutCycleHistory_Radarr_ClearsTheSearchedFlags()
    {
        // Arrange
        SetupRadarr([Movie(1), Movie(2)]);
        SeekerEvaluation evaluation = await EvaluateRadarrAsync(
            history:
            [
                History(_radarr, 1, _timeProvider.GetUtcNow().AddDays(-3)),
                History(_radarr, 2, _timeProvider.GetUtcNow().AddDays(-2)),
            ]);
        evaluation.Remaining.ShouldBeEmpty();

        // Act
        SeekerEvaluation fresh = evaluation.WithoutCycleHistory();

        // Assert
        fresh.Units.ShouldAllBe(u => !u.Searched);
        fresh.Remaining.Count.ShouldBe(2);
        fresh.LastSearchedByItem.ShouldBeEmpty();
        fresh.Excluded.ShouldBeEmpty();
    }

    [Fact]
    public async Task WithoutCycleHistory_Sonarr_ClearsTheSearchedFlags()
    {
        // Arrange
        SetupSonarr([Series(1)], [Episode(10, 1)]);
        SeekerEvaluation evaluation = await EvaluateSonarrAsync(
            history: [History(_sonarr, 1, _timeProvider.GetUtcNow().AddDays(-1), seasonNumber: 1)]);
        evaluation.Remaining.ShouldBeEmpty();

        // Act
        SeekerEvaluation fresh = evaluation.WithoutCycleHistory();

        // Assert
        fresh.Units.ShouldAllBe(u => !u.Searched);
        fresh.Remaining.ShouldHaveSingleItem().SeasonNumber.ShouldBe(1);
        fresh.LastSearchedByItem.ShouldBeEmpty();
    }

    [Fact]
    public async Task WithoutCycleHistory_KeepsQueueBlocks()
    {
        // Arrange
        SetupRadarr([Movie(1)]);
        SeekerEvaluation evaluation = await EvaluateRadarrAsync(queue: [Queued(movieId: 1)]);

        // Act
        SeekerEvaluation fresh = evaluation.WithoutCycleHistory();

        // Assert
        fresh.QueuedUnits.ShouldBe(1);
        fresh.Remaining.ShouldBeEmpty();
    }

    #endregion

    #region Wanted endpoint support

    [Fact]
    public async Task EvaluateAsync_Radarr_WantedEndpointNotFound_FlagsWantedUnsupported()
    {
        // Arrange
        SetupRadarr([Movie(1), Movie(2)]);
        _radarrClient.GetMissingMoviesAsync(Arg.Any<ArrInstance>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Not found", null, HttpStatusCode.NotFound));

        // Act
        SeekerEvaluation evaluation = await EvaluateRadarrAsync();

        // Assert
        evaluation.WantedUnsupported.ShouldBeTrue();
        evaluation.Units.ShouldBeEmpty();
        evaluation.AllLibraryIds.Count.ShouldBe(2);
        evaluation.WithoutCycleHistory().WantedUnsupported.ShouldBeTrue();
    }

    [Fact]
    public async Task EvaluateAsync_Radarr_WantedEndpointServerError_Throws()
    {
        // Arrange
        SetupRadarr([Movie(1)]);
        _radarrClient.GetMissingMoviesAsync(Arg.Any<ArrInstance>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Boom", null, HttpStatusCode.InternalServerError));

        // Act & Assert
        await Should.ThrowAsync<HttpRequestException>(EvaluateRadarrAsync());
    }

    #endregion

    #region ReadRecentGrabsAsync

    [Fact]
    public async Task ReadRecentGrabsAsync_WindowOff_ReturnsEmptyWithoutCallingTheArr()
    {
        // Act
        SeekerRecentGrabs grabs = await _sut.ReadRecentGrabsAsync(
            InstanceConfig(_radarr, c => c.RecentGrabWindowHours = 0), _radarr, CancellationToken.None);

        // Assert
        grabs.MovieIds.ShouldBeEmpty();
        grabs.Episodes.ShouldBeEmpty();
        await _radarrClient.DidNotReceiveWithAnyArgs().GetRecentlyGrabbedMovieIdsAsync(default!, default, default);
    }

    [Fact]
    public async Task ReadRecentGrabsAsync_Radarr_ReadsMovieIdsSinceTheWindowStart()
    {
        // Arrange
        DateTimeOffset expectedSince = _timeProvider.GetUtcNow().AddHours(-12);
        _radarrClient.GetRecentlyGrabbedMovieIdsAsync(_radarr, expectedSince, Arg.Any<CancellationToken>())
            .Returns(new HashSet<long> { 4, 9 });

        // Act
        SeekerRecentGrabs grabs = await _sut.ReadRecentGrabsAsync(
            InstanceConfig(_radarr, c => c.RecentGrabWindowHours = 12), _radarr, CancellationToken.None);

        // Assert
        grabs.MovieIds.ShouldBe([4L, 9L], ignoreOrder: true);
    }

    [Fact]
    public async Task ReadRecentGrabsAsync_Sonarr_ReadsEpisodes()
    {
        // Arrange
        _sonarrClient.GetRecentlyGrabbedEpisodesAsync(_sonarr, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(new HashSet<(long SeriesId, long SeasonNumber, long EpisodeId)> { (1, 2, 30) });

        // Act
        SeekerRecentGrabs grabs = await _sut.ReadRecentGrabsAsync(InstanceConfig(_sonarr), _sonarr, CancellationToken.None);

        // Assert
        grabs.Episodes.ShouldHaveSingleItem().ShouldBe((1L, 2L, 30L));
        grabs.MovieIds.ShouldBeEmpty();
    }

    #endregion
}
