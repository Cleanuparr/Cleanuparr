using Cleanuparr.Domain.Entities.Arr;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Arr.Interfaces;
using Cleanuparr.Infrastructure.Realtime;
using Cleanuparr.Infrastructure.Tests.Features.Jobs.TestHelpers;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration.Arr;
using Cleanuparr.Persistence.Models.Configuration.General;
using Cleanuparr.Persistence.Models.Configuration.Seeker;
using Cleanuparr.Persistence.Models.State;
using Cleanuparr.Persistence.Providers;
using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Shouldly;
using Xunit;
using CustomFormatScoreSyncer = Cleanuparr.Infrastructure.Features.Jobs.CustomFormatScoreSyncer;

namespace Cleanuparr.Infrastructure.Tests.Features.Jobs;

[Collection(JobHandlerCollection.Name)]
public class CustomFormatScoreSyncerTests : IDisposable
{
    private readonly JobHandlerFixture _fixture;
    private readonly ILogger<CustomFormatScoreSyncer> _logger;
    private readonly IRadarrClient _radarrClient;
    private readonly ISonarrClient _sonarrClient;
    private readonly ILidarrClient _lidarrClient;
    private readonly IWhisparrV2Client _whisparrV2Client;
    private readonly IWhisparrV3Client _whisparrV3Client;
    private readonly IStatusNotifier _statusNotifier;

