namespace Cleanuparr.Infrastructure.Features.Notifications.Models;

public sealed record NotificationField
{
    public required string Title { get; init; }
    
    public required string Text { get; init; }
}