using Cleanuparr.Domain.Enums;

namespace Cleanuparr.Infrastructure.Features.Notifications.Models;

public sealed record NotificationProviderDto
{
    public Guid Id { get; init; }
    
    public string Name { get; init; } = string.Empty;
    
    public NotificationProviderType Type { get; init; }
    
    public bool IsEnabled { get; init; }
    
    public NotificationEventFlags Events { get; init; } = new();
    
    public object Configuration { get; init; } = new();
}
