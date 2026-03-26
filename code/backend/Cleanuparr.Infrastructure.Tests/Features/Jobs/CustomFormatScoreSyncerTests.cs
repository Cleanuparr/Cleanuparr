using Cleanuparr.Domain.Entities.Arr;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Arr.Interfaces;
using Cleanuparr.Infrastructure.Hubs;
using Cleanuparr.Infrastructure.Tests.Features.Jobs.TestHelpers;
using Cleanuparr.Persistence.Models.Configuration.Arr;
using Cleanuparr.Persistence.Models.Configuration.Seeker;
using Cleanuparr.Persistence.Models.State;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using CustomFormatScoreSyncer = Cleanuparr.Infrastructure.Features.Jobs.CustomFormatScoreSyncer;

namespace Cleanuparr.Infrastructure.Tests.Features.Jobs;

[Collection(JobHandlerCollection.Name)]
public class CustomFormatScoreSyncerTests : IDisposable
{
    private readonly JobHandlerFixture _fixture;
    private readonly Mock<ILogger<CustomFormatScoreSyncer>> _logger;
    private readonly Mock<IRadarrClient> _radarrClient;
    private readonly Mock<ISonarrClient> _sonarrClient;
    private readonly Mock<IHubContext<AppHub>> _hubContext;

    public CustomFormatScoreSyncerTests(JobHandlerFixture fixture)
    {
        _fixture = fixture;
        _fixture.RecreateDataContext();
        _fixture.ResetMocks();
        _logger = new Mock<ILogger<CustomFormatScoreSyncer>>();
        _radarrClient = new Mock<IRadarrClient>();
        _sonarrClient = new Mock<ISonarrClient>();
        _hubContext = new Mock<IHubContext<AppHub>>();

        var mockClients = new Mock<IHubClients>();
        var mockClientProxy = new Mock<IClientProxy>();
        mockClients.Setup(c => c.All).Returns(mockClientProxy.Object);
        _hubContext.Setup(h => h.Clients).Returns(mockClients.Object);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    private CustomFormatScoreSyncer CreateSut()
    {
        return new CustomFormatScoreSyncer(
            _logger.Object,
            _fixture.DataContext,
            _radarrClient.Object,
            _sonarrClient.Object,
            _fixture.TimeProvider,
            _hubContext.Object
        );
    }

    #region ExecuteAsync Tests

    [Fact]
    public async Task ExecuteAsync_WhenCustomFormatScoreDisabled_ReturnsEarly()
    {
        // Arrange — UseCustomFormatScore is false by default in seed data
        var config = await _fixture.DataContext.SeekerConfigs.FirstAsync();
        config.UseCustomFormatScore = false;
        await _fixture.DataContext.SaveChangesAsync();

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert — no API calls made
        _radarrClient.Verify(
            x => x.GetAllMoviesAsync(It.IsAny<ArrInstance>()),
            Times.Never);
        _sonarrClient.Verify(
            x => x.GetAllSeriesAsync(It.IsAny<ArrInstance>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoEnabledInstances_ReturnsEarly()
    {
        // Arrange — enable CF scoring but add no SeekerInstanceConfigs
        var config = await _fixture.DataContext.SeekerConfigs.FirstAsync();
        config.UseCustomFormatScore = true;
        await _fixture.DataContext.SaveChangesAsync();

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert — no API calls
        _radarrClient.Verify(
            x => x.GetAllMoviesAsync(It.IsAny<ArrInstance>()),
            Times.Never);
        _sonarrClient.Verify(
            x => x.GetAllSeriesAsync(It.IsAny<ArrInstance>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_SyncsRadarrMovieScores()
    {
        // Arrange
        var config = await _fixture.DataContext.SeekerConfigs.FirstAsync();
        config.UseCustomFormatScore = true;
        await _fixture.DataContext.SaveChangesAsync();

        var radarrInstance = TestDataContextFactory.AddRadarrInstance(_fixture.DataContext);

        _fixture.DataContext.SeekerInstanceConfigs.Add(new SeekerInstanceConfig
        {
            ArrInstanceId = radarrInstance.Id,
            ArrInstance = radarrInstance,
            Enabled = true
        });
        await _fixture.DataContext.SaveChangesAsync();

        // Mock quality profiles
        _radarrClient
            .Setup(x => x.GetQualityProfilesAsync(radarrInstance))
            .ReturnsAsync([new ArrQualityProfile { Id = 1, Name = "HD", CutoffFormatScore = 500 }]);

        // Mock movies with files
        _radarrClient
            .Setup(x => x.GetAllMoviesAsync(radarrInstance))
            .ReturnsAsync([
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
            ]);

        // Mock file scores
        _radarrClient
            .Setup(x => x.GetMovieFileScoresAsync(radarrInstance, It.Is<List<long>>(ids => ids.Contains(100))))
            .ReturnsAsync(new Dictionary<long, int> { { 100, 250 } });

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert — CF score entry was saved
        var entries = await _fixture.DataContext.CustomFormatScoreEntries.ToListAsync();
        Assert.Single(entries);

        var entry = entries[0];
        Assert.Equal(radarrInstance.Id, entry.ArrInstanceId);
        Assert.Equal(10, entry.ExternalItemId);
        Assert.Equal(250, entry.CurrentScore);
        Assert.Equal(500, entry.CutoffScore);
        Assert.Equal("HD", entry.QualityProfileName);
        Assert.Equal(InstanceType.Radarr, entry.ItemType);
        Assert.True(entry.IsMonitored);

        // Initial history entry should also be created
        var history = await _fixture.DataContext.CustomFormatScoreHistory.ToListAsync();
        Assert.Single(history);
        Assert.Equal(250, history[0].Score);
    }

    [Fact]
    public async Task ExecuteAsync_RecordsHistoryOnScoreChange()
    {
        // Arrange
        var config = await _fixture.DataContext.SeekerConfigs.FirstAsync();
        config.UseCustomFormatScore = true;
        await _fixture.DataContext.SaveChangesAsync();

        var radarrInstance = TestDataContextFactory.AddRadarrInstance(_fixture.DataContext);

        _fixture.DataContext.SeekerInstanceConfigs.Add(new SeekerInstanceConfig
        {
            ArrInstanceId = radarrInstance.Id,
            ArrInstance = radarrInstance,
            Enabled = true
        });

        // Pre-existing CF score entry with a different score
        _fixture.DataContext.CustomFormatScoreEntries.Add(new CustomFormatScoreEntry
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

        // Mock quality profiles
        _radarrClient
            .Setup(x => x.GetQualityProfilesAsync(radarrInstance))
            .ReturnsAsync([new ArrQualityProfile { Id = 1, Name = "HD", CutoffFormatScore = 500 }]);

        // Mock movies — same movie but score changed from 200 to 350
        _radarrClient
            .Setup(x => x.GetAllMoviesAsync(radarrInstance))
            .ReturnsAsync([
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
            ]);

        _radarrClient
            .Setup(x => x.GetMovieFileScoresAsync(radarrInstance, It.Is<List<long>>(ids => ids.Contains(100))))
            .ReturnsAsync(new Dictionary<long, int> { { 100, 350 } });

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert — existing entry should be updated
        var entries = await _fixture.DataContext.CustomFormatScoreEntries.ToListAsync();
        Assert.Single(entries);
        Assert.Equal(350, entries[0].CurrentScore);

        // History entry should be created because score changed (200 -> 350)
        var history = await _fixture.DataContext.CustomFormatScoreHistory.ToListAsync();
        Assert.Single(history);
        Assert.Equal(350, history[0].Score);
        Assert.Equal(InstanceType.Radarr, history[0].ItemType);
    }

    [Fact]
    public async Task ExecuteAsync_TracksUnmonitoredMovie()
    {
        // Arrange
        var config = await _fixture.DataContext.SeekerConfigs.FirstAsync();
        config.UseCustomFormatScore = true;
        await _fixture.DataContext.SaveChangesAsync();

        var radarrInstance = TestDataContextFactory.AddRadarrInstance(_fixture.DataContext);

        _fixture.DataContext.SeekerInstanceConfigs.Add(new SeekerInstanceConfig
        {
            ArrInstanceId = radarrInstance.Id,
            ArrInstance = radarrInstance,
            Enabled = true
        });
        await _fixture.DataContext.SaveChangesAsync();

        _radarrClient
            .Setup(x => x.GetQualityProfilesAsync(radarrInstance))
            .ReturnsAsync([new ArrQualityProfile { Id = 1, Name = "HD", CutoffFormatScore = 500 }]);

        _radarrClient
            .Setup(x => x.GetAllMoviesAsync(radarrInstance))
            .ReturnsAsync([
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
            ]);

        _radarrClient
            .Setup(x => x.GetMovieFileScoresAsync(radarrInstance, It.Is<List<long>>(ids => ids.Contains(100))))
            .ReturnsAsync(new Dictionary<long, int> { { 100, 250 } });

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert — entry should be saved with IsMonitored = false
        var entries = await _fixture.DataContext.CustomFormatScoreEntries.ToListAsync();
        Assert.Single(entries);
        Assert.False(entries[0].IsMonitored);
    }

    [Fact]
    public async Task ExecuteAsync_UpdatesMonitoredStatusOnSync()
    {
        // Arrange
        var config = await _fixture.DataContext.SeekerConfigs.FirstAsync();
        config.UseCustomFormatScore = true;
        await _fixture.DataContext.SaveChangesAsync();

        var radarrInstance = TestDataContextFactory.AddRadarrInstance(_fixture.DataContext);

        _fixture.DataContext.SeekerInstanceConfigs.Add(new SeekerInstanceConfig
        {
            ArrInstanceId = radarrInstance.Id,
            ArrInstance = radarrInstance,
            Enabled = true
        });

        // Pre-existing entry that was monitored
        _fixture.DataContext.CustomFormatScoreEntries.Add(new CustomFormatScoreEntry
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

        _radarrClient
            .Setup(x => x.GetQualityProfilesAsync(radarrInstance))
            .ReturnsAsync([new ArrQualityProfile { Id = 1, Name = "HD", CutoffFormatScore = 500 }]);

        // Movie is now unmonitored
        _radarrClient
            .Setup(x => x.GetAllMoviesAsync(radarrInstance))
            .ReturnsAsync([
                new SearchableMovie
                {
                    Id = 10, Title = "Test Movie", HasFile = true,
                    MovieFile = new MovieFileInfo { Id = 100, QualityCutoffNotMet = false },
                    QualityProfileId = 1, Status = "released", Monitored = false
                }
            ]);

        _radarrClient
            .Setup(x => x.GetMovieFileScoresAsync(radarrInstance, It.IsAny<List<long>>()))
            .ReturnsAsync(new Dictionary<long, int> { { 100, 250 } });

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert — IsMonitored should be updated to false
        var entries = await _fixture.DataContext.CustomFormatScoreEntries.ToListAsync();
        Assert.Single(entries);
        Assert.False(entries[0].IsMonitored);
    }

    #endregion

    #region Sonarr Sync Tests

    [Fact]
    public async Task ExecuteAsync_SyncsSonarrEpisodeScores()
    {
        // Arrange
        var config = await _fixture.DataContext.SeekerConfigs.FirstAsync();
        config.UseCustomFormatScore = true;
        await _fixture.DataContext.SaveChangesAsync();

        var sonarrInstance = TestDataContextFactory.AddSonarrInstance(_fixture.DataContext);

        _fixture.DataContext.SeekerInstanceConfigs.Add(new SeekerInstanceConfig
        {
            ArrInstanceId = sonarrInstance.Id,
            ArrInstance = sonarrInstance,
            Enabled = true
        });
        await _fixture.DataContext.SaveChangesAsync();

        // Mock quality profiles
        _sonarrClient
            .Setup(x => x.GetQualityProfilesAsync(sonarrInstance))
            .ReturnsAsync([new ArrQualityProfile { Id = 1, Name = "HD", CutoffFormatScore = 500 }]);

        // Mock series
        _sonarrClient
            .Setup(x => x.GetAllSeriesAsync(sonarrInstance))
            .ReturnsAsync([
                new SearchableSeries { Id = 10, Title = "Test Series", QualityProfileId = 1, Monitored = true }
            ]);

        // Mock episodes — one with a file, one without
        _sonarrClient
            .Setup(x => x.GetEpisodesAsync(sonarrInstance, 10))
            .ReturnsAsync([
                new SearchableEpisode { Id = 100, SeasonNumber = 1, EpisodeNumber = 1, EpisodeFileId = 500, HasFile = true, Monitored = true },
                new SearchableEpisode { Id = 101, SeasonNumber = 1, EpisodeNumber = 2, EpisodeFileId = 0, HasFile = false }
            ]);

        // Mock episode files with CF scores
        _sonarrClient
            .Setup(x => x.GetEpisodeFilesAsync(sonarrInstance, 10))
            .ReturnsAsync([
                new ArrEpisodeFile { Id = 500, CustomFormatScore = 300, QualityCutoffNotMet = false }
            ]);

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert — only the episode with a file should have an entry
        var entries = await _fixture.DataContext.CustomFormatScoreEntries.ToListAsync();
        Assert.Single(entries);

        var entry = entries[0];
        Assert.Equal(sonarrInstance.Id, entry.ArrInstanceId);
        Assert.Equal(10, entry.ExternalItemId);
        Assert.Equal(100, entry.EpisodeId);
        Assert.Equal(300, entry.CurrentScore);
        Assert.Equal(500, entry.CutoffScore);
        Assert.Equal(InstanceType.Sonarr, entry.ItemType);
        Assert.True(entry.IsMonitored);
        Assert.Contains("S01E01", entry.Title);

        // Initial history should be created
        var history = await _fixture.DataContext.CustomFormatScoreHistory.ToListAsync();
        Assert.Single(history);
        Assert.Equal(300, history[0].Score);
    }

    [Fact]
    public async Task ExecuteAsync_SonarrSync_SkipsEpisodesWithoutFiles()
    {
        // Arrange
        var config = await _fixture.DataContext.SeekerConfigs.FirstAsync();
        config.UseCustomFormatScore = true;
        await _fixture.DataContext.SaveChangesAsync();

        var sonarrInstance = TestDataContextFactory.AddSonarrInstance(_fixture.DataContext);

        _fixture.DataContext.SeekerInstanceConfigs.Add(new SeekerInstanceConfig
        {
            ArrInstanceId = sonarrInstance.Id,
            ArrInstance = sonarrInstance,
            Enabled = true
        });
        await _fixture.DataContext.SaveChangesAsync();

        _sonarrClient
            .Setup(x => x.GetQualityProfilesAsync(sonarrInstance))
            .ReturnsAsync([new ArrQualityProfile { Id = 1, Name = "HD", CutoffFormatScore = 500 }]);

        _sonarrClient
            .Setup(x => x.GetAllSeriesAsync(sonarrInstance))
            .ReturnsAsync([
                new SearchableSeries { Id = 10, Title = "Test Series", QualityProfileId = 1, Monitored = true }
            ]);

        // All episodes have EpisodeFileId = 0 (no file)
        _sonarrClient
            .Setup(x => x.GetEpisodesAsync(sonarrInstance, 10))
            .ReturnsAsync([
                new SearchableEpisode { Id = 100, SeasonNumber = 1, EpisodeNumber = 1, EpisodeFileId = 0, HasFile = false }
            ]);

        _sonarrClient
            .Setup(x => x.GetEpisodeFilesAsync(sonarrInstance, 10))
            .ReturnsAsync([]);

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert — no entries created
        var entries = await _fixture.DataContext.CustomFormatScoreEntries.ToListAsync();
        Assert.Empty(entries);
    }

    #endregion

    #region Score Unchanged Tests

    [Fact]
    public async Task ExecuteAsync_ScoreUnchanged_DoesNotRecordHistory()
    {
        // Arrange
        var config = await _fixture.DataContext.SeekerConfigs.FirstAsync();
        config.UseCustomFormatScore = true;
        await _fixture.DataContext.SaveChangesAsync();

        var radarrInstance = TestDataContextFactory.AddRadarrInstance(_fixture.DataContext);

        _fixture.DataContext.SeekerInstanceConfigs.Add(new SeekerInstanceConfig
        {
            ArrInstanceId = radarrInstance.Id,
            ArrInstance = radarrInstance,
            Enabled = true
        });

        // Pre-existing entry with score = 250 (same as what will be returned)
        _fixture.DataContext.CustomFormatScoreEntries.Add(new CustomFormatScoreEntry
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

        _radarrClient
            .Setup(x => x.GetQualityProfilesAsync(radarrInstance))
            .ReturnsAsync([new ArrQualityProfile { Id = 1, Name = "HD", CutoffFormatScore = 500 }]);

        _radarrClient
            .Setup(x => x.GetAllMoviesAsync(radarrInstance))
            .ReturnsAsync([
                new SearchableMovie
                {
                    Id = 10, Title = "Test Movie", HasFile = true,
                    MovieFile = new MovieFileInfo { Id = 100, QualityCutoffNotMet = false },
                    QualityProfileId = 1, Status = "released", Monitored = true
                }
            ]);

        // Score unchanged: still 250
        _radarrClient
            .Setup(x => x.GetMovieFileScoresAsync(radarrInstance, It.Is<List<long>>(ids => ids.Contains(100))))
            .ReturnsAsync(new Dictionary<long, int> { { 100, 250 } });

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert — no history entries (score didn't change)
        var history = await _fixture.DataContext.CustomFormatScoreHistory.ToListAsync();
        Assert.Empty(history);

        // Entry should still be updated (LastSyncedAt changes)
        var entries = await _fixture.DataContext.CustomFormatScoreEntries.ToListAsync();
        Assert.Single(entries);
        Assert.Equal(250, entries[0].CurrentScore);
    }

    #endregion

    #region Stale Entry Cleanup Tests

    [Fact]
    public async Task ExecuteAsync_CleansUpEntriesForRemovedMovies()
    {
        // Arrange
        var config = await _fixture.DataContext.SeekerConfigs.FirstAsync();
        config.UseCustomFormatScore = true;
        await _fixture.DataContext.SaveChangesAsync();

        var radarrInstance = TestDataContextFactory.AddRadarrInstance(_fixture.DataContext);

        _fixture.DataContext.SeekerInstanceConfigs.Add(new SeekerInstanceConfig
        {
            ArrInstanceId = radarrInstance.Id,
            ArrInstance = radarrInstance,
            Enabled = true
        });

        // Pre-existing entry for a movie that no longer exists in library
        _fixture.DataContext.CustomFormatScoreEntries.Add(new CustomFormatScoreEntry
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

        _radarrClient
            .Setup(x => x.GetQualityProfilesAsync(radarrInstance))
            .ReturnsAsync([new ArrQualityProfile { Id = 1, Name = "HD", CutoffFormatScore = 500 }]);

        // Library now only has movie 10 (not 999)
        _radarrClient
            .Setup(x => x.GetAllMoviesAsync(radarrInstance))
            .ReturnsAsync([
                new SearchableMovie
                {
                    Id = 10, Title = "Current Movie", HasFile = true,
                    MovieFile = new MovieFileInfo { Id = 100, QualityCutoffNotMet = false },
                    QualityProfileId = 1, Status = "released", Monitored = true
                }
            ]);

        _radarrClient
            .Setup(x => x.GetMovieFileScoresAsync(radarrInstance, It.IsAny<List<long>>()))
            .ReturnsAsync(new Dictionary<long, int> { { 100, 250 } });

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert — entry for removed movie 999 should be deleted
        var entries = await _fixture.DataContext.CustomFormatScoreEntries.ToListAsync();
        Assert.Single(entries);
        Assert.Equal(10, entries[0].ExternalItemId);
    }

    [Fact]
    public async Task ExecuteAsync_PreservesEntryWhenMovieExistsButHasNoFile()
    {
        // Arrange — simulates an RSS upgrade where the old file was removed
        // but the new file hasn't been imported yet
        var config = await _fixture.DataContext.SeekerConfigs.FirstAsync();
        config.UseCustomFormatScore = true;
        await _fixture.DataContext.SaveChangesAsync();

        var radarrInstance = TestDataContextFactory.AddRadarrInstance(_fixture.DataContext);

        _fixture.DataContext.SeekerInstanceConfigs.Add(new SeekerInstanceConfig
        {
            ArrInstanceId = radarrInstance.Id,
            ArrInstance = radarrInstance,
            Enabled = true
        });

        // Pre-existing entry with score history
        _fixture.DataContext.CustomFormatScoreEntries.Add(new CustomFormatScoreEntry
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
        _fixture.DataContext.CustomFormatScoreHistory.Add(new CustomFormatScoreHistory
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

        _radarrClient
            .Setup(x => x.GetQualityProfilesAsync(radarrInstance))
            .ReturnsAsync([new ArrQualityProfile { Id = 1, Name = "HD", CutoffFormatScore = 500 }]);

        // Movie still exists in Radarr but HasFile is false (RSS upgrade in progress)
        _radarrClient
            .Setup(x => x.GetAllMoviesAsync(radarrInstance))
            .ReturnsAsync([
                new SearchableMovie
                {
                    Id = 10, Title = "Mario Bros", HasFile = false,
                    MovieFile = null,
                    QualityProfileId = 1, Status = "released", Monitored = true
                }
            ]);

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert — entry and history should be preserved since the movie still exists
        var entries = await _fixture.DataContext.CustomFormatScoreEntries.ToListAsync();
        Assert.Single(entries);
        Assert.Equal(10, entries[0].ExternalItemId);
        Assert.Equal(250, entries[0].CurrentScore);

        var history = await _fixture.DataContext.CustomFormatScoreHistory.ToListAsync();
        Assert.Single(history);
        Assert.Equal(250, history[0].Score);
    }

    [Fact]
    public async Task ExecuteAsync_PreservesEntryWhenMovieFileScoreNotReturned()
    {
        // Arrange — simulates a newly imported file that doesn't have CF scores calculated yet
        var config = await _fixture.DataContext.SeekerConfigs.FirstAsync();
        config.UseCustomFormatScore = true;
        await _fixture.DataContext.SaveChangesAsync();

        var radarrInstance = TestDataContextFactory.AddRadarrInstance(_fixture.DataContext);

        _fixture.DataContext.SeekerInstanceConfigs.Add(new SeekerInstanceConfig
        {
            ArrInstanceId = radarrInstance.Id,
            ArrInstance = radarrInstance,
            Enabled = true
        });

        // Pre-existing entry with history
        _fixture.DataContext.CustomFormatScoreEntries.Add(new CustomFormatScoreEntry
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
        _fixture.DataContext.CustomFormatScoreHistory.Add(new CustomFormatScoreHistory
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

        _radarrClient
            .Setup(x => x.GetQualityProfilesAsync(radarrInstance))
            .ReturnsAsync([new ArrQualityProfile { Id = 1, Name = "HD", CutoffFormatScore = 500 }]);

        // Movie has a new file (different FileId) after RSS upgrade
        _radarrClient
            .Setup(x => x.GetAllMoviesAsync(radarrInstance))
            .ReturnsAsync([
                new SearchableMovie
                {
                    Id = 10, Title = "Mario Bros", HasFile = true,
                    MovieFile = new MovieFileInfo { Id = 200, QualityCutoffNotMet = false },
                    QualityProfileId = 1, Status = "released", Monitored = true
                }
            ]);

        // New file returns no score (not yet calculated by Radarr)
        _radarrClient
            .Setup(x => x.GetMovieFileScoresAsync(radarrInstance, It.IsAny<List<long>>()))
            .ReturnsAsync(new Dictionary<long, int>());

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert — entry and history should be preserved since the movie still exists
        var entries = await _fixture.DataContext.CustomFormatScoreEntries.ToListAsync();
        Assert.Single(entries);
        Assert.Equal(10, entries[0].ExternalItemId);
        Assert.Equal(250, entries[0].CurrentScore);

        var history = await _fixture.DataContext.CustomFormatScoreHistory.ToListAsync();
        Assert.Single(history);
    }

    [Fact]
    public async Task ExecuteAsync_Sonarr_PreservesEntryWhenEpisodeTemporarilyWithoutFile()
    {
        // Arrange — simulates a Sonarr episode whose file was replaced via RSS
        var config = await _fixture.DataContext.SeekerConfigs.FirstAsync();
        config.UseCustomFormatScore = true;
        await _fixture.DataContext.SaveChangesAsync();

        var sonarrInstance = TestDataContextFactory.AddSonarrInstance(_fixture.DataContext);

        _fixture.DataContext.SeekerInstanceConfigs.Add(new SeekerInstanceConfig
        {
            ArrInstanceId = sonarrInstance.Id,
            ArrInstance = sonarrInstance,
            Enabled = true
        });

        // Pre-existing CF score entry for an episode
        _fixture.DataContext.CustomFormatScoreEntries.Add(new CustomFormatScoreEntry
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
        _fixture.DataContext.CustomFormatScoreHistory.Add(new CustomFormatScoreHistory
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

        _sonarrClient
            .Setup(x => x.GetQualityProfilesAsync(sonarrInstance))
            .ReturnsAsync([new ArrQualityProfile { Id = 1, Name = "HD", CutoffFormatScore = 500 }]);

        _sonarrClient
            .Setup(x => x.GetAllSeriesAsync(sonarrInstance))
            .ReturnsAsync([
                new SearchableSeries { Id = 10, Title = "Test Series", QualityProfileId = 1, Monitored = true }
            ]);

        // Episode exists but has no file currently (RSS upgrade in progress)
        _sonarrClient
            .Setup(x => x.GetEpisodesAsync(sonarrInstance, 10))
            .ReturnsAsync([
                new SearchableEpisode { Id = 100, SeasonNumber = 1, EpisodeNumber = 1, EpisodeFileId = 0, HasFile = false, Monitored = true }
            ]);

        _sonarrClient
            .Setup(x => x.GetEpisodeFilesAsync(sonarrInstance, 10))
            .ReturnsAsync([]);

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert — entry and history should be preserved
        var entries = await _fixture.DataContext.CustomFormatScoreEntries.ToListAsync();
        Assert.Single(entries);
        Assert.Equal(10, entries[0].ExternalItemId);
        Assert.Equal(100, entries[0].EpisodeId);
        Assert.Equal(300, entries[0].CurrentScore);

        var history = await _fixture.DataContext.CustomFormatScoreHistory.ToListAsync();
        Assert.Single(history);
    }

    #endregion
}
