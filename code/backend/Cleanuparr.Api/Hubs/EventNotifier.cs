using Cleanuparr.Api.Features.Events.Contracts.Responses;
using Cleanuparr.Api.Features.Strikes.Contracts.Responses;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Realtime;
using Cleanuparr.Persistence.Models.Events;
using Microsoft.AspNetCore.SignalR;

namespace Cleanuparr.Api.Hubs;

/// <inheritdoc cref="IEventNotifier" />
public sealed class EventNotifier : IEventNotifier
{
    private readonly IHubContext<AppHub> _hubContext;
    private readonly TimeProvider _timeProvider;

    public EventNotifier(IHubContext<AppHub> hubContext, TimeProvider timeProvider)
    {
        _hubContext = hubContext;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public Task NotifyEventAsync(AppEvent appEvent)
    {
        return _hubContext.Clients.All.SendAsync("EventReceived", EventListItem.From(appEvent));
    }

    /// <inheritdoc />
    public Task NotifyManualEventAsync(ManualEvent manualEvent)
    {
        return _hubContext.Clients.All.SendAsync("ManualEventReceived", ManualEventResponse.From(manualEvent));
    }

    /// <inheritdoc />
    public Task NotifyStrikeAsync(Guid strikeId, StrikeType strikeType, string downloadId, string itemTitle, bool isDryRun)
    {
        RecentStrikeDto strike = new()
        {
            Id = strikeId,
            Type = strikeType.ToString(),
            CreatedAt = _timeProvider.GetUtcNow(),
            DownloadId = downloadId,
            Title = itemTitle,
            IsDryRun = isDryRun,
        };

        return _hubContext.Clients.All.SendAsync("StrikeReceived", strike);
    }
}