    public CustomFormatScoreSyncerTests(JobHandlerFixture fixture)
    {
        _fixture = fixture;
        _fixture.RecreateDataContext();
        _fixture.ResetMocks();
        _logger = Substitute.For<ILogger<CustomFormatScoreSyncer>>();
        _radarrClient = Substitute.For<IRadarrClient>();
        _sonarrClient = Substitute.For<ISonarrClient>();
        _lidarrClient = Substitute.For<ILidarrClient>();
        _whisparrV2Client = Substitute.For<IWhisparrV2Client>();
        _whisparrV3Client = Substitute.For<IWhisparrV3Client>();
        _statusNotifier = Substitute.For<IStatusNotifier>();

    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    private CustomFormatScoreSyncer CreateSut()
    {
        return new CustomFormatScoreSyncer(
            _logger,
            _fixture.DataContext,
            _fixture.EventsContext,
            _radarrClient,
            _sonarrClient,
            _lidarrClient,
            _whisparrV2Client,
            _whisparrV3Client,
            _fixture.TimeProvider,
            _statusNotifier,
            new SqliteDatabaseProvider()
        );
    }

    private static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(IEnumerable<T> items)
    {
        foreach (T item in items)
        {
            yield return item;
        }

        await Task.CompletedTask;
    }

    #region ExecuteAsync Tests

    [Fact]
    public async Task ExecuteAsync_WhenCustomFormatScoreDisabled_ReturnsEarly()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert — no API calls made
        _radarrClient.DidNotReceive().StreamAllMoviesAsync(Arg.Any<ArrInstance>(), Arg.Any<CancellationToken>());
        _sonarrClient.DidNotReceive().StreamAllSeriesAsync(Arg.Any<ArrInstance>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoEnabledInstances_ReturnsEarly()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert — no API calls
        _radarrClient.DidNotReceive().StreamAllMoviesAsync(Arg.Any<ArrInstance>(), Arg.Any<CancellationToken>());
        _sonarrClient.DidNotReceive().StreamAllSeriesAsync(Arg.Any<ArrInstance>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_SyncsRadarrMovieScores()
    {
        // Arrange
        var radarrInstance = TestDataContextFactory.AddRadarrInstance(_fixture.DataContext);

        _fixture.DataContext.SeekerInstanceConfigs.Add(new SeekerInstanceConfig
        {
            ArrInstanceId = radarrInstance.Id,
            ArrInstance = radarrInstance,
            Enabled = true,
            UseCustomFormatScore = true
        });
        await _fixture.DataContext.SaveChangesAsync();
        await _fixture.EventsContext.SaveChangesAsync();

        // Mock quality profiles
        _radarrClient
            .GetQualityProfilesAsync(radarrInstance)
            .Returns([new ArrQualityProfile { Id = 1, Name = "HD", CutoffFormatScore = 500 }]);

        // Mock movies with files
        _radarrClient
            .StreamAllMoviesAsync(radarrInstance, Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable<SearchableMovie>([
                new SearchableMovie
                {
                    Id = 10,
                    Title = "Test Movie",
                    HasFile = true,
                    MovieFile = new MovieFileInfo { Id = 100, QualityCutoffNotMet = false },
                    QualityProfileId = 1,
                    Status = "released",
                    Monitored = true
                }
            ]));

        // Mock file scores
        _radarrClient
            .GetMovieFileScoresAsync(radarrInstance, Arg.Is<List<long>>(ids => ids.Contains(100)))
            .Returns(new Dictionary<long, int> { { 100, 250 } });

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert — CF score entry was saved
        var entries = await _fixture.EventsContext.CustomFormatScoreEntries.ToListAsync();
        entries.ShouldHaveSingleItem();

        var entry = entries[0];
        entry.ArrInstanceId.ShouldBe(radarrInstance.Id);
        entry.ExternalItemId.ShouldBe(10);
        entry.CurrentScore.ShouldBe(250);
        entry.CutoffScore.ShouldBe(500);
        entry.QualityProfileName.ShouldBe("HD");
        entry.ItemType.ShouldBe(InstanceType.Radarr);
        entry.IsMonitored.ShouldBeTrue();

        // Initial history entry should also be created
        var history = await _fixture.EventsContext.CustomFormatScoreHistory.ToListAsync();
        history.ShouldHaveSingleItem();
        history[0].Score.ShouldBe(250);
    }

    [Fact]
    public async Task ExecuteAsync_WhisparrV3_SyncsMovieScoresAsWhisparrEntries()
    {
        // Arrange
        ArrInstance whisparrInstance = TestDataContextFactory.AddWhisparrInstance(_fixture.DataContext, version: 3);

        _fixture.DataContext.SeekerInstanceConfigs.Add(new SeekerInstanceConfig
        {
            ArrInstanceId = whisparrInstance.Id,
            ArrInstance = whisparrInstance,
            Enabled = true,
            UseCustomFormatScore = true
        });
        _fixture.EventsContext.CustomFormatScoreEntries.AddRange(
            StaleEntry(whisparrInstance.Id, 999, InstanceType.Whisparr),
            StaleEntry(whisparrInstance.Id, 998, InstanceType.Radarr));
        await _fixture.DataContext.SaveChangesAsync();
        await _fixture.EventsContext.SaveChangesAsync();

        _whisparrV3Client
            .GetQualityProfilesAsync(whisparrInstance)
            .Returns([new ArrQualityProfile { Id = 1, Name = "HD", CutoffFormatScore = 500 }]);
        _whisparrV3Client
            .StreamAllMoviesAsync(whisparrInstance, Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable<SearchableMovie>([
                new SearchableMovie
                {
                    Id = 10, Title = "Test Scene", HasFile = true,
                    MovieFile = new MovieFileInfo { Id = 100, QualityCutoffNotMet = false },
                    QualityProfileId = 1, Status = "released", Monitored = true
                }
            ]));
        _whisparrV3Client
            .GetMovieFileScoresAsync(whisparrInstance, Arg.Any<List<long>>())
            .Returns(new Dictionary<long, int> { { 100, 250 } });

        CustomFormatScoreSyncer sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert
        List<CustomFormatScoreEntry> entries = await _fixture.EventsContext.CustomFormatScoreEntries
            .OrderBy(e => e.ExternalItemId)
            .ToListAsync();
        entries.Count.ShouldBe(2);
        entries[0].ExternalItemId.ShouldBe(10);
        entries[0].ItemType.ShouldBe(InstanceType.Whisparr);
        entries[0].CurrentScore.ShouldBe(250);
        entries[1].ExternalItemId.ShouldBe(998);
        entries[1].ItemType.ShouldBe(InstanceType.Radarr);
        _radarrClient.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_WhisparrV2_SyncsEpisodeScoresAsWhisparrEntries()
    {
        // Arrange
        ArrInstance whisparrInstance = TestDataContextFactory.AddWhisparrInstance(_fixture.DataContext, version: 2);

        _fixture.DataContext.SeekerInstanceConfigs.Add(new SeekerInstanceConfig
        {
            ArrInstanceId = whisparrInstance.Id,
            ArrInstance = whisparrInstance,
            Enabled = true,
            UseCustomFormatScore = true
        });
        _fixture.EventsContext.CustomFormatScoreEntries.AddRange(
            StaleEntry(whisparrInstance.Id, 999, InstanceType.Whisparr),
            StaleEntry(whisparrInstance.Id, 998, InstanceType.Sonarr));
        await _fixture.DataContext.SaveChangesAsync();
        await _fixture.EventsContext.SaveChangesAsync();

        DateTimeOffset releaseDate = new(2024, 5, 12, 0, 0, 0, TimeSpan.Zero);
        _whisparrV2Client
            .GetQualityProfilesAsync(whisparrInstance)
            .Returns([new ArrQualityProfile { Id = 1, Name = "HD", CutoffFormatScore = 500 }]);
        _whisparrV2Client
            .StreamAllSeriesAsync(whisparrInstance, Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable<SearchableSeries>([
                new SearchableSeries { Id = 10, Title = "Site", QualityProfileId = 1, Monitored = true }
            ]));
        _whisparrV2Client
            .GetEpisodesAsync(whisparrInstance, 10, Arg.Any<CancellationToken>())
            .Returns([
                new SearchableEpisode { Id = 100, SeasonNumber = 2024, EpisodeFileId = 500, HasFile = true, Monitored = true, AirDateUtc = releaseDate }
            ]);
        _whisparrV2Client
            .GetEpisodeFilesAsync(whisparrInstance, 10, Arg.Any<CancellationToken>())
            .Returns([
                new ArrEpisodeFile { Id = 500, CustomFormatScore = 300, QualityCutoffNotMet = false }
            ]);

        CustomFormatScoreSyncer sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert
        List<CustomFormatScoreEntry> entries = await _fixture.EventsContext.CustomFormatScoreEntries
            .OrderBy(e => e.ExternalItemId)
            .ToListAsync();
        entries.Count.ShouldBe(2);
        entries[0].ExternalItemId.ShouldBe(10);
        entries[0].EpisodeId.ShouldBe(100);
        entries[0].ItemType.ShouldBe(InstanceType.Whisparr);
        entries[0].Title.ShouldBe("Site 2024-05-12");
        entries[0].SeasonNumber.ShouldBe(2024);
        entries[0].AirDateUtc.ShouldBe(releaseDate);
        entries[0].CurrentScore.ShouldBe(300);
        entries[1].ExternalItemId.ShouldBe(998);
        entries[1].ItemType.ShouldBe(InstanceType.Sonarr);
        _sonarrClient.ReceivedCalls().ShouldBeEmpty();
    }

    private static CustomFormatScoreEntry StaleEntry(Guid arrInstanceId, long externalItemId, InstanceType itemType) => new()
    {
        ArrInstanceId = arrInstanceId,
        ExternalItemId = externalItemId,
        ItemType = itemType,
        Title = "Deleted Movie",
        FileId = externalItemId,
        CurrentScore = 100,
        CutoffScore = 500,
        QualityProfileName = "HD",
        LastSyncedAt = new DateTime(1999, 1, 1, 0, 0, 0, DateTimeKind.Utc)
    };

    [Fact]
    public async Task ExecuteAsync_RecordsHistoryOnScoreChange()
    {
        // Arrange
        var radarrInstance = TestDataContextFactory.AddRadarrInstance(_fixture.DataContext);

        _fixture.DataContext.SeekerInstanceConfigs.Add(new SeekerInstanceConfig
        {
            ArrInstanceId = radarrInstance.Id,
            ArrInstance = radarrInstance,
            Enabled = true,
            UseCustomFormatScore = true
        });

        // Pre-existing CF score entry with a different score
        _fixture.EventsContext.CustomFormatScoreEntries.Add(new CustomFormatScoreEntry
        {
            ArrInstanceId = radarrInstance.Id,
            ExternalItemId = 10,
            EpisodeId = 0,
            ItemType = InstanceType.Radarr,
            Title = "Test Movie",
            FileId = 100,
            CurrentScore = 200,
            CutoffScore = 500,
            QualityProfileName = "HD",
            LastSyncedAt = DateTime.UtcNow.AddHours(-1)
        });
        await _fixture.DataContext.SaveChangesAsync();
        await _fixture.EventsContext.SaveChangesAsync();

        // Mock quality profiles
        _radarrClient
            .GetQualityProfilesAsync(radarrInstance)
            .Returns([new ArrQualityProfile { Id = 1, Name = "HD", CutoffFormatScore = 500 }]);

        // Mock movies — same movie but score changed from 200 to 350
        _radarrClient
            .StreamAllMoviesAsync(radarrInstance, Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable<SearchableMovie>([
                new SearchableMovie
                {
                    Id = 10,
                    Title = "Test Movie",
                    HasFile = true,
                    MovieFile = new MovieFileInfo { Id = 100, QualityCutoffNotMet = false },
                    QualityProfileId = 1,
                    Status = "released",
                    Monitored = true
                }
            ]));

        _radarrClient
            .GetMovieFileScoresAsync(radarrInstance, Arg.Is<List<long>>(ids => ids.Contains(100)))
            .Returns(new Dictionary<long, int> { { 100, 350 } });

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert — existing entry should be updated
        var entries = await _fixture.EventsContext.CustomFormatScoreEntries.ToListAsync();
        entries.ShouldHaveSingleItem();
        entries[0].CurrentScore.ShouldBe(350);

        // History entry should be created because score changed (200 -> 350)
        var history = await _fixture.EventsContext.CustomFormatScoreHistory.ToListAsync();
        history.ShouldHaveSingleItem();
        history[0].Score.ShouldBe(350);
        history[0].ItemType.ShouldBe(InstanceType.Radarr);
    }

    [Fact]
    public async Task ExecuteAsync_TracksUnmonitoredMovie()
    {
        // Arrange
        var radarrInstance = TestDataContextFactory.AddRadarrInstance(_fixture.DataContext);

        _fixture.DataContext.SeekerInstanceConfigs.Add(new SeekerInstanceConfig
        {
            ArrInstanceId = radarrInstance.Id,
            ArrInstance = radarrInstance,
            Enabled = true,
            UseCustomFormatScore = true
        });
        await _fixture.DataContext.SaveChangesAsync();
        await _fixture.EventsContext.SaveChangesAsync();

        _radarrClient
            .GetQualityProfilesAsync(radarrInstance)
            .Returns([new ArrQualityProfile { Id = 1, Name = "HD", CutoffFormatScore = 500 }]);

        _radarrClient
            .StreamAllMoviesAsync(radarrInstance, Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable<SearchableMovie>([
                new SearchableMovie
                {
                    Id = 10,
                    Title = "Unmonitored Movie",
                    HasFile = true,
                    MovieFile = new MovieFileInfo { Id = 100, QualityCutoffNotMet = false },
                    QualityProfileId = 1,
                    Status = "released",
                    Monitored = false
                }
            ]));

        _radarrClient
            .GetMovieFileScoresAsync(radarrInstance, Arg.Is<List<long>>(ids => ids.Contains(100)))
            .Returns(new Dictionary<long, int> { { 100, 250 } });

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert — entry should be saved with IsMonitored = false
        var entries = await _fixture.EventsContext.CustomFormatScoreEntries.ToListAsync();
        entries.ShouldHaveSingleItem();
        entries[0].IsMonitored.ShouldBeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_UpdatesMonitoredStatusOnSync()
    {
        // Arrange
        var radarrInstance = TestDataContextFactory.AddRadarrInstance(_fixture.DataContext);

        _fixture.DataContext.SeekerInstanceConfigs.Add(new SeekerInstanceConfig
        {
            ArrInstanceId = radarrInstance.Id,
            ArrInstance = radarrInstance,
            Enabled = true,
            UseCustomFormatScore = true
        });

        // Pre-existing entry that was monitored
        _fixture.EventsContext.CustomFormatScoreEntries.Add(new CustomFormatScoreEntry
        {
            ArrInstanceId = radarrInstance.Id,
            ExternalItemId = 10,
            EpisodeId = 0,
            ItemType = InstanceType.Radarr,
            Title = "Test Movie",
            FileId = 100,
            CurrentScore = 250,
            CutoffScore = 500,
            QualityProfileName = "HD",
            IsMonitored = true,
            LastSyncedAt = DateTime.UtcNow.AddHours(-1)
        });
        await _fixture.DataContext.SaveChangesAsync();
        await _fixture.EventsContext.SaveChangesAsync();

        _radarrClient
            .GetQualityProfilesAsync(radarrInstance)
            .Returns([new ArrQualityProfile { Id = 1, Name = "HD", CutoffFormatScore = 500 }]);

        // Movie is now unmonitored
        _radarrClient
            .StreamAllMoviesAsync(radarrInstance, Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable<SearchableMovie>([
                new SearchableMovie
                {
                    Id = 10, Title = "Test Movie", HasFile = true,
                    MovieFile = new MovieFileInfo { Id = 100, QualityCutoffNotMet = false },
                    QualityProfileId = 1, Status = "released", Monitored = false
                }
            ]));

        _radarrClient
            .GetMovieFileScoresAsync(radarrInstance, Arg.Any<List<long>>())
            .Returns(new Dictionary<long, int> { { 100, 250 } });

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert — IsMonitored should be updated to false
        var entries = await _fixture.EventsContext.CustomFormatScoreEntries.ToListAsync();
        entries.ShouldHaveSingleItem();
        entries[0].IsMonitored.ShouldBeFalse();
    }

    #endregion

    #region Sonarr Sync Tests

    [Fact]
    public async Task ExecuteAsync_SyncsSonarrEpisodeScores()
    {
        // Arrange
        var sonarrInstance = TestDataContextFactory.AddSonarrInstance(_fixture.DataContext);

        _fixture.DataContext.SeekerInstanceConfigs.Add(new SeekerInstanceConfig
        {
            ArrInstanceId = sonarrInstance.Id,
            ArrInstance = sonarrInstance,
            Enabled = true,
            UseCustomFormatScore = true
        });
        await _fixture.DataContext.SaveChangesAsync();
        await _fixture.EventsContext.SaveChangesAsync();

        // Mock quality profiles
        _sonarrClient
            .GetQualityProfilesAsync(sonarrInstance)
            .Returns([new ArrQualityProfile { Id = 1, Name = "HD", CutoffFormatScore = 500 }]);

        // Mock series
        _sonarrClient
            .StreamAllSeriesAsync(sonarrInstance, Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable<SearchableSeries>([
                new SearchableSeries { Id = 10, Title = "Test Series", QualityProfileId = 1, Monitored = true }
            ]));

        // Mock episodes — one with a file, one without
        _sonarrClient
            .GetEpisodesAsync(sonarrInstance, 10)
            .Returns([
                new SearchableEpisode { Id = 100, SeasonNumber = 1, EpisodeNumber = 1, EpisodeFileId = 500, HasFile = true, Monitored = true },
                new SearchableEpisode { Id = 101, SeasonNumber = 1, EpisodeNumber = 2, EpisodeFileId = 0, HasFile = false }
            ]);

        // Mock episode files with CF scores
        _sonarrClient
            .GetEpisodeFilesAsync(sonarrInstance, 10)
            .Returns([
                new ArrEpisodeFile { Id = 500, CustomFormatScore = 300, QualityCutoffNotMet = false }
            ]);

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert — only the episode with a file should have an entry
        var entries = await _fixture.EventsContext.CustomFormatScoreEntries.ToListAsync();
        entries.ShouldHaveSingleItem();

        var entry = entries[0];
        entry.ArrInstanceId.ShouldBe(sonarrInstance.Id);
        entry.ExternalItemId.ShouldBe(10);
        entry.EpisodeId.ShouldBe(100);
        entry.CurrentScore.ShouldBe(300);
        entry.CutoffScore.ShouldBe(500);
        entry.ItemType.ShouldBe(InstanceType.Sonarr);
        entry.IsMonitored.ShouldBeTrue();
        entry.Title.ShouldContain("S01E01");

        // Initial history should be created
        var history = await _fixture.EventsContext.CustomFormatScoreHistory.ToListAsync();
        history.ShouldHaveSingleItem();
        history[0].Score.ShouldBe(300);
    }

    [Fact]
    public async Task ExecuteAsync_SyncsSonarrEpisodeScores_StoresSeasonNumber()
    {
        // Arrange
        ArrInstance sonarrInstance = TestDataContextFactory.AddSonarrInstance(_fixture.DataContext);

        _fixture.DataContext.SeekerInstanceConfigs.Add(new SeekerInstanceConfig
        {
            ArrInstanceId = sonarrInstance.Id,
            ArrInstance = sonarrInstance,
            Enabled = true,
            UseCustomFormatScore = true
        });
        await _fixture.DataContext.SaveChangesAsync();
        await _fixture.EventsContext.SaveChangesAsync();

        _sonarrClient
            .GetQualityProfilesAsync(sonarrInstance)
            .Returns([new ArrQualityProfile { Id = 1, Name = "HD", CutoffFormatScore = 500 }]);

        _sonarrClient
            .StreamAllSeriesAsync(sonarrInstance, Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable<SearchableSeries>([
                new SearchableSeries { Id = 10, Title = "Test Series", QualityProfileId = 1, Monitored = true }
            ]));

        DateTimeOffset airDateUtc = new(2026, 1, 15, 0, 0, 0, TimeSpan.Zero);
        _sonarrClient
            .GetEpisodesAsync(sonarrInstance, 10)
            .Returns([
                new SearchableEpisode { Id = 100, SeasonNumber = 3, EpisodeNumber = 1, EpisodeFileId = 500, HasFile = true, Monitored = true, AirDateUtc = airDateUtc }
            ]);

        _sonarrClient
            .GetEpisodeFilesAsync(sonarrInstance, 10)
            .Returns([
                new ArrEpisodeFile { Id = 500, CustomFormatScore = 300, QualityCutoffNotMet = false }
            ]);

        CustomFormatScoreSyncer sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert: the synced entry carries the episode's season number and air date
        List<CustomFormatScoreEntry> entries = await _fixture.EventsContext.CustomFormatScoreEntries.ToListAsync();
        entries.ShouldHaveSingleItem();
        entries[0].SeasonNumber.ShouldBe(3);
        entries[0].AirDateUtc.ShouldBe(airDateUtc);
    }

    [Fact]
    public async Task ExecuteAsync_SyncsSonarrEpisodeScores_FillsMissingSeasonNumberOnExistingEntry()
    {
        // Arrange
        ArrInstance sonarrInstance = TestDataContextFactory.AddSonarrInstance(_fixture.DataContext);

        _fixture.DataContext.SeekerInstanceConfigs.Add(new SeekerInstanceConfig
        {
            ArrInstanceId = sonarrInstance.Id,
            ArrInstance = sonarrInstance,
            Enabled = true,
            UseCustomFormatScore = true
        });

        // Pre-existing entry from before SeasonNumber/AirDateUtc were tracked
        _fixture.EventsContext.CustomFormatScoreEntries.Add(new CustomFormatScoreEntry
        {
            ArrInstanceId = sonarrInstance.Id,
            ExternalItemId = 10,
            EpisodeId = 100,
            ItemType = InstanceType.Sonarr,
            Title = "Test Series S02E01",
            FileId = 500,
            CurrentScore = 300,
            CutoffScore = 500,
            QualityProfileName = "HD",
            SeasonNumber = null,
            AirDateUtc = null,
            LastSyncedAt = DateTime.UtcNow.AddHours(-1)
        });
        await _fixture.DataContext.SaveChangesAsync();
        await _fixture.EventsContext.SaveChangesAsync();

        _sonarrClient
            .GetQualityProfilesAsync(sonarrInstance)
            .Returns([new ArrQualityProfile { Id = 1, Name = "HD", CutoffFormatScore = 500 }]);

        _sonarrClient
            .StreamAllSeriesAsync(sonarrInstance, Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable<SearchableSeries>([
                new SearchableSeries { Id = 10, Title = "Test Series", QualityProfileId = 1, Monitored = true }
            ]));

        DateTimeOffset airDateUtc = new(2026, 2, 20, 0, 0, 0, TimeSpan.Zero);
        _sonarrClient
            .GetEpisodesAsync(sonarrInstance, 10)
            .Returns([
                new SearchableEpisode { Id = 100, SeasonNumber = 2, EpisodeNumber = 1, EpisodeFileId = 500, HasFile = true, Monitored = true, AirDateUtc = airDateUtc }
            ]);

        _sonarrClient
            .GetEpisodeFilesAsync(sonarrInstance, 10)
            .Returns([
                new ArrEpisodeFile { Id = 500, CustomFormatScore = 300, QualityCutoffNotMet = false }
            ]);

        CustomFormatScoreSyncer sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert: the next sync backfills the season number and air date on the existing row
        List<CustomFormatScoreEntry> entries = await _fixture.EventsContext.CustomFormatScoreEntries.ToListAsync();
        entries.ShouldHaveSingleItem();
        entries[0].SeasonNumber.ShouldBe(2);
        entries[0].AirDateUtc.ShouldBe(airDateUtc);
    }

    [Fact]
    public async Task ExecuteAsync_SyncsSonarrEpisodeScores_StoresLastSearchTime()
    {
        // Arrange
        ArrInstance sonarrInstance = TestDataContextFactory.AddSonarrInstance(_fixture.DataContext);

        _fixture.DataContext.SeekerInstanceConfigs.Add(new SeekerInstanceConfig
        {
            ArrInstanceId = sonarrInstance.Id,
            ArrInstance = sonarrInstance,
            Enabled = true,
            UseCustomFormatScore = true
        });
        await _fixture.DataContext.SaveChangesAsync();
        await _fixture.EventsContext.SaveChangesAsync();

        _sonarrClient
            .GetQualityProfilesAsync(sonarrInstance)
            .Returns([new ArrQualityProfile { Id = 1, Name = "HD", CutoffFormatScore = 500 }]);

        _sonarrClient
            .StreamAllSeriesAsync(sonarrInstance, Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable<SearchableSeries>([
                new SearchableSeries { Id = 10, Title = "Test Series", QualityProfileId = 1, Monitored = true }
            ]));

        DateTimeOffset lastSearchTime = new(2026, 3, 10, 0, 0, 0, TimeSpan.Zero);
        _sonarrClient
            .GetEpisodesAsync(sonarrInstance, 10)
            .Returns([
                new SearchableEpisode { Id = 100, SeasonNumber = 3, EpisodeNumber = 1, EpisodeFileId = 500, HasFile = true, Monitored = true, LastSearchTime = lastSearchTime }
            ]);

        _sonarrClient
            .GetEpisodeFilesAsync(sonarrInstance, 10)
            .Returns([
                new ArrEpisodeFile { Id = 500, CustomFormatScore = 300, QualityCutoffNotMet = false }
            ]);

        CustomFormatScoreSyncer sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert: the synced entry carries the episode's last search time
        List<CustomFormatScoreEntry> entries = await _fixture.EventsContext.CustomFormatScoreEntries.ToListAsync();
        entries.ShouldHaveSingleItem();
        entries[0].LastSearchTime.ShouldBe(lastSearchTime);
    }

    [Fact]
    public async Task ExecuteAsync_SyncsSonarrEpisodeScores_RefreshesLastSearchTimeOnExistingEntry()
    {
        // Arrange
        ArrInstance sonarrInstance = TestDataContextFactory.AddSonarrInstance(_fixture.DataContext);

        _fixture.DataContext.SeekerInstanceConfigs.Add(new SeekerInstanceConfig
        {
            ArrInstanceId = sonarrInstance.Id,
            ArrInstance = sonarrInstance,
            Enabled = true,
            UseCustomFormatScore = true
        });

        _fixture.EventsContext.CustomFormatScoreEntries.Add(new CustomFormatScoreEntry
        {
            ArrInstanceId = sonarrInstance.Id,
            ExternalItemId = 10,
            EpisodeId = 100,
            ItemType = InstanceType.Sonarr,
            Title = "Test Series S02E01",
            FileId = 500,
            CurrentScore = 300,
            CutoffScore = 500,
            QualityProfileName = "HD",
            LastSearchTime = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            LastSyncedAt = DateTime.UtcNow.AddHours(-1)
        });
        await _fixture.DataContext.SaveChangesAsync();
        await _fixture.EventsContext.SaveChangesAsync();

        _sonarrClient
            .GetQualityProfilesAsync(sonarrInstance)
            .Returns([new ArrQualityProfile { Id = 1, Name = "HD", CutoffFormatScore = 500 }]);

        _sonarrClient
            .StreamAllSeriesAsync(sonarrInstance, Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable<SearchableSeries>([
                new SearchableSeries { Id = 10, Title = "Test Series", QualityProfileId = 1, Monitored = true }
            ]));

        DateTimeOffset lastSearchTime = new(2026, 4, 5, 0, 0, 0, TimeSpan.Zero);
        _sonarrClient
            .GetEpisodesAsync(sonarrInstance, 10)
            .Returns([
                new SearchableEpisode { Id = 100, SeasonNumber = 2, EpisodeNumber = 1, EpisodeFileId = 500, HasFile = true, Monitored = true, LastSearchTime = lastSearchTime }
            ]);

        _sonarrClient
            .GetEpisodeFilesAsync(sonarrInstance, 10)
            .Returns([
                new ArrEpisodeFile { Id = 500, CustomFormatScore = 300, QualityCutoffNotMet = false }
            ]);

        CustomFormatScoreSyncer sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert: the next sync overwrites the stored last search time
        List<CustomFormatScoreEntry> entries = await _fixture.EventsContext.CustomFormatScoreEntries.ToListAsync();
        entries.ShouldHaveSingleItem();
        entries[0].LastSearchTime.ShouldBe(lastSearchTime);
    }

    [Fact]
    public async Task ExecuteAsync_SonarrSync_SkipsEpisodesWithoutFiles()
    {
        // Arrange
        var sonarrInstance = TestDataContextFactory.AddSonarrInstance(_fixture.DataContext);

        _fixture.DataContext.SeekerInstanceConfigs.Add(new SeekerInstanceConfig
        {
            ArrInstanceId = sonarrInstance.Id,
            ArrInstance = sonarrInstance,
            Enabled = true,
            UseCustomFormatScore = true
        });
        await _fixture.DataContext.SaveChangesAsync();
        await _fixture.EventsContext.SaveChangesAsync();

        _sonarrClient
            .GetQualityProfilesAsync(sonarrInstance)
            .Returns([new ArrQualityProfile { Id = 1, Name = "HD", CutoffFormatScore = 500 }]);

        _sonarrClient
            .StreamAllSeriesAsync(sonarrInstance, Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable<SearchableSeries>([
                new SearchableSeries { Id = 10, Title = "Test Series", QualityProfileId = 1, Monitored = true }
            ]));

        // All episodes have EpisodeFileId = 0 (no file)
        _sonarrClient
            .GetEpisodesAsync(sonarrInstance, 10)
            .Returns([
                new SearchableEpisode { Id = 100, SeasonNumber = 1, EpisodeNumber = 1, EpisodeFileId = 0, HasFile = false }
            ]);

        _sonarrClient
            .GetEpisodeFilesAsync(sonarrInstance, 10)
            .Returns([]);

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert — no entries created
        var entries = await _fixture.EventsContext.CustomFormatScoreEntries.ToListAsync();
        entries.ShouldBeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_SyncSonarr_FetchesSeriesConcurrentlyButWritesSerially()
    {
        const int FetchConcurrency = 5;
        const int SeriesCount = 20;

        string dbPath = Path.Combine(Path.GetTempPath(), $"cfsync-concurrency-{Guid.NewGuid():N}.db");
        string eventsDbPath = Path.Combine(Path.GetTempPath(), $"cfsync-concurrency-events-{Guid.NewGuid():N}.db");
        DataContext dataContext = CreateFileBackedWalDataContext(dbPath);
        EventsContext eventsContext = CreateFileBackedWalEventsContext(eventsDbPath);

        try
        {
            ArrConfig sonarrConfig = dataContext.ArrConfigs.First(c => c.Type == InstanceType.Sonarr);
            ArrInstance sonarrInstance = new()
            {
                Id = Guid.NewGuid(),
                Name = "Test Sonarr",
                Url = new Uri("http://sonarr:8989"),
                ApiKey = "test-api-key",
                Enabled = true,
                ArrConfigId = sonarrConfig.Id,
                ArrConfig = sonarrConfig
            };
            dataContext.ArrInstances.Add(sonarrInstance);
            dataContext.SeekerInstanceConfigs.Add(new SeekerInstanceConfig
            {
                ArrInstanceId = sonarrInstance.Id,
                ArrInstance = sonarrInstance,
                Enabled = true,
                UseCustomFormatScore = true
            });
            await dataContext.SaveChangesAsync();

            _sonarrClient
                .GetQualityProfilesAsync(sonarrInstance)
                .Returns([new ArrQualityProfile { Id = 1, Name = "HD", CutoffFormatScore = 500 }]);

            List<SearchableSeries> seriesList = new(SeriesCount);
            for (int i = 1; i <= SeriesCount; i++)
            {
                seriesList.Add(new SearchableSeries { Id = i, Title = $"Series {i}", QualityProfileId = 1, Monitored = true });
            }

            _sonarrClient
                .StreamAllSeriesAsync(sonarrInstance, Arg.Any<CancellationToken>())
                .Returns(ToAsyncEnumerable(seriesList));

            int inFlight = 0;
            int maxInFlight = 0;
            object gate = new();

            async Task TrackInFlightAsync()
            {
                lock (gate)
                {
                    inFlight++;
                    maxInFlight = Math.Max(maxInFlight, inFlight);
                }

                await Task.Delay(50);

                lock (gate)
                {
                    inFlight--;
                }
            }

            _sonarrClient
                .GetEpisodesAsync(sonarrInstance, Arg.Any<long>())
                .Returns(async callInfo =>
                {
                    await TrackInFlightAsync();
                    long seriesId = callInfo.Arg<long>();
                    return new List<SearchableEpisode>
                    {
                        new() { Id = seriesId * 100, SeasonNumber = 1, EpisodeNumber = 1, EpisodeFileId = seriesId * 1000, HasFile = true, Monitored = true }
                    };
                });

            _sonarrClient
                .GetEpisodeFilesAsync(sonarrInstance, Arg.Any<long>())
                .Returns(async callInfo =>
                {
                    await TrackInFlightAsync();
                    long seriesId = callInfo.Arg<long>();
                    return new List<ArrEpisodeFile>
                    {
                        new() { Id = seriesId * 1000, CustomFormatScore = 300, QualityCutoffNotMet = false }
                    };
                });

            CustomFormatScoreSyncer sut = new(
                _logger,
                dataContext,
                eventsContext,
                _radarrClient,
                _sonarrClient,
                _lidarrClient,
                _whisparrV2Client,
                _whisparrV3Client,
                new FakeTimeProvider(),
                _statusNotifier,
                new SqliteDatabaseProvider());

            await sut.ExecuteAsync();

            maxInFlight.ShouldBeGreaterThanOrEqualTo(3);
            maxInFlight.ShouldBeLessThanOrEqualTo(FetchConcurrency * 2);

            List<CustomFormatScoreEntry> entries = await eventsContext.CustomFormatScoreEntries.ToListAsync();
            entries.Count.ShouldBe(SeriesCount);

            foreach (SearchableSeries series in seriesList)
            {
                CustomFormatScoreEntry? entry = entries.FirstOrDefault(e => e.ExternalItemId == series.Id);
                entry.ShouldNotBeNull();
                entry.CurrentScore.ShouldBe(300);
                entry.CutoffScore.ShouldBe(500);
            }

            List<CustomFormatScoreHistory> history = await eventsContext.CustomFormatScoreHistory.ToListAsync();
            history.Count.ShouldBe(SeriesCount);
        }
        finally
        {
            dataContext.Dispose();
            eventsContext.Dispose();
            File.Delete(dbPath);
            File.Delete($"{dbPath}-wal");
            File.Delete($"{dbPath}-shm");
            File.Delete(eventsDbPath);
            File.Delete($"{eventsDbPath}-wal");
            File.Delete($"{eventsDbPath}-shm");
        }
    }

    #endregion

    #region Lidarr Sync Tests

    private async Task<ArrInstance> AddLidarrWithSyncAsync()
    {
        ArrInstance lidarrInstance = TestDataContextFactory.AddLidarrInstance(_fixture.DataContext);

        _fixture.DataContext.SeekerInstanceConfigs.Add(new SeekerInstanceConfig
        {
            ArrInstanceId = lidarrInstance.Id,
            ArrInstance = lidarrInstance,
            Enabled = true,
            UseCustomFormatScore = true
        });
        await _fixture.DataContext.SaveChangesAsync();
        await _fixture.EventsContext.SaveChangesAsync();

        _lidarrClient
            .GetQualityProfilesAsync(lidarrInstance)
            .Returns([new ArrQualityProfile { Id = 1, Name = "Lossless", CutoffFormatScore = 300 }]);

        return lidarrInstance;
    }

    private void StubLidarrLibrary(
        ArrInstance lidarrInstance,
        SearchableArtist artist,
        List<SearchableAlbum> albums,
        List<ArrTrackFile> trackFiles)
    {
        _lidarrClient
            .StreamAllArtistsAsync(lidarrInstance, Arg.Any<CancellationToken>())
            .Returns(_ => ToAsyncEnumerable<SearchableArtist>([artist]));
        _lidarrClient
            .GetAlbumsAsync(lidarrInstance, artist.Id, Arg.Any<CancellationToken>())
            .Returns(albums);
        _lidarrClient
            .GetTrackFilesAsync(lidarrInstance, artist.Id, Arg.Any<CancellationToken>())
            .Returns(trackFiles);
    }

    private static SearchableArtist LidarrArtist(bool monitored = true) => new()
    {
        Id = 5,
        ArtistName = "Test Artist",
        Monitored = monitored,
        QualityProfileId = 1,
    };

    private static SearchableAlbum LidarrAlbum(long id, int trackFileCount, int trackCount, bool monitored = true) => new()
    {
        Id = id,
        Title = $"Album {id}",
        ArtistId = 5,
        Monitored = monitored,
        ReleaseDate = new DateTimeOffset(2020, 5, 6, 0, 0, 0, TimeSpan.Zero),
        LastSearchTime = new DateTimeOffset(2026, 2, 3, 0, 0, 0, TimeSpan.Zero),
        Statistics = new AlbumStatistics { TrackFileCount = trackFileCount, TrackCount = trackCount },
    };

    [Fact]
    public async Task ExecuteAsync_SyncsLidarrAlbumScoreFromTheFirstTrackFile()
    {
        // Arrange
        ArrInstance lidarrInstance = await AddLidarrWithSyncAsync();
        StubLidarrLibrary(
            lidarrInstance,
            LidarrArtist(),
            [LidarrAlbum(50, trackFileCount: 3, trackCount: 3)],
            [
                new ArrTrackFile { Id = 903, AlbumId = 50, CustomFormatScore = 5 },
                new ArrTrackFile { Id = 901, AlbumId = 50, CustomFormatScore = 120 },
                new ArrTrackFile { Id = 902, AlbumId = 50, CustomFormatScore = 40 },
            ]);

        CustomFormatScoreSyncer sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert
        CustomFormatScoreEntry entry = (await _fixture.EventsContext.CustomFormatScoreEntries.ToListAsync()).ShouldHaveSingleItem();
        entry.ItemType.ShouldBe(InstanceType.Lidarr);
        entry.ExternalItemId.ShouldBe(5);
        entry.EpisodeId.ShouldBe(50);
        entry.Title.ShouldBe("Test Artist - Album 50");
        entry.FileId.ShouldBe(901);
        entry.CurrentScore.ShouldBe(120);
        entry.SeasonNumber.ShouldBe(0);
        entry.AirDateUtc.ShouldBe(new DateTimeOffset(2020, 5, 6, 0, 0, 0, TimeSpan.Zero));
        entry.LastSearchTime.ShouldBe(new DateTimeOffset(2026, 2, 3, 0, 0, 0, TimeSpan.Zero));
        _sonarrClient.ReceivedCalls().ShouldBeEmpty();
        _radarrClient.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_LidarrAlbumWithMissingTracks_HasNoEntry()
    {
        // Arrange
        ArrInstance lidarrInstance = await AddLidarrWithSyncAsync();
        StubLidarrLibrary(
            lidarrInstance,
            LidarrArtist(),
            [LidarrAlbum(50, trackFileCount: 2, trackCount: 3), LidarrAlbum(51, trackFileCount: 0, trackCount: 3)],
            [
                new ArrTrackFile { Id = 901, AlbumId = 50, CustomFormatScore = 120 },
                new ArrTrackFile { Id = 902, AlbumId = 50, CustomFormatScore = 40 },
            ]);

        CustomFormatScoreSyncer sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert
        (await _fixture.EventsContext.CustomFormatScoreEntries.ToListAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_LidarrAlbumLosingTracks_KeepsItsEntryAgainstStaleCleanup()
    {
        // Arrange
        ArrInstance lidarrInstance = await AddLidarrWithSyncAsync();
        _fixture.EventsContext.CustomFormatScoreEntries.Add(new CustomFormatScoreEntry
        {
            ArrInstanceId = lidarrInstance.Id,
            ExternalItemId = 5,
            EpisodeId = 50,
            ItemType = InstanceType.Lidarr,
            Title = "Test Artist - Album 50",
            FileId = 901,
            CurrentScore = 120,
            CutoffScore = 300,
            QualityProfileName = "Lossless",
            LastSyncedAt = new DateTime(1999, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });
        await _fixture.EventsContext.SaveChangesAsync();
        StubLidarrLibrary(
            lidarrInstance,
            LidarrArtist(),
            [LidarrAlbum(50, trackFileCount: 2, trackCount: 3)],
            [new ArrTrackFile { Id = 901, AlbumId = 50, CustomFormatScore = 120 }]);

        CustomFormatScoreSyncer sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert
        (await _fixture.EventsContext.CustomFormatScoreEntries.ToListAsync()).ShouldHaveSingleItem().EpisodeId.ShouldBe(50);
    }

    [Fact]
    public async Task ExecuteAsync_SyncsLidarrCutoffFromTheArtistsProfile()
    {
        // Arrange
        ArrInstance lidarrInstance = await AddLidarrWithSyncAsync();
        StubLidarrLibrary(
            lidarrInstance,
            LidarrArtist(),
            [LidarrAlbum(50, trackFileCount: 1, trackCount: 1)],
            [new ArrTrackFile { Id = 901, AlbumId = 50, CustomFormatScore = 120 }]);

        CustomFormatScoreSyncer sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert
        CustomFormatScoreEntry entry = (await _fixture.EventsContext.CustomFormatScoreEntries.ToListAsync()).ShouldHaveSingleItem();
        entry.CutoffScore.ShouldBe(300);
        entry.QualityProfileName.ShouldBe("Lossless");
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    public async Task ExecuteAsync_LidarrIsMonitored_RequiresArtistAndAlbum(bool artistMonitored, bool albumMonitored, bool expected)
    {
        // Arrange
        ArrInstance lidarrInstance = await AddLidarrWithSyncAsync();
        StubLidarrLibrary(
            lidarrInstance,
            LidarrArtist(artistMonitored),
            [LidarrAlbum(50, trackFileCount: 1, trackCount: 1, monitored: albumMonitored)],
            [new ArrTrackFile { Id = 901, AlbumId = 50, CustomFormatScore = 120 }]);

        CustomFormatScoreSyncer sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert
        (await _fixture.EventsContext.CustomFormatScoreEntries.ToListAsync()).ShouldHaveSingleItem().IsMonitored.ShouldBe(expected);
    }

    [Fact]
    public async Task ExecuteAsync_LidarrSync_CleansUpEntriesForRemovedAlbums()
    {
        // Arrange
        ArrInstance lidarrInstance = await AddLidarrWithSyncAsync();
        _fixture.EventsContext.CustomFormatScoreEntries.Add(new CustomFormatScoreEntry
        {
            ArrInstanceId = lidarrInstance.Id,
            ExternalItemId = 5,
            EpisodeId = 999,
            ItemType = InstanceType.Lidarr,
            Title = "Test Artist - Deleted Album",
            FileId = 999,
            CurrentScore = 100,
            CutoffScore = 300,
            QualityProfileName = "Lossless",
            LastSyncedAt = new DateTime(1999, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });
        await _fixture.EventsContext.SaveChangesAsync();
        StubLidarrLibrary(
            lidarrInstance,
            LidarrArtist(),
            [LidarrAlbum(50, trackFileCount: 1, trackCount: 1)],
            [new ArrTrackFile { Id = 901, AlbumId = 50, CustomFormatScore = 120 }]);

        CustomFormatScoreSyncer sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert
        (await _fixture.EventsContext.CustomFormatScoreEntries.ToListAsync()).ShouldHaveSingleItem().EpisodeId.ShouldBe(50);
    }

    [Fact]
    public async Task ExecuteAsync_LidarrFetchFails_KeepsTheArtistsEntries()
    {
        // Arrange
        ArrInstance lidarrInstance = await AddLidarrWithSyncAsync();
        _fixture.EventsContext.CustomFormatScoreEntries.Add(new CustomFormatScoreEntry
        {
            ArrInstanceId = lidarrInstance.Id,
            ExternalItemId = 5,
            EpisodeId = 50,
            ItemType = InstanceType.Lidarr,
            Title = "Test Artist - Album 50",
            FileId = 901,
            CurrentScore = 120,
            CutoffScore = 300,
            QualityProfileName = "Lossless",
            LastSyncedAt = new DateTime(1999, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });
        await _fixture.EventsContext.SaveChangesAsync();
        _lidarrClient
            .StreamAllArtistsAsync(lidarrInstance, Arg.Any<CancellationToken>())
            .Returns(_ => ToAsyncEnumerable<SearchableArtist>([LidarrArtist()]));
        _lidarrClient
            .GetAlbumsAsync(lidarrInstance, 5, Arg.Any<CancellationToken>())
            .Returns<List<SearchableAlbum>>(_ => throw new HttpRequestException("boom"));
        _lidarrClient
            .GetTrackFilesAsync(lidarrInstance, 5, Arg.Any<CancellationToken>())
            .Returns([]);

        CustomFormatScoreSyncer sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert
        (await _fixture.EventsContext.CustomFormatScoreEntries.ToListAsync()).ShouldHaveSingleItem().EpisodeId.ShouldBe(50);
    }

    [Fact]
    public async Task ExecuteAsync_LidarrAlbumLosingTracksAndUnmonitored_ClearsIsMonitored()
    {
        // Arrange
        ArrInstance lidarrInstance = await AddLidarrWithSyncAsync();
        _fixture.EventsContext.CustomFormatScoreEntries.Add(new CustomFormatScoreEntry
        {
            ArrInstanceId = lidarrInstance.Id,
            ExternalItemId = 5,
            EpisodeId = 50,
            ItemType = InstanceType.Lidarr,
            Title = "Test Artist - Album 50",
            FileId = 901,
            CurrentScore = 120,
            CutoffScore = 300,
            QualityProfileName = "Lossless",
            IsMonitored = true,
            LastSyncedAt = new DateTime(1999, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });
        await _fixture.EventsContext.SaveChangesAsync();
        StubLidarrLibrary(
            lidarrInstance,
            LidarrArtist(),
            [LidarrAlbum(50, trackFileCount: 2, trackCount: 3, monitored: false)],
            [new ArrTrackFile { Id = 901, AlbumId = 50, CustomFormatScore = 120 }]);

        CustomFormatScoreSyncer sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert
        (await _fixture.EventsContext.CustomFormatScoreEntries.AsNoTracking().ToListAsync()).ShouldHaveSingleItem().IsMonitored.ShouldBeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_LidarrSync_RemovedAlbumKeepsSiblingHistory()
    {
        // Arrange
        ArrInstance lidarrInstance = await AddLidarrWithSyncAsync();
        foreach (long albumId in new long[] { 50, 999 })
        {
            _fixture.EventsContext.CustomFormatScoreEntries.Add(new CustomFormatScoreEntry
            {
                ArrInstanceId = lidarrInstance.Id,
                ExternalItemId = 5,
                EpisodeId = albumId,
                ItemType = InstanceType.Lidarr,
                Title = $"Test Artist - Album {albumId}",
                FileId = 900 + albumId,
                CurrentScore = 120,
                CutoffScore = 300,
                QualityProfileName = "Lossless",
                LastSyncedAt = new DateTime(1999, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            });
            _fixture.EventsContext.CustomFormatScoreHistory.Add(new CustomFormatScoreHistory
            {
                ArrInstanceId = lidarrInstance.Id,
                ExternalItemId = 5,
                EpisodeId = albumId,
                ItemType = InstanceType.Lidarr,
                Title = $"Test Artist - Album {albumId}",
                Score = 120,
                CutoffScore = 300,
                RecordedAt = DateTime.UtcNow.AddHours(-1)
            });
        }
        await _fixture.EventsContext.SaveChangesAsync();
        StubLidarrLibrary(
            lidarrInstance,
            LidarrArtist(),
            [LidarrAlbum(50, trackFileCount: 1, trackCount: 1)],
            [new ArrTrackFile { Id = 950, AlbumId = 50, CustomFormatScore = 120 }]);

        CustomFormatScoreSyncer sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert
        (await _fixture.EventsContext.CustomFormatScoreEntries.ToListAsync()).ShouldHaveSingleItem().EpisodeId.ShouldBe(50);
        (await _fixture.EventsContext.CustomFormatScoreHistory.ToListAsync()).ShouldHaveSingleItem().EpisodeId.ShouldBe(50);
    }

    [Fact]
    public async Task ExecuteAsync_LidarrSync_FlushesFullArtistChunks()
    {
        // Arrange
        ArrInstance lidarrInstance = await AddLidarrWithSyncAsync();
        List<SearchableArtist> artists = [];
        for (int i = 1; i <= ChunkSize + 1; i++)
        {
            artists.Add(new SearchableArtist { Id = i, ArtistName = $"Artist {i}", Monitored = true, QualityProfileId = 1 });
        }

        _lidarrClient
            .StreamAllArtistsAsync(lidarrInstance, Arg.Any<CancellationToken>())
            .Returns(_ => ToAsyncEnumerable(artists));
        _lidarrClient
            .GetAlbumsAsync(lidarrInstance, Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => Task.FromResult(new List<SearchableAlbum> { LidarrAlbum(callInfo.Arg<long>() * 10, trackFileCount: 1, trackCount: 1) }));
        _lidarrClient
            .GetTrackFilesAsync(lidarrInstance, Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => Task.FromResult(new List<ArrTrackFile> { new() { Id = callInfo.Arg<long>() * 100, AlbumId = callInfo.Arg<long>() * 10, CustomFormatScore = 120 } }));

        CustomFormatScoreSyncer sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert
        (await _fixture.EventsContext.CustomFormatScoreEntries.CountAsync()).ShouldBe(ChunkSize + 1);
    }

    [Fact]
    public async Task ExecuteAsync_SonarrSync_FlushesFullSeriesChunks()
    {
        // Arrange
        ArrInstance sonarrInstance = TestDataContextFactory.AddSonarrInstance(_fixture.DataContext);
        _fixture.DataContext.SeekerInstanceConfigs.Add(new SeekerInstanceConfig
        {
            ArrInstanceId = sonarrInstance.Id,
            ArrInstance = sonarrInstance,
            Enabled = true,
            UseCustomFormatScore = true
        });
        await _fixture.DataContext.SaveChangesAsync();
        await _fixture.EventsContext.SaveChangesAsync();

        List<SearchableSeries> series = [];
        for (int i = 1; i <= ChunkSize + 1; i++)
        {
            series.Add(new SearchableSeries { Id = i, Title = $"Series {i}", Monitored = true, QualityProfileId = 1 });
        }

        _sonarrClient
            .GetQualityProfilesAsync(sonarrInstance)
            .Returns([new ArrQualityProfile { Id = 1, Name = "HD", CutoffFormatScore = 500 }]);
        _sonarrClient
            .StreamAllSeriesAsync(sonarrInstance, Arg.Any<CancellationToken>())
            .Returns(_ => ToAsyncEnumerable(series));
        _sonarrClient
            .GetEpisodesAsync(sonarrInstance, Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => Task.FromResult(new List<SearchableEpisode>
            {
                new() { Id = callInfo.ArgAt<long>(1) * 10, SeasonNumber = 1, EpisodeNumber = 1, EpisodeFileId = callInfo.ArgAt<long>(1) * 100, HasFile = true, Monitored = true }
            }));
        _sonarrClient
            .GetEpisodeFilesAsync(sonarrInstance, Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => Task.FromResult(new List<ArrEpisodeFile>
            {
                new() { Id = callInfo.ArgAt<long>(1) * 100, CustomFormatScore = 120 }
            }));

        CustomFormatScoreSyncer sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert
        (await _fixture.EventsContext.CustomFormatScoreEntries.CountAsync()).ShouldBe(ChunkSize + 1);
    }

    [Fact]
    public async Task ExecuteAsync_LidarrCompleteAlbumWithoutTrackFile_KeepsEntryAndSkips()
    {
        // Arrange
        ArrInstance lidarrInstance = await AddLidarrWithSyncAsync();
        DateTimeOffset oldSync = new(1999, 1, 1, 0, 0, 0, TimeSpan.Zero);
        _fixture.EventsContext.CustomFormatScoreEntries.Add(new CustomFormatScoreEntry
        {
            ArrInstanceId = lidarrInstance.Id,
            ExternalItemId = 5,
            EpisodeId = 50,
            ItemType = InstanceType.Lidarr,
            Title = "Test Artist - Album 50",
            FileId = 901,
            CurrentScore = 120,
            CutoffScore = 300,
            QualityProfileName = "Lossless",
            LastSyncedAt = oldSync
        });
        await _fixture.EventsContext.SaveChangesAsync();
        StubLidarrLibrary(
            lidarrInstance,
            LidarrArtist(),
            [LidarrAlbum(50, trackFileCount: 3, trackCount: 3)],
            []);

        CustomFormatScoreSyncer sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert
        CustomFormatScoreEntry entry = (await _fixture.EventsContext.CustomFormatScoreEntries.AsNoTracking().ToListAsync()).ShouldHaveSingleItem();
        entry.CurrentScore.ShouldBe(120);
        entry.LastSyncedAt.ShouldBeGreaterThan(oldSync);
    }

    [Fact]
    public async Task ExecuteAsync_LidarrArtistProfileMissing_UsesUnknownProfile()
    {
        // Arrange
        ArrInstance lidarrInstance = await AddLidarrWithSyncAsync();
        StubLidarrLibrary(
            lidarrInstance,
            new SearchableArtist { Id = 5, ArtistName = "Test Artist", Monitored = true, QualityProfileId = 99 },
            [LidarrAlbum(50, trackFileCount: 1, trackCount: 1)],
            [new ArrTrackFile { Id = 901, AlbumId = 50, CustomFormatScore = 120 }]);

        CustomFormatScoreSyncer sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert
        CustomFormatScoreEntry entry = (await _fixture.EventsContext.CustomFormatScoreEntries.ToListAsync()).ShouldHaveSingleItem();
        entry.CutoffScore.ShouldBe(0);
        entry.QualityProfileName.ShouldBe("Unknown");
    }

    #endregion

    #region Score Unchanged Tests

    [Fact]
    public async Task ExecuteAsync_ScoreUnchanged_DoesNotRecordHistory()
    {
        // Arrange
        var radarrInstance = TestDataContextFactory.AddRadarrInstance(_fixture.DataContext);

        _fixture.DataContext.SeekerInstanceConfigs.Add(new SeekerInstanceConfig
        {
            ArrInstanceId = radarrInstance.Id,
            ArrInstance = radarrInstance,
            Enabled = true,
            UseCustomFormatScore = true
        });

        // Pre-existing entry with score = 250 (same as what will be returned)
        _fixture.EventsContext.CustomFormatScoreEntries.Add(new CustomFormatScoreEntry
        {
            ArrInstanceId = radarrInstance.Id,
            ExternalItemId = 10,
            EpisodeId = 0,
            ItemType = InstanceType.Radarr,
            Title = "Test Movie",
            FileId = 100,
            CurrentScore = 250,
            CutoffScore = 500,
            QualityProfileName = "HD",
            LastSyncedAt = DateTime.UtcNow.AddHours(-1)
        });
        await _fixture.DataContext.SaveChangesAsync();
        await _fixture.EventsContext.SaveChangesAsync();

        _radarrClient
            .GetQualityProfilesAsync(radarrInstance)
            .Returns([new ArrQualityProfile { Id = 1, Name = "HD", CutoffFormatScore = 500 }]);

        _radarrClient
            .StreamAllMoviesAsync(radarrInstance, Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable<SearchableMovie>([
                new SearchableMovie
                {
                    Id = 10, Title = "Test Movie", HasFile = true,
                    MovieFile = new MovieFileInfo { Id = 100, QualityCutoffNotMet = false },
                    QualityProfileId = 1, Status = "released", Monitored = true
                }
            ]));

        // Score unchanged: still 250
        _radarrClient
            .GetMovieFileScoresAsync(radarrInstance, Arg.Is<List<long>>(ids => ids.Contains(100)))
            .Returns(new Dictionary<long, int> { { 100, 250 } });

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert — no history entries (score didn't change)
        var history = await _fixture.EventsContext.CustomFormatScoreHistory.ToListAsync();
        history.ShouldBeEmpty();

        // Entry should still be updated (LastSyncedAt changes)
        var entries = await _fixture.EventsContext.CustomFormatScoreEntries.ToListAsync();
        entries.ShouldHaveSingleItem();
        entries[0].CurrentScore.ShouldBe(250);
    }

    #endregion

    #region Stale Entry Cleanup Tests

    [Fact]
    public async Task ExecuteAsync_CleansUpEntriesForRemovedMovies()
    {
        // Arrange
        var radarrInstance = TestDataContextFactory.AddRadarrInstance(_fixture.DataContext);

        _fixture.DataContext.SeekerInstanceConfigs.Add(new SeekerInstanceConfig
        {
            ArrInstanceId = radarrInstance.Id,
            ArrInstance = radarrInstance,
            Enabled = true,
            UseCustomFormatScore = true
        });

        // Pre-existing entry for a movie that no longer exists in library
        _fixture.EventsContext.CustomFormatScoreEntries.Add(new CustomFormatScoreEntry
        {
            ArrInstanceId = radarrInstance.Id,
            ExternalItemId = 999,
            EpisodeId = 0,
            ItemType = InstanceType.Radarr,
            Title = "Deleted Movie",
            FileId = 999,
            CurrentScore = 100,
            CutoffScore = 500,
            QualityProfileName = "HD",
            LastSyncedAt = new DateTime(1999, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });
        await _fixture.DataContext.SaveChangesAsync();
        await _fixture.EventsContext.SaveChangesAsync();

        _radarrClient
            .GetQualityProfilesAsync(radarrInstance)
            .Returns([new ArrQualityProfile { Id = 1, Name = "HD", CutoffFormatScore = 500 }]);

        // Library now only has movie 10 (not 999)
        _radarrClient
            .StreamAllMoviesAsync(radarrInstance, Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable<SearchableMovie>([
                new SearchableMovie
                {
                    Id = 10, Title = "Current Movie", HasFile = true,
                    MovieFile = new MovieFileInfo { Id = 100, QualityCutoffNotMet = false },
                    QualityProfileId = 1, Status = "released", Monitored = true
                }
            ]));

        _radarrClient
            .GetMovieFileScoresAsync(radarrInstance, Arg.Any<List<long>>())
            .Returns(new Dictionary<long, int> { { 100, 250 } });

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert — entry for removed movie 999 should be deleted
        var entries = await _fixture.EventsContext.CustomFormatScoreEntries.ToListAsync();
        entries.ShouldHaveSingleItem();
        entries[0].ExternalItemId.ShouldBe(10);
    }

    [Fact]
    public async Task ExecuteAsync_PreservesEntryWhenMovieExistsButHasNoFile()
    {
        // Arrange
        var radarrInstance = TestDataContextFactory.AddRadarrInstance(_fixture.DataContext);

        _fixture.DataContext.SeekerInstanceConfigs.Add(new SeekerInstanceConfig
        {
            ArrInstanceId = radarrInstance.Id,
            ArrInstance = radarrInstance,
            Enabled = true,
            UseCustomFormatScore = true
        });

        // Pre-existing entry with score history
        _fixture.EventsContext.CustomFormatScoreEntries.Add(new CustomFormatScoreEntry
        {
            ArrInstanceId = radarrInstance.Id,
            ExternalItemId = 10,
            EpisodeId = 0,
            ItemType = InstanceType.Radarr,
            Title = "Mario Bros",
            FileId = 100,
            CurrentScore = 250,
            CutoffScore = 500,
            QualityProfileName = "HD",
            LastSyncedAt = DateTime.UtcNow.AddHours(-1)
        });
        _fixture.EventsContext.CustomFormatScoreHistory.Add(new CustomFormatScoreHistory
        {
            ArrInstanceId = radarrInstance.Id,
            ExternalItemId = 10,
            EpisodeId = 0,
            ItemType = InstanceType.Radarr,
            Title = "Mario Bros",
            Score = 250,
            CutoffScore = 500,
            RecordedAt = DateTime.UtcNow.AddHours(-1)
        });
        await _fixture.DataContext.SaveChangesAsync();
        await _fixture.EventsContext.SaveChangesAsync();

        _radarrClient
            .GetQualityProfilesAsync(radarrInstance)
            .Returns([new ArrQualityProfile { Id = 1, Name = "HD", CutoffFormatScore = 500 }]);

        // Movie still exists in Radarr but HasFile is false (RSS upgrade in progress)
        _radarrClient
            .StreamAllMoviesAsync(radarrInstance, Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable<SearchableMovie>([
                new SearchableMovie
                {
                    Id = 10, Title = "Mario Bros", HasFile = false,
                    MovieFile = null,
                    QualityProfileId = 1, Status = "released", Monitored = true
                }
            ]));

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert — entry and history should be preserved since the movie still exists
        var entries = await _fixture.EventsContext.CustomFormatScoreEntries.ToListAsync();
        entries.ShouldHaveSingleItem();
        entries[0].ExternalItemId.ShouldBe(10);
        entries[0].CurrentScore.ShouldBe(250);

        var history = await _fixture.EventsContext.CustomFormatScoreHistory.ToListAsync();
        history.ShouldHaveSingleItem();
        history[0].Score.ShouldBe(250);
    }

    [Fact]
    public async Task ExecuteAsync_ClearsIsMonitoredWhenMovieWithoutFileIsUnmonitored()
    {
        // Arrange
        ArrInstance radarrInstance = TestDataContextFactory.AddRadarrInstance(_fixture.DataContext);

        _fixture.DataContext.SeekerInstanceConfigs.Add(new SeekerInstanceConfig
        {
            ArrInstanceId = radarrInstance.Id,
            ArrInstance = radarrInstance,
            Enabled = true,
            UseCustomFormatScore = true
        });

        _fixture.EventsContext.CustomFormatScoreEntries.Add(new CustomFormatScoreEntry
        {
            ArrInstanceId = radarrInstance.Id,
            ExternalItemId = 10,
            EpisodeId = 0,
            ItemType = InstanceType.Radarr,
            Title = "Mario Bros",
            FileId = 100,
            CurrentScore = 250,
            CutoffScore = 500,
            QualityProfileName = "HD",
            IsMonitored = true,
            LastSyncedAt = DateTime.UtcNow.AddHours(-1)
        });
        await _fixture.DataContext.SaveChangesAsync();
        await _fixture.EventsContext.SaveChangesAsync();

        _radarrClient
            .GetQualityProfilesAsync(radarrInstance)
            .Returns([new ArrQualityProfile { Id = 1, Name = "HD", CutoffFormatScore = 500 }]);

        _radarrClient
            .StreamAllMoviesAsync(radarrInstance, Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable<SearchableMovie>([
                new SearchableMovie
                {
                    Id = 10, Title = "Mario Bros", HasFile = false,
                    MovieFile = null,
                    QualityProfileId = 1, Status = "released", Monitored = false
                }
            ]));

        CustomFormatScoreSyncer sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert
        List<CustomFormatScoreEntry> entries = await _fixture.EventsContext.CustomFormatScoreEntries.AsNoTracking().ToListAsync();
        entries.ShouldHaveSingleItem();
        entries[0].IsMonitored.ShouldBeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_ClearsIsMonitoredWhenUnmonitoredMovieFileScoreNotReturned()
    {
        // Arrange
        ArrInstance radarrInstance = TestDataContextFactory.AddRadarrInstance(_fixture.DataContext);

        _fixture.DataContext.SeekerInstanceConfigs.Add(new SeekerInstanceConfig
        {
            ArrInstanceId = radarrInstance.Id,
            ArrInstance = radarrInstance,
            Enabled = true,
            UseCustomFormatScore = true
        });

        _fixture.EventsContext.CustomFormatScoreEntries.Add(new CustomFormatScoreEntry
        {
            ArrInstanceId = radarrInstance.Id,
            ExternalItemId = 10,
            EpisodeId = 0,
            ItemType = InstanceType.Radarr,
            Title = "Mario Bros",
            FileId = 100,
            CurrentScore = 250,
            CutoffScore = 500,
            QualityProfileName = "HD",
            IsMonitored = true,
            LastSyncedAt = DateTime.UtcNow.AddHours(-1)
        });
        await _fixture.DataContext.SaveChangesAsync();
        await _fixture.EventsContext.SaveChangesAsync();

        _radarrClient
            .GetQualityProfilesAsync(radarrInstance)
            .Returns([new ArrQualityProfile { Id = 1, Name = "HD", CutoffFormatScore = 500 }]);

        _radarrClient
            .StreamAllMoviesAsync(radarrInstance, Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable<SearchableMovie>([
                new SearchableMovie
                {
                    Id = 10, Title = "Mario Bros", HasFile = true,
                    MovieFile = new MovieFileInfo { Id = 200, QualityCutoffNotMet = false },
                    QualityProfileId = 1, Status = "released", Monitored = false
                }
            ]));

        _radarrClient
            .GetMovieFileScoresAsync(radarrInstance, Arg.Any<List<long>>())
            .Returns(new Dictionary<long, int>());

        CustomFormatScoreSyncer sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert
        List<CustomFormatScoreEntry> entries = await _fixture.EventsContext.CustomFormatScoreEntries.AsNoTracking().ToListAsync();
        entries.ShouldHaveSingleItem();
        entries[0].IsMonitored.ShouldBeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_PreservesEntryWhenMovieFileScoreNotReturned()
    {
        // Arrange
        var radarrInstance = TestDataContextFactory.AddRadarrInstance(_fixture.DataContext);

        _fixture.DataContext.SeekerInstanceConfigs.Add(new SeekerInstanceConfig
        {
            ArrInstanceId = radarrInstance.Id,
            ArrInstance = radarrInstance,
            Enabled = true,
            UseCustomFormatScore = true
        });

        // Pre-existing entry with history
        _fixture.EventsContext.CustomFormatScoreEntries.Add(new CustomFormatScoreEntry
        {
            ArrInstanceId = radarrInstance.Id,
            ExternalItemId = 10,
            EpisodeId = 0,
            ItemType = InstanceType.Radarr,
            Title = "Mario Bros",
            FileId = 100,
            CurrentScore = 250,
            CutoffScore = 500,
            QualityProfileName = "HD",
            LastSyncedAt = DateTime.UtcNow.AddHours(-1)
        });
        _fixture.EventsContext.CustomFormatScoreHistory.Add(new CustomFormatScoreHistory
        {
            ArrInstanceId = radarrInstance.Id,
            ExternalItemId = 10,
            EpisodeId = 0,
            ItemType = InstanceType.Radarr,
            Title = "Mario Bros",
            Score = 250,
            CutoffScore = 500,
            RecordedAt = DateTime.UtcNow.AddHours(-1)
        });
        await _fixture.DataContext.SaveChangesAsync();
        await _fixture.EventsContext.SaveChangesAsync();

        _radarrClient
            .GetQualityProfilesAsync(radarrInstance)
            .Returns([new ArrQualityProfile { Id = 1, Name = "HD", CutoffFormatScore = 500 }]);

        // Movie has a new file (different FileId) after RSS upgrade
        _radarrClient
            .StreamAllMoviesAsync(radarrInstance, Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable<SearchableMovie>([
                new SearchableMovie
                {
                    Id = 10, Title = "Mario Bros", HasFile = true,
                    MovieFile = new MovieFileInfo { Id = 200, QualityCutoffNotMet = false },
                    QualityProfileId = 1, Status = "released", Monitored = true
                }
            ]));

        // New file returns no score (not yet calculated by Radarr)
        _radarrClient
            .GetMovieFileScoresAsync(radarrInstance, Arg.Any<List<long>>())
            .Returns(new Dictionary<long, int>());

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert — entry and history should be preserved since the movie still exists
        var entries = await _fixture.EventsContext.CustomFormatScoreEntries.ToListAsync();
        entries.ShouldHaveSingleItem();
        entries[0].ExternalItemId.ShouldBe(10);
        entries[0].CurrentScore.ShouldBe(250);

        var history = await _fixture.EventsContext.CustomFormatScoreHistory.ToListAsync();
        history.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task ExecuteAsync_Sonarr_PreservesEntryWhenEpisodeTemporarilyWithoutFile()
    {
        // Arrange
        var sonarrInstance = TestDataContextFactory.AddSonarrInstance(_fixture.DataContext);

        _fixture.DataContext.SeekerInstanceConfigs.Add(new SeekerInstanceConfig
        {
            ArrInstanceId = sonarrInstance.Id,
            ArrInstance = sonarrInstance,
            Enabled = true,
            UseCustomFormatScore = true
        });

        // Pre-existing CF score entry for an episode
        _fixture.EventsContext.CustomFormatScoreEntries.Add(new CustomFormatScoreEntry
        {
            ArrInstanceId = sonarrInstance.Id,
            ExternalItemId = 10,
            EpisodeId = 100,
            ItemType = InstanceType.Sonarr,
            Title = "Test Series S01E01",
            FileId = 500,
            CurrentScore = 300,
            CutoffScore = 500,
            QualityProfileName = "HD",
            LastSyncedAt = DateTime.UtcNow.AddHours(-1)
        });
        _fixture.EventsContext.CustomFormatScoreHistory.Add(new CustomFormatScoreHistory
        {
            ArrInstanceId = sonarrInstance.Id,
            ExternalItemId = 10,
            EpisodeId = 100,
            ItemType = InstanceType.Sonarr,
            Title = "Test Series S01E01",
            Score = 300,
            CutoffScore = 500,
            RecordedAt = DateTime.UtcNow.AddHours(-1)
        });
        await _fixture.DataContext.SaveChangesAsync();
        await _fixture.EventsContext.SaveChangesAsync();

        _sonarrClient
            .GetQualityProfilesAsync(sonarrInstance)
            .Returns([new ArrQualityProfile { Id = 1, Name = "HD", CutoffFormatScore = 500 }]);

        _sonarrClient
            .StreamAllSeriesAsync(sonarrInstance, Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable<SearchableSeries>([
                new SearchableSeries { Id = 10, Title = "Test Series", QualityProfileId = 1, Monitored = true }
            ]));

        // Episode exists but has no file currently (RSS upgrade in progress)
        _sonarrClient
            .GetEpisodesAsync(sonarrInstance, 10)
            .Returns([
                new SearchableEpisode { Id = 100, SeasonNumber = 1, EpisodeNumber = 1, EpisodeFileId = 0, HasFile = false, Monitored = true }
            ]);

        _sonarrClient
            .GetEpisodeFilesAsync(sonarrInstance, 10)
            .Returns([]);

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert — entry and history should be preserved
        var entries = await _fixture.EventsContext.CustomFormatScoreEntries.ToListAsync();
        entries.ShouldHaveSingleItem();
        entries[0].ExternalItemId.ShouldBe(10);
        entries[0].EpisodeId.ShouldBe(100);
        entries[0].CurrentScore.ShouldBe(300);

        var history = await _fixture.EventsContext.CustomFormatScoreHistory.ToListAsync();
        history.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task ExecuteAsync_Sonarr_ClearsIsMonitoredWhenEpisodeWithoutFileIsUnmonitored()
    {
        // Arrange
        ArrInstance sonarrInstance = TestDataContextFactory.AddSonarrInstance(_fixture.DataContext);

        _fixture.DataContext.SeekerInstanceConfigs.Add(new SeekerInstanceConfig
        {
            ArrInstanceId = sonarrInstance.Id,
            ArrInstance = sonarrInstance,
            Enabled = true,
            UseCustomFormatScore = true
        });

        _fixture.EventsContext.CustomFormatScoreEntries.Add(new CustomFormatScoreEntry
        {
            ArrInstanceId = sonarrInstance.Id,
            ExternalItemId = 10,
            EpisodeId = 100,
            ItemType = InstanceType.Sonarr,
            Title = "Test Series S01E01",
            FileId = 500,
            CurrentScore = 300,
            CutoffScore = 500,
            QualityProfileName = "HD",
            IsMonitored = true,
            LastSyncedAt = new DateTime(1999, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });
        await _fixture.DataContext.SaveChangesAsync();
        await _fixture.EventsContext.SaveChangesAsync();

        _sonarrClient
            .GetQualityProfilesAsync(sonarrInstance)
            .Returns([new ArrQualityProfile { Id = 1, Name = "HD", CutoffFormatScore = 500 }]);

        _sonarrClient
            .StreamAllSeriesAsync(sonarrInstance, Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable<SearchableSeries>([
                new SearchableSeries { Id = 10, Title = "Test Series", QualityProfileId = 1, Monitored = true }
            ]));

        _sonarrClient
            .GetEpisodesAsync(sonarrInstance, 10)
            .Returns([
                new SearchableEpisode { Id = 100, SeasonNumber = 1, EpisodeNumber = 1, EpisodeFileId = 0, HasFile = false, Monitored = false }
            ]);

        _sonarrClient
            .GetEpisodeFilesAsync(sonarrInstance, 10)
            .Returns([]);

        CustomFormatScoreSyncer sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert
        List<CustomFormatScoreEntry> entries = await _fixture.EventsContext.CustomFormatScoreEntries.AsNoTracking().ToListAsync();
        entries.ShouldHaveSingleItem();
        entries[0].IsMonitored.ShouldBeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_Sonarr_RemovedEpisodeKeepsSiblingHistory()
    {
        // Arrange
        ArrInstance sonarrInstance = TestDataContextFactory.AddSonarrInstance(_fixture.DataContext);

        _fixture.DataContext.SeekerInstanceConfigs.Add(new SeekerInstanceConfig
        {
            ArrInstanceId = sonarrInstance.Id,
            ArrInstance = sonarrInstance,
            Enabled = true,
            UseCustomFormatScore = true
        });

        _fixture.EventsContext.CustomFormatScoreEntries.Add(new CustomFormatScoreEntry
        {
            ArrInstanceId = sonarrInstance.Id,
            ExternalItemId = 10,
            EpisodeId = 100,
            ItemType = InstanceType.Sonarr,
            Title = "Test Series S01E01",
            FileId = 500,
            CurrentScore = 300,
            CutoffScore = 500,
            QualityProfileName = "HD",
            LastSyncedAt = new DateTime(1999, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });
        _fixture.EventsContext.CustomFormatScoreEntries.Add(new CustomFormatScoreEntry
        {
            ArrInstanceId = sonarrInstance.Id,
            ExternalItemId = 10,
            EpisodeId = 101,
            ItemType = InstanceType.Sonarr,
            Title = "Test Series S01E02",
            FileId = 501,
            CurrentScore = 300,
            CutoffScore = 500,
            QualityProfileName = "HD",
            LastSyncedAt = new DateTime(1999, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });
        _fixture.EventsContext.CustomFormatScoreHistory.Add(new CustomFormatScoreHistory
        {
            ArrInstanceId = sonarrInstance.Id,
            ExternalItemId = 10,
            EpisodeId = 100,
            ItemType = InstanceType.Sonarr,
            Title = "Test Series S01E01",
            Score = 300,
            CutoffScore = 500,
            RecordedAt = DateTime.UtcNow.AddHours(-1)
        });
        _fixture.EventsContext.CustomFormatScoreHistory.Add(new CustomFormatScoreHistory
        {
            ArrInstanceId = sonarrInstance.Id,
            ExternalItemId = 10,
            EpisodeId = 101,
            ItemType = InstanceType.Sonarr,
            Title = "Test Series S01E02",
            Score = 300,
            CutoffScore = 500,
            RecordedAt = DateTime.UtcNow.AddHours(-1)
        });
        await _fixture.DataContext.SaveChangesAsync();
        await _fixture.EventsContext.SaveChangesAsync();

        _sonarrClient
            .GetQualityProfilesAsync(sonarrInstance)
            .Returns([new ArrQualityProfile { Id = 1, Name = "HD", CutoffFormatScore = 500 }]);

        _sonarrClient
            .StreamAllSeriesAsync(sonarrInstance, Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable<SearchableSeries>([
                new SearchableSeries { Id = 10, Title = "Test Series", QualityProfileId = 1, Monitored = true }
            ]));

        _sonarrClient
            .GetEpisodesAsync(sonarrInstance, 10)
            .Returns([
                new SearchableEpisode { Id = 100, SeasonNumber = 1, EpisodeNumber = 1, EpisodeFileId = 500, HasFile = true, Monitored = true }
            ]);

        _sonarrClient
            .GetEpisodeFilesAsync(sonarrInstance, 10)
            .Returns([
                new ArrEpisodeFile { Id = 500, CustomFormatScore = 300, QualityCutoffNotMet = false }
            ]);

        CustomFormatScoreSyncer sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert
        List<CustomFormatScoreEntry> entries = await _fixture.EventsContext.CustomFormatScoreEntries.ToListAsync();
        entries.ShouldHaveSingleItem();
        entries[0].EpisodeId.ShouldBe(100);

        List<CustomFormatScoreHistory> history = await _fixture.EventsContext.CustomFormatScoreHistory.ToListAsync();
        history.ShouldHaveSingleItem();
        history[0].EpisodeId.ShouldBe(100);
    }

    [Fact]
    public async Task ExecuteAsync_Sonarr_PreservesEntryWhenSeriesFetchFails()
    {
        var sonarrInstance = TestDataContextFactory.AddSonarrInstance(_fixture.DataContext);

        _fixture.DataContext.SeekerInstanceConfigs.Add(new SeekerInstanceConfig
        {
            ArrInstanceId = sonarrInstance.Id,
            ArrInstance = sonarrInstance,
            Enabled = true,
            UseCustomFormatScore = true
        });

        _fixture.EventsContext.CustomFormatScoreEntries.Add(new CustomFormatScoreEntry
        {
            ArrInstanceId = sonarrInstance.Id,
            ExternalItemId = 10,
            EpisodeId = 100,
            ItemType = InstanceType.Sonarr,
            Title = "Test Series S01E01",
            FileId = 500,
            CurrentScore = 300,
            CutoffScore = 500,
            QualityProfileName = "HD",
            LastSyncedAt = new DateTime(1999, 12, 15, 0, 0, 0, DateTimeKind.Utc)
        });
        _fixture.EventsContext.CustomFormatScoreHistory.Add(new CustomFormatScoreHistory
        {
            ArrInstanceId = sonarrInstance.Id,
            ExternalItemId = 10,
            EpisodeId = 100,
            ItemType = InstanceType.Sonarr,
            Title = "Test Series S01E01",
            Score = 300,
            CutoffScore = 500,
            RecordedAt = new DateTime(1999, 12, 15, 0, 0, 0, DateTimeKind.Utc)
        });
        await _fixture.DataContext.SaveChangesAsync();
        await _fixture.EventsContext.SaveChangesAsync();

        _sonarrClient
            .GetQualityProfilesAsync(sonarrInstance)
            .Returns([new ArrQualityProfile { Id = 1, Name = "HD", CutoffFormatScore = 500 }]);

        _sonarrClient
            .StreamAllSeriesAsync(sonarrInstance, Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable<SearchableSeries>([
                new SearchableSeries { Id = 10, Title = "Test Series", QualityProfileId = 1, Monitored = true }
            ]));

        _sonarrClient
            .GetEpisodesAsync(sonarrInstance, 10)
            .Returns(Task.FromException<List<SearchableEpisode>>(new InvalidOperationException("transient failure")));

        _sonarrClient
            .GetEpisodeFilesAsync(sonarrInstance, 10)
            .Returns([]);

        var sut = CreateSut();

        await sut.ExecuteAsync();

        var entries = await _fixture.EventsContext.CustomFormatScoreEntries.ToListAsync();
        entries.ShouldHaveSingleItem();
        entries[0].ExternalItemId.ShouldBe(10);
        entries[0].CurrentScore.ShouldBe(300);

        var history = await _fixture.EventsContext.CustomFormatScoreHistory.ToListAsync();
        history.ShouldHaveSingleItem();
    }

    #endregion

    #region WAL Checkpoint Tests

    private const int CheckpointIntervalChunks = 10;
    private const int ChunkSize = 200;

    private static DataContext CreateFileBackedWalDataContext(string dbPath, params IInterceptor[] interceptors)
    {
        SqliteConnectionStringBuilder connectionStringBuilder = new()
        {
            DataSource = dbPath
        };

        DbContextOptionsBuilder<DataContext> optionsBuilder = new DbContextOptionsBuilder<DataContext>()
            .UseSqlite(connectionStringBuilder.ConnectionString);

        if (interceptors.Length > 0)
        {
            optionsBuilder.AddInterceptors(interceptors);
        }

        DbContextOptions<DataContext> options = optionsBuilder.Options;

        DataContext context = new(options);
        context.Database.EnsureCreated();
        context.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;");

        context.GeneralConfigs.Add(new GeneralConfig
        {
            Id = Guid.NewGuid(),
            DryRun = false,
            IgnoredDownloads = [],
            Log = new LoggingConfig()
        });

        context.ArrConfigs.AddRange(
            new ArrConfig { Id = Guid.NewGuid(), Type = InstanceType.Sonarr, Instances = [], FailedImportMaxStrikes = 3 },
            new ArrConfig { Id = Guid.NewGuid(), Type = InstanceType.Radarr, Instances = [], FailedImportMaxStrikes = 3 }
        );

        context.SaveChanges();

        return context;
    }

    private static EventsContext CreateFileBackedWalEventsContext(string dbPath, params IInterceptor[] interceptors)
    {
        SqliteConnectionStringBuilder connectionStringBuilder = new()
        {
            DataSource = dbPath
        };

        DbContextOptionsBuilder<EventsContext> optionsBuilder = new DbContextOptionsBuilder<EventsContext>()
            .UseSqlite(connectionStringBuilder.ConnectionString);

        if (interceptors.Length > 0)
        {
            optionsBuilder.AddInterceptors(interceptors);
        }

        EventsContext context = new(optionsBuilder.Options);
        context.Database.EnsureCreated();
        context.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;");

        return context;
    }

    [Fact]
    public async Task ExecuteAsync_Radarr_CheckspointsWalPeriodicallyOnLargeLibrary()
    {
        string dbPath = Path.Combine(Path.GetTempPath(), $"cfsync-wal-{Guid.NewGuid():N}.db");
        string eventsDbPath = Path.Combine(Path.GetTempPath(), $"cfsync-wal-events-{Guid.NewGuid():N}.db");
        string walPath = $"{eventsDbPath}-wal";
        CheckpointCountingInterceptor checkpointCounter = new();
        DataContext dataContext = CreateFileBackedWalDataContext(dbPath);
        EventsContext eventsContext = CreateFileBackedWalEventsContext(eventsDbPath, checkpointCounter);

        try
        {
            ArrConfig radarrConfig = dataContext.ArrConfigs.First(c => c.Type == InstanceType.Radarr);
            ArrInstance radarrInstance = new()
            {
                Id = Guid.NewGuid(),
                Name = "Test Radarr",
                Url = new Uri("http://radarr:7878"),
                ApiKey = "test-api-key",
                Enabled = true,
                ArrConfigId = radarrConfig.Id,
                ArrConfig = radarrConfig
            };
            dataContext.ArrInstances.Add(radarrInstance);
            dataContext.SeekerInstanceConfigs.Add(new SeekerInstanceConfig
            {
                ArrInstanceId = radarrInstance.Id,
                ArrInstance = radarrInstance,
                Enabled = true,
                UseCustomFormatScore = true
            });
            await dataContext.SaveChangesAsync();

            _radarrClient
                .GetQualityProfilesAsync(radarrInstance)
                .Returns([new ArrQualityProfile { Id = 1, Name = "HD", CutoffFormatScore = 500 }]);

            int movieCount = (CheckpointIntervalChunks * ChunkSize) + ChunkSize;

            List<SearchableMovie> movies = new(movieCount);
            Dictionary<long, int> scores = new(movieCount);
            for (int i = 1; i <= movieCount; i++)
            {
                movies.Add(new SearchableMovie
                {
                    Id = i,
                    Title = $"Movie {i}",
                    HasFile = true,
                    MovieFile = new MovieFileInfo { Id = i, QualityCutoffNotMet = false },
                    QualityProfileId = 1,
                    Status = "released",
                    Monitored = true
                });
                scores[i] = 100 + (i % 400);
            }

            _radarrClient
                .StreamAllMoviesAsync(radarrInstance, Arg.Any<CancellationToken>())
                .Returns(ToAsyncEnumerable(movies));

            _radarrClient
                .GetMovieFileScoresAsync(radarrInstance, Arg.Any<List<long>>())
                .Returns(callInfo =>
                {
                    List<long> fileIds = callInfo.Arg<List<long>>();
                    return fileIds
                        .Where(scores.ContainsKey)
                        .ToDictionary(id => id, id => scores[id]);
                });

            CustomFormatScoreSyncer sut = new(
                _logger,
                dataContext,
                eventsContext,
                _radarrClient,
                _sonarrClient,
                _lidarrClient,
                _whisparrV2Client,
                _whisparrV3Client,
                new FakeTimeProvider(),
                _statusNotifier,
                new SqliteDatabaseProvider());

            await sut.ExecuteAsync();

            int entryCount = await eventsContext.CustomFormatScoreEntries.CountAsync();
            entryCount.ShouldBe(movieCount);

            checkpointCounter.CheckpointCount.ShouldBeGreaterThanOrEqualTo(2);

            long walSizeBytes = File.Exists(walPath) ? new FileInfo(walPath).Length : 0;
            walSizeBytes.ShouldBeLessThan(2 * 1024 * 1024);
        }
        finally
        {
            dataContext.Dispose();
            eventsContext.Dispose();
            File.Delete(dbPath);
            File.Delete($"{dbPath}-wal");
            File.Delete($"{dbPath}-shm");
            File.Delete(eventsDbPath);
            File.Delete(walPath);
            File.Delete($"{eventsDbPath}-shm");
        }
    }

    #endregion

    private sealed class CheckpointCountingInterceptor : DbCommandInterceptor
    {
        private int _checkpointCount;

        public int CheckpointCount => _checkpointCount;

        private void CountIfCheckpoint(DbCommand command)
        {
            if (command.CommandText.Contains("wal_checkpoint", StringComparison.OrdinalIgnoreCase))
            {
                Interlocked.Increment(ref _checkpointCount);
            }
        }

        public override InterceptionResult<int> NonQueryExecuting(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
        {
            CountIfCheckpoint(command);
            return base.NonQueryExecuting(command, eventData, result);
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            CountIfCheckpoint(command);
            return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}
