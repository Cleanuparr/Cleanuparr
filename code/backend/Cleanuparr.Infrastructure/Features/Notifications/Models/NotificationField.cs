namespace Cleanuparr.Infrastructure.Features.Notifications.Models;

public sealed record NotificationField
{
    public required string Key { get; init; }
    
    public required string Value { get; init; }
}