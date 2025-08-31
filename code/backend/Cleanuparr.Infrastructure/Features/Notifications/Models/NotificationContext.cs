using Cleanuparr.Domain.Enums;

namespace Cleanuparr.Infrastructure.Features.Notifications.Models;

public sealed record NotificationContext
{
    public NotificationEventType EventType { get; init; }
    
    public required string Title { get; init; }
    
    public required string Description { get; init; }
    
    public Dictionary<string, string> Data { get; init; } = new();
    
    public EventSeverity Severity { get; init; } = EventSeverity.Information;
    
    public Uri? Image { get; set; }
}
