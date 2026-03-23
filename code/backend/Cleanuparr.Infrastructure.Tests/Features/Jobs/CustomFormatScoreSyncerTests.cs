using Cleanuparr.Domain.Entities.Arr;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Arr.Interfaces;
using Cleanuparr.Infrastructure.Tests.Features.Jobs.TestHelpers;
using Cleanuparr.Persistence.Models.Configuration.Arr;
using Cleanuparr.Persistence.Models.Configuration.Seeker;
using Cleanuparr.Persistence.Models.State;
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

    public CustomFormatScoreSyncerTests(JobHandlerFixture fixture)
    {
        _fixture = fixture;
        _fixture.RecreateDataContext();
        _fixture.ResetMocks();
        _logger = new Mock<ILogger<CustomFormatScoreSyncer>>();
        _radarrClient = new Mock<IRadarrClient>();
        _sonarrClient = new Mock<ISonarrClient>();
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
            _fixture.TimeProvider
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

    #endregion
}
