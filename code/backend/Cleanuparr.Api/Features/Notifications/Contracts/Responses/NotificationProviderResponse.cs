using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Notifications.Models;

namespace Cleanuparr.Api.Features.Notifications.Contracts.Responses;

public sealed record NotificationProviderResponse
{
    public Guid Id { get; init; }
    
    public string Name { get; init; } = string.Empty;
    
    public NotificationProviderType Type { get; init; }
    
    public bool IsEnabled { get; init; }
    
    public NotificationEventFlags Events { get; init; } = new();
    
    public object Configuration { get; init; } = new();
}
