namespace Cleanuparr.Infrastructure.Features.Notifications.Models;

public abstract record Notification
{
    public required string Title { get; init; }
    
    public required string Description { get; init; }
    
    public List<NotificationField>? Fields { get; init; }
    
    public NotificationLevel Level { get; init; }
}