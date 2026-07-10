using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Seeker;
using Cleanuparr.Infrastructure.Tests.Features.Jobs.TestHelpers;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.State;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.Seeker;

public sealed class SeekerStateCleanupTests : IDisposable
{
    private readonly EventsContext _eventsContext;

    public SeekerStateCleanupTests()
    {
        _eventsContext = TestDataContextFactory.CreateEvents();
    }

    public void Dispose()
    {
        _eventsContext.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task DeleteForInstanceAsync_RemovesOnlyTheGivenInstanceState()
    {
        Guid target = Guid.NewGuid();
        Guid other = Guid.NewGuid();

        SeedFor(target);
        SeedFor(other);
        await _eventsContext.SaveChangesAsync();

        SeekerStateCleanup sut = new(_eventsContext);

        await sut.DeleteForInstanceAsync(target);

        (await _eventsContext.CustomFormatScoreEntries.CountAsync(e => e.ArrInstanceId == target)).ShouldBe(0);
        (await _eventsContext.CustomFormatScoreHistory.CountAsync(e => e.ArrInstanceId == target)).ShouldBe(0);
        (await _eventsContext.SeekerHistory.CountAsync(e => e.ArrInstanceId == target)).ShouldBe(0);
        (await _eventsContext.SearchQueue.CountAsync(e => e.ArrInstanceId == target)).ShouldBe(0);
        (await _eventsContext.SeekerCommandTrackers.CountAsync(e => e.ArrInstanceId == target)).ShouldBe(0);

        (await _eventsContext.CustomFormatScoreEntries.CountAsync(e => e.ArrInstanceId == other)).ShouldBe(1);
        (await _eventsContext.CustomFormatScoreHistory.CountAsync(e => e.ArrInstanceId == other)).ShouldBe(1);
        (await _eventsContext.SeekerHistory.CountAsync(e => e.ArrInstanceId == other)).ShouldBe(1);
        (await _eventsContext.SearchQueue.CountAsync(e => e.ArrInstanceId == other)).ShouldBe(1);
        (await _eventsContext.SeekerCommandTrackers.CountAsync(e => e.ArrInstanceId == other)).ShouldBe(1);
    }

    private void SeedFor(Guid arrInstanceId)
    {
        _eventsContext.CustomFormatScoreEntries.Add(new CustomFormatScoreEntry
        {
            ArrInstanceId = arrInstanceId,
            ExternalItemId = 1,
            EpisodeId = 0,
            ItemType = InstanceType.Radarr,
            Title = "Movie",
            FileId = 10,
            CurrentScore = 100,
            CutoffScore = 500,
            QualityProfileName = "HD",
            LastSyncedAt = DateTimeOffset.UtcNow,
        });
        _eventsContext.CustomFormatScoreHistory.Add(new CustomFormatScoreHistory
        {
            ArrInstanceId = arrInstanceId,
            ExternalItemId = 1,
            EpisodeId = 0,
            ItemType = InstanceType.Radarr,
            Title = "Movie",
            Score = 100,
            CutoffScore = 500,
            RecordedAt = DateTimeOffset.UtcNow,
        });
        _eventsContext.SeekerHistory.Add(new SeekerHistory
        {
            ArrInstanceId = arrInstanceId,
            ExternalItemId = 1,
            ItemType = InstanceType.Radarr,
            SeasonNumber = 0,
            CycleId = Guid.NewGuid(),
            LastSearchedAt = DateTimeOffset.UtcNow,
            ItemTitle = "Movie",
        });
        _eventsContext.SearchQueue.Add(new SearchQueueItem
        {
            ArrInstanceId = arrInstanceId,
            ItemId = 1,
            Title = "Movie",
        });
        _eventsContext.SeekerCommandTrackers.Add(new SeekerCommandTracker
        {
            ArrInstanceId = arrInstanceId,
            CommandId = 1,
            EventId = Guid.NewGuid(),
            ExternalItemId = 1,
            ItemTitle = "Movie",
        });
    }
}
