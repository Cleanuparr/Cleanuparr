using Cleanuparr.Domain.Enums;

namespace Cleanuparr.Infrastructure.Features.Notifications.Models;

public sealed record NotificationContext
{
    public NotificationEventType EventType { get; init; }
    
    public string Title { get; init; } = string.Empty;
    
    public string Description { get; init; } = string.Empty;
    
    public Dictionary<string, object> Data { get; init; } = new();
    
    public EventSeverity Severity { get; init; } = EventSeverity.Information;
}
