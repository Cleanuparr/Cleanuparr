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
    private readonly ILidarrClient _lidarrClient;
    private readonly IChaptarrClient _chaptarrClient;
    private readonly IWhisparrV2Client _whisparrV2Client;
    private readonly IWhisparrV3Client _whisparrV3Client;
    private readonly FakeTimeProvider _timeProvider;
    private readonly SeekerUnitEvaluator _sut;
    private readonly SeekerConfig _config = new() { PostReleaseGraceHours = 6 };
    private readonly ArrInstance _radarr;
    private readonly ArrInstance _sonarr;
    private readonly ArrInstance _whisparr;
    private readonly ArrInstance _lidarr;
    private readonly ArrInstance _chaptarr;

    public SeekerUnitEvaluatorTests()
    {
        _dataContext = TestDataContextFactory.Create();
        _eventsContext = TestDataContextFactory.CreateEvents();
        _radarrClient = Substitute.For<IRadarrClient>();
        _sonarrClient = Substitute.For<ISonarrClient>();
        _lidarrClient = Substitute.For<ILidarrClient>();
        _chaptarrClient = Substitute.For<IChaptarrClient>();
        _whisparrV2Client = Substitute.For<IWhisparrV2Client>();
        _whisparrV3Client = Substitute.For<IWhisparrV3Client>();
        _timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 1, 10, 12, 0, 0, TimeSpan.Zero));
        _radarr = TestDataContextFactory.AddRadarrInstance(_dataContext);
        _sonarr = TestDataContextFactory.AddSonarrInstance(_dataContext);
        _whisparr = TestDataContextFactory.AddWhisparrInstance(_dataContext, version: 3);
        _lidarr = TestDataContextFactory.AddLidarrInstance(_dataContext);
        _chaptarr = TestDataContextFactory.AddChaptarrInstance(_dataContext);

        _radarrClient.GetAllTagsAsync(Arg.Any<ArrInstance>()).Returns([]);
        _sonarrClient.GetAllTagsAsync(Arg.Any<ArrInstance>()).Returns([]);
        _radarrClient.GetQualityProfilesAsync(Arg.Any<ArrInstance>()).Returns([]);
        _sonarrClient.GetQualityProfilesAsync(Arg.Any<ArrInstance>()).Returns([]);
        _radarrClient.GetCutoffUnmetMoviesAsync(Arg.Any<ArrInstance>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns([]);
        _sonarrClient.GetCutoffUnmetEpisodesAsync(Arg.Any<ArrInstance>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns([]);
        _lidarrClient.GetAllTagsAsync(Arg.Any<ArrInstance>()).Returns([]);
        _lidarrClient.GetQualityProfilesAsync(Arg.Any<ArrInstance>()).Returns([]);
        _lidarrClient.GetCutoffUnmetAlbumsAsync(Arg.Any<ArrInstance>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns([]);
        _chaptarrClient.GetAllTagsAsync(Arg.Any<ArrInstance>()).Returns([]);
        _chaptarrClient.GetCutoffUnmetBooksAsync(Arg.Any<ArrInstance>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns([]);
        _whisparrV3Client.GetAllTagsAsync(Arg.Any<ArrInstance>()).Returns([]);
        _whisparrV3Client.GetQualityProfilesAsync(Arg.Any<ArrInstance>()).Returns([]);
        _whisparrV3Client.GetCutoffUnmetMoviesAsync(Arg.Any<ArrInstance>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns([]);

        _sut = new SeekerUnitEvaluator(
            Substitute.For<ILogger<SeekerUnitEvaluator>>(),
            _eventsContext,
            _radarrClient,
            _sonarrClient,
            _lidarrClient,
            _chaptarrClient,
            _whisparrV2Client,
            _whisparrV3Client,
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
        DateTimeOffset? releaseDate = null,
        DateTimeOffset? added = null,
        DateTimeOffset? lastSearchTime = null,
        bool isAvailable = false)
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
            ReleaseDate = releaseDate,
            Added = added,
            LastSearchTime = lastSearchTime,
            IsAvailable = isAvailable,
        };
    }

    private static SearchableSeries Series(
        long id,
        string status = "continuing",
        bool monitored = true,
        List<long>? tags = null,
        int qualityProfileId = 0,
        string seriesType = "standard",
        List<SeriesSeason>? seasons = null)
    {
        return new SearchableSeries
        {
            Id = id,
            Title = $"Series {id}",
            Status = status,
            Monitored = monitored,
            Tags = tags ?? [],
            SeriesType = seriesType,
            QualityProfileId = qualityProfileId,
            Seasons = seasons ?? [],
        };
    }

    private static SeriesSeason Season(int number, int episodeCount, DateTimeOffset? nextAiring = null)
    {
        return new SeriesSeason
        {
            SeasonNumber = number,
            Monitored = true,
            Statistics = new SeasonStatistics
            {
                EpisodeCount = episodeCount,
                TotalEpisodeCount = episodeCount,
                NextAiring = nextAiring,
            },
        };
    }

    private SearchableEpisode Episode(long id, long seriesId, int season = 1, int number = 1, DateTimeOffset? airDate = null, bool noAirDate = false, DateTimeOffset? lastSearchTime = null)
    {
        return new SearchableEpisode
        {
            Id = id,
            SeriesId = seriesId,
            SeasonNumber = season,
            EpisodeNumber = number,
            Monitored = true,
            AirDateUtc = noAirDate ? null : airDate ?? _timeProvider.GetUtcNow().AddDays(-30),
            LastSearchTime = lastSearchTime,
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

    private void SetupWhisparr(List<SearchableMovie> library, List<SearchableMovie>? missing = null)
    {
        List<SearchableMovie> wanted = missing ?? library.Where(m => !m.HasFile).ToList();

        _whisparrV3Client.StreamAllMoviesAsync(Arg.Any<ArrInstance>(), Arg.Any<CancellationToken>())
            .Returns(_ => ToAsyncEnumerable(library));
        _whisparrV3Client.GetMissingMoviesAsync(Arg.Any<ArrInstance>(), true, Arg.Any<CancellationToken>())
            .Returns(wanted.Where(m => m.Monitored).ToList());
        _whisparrV3Client.GetMissingMoviesAsync(Arg.Any<ArrInstance>(), false, Arg.Any<CancellationToken>())
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

    private Task<SeekerEvaluation> EvaluateWhisparrAsync(SeekerInstanceConfig? instanceConfig = null)
    {
        return _sut.EvaluateAsync(
            _config,
            instanceConfig ?? InstanceConfig(_whisparr),
            _whisparr,
            [],
            new SeekerRecentGrabs(),
            [],
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

    private async Task AddSonarrCustomFormatEntryAsync(long episodeId, int currentScore, int cutoffScore)
    {
        await _eventsContext.CustomFormatScoreEntries.AddAsync(new CustomFormatScoreEntry
        {
            ArrInstanceId = _sonarr.Id,
            ExternalItemId = 1,
            EpisodeId = episodeId,
            Title = $"Series 1 episode {episodeId}",
            ItemType = InstanceType.Sonarr,
            SeasonNumber = 1,
            AirDateUtc = _timeProvider.GetUtcNow().AddDays(-30),
            IsMonitored = true,
            CurrentScore = currentScore,
            CutoffScore = cutoffScore,
        });
        await _eventsContext.SaveChangesAsync();
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
    public async Task EvaluateAsync_Radarr_ArrAvailability_InCinemasMovieThatIsAvailable_BuildsUnit()
    {
        // Arrange
        SetupRadarr([Movie(1, status: "inCinemas", isAvailable: true)]);

        // Act
        SeekerEvaluation evaluation = await EvaluateRadarrAsync(
            InstanceConfig(_radarr, c => c.RadarrReleaseGate = RadarrReleaseGate.ArrAvailability));

        // Assert
        evaluation.Units.ShouldHaveSingleItem().Name.ShouldBe("Movie 1");
    }

    [Fact]
    public async Task EvaluateAsync_Radarr_ArrAvailability_ReleasedMovieThatIsNotAvailable_ExcludesAsNotAvailable()
    {
        // Arrange
        SetupRadarr([Movie(1, digitalRelease: _timeProvider.GetUtcNow().AddDays(-30), isAvailable: false)]);

        // Act
        SeekerEvaluation evaluation = await EvaluateRadarrAsync(
            InstanceConfig(_radarr, c => c.RadarrReleaseGate = RadarrReleaseGate.ArrAvailability));

        // Assert
        ReasonOf(evaluation, "Movie 1").ShouldBe(SeekerExclusionReason.NotAvailable);
        evaluation.Units.ShouldBeEmpty();
    }

    [Fact]
    public async Task EvaluateAsync_Radarr_ArrAvailability_AvailableMovieInsideGraceWindow_BuildsUnit()
    {
        // Arrange
        SetupRadarr([Movie(1, digitalRelease: _timeProvider.GetUtcNow().AddHours(-1), isAvailable: true)]);

        // Act
        SeekerEvaluation evaluation = await EvaluateRadarrAsync(
            InstanceConfig(_radarr, c => c.RadarrReleaseGate = RadarrReleaseGate.ArrAvailability));

        // Assert
        evaluation.Units.ShouldHaveSingleItem().Name.ShouldBe("Movie 1");
    }

    [Fact]
    public async Task EvaluateAsync_Radarr_ReleaseDate_InCinemasMovieThatIsAvailable_ExcludesAsNotReleased()
    {
        // Arrange
        SetupRadarr([Movie(1, status: "inCinemas", isAvailable: true)]);

        // Act
        SeekerEvaluation evaluation = await EvaluateRadarrAsync();

        // Assert
        ReasonOf(evaluation, "Movie 1").ShouldBe(SeekerExclusionReason.NotReleased);
        evaluation.Units.ShouldBeEmpty();
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

    [Fact]
    public async Task EvaluateAsync_Radarr_BelowCustomFormatCutoff_SetsTheGapToCutoffMinusScore()
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
            CutoffScore = 30,
        });
        await _eventsContext.SaveChangesAsync();

        // Act
        SeekerEvaluation evaluation = await EvaluateRadarrAsync(InstanceConfig(_radarr, c => c.UseCustomFormatScore = true));

        // Assert
        evaluation.Units.ShouldHaveSingleItem().CustomFormatGap.ShouldBe(25);
    }

    [Fact]
    public async Task EvaluateAsync_Radarr_MissingWithUpgradesDisallowed_KeepsTheGapAtZero()
    {
        // Arrange
        SetupRadarr([Movie(1, qualityProfileId: 4)]);
        _radarrClient.GetQualityProfilesAsync(Arg.Any<ArrInstance>())
            .Returns([new ArrQualityProfile { Id = 4, Name = "Locked", UpgradeAllowed = false }]);
        _eventsContext.CustomFormatScoreEntries.Add(new CustomFormatScoreEntry
        {
            ArrInstanceId = _radarr.Id,
            ExternalItemId = 1,
            ItemType = InstanceType.Radarr,
            CurrentScore = 5,
            CutoffScore = 30,
        });
        await _eventsContext.SaveChangesAsync();

        // Act
        SeekerEvaluation evaluation = await EvaluateRadarrAsync(InstanceConfig(_radarr, c => c.UseCustomFormatScore = true));

        // Assert
        SeekerSearchUnit unit = evaluation.Units.ShouldHaveSingleItem();
        unit.Reasons.ShouldBe([SeekerSearchReason.Missing]);
        unit.CustomFormatGap.ShouldBe(0);
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
        SeekerEvaluation evaluation = await EvaluateSonarrAsync(
            InstanceConfig(_sonarr, c => c.SonarrSearchMode = SonarrSearchMode.Season),
            queue: [Queued(seriesId: 1, seasonNumber: 1)]);

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
            InstanceConfig(_sonarr, c => c.SonarrSearchMode = SonarrSearchMode.Season),
            history: [History(_sonarr, 1, _timeProvider.GetUtcNow().AddDays(-1), seasonNumber: 1)]);

        // Assert
        ReasonOf(evaluation, "Series 1 S01").ShouldBe(SeekerExclusionReason.SearchedThisCycle);
    }

        [Fact]
    public async Task EvaluateAsync_Sonarr_MissingEpisodeWithStaleCustomFormatRowAndUpgradesDisabled_IsNotExcluded()
    {
        // Arrange
        SetupSonarr([Series(1, qualityProfileId: 4)], [Episode(20, 1)]);
        _sonarrClient.GetQualityProfilesAsync(Arg.Any<ArrInstance>())
            .Returns([new ArrQualityProfile { Id = 4, Name = "Locked", UpgradeAllowed = false }]);
        _eventsContext.CustomFormatScoreEntries.Add(new CustomFormatScoreEntry
        {
            ArrInstanceId = _sonarr.Id,
            ExternalItemId = 1,
            EpisodeId = 20,
            Title = "Series 1 S01E01",
            ItemType = InstanceType.Sonarr,
            SeasonNumber = 1,
            AirDateUtc = _timeProvider.GetUtcNow().AddDays(-30),
            IsMonitored = true,
            CurrentScore = 5,
            CutoffScore = 10,
        });
        await _eventsContext.SaveChangesAsync();

        // Act
        SeekerEvaluation evaluation = await EvaluateSonarrAsync(InstanceConfig(_sonarr, c => c.UseCustomFormatScore = true));

        // Assert
        SeekerSearchUnit unit = evaluation.Units.ShouldHaveSingleItem();
        unit.Reasons.ShouldBe([SeekerSearchReason.Missing]);
        evaluation.Excluded.ShouldNotContain(e => e.Reason == SeekerExclusionReason.UpgradesDisabled);
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
            InstanceConfig(_sonarr, c =>
            {
                c.SkipTags = ["skip"];
                c.SonarrSearchMode = SonarrSearchMode.Season;
            }),
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

    #region Sonarr search mode

    [Fact]
    public async Task EvaluateAsync_Sonarr_AutoAndEveryAiredEpisodeWanted_BuildsSeasonUnit()
    {
        // Arrange
        SetupSonarr([Series(1, seasons: [Season(1, 2)])], [Episode(102, 1), Episode(103, 1, number: 2)]);

        // Act
        SeekerEvaluation evaluation = await EvaluateSonarrAsync();

        // Assert
        SeekerSearchUnit unit = evaluation.Units.ShouldHaveSingleItem();
        unit.EpisodeId.ShouldBe(0);
        unit.Name.ShouldBe("Series 1 S01");
    }

    [Fact]
    public async Task EvaluateAsync_Sonarr_AutoAndSeasonPartlyWanted_BuildsEpisodeUnits()
    {
        // Arrange
        SetupSonarr([Series(1, seasons: [Season(1, 10)])], [Episode(102, 1), Episode(103, 1, number: 2)]);

        // Act
        SeekerEvaluation evaluation = await EvaluateSonarrAsync();

        // Assert
        evaluation.Units.Select(u => u.EpisodeId).ShouldBe([102, 103], ignoreOrder: true);
    }

    [Fact]
    public async Task EvaluateAsync_Sonarr_AutoAndSeasonStillAiring_BuildsEpisodeUnits()
    {
        // Arrange
        SetupSonarr(
            [Series(1, seasons: [Season(1, 2, nextAiring: _timeProvider.GetUtcNow().AddDays(7))])],
            [Episode(102, 1), Episode(103, 1, number: 2)]);

        // Act
        SeekerEvaluation evaluation = await EvaluateSonarrAsync();

        // Assert
        evaluation.Units.Select(u => u.EpisodeId).ShouldBe([102, 103], ignoreOrder: true);
    }

    [Fact]
    public async Task EvaluateAsync_Sonarr_AutoAndWantedEpisodeUnmonitored_BuildsEpisodeUnits()
    {
        // Arrange
        SetupSonarr(
            [Series(1, seasons: [Season(1, 2)])],
            [Episode(102, 1), Episode(103, 1, number: 2) with { Monitored = false }]);

        // Act
        SeekerEvaluation evaluation = await EvaluateSonarrAsync(InstanceConfig(_sonarr, c => c.MonitoredOnly = false));

        // Assert
        evaluation.Units.Select(u => u.EpisodeId).ShouldBe([102, 103], ignoreOrder: true);
    }

    [Fact]
    public async Task EvaluateAsync_Sonarr_AutoAndSeasonMissingFromStatistics_BuildsEpisodeUnits()
    {
        // Arrange
        SetupSonarr([Series(1, seasons: [Season(2, 2)])], [Episode(102, 1), Episode(103, 1, number: 2)]);

        // Act
        SeekerEvaluation evaluation = await EvaluateSonarrAsync();

        // Assert
        evaluation.Units.Select(u => u.EpisodeId).ShouldBe([102, 103], ignoreOrder: true);
    }

    [Fact]
    public async Task EvaluateAsync_Sonarr_AutoAndFullDailySeason_BuildsDailySeasonUnit()
    {
        // Arrange
        SetupSonarr(
            [Series(1, seriesType: "daily", seasons: [Season(1, 2)])],
            [Episode(102, 1), Episode(103, 1, number: 2)]);

        // Act
        SeekerEvaluation evaluation = await EvaluateSonarrAsync();

        // Assert
        SeekerSearchUnit unit = evaluation.Units.ShouldHaveSingleItem();
        unit.EpisodeId.ShouldBe(0);
        unit.ContentKind.ShouldBe(SeekerContentKind.Daily);
    }

    [Fact]
    public async Task EvaluateAsync_Sonarr_AutoAndPartlyWantedDailySeason_BuildsEpisodeUnits()
    {
        // Arrange
        SetupSonarr(
            [Series(1, seriesType: "daily", seasons: [Season(1, 10)])],
            [Episode(102, 1), Episode(103, 1, number: 2)]);

        // Act
        SeekerEvaluation evaluation = await EvaluateSonarrAsync();

        // Assert
        evaluation.Units.Select(u => u.EpisodeId).ShouldBe([102, 103], ignoreOrder: true);
    }

    [Theory]
    [InlineData(SonarrSearchMode.Auto)]
    [InlineData(SonarrSearchMode.Episode)]
    public async Task EvaluateAsync_Sonarr_FinishedAnimeSeasonPartlyWanted_BuildsSeasonUnit(SonarrSearchMode mode)
    {
        // Arrange
        List<SearchableEpisode> wanted = [Episode(102, 1), Episode(103, 1, number: 2)];
        SetupSonarr([Series(1, seriesType: "anime", seasons: [Season(1, 10)])], wanted);
        _sonarrClient.GetEpisodesAsync(Arg.Any<ArrInstance>(), 1, Arg.Any<CancellationToken>())
            .Returns([..wanted, Episode(104, 1, number: 3)]);

        // Act
        SeekerEvaluation evaluation = await EvaluateSonarrAsync(InstanceConfig(_sonarr, c => c.SonarrSearchMode = mode));

        // Assert
        SeekerSearchUnit unit = evaluation.Units.ShouldHaveSingleItem();
        unit.EpisodeId.ShouldBe(0);
        unit.ContentKind.ShouldBe(SeekerContentKind.Anime);
    }

    [Fact]
    public async Task EvaluateAsync_Sonarr_SeasonModeAndSeasonPartlyWanted_BuildsSeasonUnit()
    {
        // Arrange
        SetupSonarr([Series(1, seasons: [Season(1, 10)])], [Episode(102, 1), Episode(103, 1, number: 2)]);

        // Act
        SeekerEvaluation evaluation = await EvaluateSonarrAsync(
            InstanceConfig(_sonarr, c => c.SonarrSearchMode = SonarrSearchMode.Season));

        // Assert
        evaluation.Units.ShouldHaveSingleItem().EpisodeId.ShouldBe(0);
    }

    [Fact]
    public async Task EvaluateAsync_Sonarr_EpisodeModeAndEveryAiredEpisodeWanted_BuildsEpisodeUnits()
    {
        // Arrange
        SetupSonarr([Series(1, seasons: [Season(1, 2)])], [Episode(102, 1), Episode(103, 1, number: 2)]);

        // Act
        SeekerEvaluation evaluation = await EvaluateSonarrAsync(
            InstanceConfig(_sonarr, c => c.SonarrSearchMode = SonarrSearchMode.Episode));

        // Assert
        evaluation.Units.Select(u => u.EpisodeId).ShouldBe([102, 103], ignoreOrder: true);
    }

    [Theory]
    [InlineData(SonarrSearchMode.Auto)]
    [InlineData(SonarrSearchMode.Season)]
    [InlineData(SonarrSearchMode.Episode)]
    public async Task EvaluateAsync_Sonarr_FullSpecialsSeason_BuildsEpisodeUnits(SonarrSearchMode mode)
    {
        // Arrange
        SetupSonarr([Series(1, seasons: [Season(0, 2)])], [Episode(102, 1, season: 0), Episode(103, 1, season: 0, number: 2)]);

        // Act
        SeekerEvaluation evaluation = await EvaluateSonarrAsync(InstanceConfig(_sonarr, c => c.SonarrSearchMode = mode));

        // Assert
        evaluation.Units.Select(u => u.EpisodeId).ShouldBe([102, 103], ignoreOrder: true);
        evaluation.Units.ShouldAllBe(u => u.ContentKind == SeekerContentKind.Special);
    }

    [Fact]
    public async Task EvaluateAsync_Sonarr_AutoAndSeasonSearchedThisCycle_MarksEpisodeUnitsSearched()
    {
        // Arrange
        SetupSonarr([Series(1, seasons: [Season(1, 10)])], [Episode(102, 1), Episode(103, 1, number: 2)]);

        // Act
        SeekerEvaluation evaluation = await EvaluateSonarrAsync(
            history: [History(_sonarr, 1, _timeProvider.GetUtcNow().AddDays(-1), seasonNumber: 1)]);

        // Assert
        evaluation.Units.Select(u => u.EpisodeId).ShouldBe([102, 103], ignoreOrder: true);
        evaluation.Units.ShouldAllBe(u => u.Searched);
        evaluation.Remaining.ShouldBeEmpty();
    }

    [Fact]
    public async Task EvaluateAsync_Sonarr_SeasonModeAndEpisodeSearchedThisCycle_ContinuesByEpisode()
    {
        // Arrange
        SetupSonarr(
            [Series(1, seasons: [Season(1, 10)])],
            [Episode(102, 1), Episode(103, 1, number: 2), Episode(104, 1, number: 3)]);

        // Act
        SeekerEvaluation evaluation = await EvaluateSonarrAsync(
            InstanceConfig(_sonarr, c => c.SonarrSearchMode = SonarrSearchMode.Season),
            history: [History(_sonarr, 1, _timeProvider.GetUtcNow().AddDays(-1), seasonNumber: 1, episodeId: 102)]);

        // Assert
        evaluation.Units.Select(u => u.EpisodeId).ShouldBe([102, 103, 104], ignoreOrder: true);
        evaluation.Remaining.Select(u => u.EpisodeId).ShouldBe([103, 104], ignoreOrder: true);
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
        evaluation.Remaining.Count().ShouldBe(4);
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

    [Fact]
    public async Task SelectNext_OldestSearchFirst_OrdersMoviesByTheArrLastSearchTime()
    {
        // Arrange
        DateTimeOffset now = _timeProvider.GetUtcNow();
        SetupRadarr(
        [
            Movie(1, lastSearchTime: now.AddDays(-1)),
            Movie(2),
            Movie(3, lastSearchTime: now.AddDays(-5)),
        ]);
        SeekerEvaluation evaluation = await EvaluateRadarrAsync();

        // Act
        List<SeekerSearchCandidate> candidates = SeekerUnitEvaluator.SelectNext(evaluation, SelectionStrategy.OldestSearchFirst, 3);

        // Assert
        candidates.Select(c => c.ItemId).ShouldBe([2L, 3L, 1L]);
    }

    [Fact]
    public async Task SelectNext_OldestSearchFirst_RanksSeriesByTheirLatestEpisodeSearch()
    {
        // Arrange
        DateTimeOffset now = _timeProvider.GetUtcNow();
        SetupSonarr([Series(1), Series(2)],
        [
            Episode(10, 1, season: 1, lastSearchTime: now.AddDays(-10)),
            Episode(11, 1, season: 2, lastSearchTime: now.AddDays(-1)),
            Episode(20, 2, season: 1, lastSearchTime: now.AddDays(-5)),
        ]);
        SeekerEvaluation evaluation = await EvaluateSonarrAsync();

        // Act
        List<SeekerSearchCandidate> candidates = SeekerUnitEvaluator.SelectNext(evaluation, SelectionStrategy.OldestSearchFirst, 1);

        // Assert
        candidates.ShouldHaveSingleItem().ItemId.ShouldBe(2);
    }

    [Fact]
    public async Task SelectNext_OldestSearchFirst_CycleSearchNewerThanTheArrTimeWins()
    {
        // Arrange
        DateTimeOffset now = _timeProvider.GetUtcNow();
        SetupSonarr([Series(1), Series(2)],
        [
            Episode(10, 1, season: 1, lastSearchTime: now.AddDays(-10)),
            Episode(11, 1, season: 2, lastSearchTime: now.AddDays(-10)),
            Episode(20, 2, season: 1, lastSearchTime: now.AddDays(-5)),
        ]);
        SeekerEvaluation evaluation = await EvaluateSonarrAsync(
            history: [History(_sonarr, 1, now.AddDays(-1), seasonNumber: 1)]);

        // Act
        List<SeekerSearchCandidate> candidates = SeekerUnitEvaluator.SelectNext(evaluation, SelectionStrategy.OldestSearchFirst, 1);

        // Assert
        candidates.ShouldHaveSingleItem().ItemId.ShouldBe(2);
    }

    [Fact]
    public async Task SelectNext_OldestSearchFirst_BreaksTiesAtRandom()
    {
        // Arrange
        SetupRadarr([Movie(1), Movie(2)]);
        SeekerEvaluation evaluation = await EvaluateRadarrAsync();
        HashSet<long> picked = [];

        // Act
        for (int i = 0; i < 64; i++)
        {
            picked.Add(SeekerUnitEvaluator.SelectNext(evaluation, SelectionStrategy.OldestSearchFirst, 1).Single().ItemId);
        }

        // Assert
        picked.ShouldBe([1L, 2L], ignoreOrder: true);
    }

    [Fact]
    public async Task SelectNext_CustomFormatGapWeighted_FavoursTheMissingMovie()
    {
        // Arrange
        SearchableMovie upgradable = Movie(2, hasFile: true);
        SetupRadarr([Movie(1), upgradable]);
        _radarrClient.GetCutoffUnmetMoviesAsync(Arg.Any<ArrInstance>(), true, Arg.Any<CancellationToken>())
            .Returns([upgradable]);
        SeekerEvaluation evaluation = await EvaluateRadarrAsync(InstanceConfig(_radarr, c => c.UseCutoff = true));

        // Act
        int missingPicks = Enumerable.Range(0, 1000)
            .Count(_ => SeekerUnitEvaluator.SelectNext(evaluation, SelectionStrategy.CustomFormatGapWeighted, 1)[0].ItemId == 1);

        // Assert
        // The missing movie takes 60% or 80% of the weight, depending on the shuffled recency rank
        missingPicks.ShouldBeGreaterThan(600);
    }

    [Fact]
    public async Task SelectNext_CustomFormatGapWeighted_Sonarr_SearchesTheLargestGapUnit()
    {
        // Arrange
        SetupSonarr([Series(1)], []);
        await AddSonarrCustomFormatEntryAsync(episodeId: 20, currentScore: 5, cutoffScore: 10);
        await AddSonarrCustomFormatEntryAsync(episodeId: 21, currentScore: 0, cutoffScore: 30);
        SeekerEvaluation evaluation = await EvaluateSonarrAsync(InstanceConfig(_sonarr, c =>
        {
            c.UseCustomFormatScore = true;
            c.SonarrSearchMode = SonarrSearchMode.Episode;
        }));

        // Act
        List<long> picked = Enumerable.Range(0, 50)
            .Select(_ => SeekerUnitEvaluator.SelectNext(evaluation, SelectionStrategy.CustomFormatGapWeighted, 1)[0].EpisodeId)
            .ToList();

        // Assert
        picked.ShouldAllBe(id => id == 21);
    }

    [Theory]
    [InlineData(SelectionStrategy.CustomFormatGapWeighted, false, SelectionStrategy.BalancedWeighted)]
    [InlineData(SelectionStrategy.CustomFormatGapWeighted, true, SelectionStrategy.CustomFormatGapWeighted)]
    [InlineData(SelectionStrategy.NewestFirst, false, SelectionStrategy.NewestFirst)]
    public void EffectiveStrategy_FallsBackToBalancedOnlyForTheGapStrategyWithoutCustomFormatScores(
        SelectionStrategy strategy, bool useCustomFormatScore, SelectionStrategy expected)
    {
        // Arrange
        SeekerInstanceConfig instanceConfig = InstanceConfig(_radarr, c => c.UseCustomFormatScore = useCustomFormatScore);

        // Act
        SelectionStrategy effective = SeekerUnitEvaluator.EffectiveStrategy(strategy, instanceConfig);

        // Assert
        effective.ShouldBe(expected);
    }

    [Fact]
    public async Task HasTiedPicks_OldestSearchFirst_NeverSearchedMovies_ReturnsTrue()
    {
        // Arrange
        SetupRadarr([Movie(1), Movie(2)]);
        SeekerEvaluation evaluation = await EvaluateRadarrAsync();

        // Act
        bool tied = SeekerUnitEvaluator.HasTiedPicks(evaluation, SelectionStrategy.OldestSearchFirst, new HashSet<long> { 1 });

        // Assert
        tied.ShouldBeTrue();
    }

    [Fact]
    public async Task HasTiedPicks_OldestSearchFirst_DistinctSearchTimes_ReturnsFalse()
    {
        // Arrange
        DateTimeOffset now = _timeProvider.GetUtcNow();
        SetupRadarr([Movie(1, lastSearchTime: now.AddDays(-1)), Movie(2, lastSearchTime: now.AddDays(-5))]);
        SeekerEvaluation evaluation = await EvaluateRadarrAsync();

        // Act
        bool tied = SeekerUnitEvaluator.HasTiedPicks(evaluation, SelectionStrategy.OldestSearchFirst, new HashSet<long> { 2 });

        // Assert
        tied.ShouldBeFalse();
    }

    [Fact]
    public async Task HasTiedPicks_NewestFirst_SameAddedDate_ReturnsTrue()
    {
        // Arrange
        DateTimeOffset added = _timeProvider.GetUtcNow().AddDays(-3);
        SetupRadarr([Movie(1, added: added), Movie(2, added: added)]);
        SeekerEvaluation evaluation = await EvaluateRadarrAsync();

        // Act
        bool tied = SeekerUnitEvaluator.HasTiedPicks(evaluation, SelectionStrategy.NewestFirst, new HashSet<long> { 1 });

        // Assert
        tied.ShouldBeTrue();
    }

    [Fact]
    public async Task HasTiedPicks_NewestFirst_DistinctAddedDates_ReturnsFalse()
    {
        // Arrange
        DateTimeOffset now = _timeProvider.GetUtcNow();
        SetupRadarr([Movie(1, added: now.AddDays(-3)), Movie(2, added: now.AddDays(-4))]);
        SeekerEvaluation evaluation = await EvaluateRadarrAsync();

        // Act
        bool tied = SeekerUnitEvaluator.HasTiedPicks(evaluation, SelectionStrategy.NewestFirst, new HashSet<long> { 1 });

        // Assert
        tied.ShouldBeFalse();
    }

    [Fact]
    public async Task EvaluateAsync_Sonarr_CustomFormatOnlyEpisode_TakesTheCachedLastSearchTime()
    {
        // Arrange
        DateTimeOffset lastSearchTime = _timeProvider.GetUtcNow().AddDays(-3);
        SetupSonarr([Series(1)], []);
        _sonarrClient.GetQualityProfilesAsync(Arg.Any<ArrInstance>()).Returns([]);
        _eventsContext.CustomFormatScoreEntries.Add(new CustomFormatScoreEntry
        {
            ArrInstanceId = _sonarr.Id,
            ExternalItemId = 1,
            EpisodeId = 20,
            Title = "Series 1 S01E01",
            ItemType = InstanceType.Sonarr,
            SeasonNumber = 1,
            AirDateUtc = _timeProvider.GetUtcNow().AddDays(-30),
            LastSearchTime = lastSearchTime,
            IsMonitored = true,
            CurrentScore = 5,
            CutoffScore = 10,
        });
        await _eventsContext.SaveChangesAsync();

        // Act
        SeekerEvaluation evaluation = await EvaluateSonarrAsync(InstanceConfig(_sonarr, c => c.UseCustomFormatScore = true));

        // Assert
        evaluation.Units.ShouldHaveSingleItem().LastSearchTime.ShouldBe(lastSearchTime);
    }

    [Fact]
    public async Task EvaluateAsync_Sonarr_SeasonUnit_TakesTheLargestEpisodeGap()
    {
        // Arrange
        SetupSonarr([Series(1)], []);
        await AddSonarrCustomFormatEntryAsync(episodeId: 20, currentScore: 5, cutoffScore: 10);
        await AddSonarrCustomFormatEntryAsync(episodeId: 21, currentScore: 0, cutoffScore: 30);

        // Act
        SeekerEvaluation evaluation = await EvaluateSonarrAsync(InstanceConfig(_sonarr, c =>
        {
            c.UseCustomFormatScore = true;
            c.SonarrSearchMode = SonarrSearchMode.Season;
        }));

        // Assert
        SeekerSearchUnit unit = evaluation.Units.ShouldHaveSingleItem();
        unit.EpisodeId.ShouldBe(0);
        unit.CustomFormatGap.ShouldBe(30);
    }

    [Fact]
    public async Task EvaluateAsync_Sonarr_EpisodeUnits_TakeTheirOwnGap()
    {
        // Arrange
        SetupSonarr([Series(1)], []);
        await AddSonarrCustomFormatEntryAsync(episodeId: 20, currentScore: 5, cutoffScore: 10);
        await AddSonarrCustomFormatEntryAsync(episodeId: 21, currentScore: 0, cutoffScore: 30);

        // Act
        SeekerEvaluation evaluation = await EvaluateSonarrAsync(InstanceConfig(_sonarr, c =>
        {
            c.UseCustomFormatScore = true;
            c.SonarrSearchMode = SonarrSearchMode.Episode;
        }));

        // Assert
        evaluation.Units.Single(u => u.EpisodeId == 20).CustomFormatGap.ShouldBe(5);
        evaluation.Units.Single(u => u.EpisodeId == 21).CustomFormatGap.ShouldBe(30);
    }

    [Fact]
    public async Task EvaluateAsync_Sonarr_QualityCutoffOnlyEpisode_HasNoGap()
    {
        // Arrange
        SetupSonarr([Series(1)], []);
        _sonarrClient.GetCutoffUnmetEpisodesAsync(Arg.Any<ArrInstance>(), true, Arg.Any<CancellationToken>())
            .Returns([Episode(20, 1), Episode(21, 1, number: 2)]);
        await AddSonarrCustomFormatEntryAsync(episodeId: 20, currentScore: 5, cutoffScore: 10);

        // Act
        SeekerEvaluation evaluation = await EvaluateSonarrAsync(InstanceConfig(_sonarr, c =>
        {
            c.UseCutoff = true;
            c.UseCustomFormatScore = true;
            c.SonarrSearchMode = SonarrSearchMode.Episode;
        }));

        // Assert
        evaluation.Units.Single(u => u.EpisodeId == 20).CustomFormatGap.ShouldBe(5);
        evaluation.Units.Single(u => u.EpisodeId == 21).CustomFormatGap.ShouldBe(0);
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
        fresh.Remaining.Count().ShouldBe(2);
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
    public async Task WithoutCycleHistory_KeepsTheArrLastSearchTime()
    {
        // Arrange
        DateTimeOffset lastSearchTime = _timeProvider.GetUtcNow().AddDays(-4);
        SetupRadarr([Movie(1, lastSearchTime: lastSearchTime)]);
        SeekerEvaluation evaluation = await EvaluateRadarrAsync(
            history: [History(_radarr, 1, _timeProvider.GetUtcNow().AddDays(-1))]);

        // Act
        SeekerEvaluation fresh = evaluation.WithoutCycleHistory();

        // Assert
        fresh.Units.ShouldHaveSingleItem().LastSearchTime.ShouldBe(lastSearchTime);
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

    #region Resting

    private static readonly DateTimeOffset RestNow = new(2026, 1, 10, 12, 0, 0, TimeSpan.Zero);

    private static SeekerHistory Missed(int misses, DateTimeOffset searchedAt)
    {
        return new SeekerHistory
        {
            ArrInstanceId = Guid.NewGuid(),
            ExternalItemId = 1,
            ConsecutiveMisses = misses,
            LastSearchedAt = searchedAt,
        };
    }

    private async Task SeedMissesAsync(
        ArrInstance instance,
        long itemId,
        int misses,
        DateTimeOffset searchedAt,
        int seasonNumber = 0,
        long episodeId = 0,
        bool isDryRun = false)
    {
        await _eventsContext.SeekerHistory.AddAsync(History(instance, itemId, searchedAt, seasonNumber, episodeId) with
        {
            ConsecutiveMisses = misses,
            IsDryRun = isDryRun,
        });
        await _eventsContext.SaveChangesAsync();
    }

    private async Task SeedCycleStartsAsync(ArrInstance instance, params DateTimeOffset[] startedAt)
    {
        foreach (DateTimeOffset start in startedAt)
        {
            await _eventsContext.SeekerCycles.AddAsync(new SeekerCycle { ArrInstanceId = instance.Id, StartedAt = start });
        }

        await _eventsContext.SaveChangesAsync();
    }

    [Fact]
    public void GetRest_TwoMisses_ReturnsNull()
    {
        // Arrange
        SeekerHistory unit = Missed(2, RestNow.AddDays(-2));

        // Act
        SeekerUnitRest? rest = SeekerUnitEvaluator.GetRest(unit, [], 30, RestNow);

        // Assert
        rest.ShouldBeNull();
    }

    [Fact]
    public void GetRest_ThreeMissesAndNoCycleSince_RestsForTwoMoreCycleEnds()
    {
        // Arrange
        SeekerHistory unit = Missed(3, RestNow.AddDays(-2));

        // Act
        SeekerUnitRest? rest = SeekerUnitEvaluator.GetRest(unit, [], 30, RestNow);

        // Assert
        rest.ShouldNotBeNull();
        rest.ConsecutiveMisses.ShouldBe(3);
        rest.CyclesLeft.ShouldBe(2);
        rest.Until.ShouldBe(unit.LastSearchedAt.AddDays(30));
    }

    [Fact]
    public void GetRest_LastMissAtSet_AnchorsTheRestOnTheMiss()
    {
        // Arrange
        SeekerHistory unit = Missed(3, RestNow.AddDays(-1));
        unit.LastMissAt = RestNow.AddDays(-5);

        // Act
        SeekerUnitRest? rest = SeekerUnitEvaluator.GetRest(unit, [RestNow.AddDays(-3)], 30, RestNow);

        // Assert
        rest.ShouldNotBeNull();
        rest.CyclesLeft.ShouldBe(1);
        rest.Until.ShouldBe(unit.LastMissAt.Value.AddDays(30));
    }

    [Fact]
    public void GetRest_LastMissAtPastRestMaxDays_ReturnsNull()
    {
        // Arrange
        SeekerHistory unit = Missed(3, RestNow.AddDays(-1));
        unit.LastMissAt = RestNow.AddDays(-31);

        // Act
        SeekerUnitRest? rest = SeekerUnitEvaluator.GetRest(unit, [], 30, RestNow);

        // Assert
        rest.ShouldBeNull();
    }

    [Fact]
    public void GetRest_ThreeMissesAndOneCycleSince_RestsForOneMoreCycleEnd()
    {
        // Arrange
        SeekerHistory unit = Missed(3, RestNow.AddDays(-2));

        // Act
        SeekerUnitRest? rest = SeekerUnitEvaluator.GetRest(unit, [RestNow.AddDays(-1)], 30, RestNow);

        // Assert
        rest.ShouldNotBeNull();
        rest.CyclesLeft.ShouldBe(1);
    }

    [Fact]
    public void GetRest_ThreeMissesAndTwoCyclesSince_ReturnsNull()
    {
        // Arrange
        SeekerHistory unit = Missed(3, RestNow.AddDays(-2));

        // Act
        SeekerUnitRest? rest = SeekerUnitEvaluator.GetRest(unit, [RestNow.AddDays(-1), RestNow.AddHours(-1)], 30, RestNow);

        // Assert
        rest.ShouldBeNull();
    }

    [Theory]
    [InlineData(3, 2)]
    [InlineData(4, 3)]
    [InlineData(5, 5)]
    [InlineData(6, 9)]
    [InlineData(10, 9)]
    public void GetRest_RestDoublesPerMissAndCapsAtEightCycles(int misses, int expectedCyclesLeft)
    {
        // Arrange
        SeekerHistory unit = Missed(misses, RestNow.AddDays(-2));

        // Act
        SeekerUnitRest? rest = SeekerUnitEvaluator.GetRest(unit, [], 30, RestNow);

        // Assert
        rest.ShouldNotBeNull();
        rest.CyclesLeft.ShouldBe(expectedCyclesLeft);
    }

    [Fact]
    public void GetRest_DayCapPassedWithCyclesLeft_ReturnsNull()
    {
        // Arrange
        SeekerHistory unit = Missed(6, RestNow.AddDays(-5));

        // Act
        SeekerUnitRest? rest = SeekerUnitEvaluator.GetRest(unit, [], 5, RestNow);

        // Assert
        rest.ShouldBeNull();
    }

    [Fact]
    public void GetRest_CycleStartEqualToLastSearchedAt_DoesNotCount()
    {
        // Arrange
        SeekerHistory unit = Missed(3, RestNow.AddDays(-2));

        // Act
        SeekerUnitRest? rest = SeekerUnitEvaluator.GetRest(unit, [unit.LastSearchedAt], 30, RestNow);

        // Assert
        rest.ShouldNotBeNull();
        rest.CyclesLeft.ShouldBe(2);
    }

    [Fact]
    public async Task EvaluateAsync_Radarr_RestingMovie_ExcludesAsRestingWithRestDetail()
    {
        // Arrange
        SetupRadarr([Movie(1), Movie(2)]);
        await SeedMissesAsync(_radarr, 1, 3, _timeProvider.GetUtcNow().AddDays(-2));

        // Act
        SeekerEvaluation evaluation = await EvaluateRadarrAsync(InstanceConfig(_radarr, c => c.RestUnitsEnabled = true));

        // Assert
        SeekerExclusion exclusion = evaluation.Excluded.Single(e => e.Name == "Movie 1");
        exclusion.Reason.ShouldBe(SeekerExclusionReason.Resting);
        exclusion.Rest.ShouldNotBeNull();
        exclusion.Rest.ConsecutiveMisses.ShouldBe(3);
        exclusion.Rest.CyclesLeft.ShouldBe(2);
        evaluation.Remaining.ShouldHaveSingleItem().ItemId.ShouldBe(2);
        evaluation.RestingUnits.ShouldBe(1);
    }

    [Fact]
    public async Task EvaluateAsync_Radarr_RestingAndQueued_QueueWins()
    {
        // Arrange
        SetupRadarr([Movie(1)]);
        await SeedMissesAsync(_radarr, 1, 3, _timeProvider.GetUtcNow().AddDays(-2));

        // Act
        SeekerEvaluation evaluation = await EvaluateRadarrAsync(
            InstanceConfig(_radarr, c => c.RestUnitsEnabled = true),
            queue: [Queued(movieId: 1)]);

        // Assert
        ReasonOf(evaluation, "Movie 1").ShouldBe(SeekerExclusionReason.InQueue);
        evaluation.RestingUnits.ShouldBe(0);
    }

    [Fact]
    public async Task EvaluateAsync_Radarr_RestingAndRecentlyGrabbed_RecentlyGrabbedWins()
    {
        // Arrange
        SetupRadarr([Movie(1)]);
        await SeedMissesAsync(_radarr, 1, 3, _timeProvider.GetUtcNow().AddDays(-2));

        // Act
        SeekerEvaluation evaluation = await EvaluateRadarrAsync(
            InstanceConfig(_radarr, c => c.RestUnitsEnabled = true),
            recentGrabs: new SeekerRecentGrabs { MovieIds = [1] });

        // Assert
        ReasonOf(evaluation, "Movie 1").ShouldBe(SeekerExclusionReason.RecentlyGrabbed);
        evaluation.RestingUnits.ShouldBe(0);
    }

    [Fact]
    public async Task EvaluateAsync_Radarr_RestingDisabled_StaysSearchable()
    {
        // Arrange
        SetupRadarr([Movie(1)]);
        await SeedMissesAsync(_radarr, 1, 5, _timeProvider.GetUtcNow().AddDays(-2));

        // Act
        SeekerEvaluation evaluation = await EvaluateRadarrAsync(InstanceConfig(_radarr));

        // Assert
        evaluation.Remaining.ShouldHaveSingleItem().ItemId.ShouldBe(1);
        evaluation.RestingUnits.ShouldBe(0);
    }

    [Fact]
    public async Task EvaluateAsync_Radarr_DryRunHistoryRow_IsIgnored()
    {
        // Arrange
        SetupRadarr([Movie(1)]);
        await SeedMissesAsync(_radarr, 1, 5, _timeProvider.GetUtcNow().AddDays(-2), isDryRun: true);

        // Act
        SeekerEvaluation evaluation = await EvaluateRadarrAsync(InstanceConfig(_radarr, c => c.RestUnitsEnabled = true));

        // Assert
        evaluation.Remaining.ShouldHaveSingleItem().ItemId.ShouldBe(1);
        evaluation.RestingUnits.ShouldBe(0);
    }

    [Fact]
    public async Task EvaluateAsync_Radarr_RestEndsAfterEnoughCycles_StaysSearchable()
    {
        // Arrange
        SetupRadarr([Movie(1)]);
        DateTimeOffset searchedAt = _timeProvider.GetUtcNow().AddDays(-3);
        await SeedMissesAsync(_radarr, 1, 3, searchedAt);
        await SeedCycleStartsAsync(_radarr, searchedAt.AddDays(1), searchedAt.AddDays(2));

        // Act
        SeekerEvaluation evaluation = await EvaluateRadarrAsync(InstanceConfig(_radarr, c => c.RestUnitsEnabled = true));

        // Assert
        evaluation.Remaining.ShouldHaveSingleItem().ItemId.ShouldBe(1);
        evaluation.RestingUnits.ShouldBe(0);
    }

    [Fact]
    public async Task EvaluateAsync_Radarr_DayCapPassed_UnitIsBackInRemaining()
    {
        // Arrange
        SetupRadarr([Movie(1)]);
        await SeedMissesAsync(_radarr, 1, 8, _timeProvider.GetUtcNow().AddDays(-2));

        // Act
        SeekerEvaluation evaluation = await EvaluateRadarrAsync(
            InstanceConfig(_radarr, c =>
            {
                c.RestUnitsEnabled = true;
                c.RestMaxDays = 1;
            }));

        // Assert
        evaluation.Remaining.ShouldHaveSingleItem().ItemId.ShouldBe(1);
        evaluation.RestingUnits.ShouldBe(0);
    }

    [Fact]
    public async Task EvaluateAsync_Sonarr_RestingSeasonUnit_ExcludesAsResting()
    {
        // Arrange
        SetupSonarr([Series(1)], [Episode(10, 1), Episode(20, 1, season: 2)]);
        await SeedMissesAsync(_sonarr, 1, 3, _timeProvider.GetUtcNow().AddDays(-2), seasonNumber: 1);

        // Act
        SeekerEvaluation evaluation = await EvaluateSonarrAsync(
            InstanceConfig(_sonarr, c =>
            {
                c.SonarrSearchMode = SonarrSearchMode.Season;
                c.RestUnitsEnabled = true;
            }));

        // Assert
        SeekerExclusion exclusion = evaluation.Excluded.Single(e => e.Name == "Series 1 S01");
        exclusion.Reason.ShouldBe(SeekerExclusionReason.Resting);
        exclusion.Rest.ShouldNotBeNull();
        evaluation.Remaining.ShouldHaveSingleItem().SeasonNumber.ShouldBe(2);
        evaluation.RestingUnits.ShouldBe(1);
    }

    [Fact]
    public async Task EvaluateAsync_Sonarr_RestingEpisodeUnit_RestsByExactEpisodeId()
    {
        // Arrange
        SetupSonarr([Series(1, seasons: [Season(1, 2)])], [Episode(102, 1), Episode(103, 1, number: 2)]);
        await SeedMissesAsync(_sonarr, 1, 3, _timeProvider.GetUtcNow().AddDays(-2), seasonNumber: 1, episodeId: 102);

        // Act
        SeekerEvaluation evaluation = await EvaluateSonarrAsync(
            InstanceConfig(_sonarr, c =>
            {
                c.SonarrSearchMode = SonarrSearchMode.Episode;
                c.RestUnitsEnabled = true;
            }));

        // Assert
        evaluation.Units.Single(u => u.EpisodeId == 102).BlockedBy.ShouldBe(SeekerExclusionReason.Resting);
        evaluation.Units.Single(u => u.EpisodeId == 103).BlockedBy.ShouldBeNull();
        evaluation.Remaining.ShouldHaveSingleItem().EpisodeId.ShouldBe(103);
        evaluation.RestingUnits.ShouldBe(1);
    }

    [Fact]
    public async Task EvaluateAsync_Sonarr_DayCapPassed_UnitIsBackInRemaining()
    {
        // Arrange
        SetupSonarr([Series(1)], [Episode(10, 1)]);
        await SeedMissesAsync(_sonarr, 1, 8, _timeProvider.GetUtcNow().AddDays(-2), seasonNumber: 1);

        // Act
        SeekerEvaluation evaluation = await EvaluateSonarrAsync(
            InstanceConfig(_sonarr, c =>
            {
                c.SonarrSearchMode = SonarrSearchMode.Season;
                c.RestUnitsEnabled = true;
                c.RestMaxDays = 1;
            }));

        // Assert
        evaluation.Remaining.ShouldHaveSingleItem().SeasonNumber.ShouldBe(1);
        evaluation.RestingUnits.ShouldBe(0);
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

    #region Whisparr v3

    [Fact]
    public async Task EvaluateAsync_WhisparrV3_UsesTheWhisparrClientAndNeverTheRadarrClient()
    {
        // Arrange
        _whisparrV3Client.GetAllTagsAsync(Arg.Any<ArrInstance>()).Returns([new Tag { Id = 7, Label = "skip" }]);
        _whisparrV3Client.GetCutoffUnmetMoviesAsync(Arg.Any<ArrInstance>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns([Movie(2, hasFile: true)]);
        SetupWhisparr([Movie(1, tags: [7]), Movie(2, hasFile: true), Movie(3)]);

        // Act
        SeekerEvaluation evaluation = await EvaluateWhisparrAsync(InstanceConfig(_whisparr, c =>
        {
            c.SkipTags = ["skip"];
            c.UseCutoff = true;
        }));

        // Assert
        ReasonOf(evaluation, "Movie 1").ShouldBe(SeekerExclusionReason.SkipTag);
        evaluation.Units.Select(u => u.ItemId).Order().ShouldBe([2L, 3L]);
        await _whisparrV3Client.Received().GetMissingMoviesAsync(_whisparr, true, Arg.Any<CancellationToken>());
        await _whisparrV3Client.Received().GetCutoffUnmetMoviesAsync(_whisparr, true, Arg.Any<CancellationToken>());
        _radarrClient.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task ReadRecentGrabsAsync_WhisparrV3_ReadsMovieIdsFromTheWhisparrClient()
    {
        // Arrange
        _whisparrV3Client.GetRecentlyGrabbedMovieIdsAsync(_whisparr, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(new HashSet<long> { 4, 9 });

        // Act
        SeekerRecentGrabs grabs = await _sut.ReadRecentGrabsAsync(
            InstanceConfig(_whisparr, c => c.RecentGrabWindowHours = 12), _whisparr, CancellationToken.None);

        // Assert
        grabs.MovieIds.ShouldBe([4L, 9L], ignoreOrder: true);
        await _radarrClient.DidNotReceiveWithAnyArgs().GetRecentlyGrabbedMovieIdsAsync(default!, default, default);
    }

    [Fact]
    public async Task EvaluateAsync_WhisparrV3_BelowCustomFormatCutoff_UsesWhisparrScoreEntries()
    {
        // Arrange
        SetupWhisparr([Movie(1, hasFile: true, qualityProfileId: 4)]);
        _whisparrV3Client.GetQualityProfilesAsync(Arg.Any<ArrInstance>())
            .Returns([new ArrQualityProfile { Id = 4, Name = "Open", UpgradeAllowed = true }]);
        _eventsContext.CustomFormatScoreEntries.Add(new CustomFormatScoreEntry
        {
            ArrInstanceId = _whisparr.Id,
            ExternalItemId = 1,
            ItemType = InstanceType.Whisparr,
            CurrentScore = 5,
            CutoffScore = 10,
        });
        await _eventsContext.SaveChangesAsync();

        // Act
        SeekerEvaluation evaluation = await EvaluateWhisparrAsync(InstanceConfig(_whisparr, c => c.UseCustomFormatScore = true));

        // Assert
        SeekerSearchUnit unit = evaluation.Units.ShouldHaveSingleItem();
        unit.Reasons.ShouldBe([SeekerSearchReason.CustomFormatScoreBelowCutoff]);
    }

    [Fact]
    public async Task EvaluateAsync_WhisparrV3_RadarrTypedScoreEntries_AreIgnored()
    {
        // Arrange
        SetupWhisparr([Movie(1, hasFile: true, qualityProfileId: 4)]);
        _whisparrV3Client.GetQualityProfilesAsync(Arg.Any<ArrInstance>())
            .Returns([new ArrQualityProfile { Id = 4, Name = "Open", UpgradeAllowed = true }]);
        _eventsContext.CustomFormatScoreEntries.Add(new CustomFormatScoreEntry
        {
            ArrInstanceId = _whisparr.Id,
            ExternalItemId = 1,
            ItemType = InstanceType.Radarr,
            CurrentScore = 5,
            CutoffScore = 10,
        });
        await _eventsContext.SaveChangesAsync();

        // Act
        SeekerEvaluation evaluation = await EvaluateWhisparrAsync(InstanceConfig(_whisparr, c => c.UseCustomFormatScore = true));

        // Assert
        evaluation.Units.ShouldBeEmpty();
        ReasonOf(evaluation, "Movie 1").ShouldBe(SeekerExclusionReason.NothingToSearch);
    }

    [Fact]
    public async Task EvaluateAsync_WhisparrV3_StatusNotReleased_ExcludesAsNotReleased()
    {
        // Arrange
        SetupWhisparr([Movie(1, status: "announced")]);

        // Act
        SeekerEvaluation evaluation = await EvaluateWhisparrAsync();

        // Assert
        ReasonOf(evaluation, "Movie 1").ShouldBe(SeekerExclusionReason.NotReleased);
        evaluation.Units.ShouldBeEmpty();
    }

    [Fact]
    public async Task EvaluateAsync_WhisparrV3_ReleaseDateInsideGraceWindow_ExcludesAsInGracePeriod()
    {
        // Arrange
        SetupWhisparr([Movie(1, releaseDate: _timeProvider.GetUtcNow().AddHours(-1))]);

        // Act
        SeekerEvaluation evaluation = await EvaluateWhisparrAsync();

        // Assert
        ReasonOf(evaluation, "Movie 1").ShouldBe(SeekerExclusionReason.InGracePeriod);
    }

    [Fact]
    public async Task EvaluateAsync_WhisparrV3_ReleaseDatePastGraceWindow_BuildsUnit()
    {
        // Arrange
        SetupWhisparr([Movie(1, releaseDate: _timeProvider.GetUtcNow().AddDays(-30))]);

        // Act
        SeekerEvaluation evaluation = await EvaluateWhisparrAsync();

        // Assert
        evaluation.Units.ShouldHaveSingleItem().Name.ShouldBe("Movie 1");
    }

    [Fact]
    public async Task EvaluateAsync_WhisparrV3_ArrAvailabilityGateConfigured_IsIgnored()
    {
        // Arrange
        SetupWhisparr([Movie(1, releaseDate: _timeProvider.GetUtcNow().AddDays(-30), isAvailable: false)]);

        // Act
        SeekerEvaluation evaluation = await EvaluateWhisparrAsync(
            InstanceConfig(_whisparr, c => c.RadarrReleaseGate = RadarrReleaseGate.ArrAvailability));

        // Assert
        evaluation.Units.ShouldHaveSingleItem().Name.ShouldBe("Movie 1");
    }

    [Fact]
    public async Task EvaluateAsync_WhisparrV3_WantedEndpointNotFound_FlagsWantedUnsupported()
    {
        // Arrange
        SetupWhisparr([Movie(1), Movie(2)]);
        _whisparrV3Client.GetMissingMoviesAsync(Arg.Any<ArrInstance>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Not found", null, HttpStatusCode.NotFound));

        // Act
        SeekerEvaluation evaluation = await EvaluateWhisparrAsync();

        // Assert
        evaluation.WantedUnsupported.ShouldBeTrue();
        evaluation.Units.ShouldBeEmpty();
    }

    [Fact]
    public async Task EvaluateAsync_Radarr_ReleaseDateInsideGraceWindowButDigitalReleasePast_BuildsUnit()
    {
        // Arrange
        SetupRadarr([Movie(
            1,
            digitalRelease: _timeProvider.GetUtcNow().AddDays(-30),
            releaseDate: _timeProvider.GetUtcNow().AddHours(-1))]);

        // Act
        SeekerEvaluation evaluation = await EvaluateRadarrAsync();

        // Assert
        evaluation.Units.ShouldHaveSingleItem().Name.ShouldBe("Movie 1");
    }

    #endregion

    #region Whisparr v2

    private static readonly DateTimeOffset SceneDate = new(2024, 5, 12, 0, 0, 0, TimeSpan.Zero);

    private static SearchableSeries Site(params SeriesSeason[] years)
    {
        return Series(1, seasons: [..years]) with { Title = "Site" };
    }

    private SearchableEpisode Scene(long id, DateTimeOffset? releaseDate = null, bool noReleaseDate = false)
    {
        return Episode(id, 1, season: 2024, number: 0, airDate: releaseDate ?? SceneDate, noAirDate: noReleaseDate);
    }

    private ArrInstance SetupWhisparrV2(List<SearchableSeries> library, List<SearchableEpisode> missing)
    {
        ArrInstance instance = TestDataContextFactory.AddWhisparrInstance(_dataContext, version: 2);
        _whisparrV2Client.GetAllTagsAsync(Arg.Any<ArrInstance>()).Returns([]);
        _whisparrV2Client.GetQualityProfilesAsync(Arg.Any<ArrInstance>()).Returns([]);
        _whisparrV2Client.StreamAllSeriesAsync(Arg.Any<ArrInstance>(), Arg.Any<CancellationToken>())
            .Returns(_ => ToAsyncEnumerable(library));
        _whisparrV2Client.GetMissingEpisodesAsync(Arg.Any<ArrInstance>(), true, Arg.Any<CancellationToken>())
            .Returns(missing.Where(e => e.Monitored).ToList());
        _whisparrV2Client.GetMissingEpisodesAsync(Arg.Any<ArrInstance>(), false, Arg.Any<CancellationToken>())
            .Returns(missing.Where(e => !e.Monitored).ToList());
        _whisparrV2Client.GetCutoffUnmetEpisodesAsync(Arg.Any<ArrInstance>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns([]);
        return instance;
    }

    private Task<SeekerEvaluation> EvaluateWhisparrV2Async(ArrInstance instance, Action<SeekerInstanceConfig>? configure = null)
    {
        return _sut.EvaluateAsync(
            _config,
            InstanceConfig(instance, configure),
            instance,
            [],
            new SeekerRecentGrabs(),
            [],
            CancellationToken.None);
    }

    [Fact]
    public async Task EvaluateAsync_WhisparrV2_ReadsThroughTheWhisparrV2ClientAndNeverTheSonarrClient()
    {
        // Arrange
        ArrInstance instance = SetupWhisparrV2([Site(Season(2024, 1))], [Scene(10)]);

        // Act
        SeekerEvaluation evaluation = await EvaluateWhisparrV2Async(instance, c => c.UseCutoff = true);

        // Assert
        evaluation.Units.ShouldHaveSingleItem();
        await _whisparrV2Client.Received().GetMissingEpisodesAsync(instance, true, Arg.Any<CancellationToken>());
        await _whisparrV2Client.Received().GetCutoffUnmetEpisodesAsync(instance, true, Arg.Any<CancellationToken>());
        _sonarrClient.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task EvaluateAsync_WhisparrV2_SceneReleasedInsideGraceWindow_ExcludesAsInGracePeriod()
    {
        // Arrange
        _config.PostReleaseGraceHours = 24;
        DateTimeOffset releaseDate = new(2026, 1, 10, 0, 0, 0, TimeSpan.Zero);
        ArrInstance instance = SetupWhisparrV2([Site(Season(2024, 1))], [Scene(10, releaseDate)]);

        // Act
        SeekerEvaluation evaluation = await EvaluateWhisparrV2Async(instance);

        // Assert
        ReasonOf(evaluation, "Site 2026-01-10").ShouldBe(SeekerExclusionReason.InGracePeriod);
        evaluation.Units.ShouldBeEmpty();
    }

    [Fact]
    public async Task EvaluateAsync_WhisparrV2_SceneWithoutReleaseDate_ExcludesAsNotReleased()
    {
        // Arrange
        ArrInstance instance = SetupWhisparrV2([Site(Season(2024, 1))], [Scene(10, noReleaseDate: true)]);

        // Act
        SeekerEvaluation evaluation = await EvaluateWhisparrV2Async(instance);

        // Assert
        ReasonOf(evaluation, "Site 2024").ShouldBe(SeekerExclusionReason.NotReleased);
        evaluation.Units.ShouldBeEmpty();
    }

    [Fact]
    public async Task EvaluateAsync_WhisparrV2_AutoAndEverySceneOfThePastYearWanted_BuildsYearUnit()
    {
        // Arrange
        ArrInstance instance = SetupWhisparrV2([Site(Season(2024, 2))], [Scene(10), Scene(11, SceneDate.AddDays(1))]);

        // Act
        SeekerEvaluation evaluation = await EvaluateWhisparrV2Async(instance);

        // Assert
        SeekerSearchUnit unit = evaluation.Units.ShouldHaveSingleItem();
        unit.EpisodeId.ShouldBe(0);
        unit.SeasonNumber.ShouldBe(2024);
        unit.Name.ShouldBe("Site 2024");
    }

    [Fact]
    public async Task EvaluateAsync_WhisparrV2_AutoAndYearPartlyWanted_BuildsSceneUnitsNamedByDate()
    {
        // Arrange
        ArrInstance instance = SetupWhisparrV2([Site(Season(2024, 5))], [Scene(10), Scene(11, SceneDate.AddDays(1))]);

        // Act
        SeekerEvaluation evaluation = await EvaluateWhisparrV2Async(instance);

        // Assert
        evaluation.Units.Select(u => u.Name).ShouldBe(["Site 2024-05-12", "Site 2024-05-13"], ignoreOrder: true);
        evaluation.Units.Select(u => u.EpisodeId).ShouldBe([10, 11], ignoreOrder: true);
    }

    [Fact]
    public async Task EvaluateAsync_WhisparrV2_EpisodeModeAndEverySceneWanted_BuildsOneUnitPerScene()
    {
        // Arrange
        ArrInstance instance = SetupWhisparrV2([Site(Season(2024, 2))], [Scene(10), Scene(11, SceneDate.AddDays(1))]);

        // Act
        SeekerEvaluation evaluation = await EvaluateWhisparrV2Async(instance, c => c.SonarrSearchMode = SonarrSearchMode.Episode);

        // Assert
        evaluation.Units.Select(u => u.EpisodeId).ShouldBe([10, 11], ignoreOrder: true);
    }

    [Fact]
    public async Task EvaluateAsync_WhisparrV2_CustomFormatEntries_ReadsWhisparrRowsOnly()
    {
        // Arrange
        ArrInstance instance = SetupWhisparrV2([Site(Season(2024, 5))], []);
        await _eventsContext.CustomFormatScoreEntries.AddAsync(new CustomFormatScoreEntry
        {
            ArrInstanceId = instance.Id,
            ExternalItemId = 1,
            EpisodeId = 20,
            Title = "Site 2024-05-12",
            ItemType = InstanceType.Whisparr,
            SeasonNumber = 2024,
            AirDateUtc = SceneDate,
            IsMonitored = true,
            CurrentScore = 5,
            CutoffScore = 10,
        });
        await _eventsContext.SaveChangesAsync();
        await AddSonarrCustomFormatEntryAsync(episodeId: 21, currentScore: 0, cutoffScore: 30);

        // Act
        SeekerEvaluation evaluation = await EvaluateWhisparrV2Async(instance, c => c.UseCustomFormatScore = true);

        // Assert
        SeekerSearchUnit unit = evaluation.Units.ShouldHaveSingleItem();
        unit.EpisodeId.ShouldBe(20);
        unit.Name.ShouldBe("Site 2024-05-12");
        unit.CustomFormatGap.ShouldBe(5);
    }

    #endregion

    #region Lidarr

    private static SearchableArtist Artist(
        long id,
        bool monitored = true,
        List<long>? tags = null,
        int qualityProfileId = 0,
        DateTimeOffset? added = null)
    {
        return new SearchableArtist
        {
            Id = id,
            ArtistName = $"Artist {id}",
            Monitored = monitored,
            Tags = tags ?? [],
            QualityProfileId = qualityProfileId,
            Added = added,
        };
    }

    private SearchableAlbum Album(
        long id,
        long artistId,
        bool monitored = true,
        DateTimeOffset? releaseDate = null,
        bool noReleaseDate = false,
        DateTimeOffset? lastSearchTime = null)
    {
        return new SearchableAlbum
        {
            Id = id,
            Title = $"Album {id}",
            ArtistId = artistId,
            Monitored = monitored,
            ReleaseDate = noReleaseDate ? null : releaseDate ?? _timeProvider.GetUtcNow().AddDays(-30),
            LastSearchTime = lastSearchTime,
        };
    }

    private void SetupLidarr(List<SearchableArtist> library, List<SearchableAlbum> missing, List<SearchableAlbum>? cutoffUnmet = null)
    {
        _lidarrClient.StreamAllArtistsAsync(Arg.Any<ArrInstance>(), Arg.Any<CancellationToken>())
            .Returns(_ => ToAsyncEnumerable(library));
        _lidarrClient.GetMissingAlbumsAsync(Arg.Any<ArrInstance>(), true, Arg.Any<CancellationToken>())
            .Returns(missing.Where(a => a.Monitored).ToList());
        _lidarrClient.GetMissingAlbumsAsync(Arg.Any<ArrInstance>(), false, Arg.Any<CancellationToken>())
            .Returns(missing.Where(a => !a.Monitored).ToList());

        if (cutoffUnmet is not null)
        {
            _lidarrClient.GetCutoffUnmetAlbumsAsync(Arg.Any<ArrInstance>(), true, Arg.Any<CancellationToken>())
                .Returns(cutoffUnmet.Where(a => a.Monitored).ToList());
            _lidarrClient.GetCutoffUnmetAlbumsAsync(Arg.Any<ArrInstance>(), false, Arg.Any<CancellationToken>())
                .Returns(cutoffUnmet.Where(a => !a.Monitored).ToList());
        }
    }

    private Task<SeekerEvaluation> EvaluateLidarrAsync(
        SeekerInstanceConfig? instanceConfig = null,
        List<QueueRecord>? queue = null,
        SeekerRecentGrabs? recentGrabs = null,
        List<SeekerHistory>? history = null)
    {
        return _sut.EvaluateAsync(
            _config,
            instanceConfig ?? InstanceConfig(_lidarr),
            _lidarr,
            queue ?? [],
            recentGrabs ?? new SeekerRecentGrabs(),
            history ?? [],
            CancellationToken.None);
    }

    private async Task AddLidarrCustomFormatEntryAsync(
        long artistId,
        long albumId,
        int currentScore,
        int cutoffScore,
        bool monitored = true,
        DateTimeOffset? airDate = null)
    {
        await _eventsContext.CustomFormatScoreEntries.AddAsync(new CustomFormatScoreEntry
        {
            ArrInstanceId = _lidarr.Id,
            ExternalItemId = artistId,
            EpisodeId = albumId,
            Title = $"Artist {artistId} - Album {albumId}",
            ItemType = InstanceType.Lidarr,
            SeasonNumber = 0,
            AirDateUtc = airDate ?? _timeProvider.GetUtcNow().AddDays(-30),
            IsMonitored = monitored,
            CurrentScore = currentScore,
            CutoffScore = cutoffScore,
        });
        await _eventsContext.SaveChangesAsync();
    }

    [Fact]
    public async Task EvaluateAsync_Lidarr_MissingAlbum_BuildsUnitKeyedByArtistAndAlbum()
    {
        // Arrange
        DateTimeOffset added = _timeProvider.GetUtcNow().AddDays(-100);
        DateTimeOffset releaseDate = _timeProvider.GetUtcNow().AddDays(-40);
        SetupLidarr([Artist(1, added: added)], [Album(10, 1, releaseDate: releaseDate)]);

        // Act
        SeekerEvaluation evaluation = await EvaluateLidarrAsync();

        // Assert
        SeekerSearchUnit unit = evaluation.Units.ShouldHaveSingleItem();
        unit.ItemId.ShouldBe(1);
        unit.SeasonNumber.ShouldBe(0);
        unit.EpisodeId.ShouldBe(10);
        unit.Name.ShouldBe("Artist 1 - Album 10");
        unit.ContentKind.ShouldBeNull();
        unit.Reasons.ShouldBe([SeekerSearchReason.Missing]);
        unit.AirDateUtc.ShouldBe(releaseDate);
        unit.Added.ShouldBe(added);
        evaluation.AllLibraryIds.ShouldBe([1L]);
    }

    [Fact]
    public async Task EvaluateAsync_Lidarr_AlbumMissingAndCutoffUnmet_CarriesBothReasons()
    {
        // Arrange
        SetupLidarr([Artist(1)], [Album(10, 1)], [Album(10, 1), Album(11, 1)]);

        // Act
        SeekerEvaluation evaluation = await EvaluateLidarrAsync(InstanceConfig(_lidarr, c => c.UseCutoff = true));

        // Assert
        evaluation.Units.Count.ShouldBe(2);
        evaluation.Units.Single(u => u.EpisodeId == 10).Reasons
            .ShouldBe([SeekerSearchReason.Missing, SeekerSearchReason.QualityCutoffNotMet]);
        evaluation.Units.Single(u => u.EpisodeId == 11).Reasons
            .ShouldBe([SeekerSearchReason.QualityCutoffNotMet]);
    }

    [Fact]
    public async Task EvaluateAsync_Lidarr_UseCutoffOff_SkipsTheCutoffRead()
    {
        // Arrange
        SetupLidarr([Artist(1)], [Album(10, 1)], [Album(11, 1)]);

        // Act
        SeekerEvaluation evaluation = await EvaluateLidarrAsync(InstanceConfig(_lidarr, c => c.UseCutoff = false));

        // Assert
        evaluation.Units.ShouldHaveSingleItem().EpisodeId.ShouldBe(10);
        await _lidarrClient.DidNotReceive()
            .GetCutoffUnmetAlbumsAsync(Arg.Any<ArrInstance>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EvaluateAsync_Lidarr_MonitoredOnlyOff_ReadsBothMonitoredValues()
    {
        // Arrange
        SetupLidarr([Artist(1, monitored: false)], [Album(10, 1, monitored: false), Album(11, 1)]);

        // Act
        SeekerEvaluation evaluation = await EvaluateLidarrAsync(InstanceConfig(_lidarr, c => c.MonitoredOnly = false));

        // Assert
        evaluation.Units.Select(u => u.EpisodeId).OrderBy(id => id).ShouldBe([10L, 11L]);
        await _lidarrClient.Received(1).GetMissingAlbumsAsync(Arg.Any<ArrInstance>(), true, Arg.Any<CancellationToken>());
        await _lidarrClient.Received(1).GetMissingAlbumsAsync(Arg.Any<ArrInstance>(), false, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EvaluateAsync_Lidarr_MonitoredOnly_ReadsOnlyMonitoredAlbums()
    {
        // Arrange
        SetupLidarr([Artist(1)], [Album(10, 1)]);

        // Act
        await EvaluateLidarrAsync();

        // Assert
        await _lidarrClient.DidNotReceive().GetMissingAlbumsAsync(Arg.Any<ArrInstance>(), false, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EvaluateAsync_Lidarr_AlbumDatedInTheFuture_ExcludesAsNotReleased()
    {
        // Arrange
        SetupLidarr([Artist(1)], [Album(10, 1, releaseDate: _timeProvider.GetUtcNow().AddDays(5))]);

        // Act
        SeekerEvaluation evaluation = await EvaluateLidarrAsync();

        // Assert
        ReasonOf(evaluation, "Artist 1 - Album 10").ShouldBe(SeekerExclusionReason.NotReleased);
        evaluation.Units.ShouldBeEmpty();
        evaluation.Excluded.ShouldNotContain(e => e.Name == "Artist 1");
    }

    [Fact]
    public async Task EvaluateAsync_Lidarr_AlbumReleasedInsideGraceWindow_ExcludesAsInGracePeriod()
    {
        // Arrange
        SetupLidarr([Artist(1)], [Album(10, 1, releaseDate: _timeProvider.GetUtcNow().AddHours(-1))]);

        // Act
        SeekerEvaluation evaluation = await EvaluateLidarrAsync();

        // Assert
        ReasonOf(evaluation, "Artist 1 - Album 10").ShouldBe(SeekerExclusionReason.InGracePeriod);
        evaluation.Units.ShouldBeEmpty();
    }

    [Fact]
    public async Task EvaluateAsync_Lidarr_AlbumReleasedPastGraceWindow_BuildsUnit()
    {
        // Arrange
        SetupLidarr([Artist(1)], [Album(10, 1, releaseDate: _timeProvider.GetUtcNow().AddHours(-7))]);

        // Act
        SeekerEvaluation evaluation = await EvaluateLidarrAsync();

        // Assert
        evaluation.Units.ShouldHaveSingleItem().EpisodeId.ShouldBe(10);
    }

    [Fact]
    public async Task EvaluateAsync_Lidarr_AlbumWithoutReleaseDate_CountsAsReleased()
    {
        // Arrange
        SetupLidarr([Artist(1)], [Album(10, 1, noReleaseDate: true)]);

        // Act
        SeekerEvaluation evaluation = await EvaluateLidarrAsync();

        // Assert
        SeekerSearchUnit unit = evaluation.Units.ShouldHaveSingleItem();
        unit.AirDateUtc.ShouldBeNull();
    }

    [Fact]
    public async Task EvaluateAsync_Lidarr_SkipTag_ExcludesArtistAsSkipTag()
    {
        // Arrange
        _lidarrClient.GetAllTagsAsync(Arg.Any<ArrInstance>()).Returns([new Tag { Id = 3, Label = "skip" }]);
        SetupLidarr([Artist(1, tags: [3])], [Album(10, 1)]);

        // Act
        SeekerEvaluation evaluation = await EvaluateLidarrAsync(InstanceConfig(_lidarr, c => c.SkipTags = ["skip"]));

        // Assert
        ReasonOf(evaluation, "Artist 1").ShouldBe(SeekerExclusionReason.SkipTag);
        evaluation.Units.ShouldBeEmpty();
    }

    [Fact]
    public async Task EvaluateAsync_Lidarr_IncludeTagMissing_ExcludesArtistAsMissingIncludeTag()
    {
        // Arrange
        _lidarrClient.GetAllTagsAsync(Arg.Any<ArrInstance>()).Returns([new Tag { Id = 3, Label = "keep" }]);
        SetupLidarr([Artist(1), Artist(2, tags: [3])], [Album(10, 1), Album(20, 2)]);

        // Act
        SeekerEvaluation evaluation = await EvaluateLidarrAsync(InstanceConfig(_lidarr, c => c.IncludeTags = ["keep"]));

        // Assert
        ReasonOf(evaluation, "Artist 1").ShouldBe(SeekerExclusionReason.MissingIncludeTag);
        evaluation.Units.ShouldHaveSingleItem().ItemId.ShouldBe(2);
    }

    [Fact]
    public async Task EvaluateAsync_Lidarr_UnmonitoredArtist_ExcludesAsNotMonitored()
    {
        // Arrange
        SetupLidarr([Artist(1, monitored: false)], [Album(10, 1)]);

        // Act
        SeekerEvaluation evaluation = await EvaluateLidarrAsync();

        // Assert
        ReasonOf(evaluation, "Artist 1").ShouldBe(SeekerExclusionReason.NotMonitored);
        evaluation.Units.ShouldBeEmpty();
    }

    [Fact]
    public async Task EvaluateAsync_Lidarr_ArtistWithoutWantedAlbums_ExcludesAsNothingToSearch()
    {
        // Arrange
        SetupLidarr([Artist(1)], []);

        // Act
        SeekerEvaluation evaluation = await EvaluateLidarrAsync();

        // Assert
        ReasonOf(evaluation, "Artist 1").ShouldBe(SeekerExclusionReason.NothingToSearch);
    }

    [Fact]
    public async Task EvaluateAsync_Lidarr_StageCounts_CountArtists()
    {
        // Arrange
        _lidarrClient.GetAllTagsAsync(Arg.Any<ArrInstance>()).Returns([new Tag { Id = 3, Label = "skip" }]);
        SetupLidarr([Artist(1), Artist(2, monitored: false), Artist(3, tags: [3])], [Album(10, 1)]);

        // Act
        SeekerEvaluation evaluation = await EvaluateLidarrAsync(InstanceConfig(_lidarr, c => c.SkipTags = ["skip"]));

        // Assert
        evaluation.ReleasedItems.ShouldBe(3);
        evaluation.MonitoredItems.ShouldBe(2);
        evaluation.TagFilteredItems.ShouldBe(1);
    }

    [Fact]
    public async Task EvaluateAsync_Lidarr_QueuedAlbum_BlocksOnlyThatAlbum()
    {
        // Arrange
        SetupLidarr([Artist(1)], [Album(10, 1), Album(11, 1)]);

        // Act
        SeekerEvaluation evaluation = await EvaluateLidarrAsync(
            queue: [new QueueRecord
            {
                Id = 1,
                Title = "Download",
                DownloadId = "hash",
                Protocol = "torrent",
                SizeLeft = 1000,
                ArtistId = 1,
                AlbumId = 10,
                TrackedDownloadState = "downloading",
            }]);

        // Assert
        evaluation.Units.Single(u => u.EpisodeId == 10).BlockedBy.ShouldBe(SeekerExclusionReason.InQueue);
        evaluation.Remaining.ShouldHaveSingleItem().EpisodeId.ShouldBe(11);
    }

    [Fact]
    public async Task EvaluateAsync_Lidarr_RecentlyGrabbedAlbum_BlocksOnlyThatAlbum()
    {
        // Arrange
        SetupLidarr([Artist(1)], [Album(10, 1), Album(11, 1)]);

        // Act
        SeekerEvaluation evaluation = await EvaluateLidarrAsync(recentGrabs: new SeekerRecentGrabs { AlbumIds = [10] });

        // Assert
        evaluation.Units.Single(u => u.EpisodeId == 10).BlockedBy.ShouldBe(SeekerExclusionReason.RecentlyGrabbed);
        evaluation.Remaining.ShouldHaveSingleItem().EpisodeId.ShouldBe(11);
    }

    [Fact]
    public async Task EvaluateAsync_Lidarr_RestingAlbum_ExcludesAsResting()
    {
        // Arrange
        SetupLidarr([Artist(1)], [Album(10, 1), Album(11, 1)]);
        await SeedMissesAsync(_lidarr, 1, 3, _timeProvider.GetUtcNow().AddDays(-2), episodeId: 10);

        // Act
        SeekerEvaluation evaluation = await EvaluateLidarrAsync(InstanceConfig(_lidarr, c => c.RestUnitsEnabled = true));

        // Assert
        SeekerSearchUnit resting = evaluation.Units.Single(u => u.EpisodeId == 10);
        resting.BlockedBy.ShouldBe(SeekerExclusionReason.Resting);
        resting.Rest.ShouldNotBeNull().ConsecutiveMisses.ShouldBe(3);
        evaluation.Remaining.ShouldHaveSingleItem().EpisodeId.ShouldBe(11);
    }

    [Fact]
    public async Task EvaluateAsync_Lidarr_AlbumInCycleHistory_IsSearchedThisCycle()
    {
        // Arrange
        SetupLidarr([Artist(1)], [Album(10, 1), Album(11, 1)]);

        // Act
        SeekerEvaluation evaluation = await EvaluateLidarrAsync(
            history: [History(_lidarr, 1, _timeProvider.GetUtcNow().AddDays(-1), episodeId: 10)]);

        // Assert
        evaluation.Units.Single(u => u.EpisodeId == 10).Searched.ShouldBeTrue();
        evaluation.Units.Single(u => u.EpisodeId == 11).Searched.ShouldBeFalse();
        evaluation.WithoutCycleHistory().Units.ShouldAllBe(u => !u.Searched);
    }

    [Fact]
    public async Task EvaluateAsync_Lidarr_BelowCustomFormatCutoff_AddsReasonAndGap()
    {
        // Arrange
        SetupLidarr([Artist(1, qualityProfileId: 5)], []);
        _lidarrClient.GetQualityProfilesAsync(Arg.Any<ArrInstance>())
            .Returns([new ArrQualityProfile { Id = 5, UpgradeAllowed = true }]);
        await AddLidarrCustomFormatEntryAsync(1, 10, currentScore: 10, cutoffScore: 35);

        // Act
        SeekerEvaluation evaluation = await EvaluateLidarrAsync(InstanceConfig(_lidarr, c => c.UseCustomFormatScore = true));

        // Assert
        SeekerSearchUnit unit = evaluation.Units.ShouldHaveSingleItem();
        unit.ItemId.ShouldBe(1);
        unit.EpisodeId.ShouldBe(10);
        unit.Name.ShouldBe("Artist 1 - Album 10");
        unit.Reasons.ShouldBe([SeekerSearchReason.CustomFormatScoreBelowCutoff]);
        unit.CustomFormatGap.ShouldBe(25);
    }

    [Fact]
    public async Task EvaluateAsync_Lidarr_CustomFormatEntryOffByDefault_IsIgnored()
    {
        // Arrange
        SetupLidarr([Artist(1)], []);
        await AddLidarrCustomFormatEntryAsync(1, 10, currentScore: 10, cutoffScore: 35);

        // Act
        SeekerEvaluation evaluation = await EvaluateLidarrAsync();

        // Assert
        evaluation.Units.ShouldBeEmpty();
    }

    [Fact]
    public async Task EvaluateAsync_Lidarr_BelowCustomFormatCutoffWithUpgradesDisabled_ExcludesAsUpgradesDisabled()
    {
        // Arrange
        SetupLidarr([Artist(1, qualityProfileId: 5)], []);
        _lidarrClient.GetQualityProfilesAsync(Arg.Any<ArrInstance>())
            .Returns([new ArrQualityProfile { Id = 5, UpgradeAllowed = false }]);
        await AddLidarrCustomFormatEntryAsync(1, 10, currentScore: 10, cutoffScore: 35);

        // Act
        SeekerEvaluation evaluation = await EvaluateLidarrAsync(InstanceConfig(_lidarr, c => c.UseCustomFormatScore = true));

        // Assert
        ReasonOf(evaluation, "Artist 1 - Album 10").ShouldBe(SeekerExclusionReason.UpgradesDisabled);
        evaluation.Units.ShouldBeEmpty();
    }

    [Fact]
    public async Task EvaluateAsync_Lidarr_MissingAlbumWithUpgradesDisabled_StaysAUnit()
    {
        // Arrange
        SetupLidarr([Artist(1, qualityProfileId: 5)], [Album(10, 1)]);
        _lidarrClient.GetQualityProfilesAsync(Arg.Any<ArrInstance>())
            .Returns([new ArrQualityProfile { Id = 5, UpgradeAllowed = false }]);
        await AddLidarrCustomFormatEntryAsync(1, 10, currentScore: 10, cutoffScore: 35);

        // Act
        SeekerEvaluation evaluation = await EvaluateLidarrAsync(InstanceConfig(_lidarr, c => c.UseCustomFormatScore = true));

        // Assert
        evaluation.Units.ShouldHaveSingleItem().Reasons.ShouldBe([SeekerSearchReason.Missing]);
    }

    [Fact]
    public async Task EvaluateAsync_Lidarr_CustomFormatEntryOfFilteredArtist_IsIgnored()
    {
        // Arrange
        SetupLidarr([Artist(1, monitored: false)], []);
        await AddLidarrCustomFormatEntryAsync(1, 10, currentScore: 10, cutoffScore: 35);

        // Act
        SeekerEvaluation evaluation = await EvaluateLidarrAsync(InstanceConfig(_lidarr, c => c.UseCustomFormatScore = true));

        // Assert
        evaluation.Units.ShouldBeEmpty();
        ReasonOf(evaluation, "Artist 1").ShouldBe(SeekerExclusionReason.NotMonitored);
    }

    [Fact]
    public async Task SelectNext_Lidarr_ReturnsOneAlbumPerArtist()
    {
        // Arrange
        SetupLidarr(
            [Artist(1), Artist(2)],
            [Album(10, 1), Album(11, 1), Album(12, 1), Album(13, 1), Album(14, 1), Album(20, 2)]);
        SeekerEvaluation evaluation = await EvaluateLidarrAsync();

        // Act
        List<SeekerSearchCandidate> candidates = SeekerUnitEvaluator.SelectNext(evaluation, SelectionStrategy.Random, 2);

        // Assert
        evaluation.Remaining.Count().ShouldBe(6);
        candidates.Select(c => c.ItemId).OrderBy(id => id).ShouldBe([1L, 2L]);
        candidates.Single(c => c.ItemId == 2).EpisodeId.ShouldBe(20);
    }

    [Fact]
    public async Task ReadRecentGrabsAsync_Lidarr_ReadsGrabbedAlbumIds()
    {
        // Arrange
        _lidarrClient
            .GetRecentlyGrabbedAlbumIdsAsync(Arg.Any<ArrInstance>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(new HashSet<long> { 10 });

        // Act
        SeekerRecentGrabs grabs = await _sut.ReadRecentGrabsAsync(
            InstanceConfig(_lidarr, c => c.RecentGrabWindowHours = 6), _lidarr, CancellationToken.None);

        // Assert
        grabs.AlbumIds.ShouldBe([10L]);
        grabs.Episodes.ShouldBeEmpty();
        await _sonarrClient.DidNotReceive()
            .GetRecentlyGrabbedEpisodesAsync(Arg.Any<ArrInstance>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    #endregion

    #region Chaptarr

    private static SearchableAuthor Author(
        long id,
        bool monitored = true,
        List<long>? audiobookTags = null,
        List<long>? ebookTags = null,
        DateTimeOffset? added = null)
    {
        return new SearchableAuthor
        {
            Id = id,
            AuthorName = $"Author {id}",
            Monitored = monitored,
            AudiobookTags = audiobookTags ?? [],
            EbookTags = ebookTags ?? [],
            Added = added,
        };
    }

    private SearchableBook Book(
        long id,
        long authorId,
        ChaptarrMediaType mediaType = ChaptarrMediaType.Ebook,
        bool monitored = true,
        DateTimeOffset? releaseDate = null,
        bool noReleaseDate = false)
    {
        return new SearchableBook
        {
            Id = id,
            Title = $"Book {id}",
            AuthorId = authorId,
            Monitored = monitored,
            MediaType = mediaType,
            ReleaseDate = noReleaseDate ? null : releaseDate ?? _timeProvider.GetUtcNow().AddDays(-30),
        };
    }

    private void SetupChaptarr(List<SearchableAuthor> library, List<SearchableBook> missing, List<SearchableBook>? cutoffUnmet = null)
    {
        _chaptarrClient.StreamAllAuthorsAsync(Arg.Any<ArrInstance>(), Arg.Any<CancellationToken>())
            .Returns(_ => ToAsyncEnumerable(library));
        _chaptarrClient.GetMissingBooksAsync(Arg.Any<ArrInstance>(), true, Arg.Any<CancellationToken>())
            .Returns(missing.Where(b => b.Monitored).ToList());
        _chaptarrClient.GetMissingBooksAsync(Arg.Any<ArrInstance>(), false, Arg.Any<CancellationToken>())
            .Returns(missing.Where(b => !b.Monitored).ToList());

        if (cutoffUnmet is not null)
        {
            _chaptarrClient.GetCutoffUnmetBooksAsync(Arg.Any<ArrInstance>(), true, Arg.Any<CancellationToken>())
                .Returns(cutoffUnmet.Where(b => b.Monitored).ToList());
            _chaptarrClient.GetCutoffUnmetBooksAsync(Arg.Any<ArrInstance>(), false, Arg.Any<CancellationToken>())
                .Returns(cutoffUnmet.Where(b => !b.Monitored).ToList());
        }
    }

    private Task<SeekerEvaluation> EvaluateChaptarrAsync(
        SeekerInstanceConfig? instanceConfig = null,
        List<QueueRecord>? queue = null,
        SeekerRecentGrabs? recentGrabs = null,
        List<SeekerHistory>? history = null)
    {
        return _sut.EvaluateAsync(
            _config,
            instanceConfig ?? InstanceConfig(_chaptarr),
            _chaptarr,
            queue ?? [],
            recentGrabs ?? new SeekerRecentGrabs(),
            history ?? [],
            CancellationToken.None);
    }

    [Fact]
    public async Task EvaluateAsync_Chaptarr_MissingBook_BuildsUnitKeyedByAuthorAndBook()
    {
        // Arrange
        DateTimeOffset added = _timeProvider.GetUtcNow().AddDays(-100);
        DateTimeOffset releaseDate = _timeProvider.GetUtcNow().AddDays(-40);
        SetupChaptarr(
            [Author(1, added: added)],
            [Book(10, 1, ChaptarrMediaType.Audiobook, releaseDate: releaseDate)]);

        // Act
        SeekerEvaluation evaluation = await EvaluateChaptarrAsync();

        // Assert
        SeekerSearchUnit unit = evaluation.Units.ShouldHaveSingleItem();
        unit.ItemId.ShouldBe(1);
        unit.SeasonNumber.ShouldBe(0);
        unit.EpisodeId.ShouldBe(10);
        unit.Name.ShouldBe("Author 1 - Book 10 (Audiobook)");
        unit.ContentKind.ShouldBeNull();
        unit.Reasons.ShouldBe([SeekerSearchReason.Missing]);
        unit.AirDateUtc.ShouldBe(releaseDate);
        unit.Added.ShouldBe(added);
        unit.LastSearchTime.ShouldBeNull();
        evaluation.AllLibraryIds.ShouldBe([1L]);
    }

    [Theory]
    [InlineData(ChaptarrMediaType.Audiobook, "Author 1 - Book 10 (Audiobook)")]
    [InlineData(ChaptarrMediaType.Ebook, "Author 1 - Book 10 (Ebook)")]
    [InlineData(ChaptarrMediaType.Unknown, "Author 1 - Book 10")]
    public async Task EvaluateAsync_Chaptarr_NamesTheUnitBySide(ChaptarrMediaType mediaType, string expectedName)
    {
        // Arrange
        SetupChaptarr([Author(1)], [Book(10, 1, mediaType)]);

        // Act
        SeekerEvaluation evaluation = await EvaluateChaptarrAsync();

        // Assert
        evaluation.Units.ShouldHaveSingleItem().Name.ShouldBe(expectedName);
    }

    [Fact]
    public async Task EvaluateAsync_Chaptarr_TwoRowsOfOneTitle_AreTwoUnits()
    {
        // Arrange
        SetupChaptarr([Author(1)], [Book(10, 1, ChaptarrMediaType.Audiobook), Book(11, 1, ChaptarrMediaType.Ebook)]);

        // Act
        SeekerEvaluation evaluation = await EvaluateChaptarrAsync();

        // Assert
        evaluation.Units.Select(u => u.EpisodeId).OrderBy(id => id).ShouldBe([10L, 11L]);
    }

    [Fact]
    public async Task EvaluateAsync_Chaptarr_BookMissingAndCutoffUnmet_CarriesBothReasons()
    {
        // Arrange
        SetupChaptarr([Author(1)], [Book(10, 1)], [Book(10, 1), Book(11, 1)]);

        // Act
        SeekerEvaluation evaluation = await EvaluateChaptarrAsync(InstanceConfig(_chaptarr, c => c.UseCutoff = true));

        // Assert
        evaluation.Units.Count.ShouldBe(2);
        evaluation.Units.Single(u => u.EpisodeId == 10).Reasons
            .ShouldBe([SeekerSearchReason.Missing, SeekerSearchReason.QualityCutoffNotMet]);
        evaluation.Units.Single(u => u.EpisodeId == 11).Reasons
            .ShouldBe([SeekerSearchReason.QualityCutoffNotMet]);
    }

    [Fact]
    public async Task EvaluateAsync_Chaptarr_UseCutoffOff_SkipsTheCutoffRead()
    {
        // Arrange
        SetupChaptarr([Author(1)], [Book(10, 1)], [Book(11, 1)]);

        // Act
        SeekerEvaluation evaluation = await EvaluateChaptarrAsync(InstanceConfig(_chaptarr, c => c.UseCutoff = false));

        // Assert
        evaluation.Units.ShouldHaveSingleItem().EpisodeId.ShouldBe(10);
        await _chaptarrClient.DidNotReceive()
            .GetCutoffUnmetBooksAsync(Arg.Any<ArrInstance>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EvaluateAsync_Chaptarr_MonitoredOnlyOff_ReadsBothMonitoredValues()
    {
        // Arrange
        SetupChaptarr([Author(1, monitored: false)], [Book(10, 1, monitored: false), Book(11, 1)]);

        // Act
        SeekerEvaluation evaluation = await EvaluateChaptarrAsync(InstanceConfig(_chaptarr, c => c.MonitoredOnly = false));

        // Assert
        evaluation.Units.Select(u => u.EpisodeId).OrderBy(id => id).ShouldBe([10L, 11L]);
        await _chaptarrClient.Received(1).GetMissingBooksAsync(Arg.Any<ArrInstance>(), true, Arg.Any<CancellationToken>());
        await _chaptarrClient.Received(1).GetMissingBooksAsync(Arg.Any<ArrInstance>(), false, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EvaluateAsync_Chaptarr_MonitoredOnly_ReadsOnlyMonitoredBooks()
    {
        // Arrange
        SetupChaptarr([Author(1)], [Book(10, 1)]);

        // Act
        await EvaluateChaptarrAsync();

        // Assert
        await _chaptarrClient.DidNotReceive().GetMissingBooksAsync(Arg.Any<ArrInstance>(), false, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EvaluateAsync_Chaptarr_BookDatedInTheFuture_ExcludesAsNotReleased()
    {
        // Arrange
        SetupChaptarr([Author(1)], [Book(10, 1, releaseDate: _timeProvider.GetUtcNow().AddDays(5))]);

        // Act
        SeekerEvaluation evaluation = await EvaluateChaptarrAsync();

        // Assert
        ReasonOf(evaluation, "Author 1 - Book 10 (Ebook)").ShouldBe(SeekerExclusionReason.NotReleased);
        evaluation.Units.ShouldBeEmpty();
        evaluation.Excluded.ShouldNotContain(e => e.Name == "Author 1");
    }

    [Fact]
    public async Task EvaluateAsync_Chaptarr_BookReleasedInsideGraceWindow_ExcludesAsInGracePeriod()
    {
        // Arrange
        SetupChaptarr([Author(1)], [Book(10, 1, releaseDate: _timeProvider.GetUtcNow().AddHours(-1))]);

        // Act
        SeekerEvaluation evaluation = await EvaluateChaptarrAsync();

        // Assert
        ReasonOf(evaluation, "Author 1 - Book 10 (Ebook)").ShouldBe(SeekerExclusionReason.InGracePeriod);
        evaluation.Units.ShouldBeEmpty();
    }

    [Fact]
    public async Task EvaluateAsync_Chaptarr_BookReleasedPastGraceWindow_BuildsUnit()
    {
        // Arrange
        SetupChaptarr([Author(1)], [Book(10, 1, releaseDate: _timeProvider.GetUtcNow().AddHours(-7))]);

        // Act
        SeekerEvaluation evaluation = await EvaluateChaptarrAsync();

        // Assert
        evaluation.Units.ShouldHaveSingleItem().EpisodeId.ShouldBe(10);
    }

    [Fact]
    public async Task EvaluateAsync_Chaptarr_BookWithoutReleaseDate_CountsAsReleased()
    {
        // Arrange
        SetupChaptarr([Author(1)], [Book(10, 1, noReleaseDate: true)]);

        // Act
        SeekerEvaluation evaluation = await EvaluateChaptarrAsync();

        // Assert
        evaluation.Units.ShouldHaveSingleItem().AirDateUtc.ShouldBeNull();
    }

    [Fact]
    public async Task EvaluateAsync_Chaptarr_SkipTagOnAudiobookSide_ExcludesOnlyTheAudiobookRow()
    {
        // Arrange
        _chaptarrClient.GetAllTagsAsync(Arg.Any<ArrInstance>()).Returns([new Tag { Id = 3, Label = "skip" }]);
        SetupChaptarr(
            [Author(1, audiobookTags: [3])],
            [Book(10, 1, ChaptarrMediaType.Audiobook), Book(11, 1, ChaptarrMediaType.Ebook)]);

        // Act
        SeekerEvaluation evaluation = await EvaluateChaptarrAsync(InstanceConfig(_chaptarr, c => c.SkipTags = ["skip"]));

        // Assert
        evaluation.Units.ShouldHaveSingleItem().EpisodeId.ShouldBe(11);
        ReasonOf(evaluation, "Author 1 - Book 10 (Audiobook)").ShouldBe(SeekerExclusionReason.SkipTag);
        evaluation.Excluded.ShouldNotContain(e => e.Name == "Author 1");
    }

    [Fact]
    public async Task EvaluateAsync_Chaptarr_IncludeTagOnEbookSideOnly_KeepsOnlyTheEbookRow()
    {
        // Arrange
        _chaptarrClient.GetAllTagsAsync(Arg.Any<ArrInstance>()).Returns([new Tag { Id = 3, Label = "keep" }]);
        SetupChaptarr(
            [Author(1, ebookTags: [3])],
            [Book(10, 1, ChaptarrMediaType.Audiobook), Book(11, 1, ChaptarrMediaType.Ebook)]);

        // Act
        SeekerEvaluation evaluation = await EvaluateChaptarrAsync(InstanceConfig(_chaptarr, c => c.IncludeTags = ["keep"]));

        // Assert
        evaluation.Units.ShouldHaveSingleItem().EpisodeId.ShouldBe(11);
        ReasonOf(evaluation, "Author 1 - Book 10 (Audiobook)").ShouldBe(SeekerExclusionReason.MissingIncludeTag);
    }

    [Fact]
    public async Task EvaluateAsync_Chaptarr_SkipTagOnBothSides_ExcludesTheAuthor()
    {
        // Arrange
        _chaptarrClient.GetAllTagsAsync(Arg.Any<ArrInstance>()).Returns([new Tag { Id = 3, Label = "skip" }]);
        SetupChaptarr(
            [Author(1, audiobookTags: [3], ebookTags: [3])],
            [Book(10, 1, ChaptarrMediaType.Audiobook), Book(11, 1, ChaptarrMediaType.Ebook)]);

        // Act
        SeekerEvaluation evaluation = await EvaluateChaptarrAsync(InstanceConfig(_chaptarr, c => c.SkipTags = ["skip"]));

        // Assert
        ReasonOf(evaluation, "Author 1").ShouldBe(SeekerExclusionReason.SkipTag);
        evaluation.Units.ShouldBeEmpty();
    }

    [Fact]
    public async Task EvaluateAsync_Chaptarr_UnknownSideWithOneSideTagged_IsExcluded()
    {
        // Arrange
        _chaptarrClient.GetAllTagsAsync(Arg.Any<ArrInstance>()).Returns([new Tag { Id = 3, Label = "skip" }]);
        SetupChaptarr([Author(1, ebookTags: [3])], [Book(10, 1, ChaptarrMediaType.Unknown)]);

        // Act
        SeekerEvaluation evaluation = await EvaluateChaptarrAsync(InstanceConfig(_chaptarr, c => c.SkipTags = ["skip"]));

        // Assert
        evaluation.Units.ShouldBeEmpty();
        ReasonOf(evaluation, "Author 1 - Book 10").ShouldBe(SeekerExclusionReason.SkipTag);
    }

    [Fact]
    public async Task EvaluateAsync_Chaptarr_UnmonitoredAuthor_ExcludesAsNotMonitored()
    {
        // Arrange
        SetupChaptarr([Author(1, monitored: false)], [Book(10, 1)]);

        // Act
        SeekerEvaluation evaluation = await EvaluateChaptarrAsync();

        // Assert
        ReasonOf(evaluation, "Author 1").ShouldBe(SeekerExclusionReason.NotMonitored);
        evaluation.Units.ShouldBeEmpty();
    }

    [Fact]
    public async Task EvaluateAsync_Chaptarr_AuthorWithoutWantedBooks_ExcludesAsNothingToSearch()
    {
        // Arrange
        SetupChaptarr([Author(1)], []);

        // Act
        SeekerEvaluation evaluation = await EvaluateChaptarrAsync();

        // Assert
        ReasonOf(evaluation, "Author 1").ShouldBe(SeekerExclusionReason.NothingToSearch);
    }

    [Fact]
    public async Task EvaluateAsync_Chaptarr_StageCounts_CountAuthors()
    {
        // Arrange
        _chaptarrClient.GetAllTagsAsync(Arg.Any<ArrInstance>()).Returns([new Tag { Id = 3, Label = "skip" }]);
        SetupChaptarr(
            [Author(1), Author(2, monitored: false), Author(3, audiobookTags: [3], ebookTags: [3])],
            [Book(10, 1)]);

        // Act
        SeekerEvaluation evaluation = await EvaluateChaptarrAsync(InstanceConfig(_chaptarr, c => c.SkipTags = ["skip"]));

        // Assert
        evaluation.ReleasedItems.ShouldBe(3);
        evaluation.MonitoredItems.ShouldBe(2);
        evaluation.TagFilteredItems.ShouldBe(1);
    }

    [Fact]
    public async Task EvaluateAsync_Chaptarr_QueuedBook_BlocksOnlyThatBook()
    {
        // Arrange
        SetupChaptarr([Author(1)], [Book(10, 1), Book(11, 1)]);

        // Act
        SeekerEvaluation evaluation = await EvaluateChaptarrAsync(
            queue: [new QueueRecord
            {
                Id = 1,
                Title = "Download",
                DownloadId = "hash",
                Protocol = "torrent",
                SizeLeft = 1000,
                AuthorId = 1,
                BookId = 10,
                TrackedDownloadState = "downloading",
            }]);

        // Assert
        evaluation.Units.Single(u => u.EpisodeId == 10).BlockedBy.ShouldBe(SeekerExclusionReason.InQueue);
        evaluation.Remaining.ShouldHaveSingleItem().EpisodeId.ShouldBe(11);
    }

    [Fact]
    public async Task EvaluateAsync_Chaptarr_RecentlyGrabbedBook_BlocksOnlyThatBook()
    {
        // Arrange
        SetupChaptarr([Author(1)], [Book(10, 1), Book(11, 1)]);

        // Act
        SeekerEvaluation evaluation = await EvaluateChaptarrAsync(recentGrabs: new SeekerRecentGrabs { BookIds = [10] });

        // Assert
        evaluation.Units.Single(u => u.EpisodeId == 10).BlockedBy.ShouldBe(SeekerExclusionReason.RecentlyGrabbed);
        evaluation.Remaining.ShouldHaveSingleItem().EpisodeId.ShouldBe(11);
    }

    [Fact]
    public async Task EvaluateAsync_Chaptarr_RestingBook_ExcludesAsResting()
    {
        // Arrange
        SetupChaptarr([Author(1)], [Book(10, 1), Book(11, 1)]);
        await SeedMissesAsync(_chaptarr, 1, 3, _timeProvider.GetUtcNow().AddDays(-2), episodeId: 10);

        // Act
        SeekerEvaluation evaluation = await EvaluateChaptarrAsync(InstanceConfig(_chaptarr, c => c.RestUnitsEnabled = true));

        // Assert
        SeekerSearchUnit resting = evaluation.Units.Single(u => u.EpisodeId == 10);
        resting.BlockedBy.ShouldBe(SeekerExclusionReason.Resting);
        resting.Rest.ShouldNotBeNull().ConsecutiveMisses.ShouldBe(3);
        evaluation.Remaining.ShouldHaveSingleItem().EpisodeId.ShouldBe(11);
    }

    [Fact]
    public async Task EvaluateAsync_Chaptarr_BookInCycleHistory_IsSearchedThisCycle()
    {
        // Arrange
        SetupChaptarr([Author(1)], [Book(10, 1), Book(11, 1)]);

        // Act
        SeekerEvaluation evaluation = await EvaluateChaptarrAsync(
            history: [History(_chaptarr, 1, _timeProvider.GetUtcNow().AddDays(-1), episodeId: 10)]);

        // Assert
        evaluation.Units.Single(u => u.EpisodeId == 10).Searched.ShouldBeTrue();
        evaluation.Units.Single(u => u.EpisodeId == 11).Searched.ShouldBeFalse();
        evaluation.WithoutCycleHistory().Units.ShouldAllBe(u => !u.Searched);
    }

    [Fact]
    public async Task EvaluateAsync_Chaptarr_CustomFormatEntryWithFlagOn_IsIgnored()
    {
        // Arrange
        SetupChaptarr([Author(1)], []);
        await _eventsContext.CustomFormatScoreEntries.AddAsync(new CustomFormatScoreEntry
        {
            ArrInstanceId = _chaptarr.Id,
            ExternalItemId = 1,
            EpisodeId = 10,
            Title = "Author 1 - Book 10",
            ItemType = InstanceType.Chaptarr,
            SeasonNumber = 0,
            AirDateUtc = _timeProvider.GetUtcNow().AddDays(-30),
            IsMonitored = true,
            CurrentScore = 10,
            CutoffScore = 35,
        });
        await _eventsContext.SaveChangesAsync();

        // Act
        SeekerEvaluation evaluation = await EvaluateChaptarrAsync(InstanceConfig(_chaptarr, c => c.UseCustomFormatScore = true));

        // Assert
        evaluation.Units.ShouldBeEmpty();
    }

    [Fact]
    public async Task SelectNext_Chaptarr_ReturnsOneBookPerAuthor()
    {
        // Arrange
        SetupChaptarr(
            [Author(1), Author(2)],
            [Book(10, 1), Book(11, 1), Book(12, 1), Book(13, 1), Book(14, 1), Book(20, 2)]);
        SeekerEvaluation evaluation = await EvaluateChaptarrAsync();

        // Act
        List<SeekerSearchCandidate> candidates = SeekerUnitEvaluator.SelectNext(evaluation, SelectionStrategy.Random, 2);

        // Assert
        evaluation.Remaining.Count().ShouldBe(6);
        candidates.Select(c => c.ItemId).OrderBy(id => id).ShouldBe([1L, 2L]);
        candidates.Single(c => c.ItemId == 2).EpisodeId.ShouldBe(20);
    }

    [Fact]
    public async Task SelectNext_Chaptarr_PicksTheEarliestReleasedBookOfTheAuthor()
    {
        // Arrange
        DateTimeOffset now = _timeProvider.GetUtcNow();
        SetupChaptarr(
            [Author(1)],
            [
                Book(12, 1, releaseDate: now.AddDays(-30)),
                Book(11, 1, releaseDate: now.AddDays(-60)),
                Book(10, 1, releaseDate: now.AddDays(-60)),
            ]);
        SeekerEvaluation evaluation = await EvaluateChaptarrAsync();

        // Act
        List<SeekerSearchCandidate> candidates = SeekerUnitEvaluator.SelectNext(evaluation, SelectionStrategy.Random, 1);

        // Assert
        candidates.ShouldHaveSingleItem().EpisodeId.ShouldBe(10);
    }

    [Fact]
    public async Task ReadRecentGrabsAsync_Chaptarr_ReadsGrabbedBookIds()
    {
        // Arrange
        _chaptarrClient
            .GetRecentlyGrabbedBookIdsAsync(Arg.Any<ArrInstance>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(new HashSet<long> { 10 });

        // Act
        SeekerRecentGrabs grabs = await _sut.ReadRecentGrabsAsync(
            InstanceConfig(_chaptarr, c => c.RecentGrabWindowHours = 6), _chaptarr, CancellationToken.None);

        // Assert
        grabs.BookIds.ShouldBe([10L]);
        grabs.Episodes.ShouldBeEmpty();
        await _sonarrClient.DidNotReceive()
            .GetRecentlyGrabbedEpisodesAsync(Arg.Any<ArrInstance>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    #endregion
}
