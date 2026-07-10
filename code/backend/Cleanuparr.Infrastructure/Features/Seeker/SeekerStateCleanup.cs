using Cleanuparr.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cleanuparr.Infrastructure.Features.Seeker;

public sealed class SeekerStateCleanup : ISeekerStateCleanup
{
    private readonly EventsContext _eventsContext;

    public SeekerStateCleanup(EventsContext eventsContext)
    {
        _eventsContext = eventsContext;
    }

    public async Task DeleteForInstanceAsync(Guid arrInstanceId, CancellationToken cancellationToken = default)
    {
        await _eventsContext.CustomFormatScoreEntries
            .Where(e => e.ArrInstanceId == arrInstanceId)
            .ExecuteDeleteAsync(cancellationToken);

        await _eventsContext.CustomFormatScoreHistory
            .Where(e => e.ArrInstanceId == arrInstanceId)
            .ExecuteDeleteAsync(cancellationToken);

        await _eventsContext.SeekerHistory
            .Where(e => e.ArrInstanceId == arrInstanceId)
            .ExecuteDeleteAsync(cancellationToken);

        await _eventsContext.SearchQueue
            .Where(e => e.ArrInstanceId == arrInstanceId)
            .ExecuteDeleteAsync(cancellationToken);

        await _eventsContext.SeekerCommandTrackers
            .Where(e => e.ArrInstanceId == arrInstanceId)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
