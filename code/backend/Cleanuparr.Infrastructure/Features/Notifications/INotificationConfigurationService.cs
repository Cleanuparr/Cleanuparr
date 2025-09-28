using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Notifications.Models;

namespace Cleanuparr.Infrastructure.Features.Notifications;

public interface INotificationConfigurationService
{
    Task<List<NotificationProviderDto>> GetActiveProvidersAsync();
    
    Task<List<NotificationProviderDto>> GetProvidersForEventAsync(NotificationEventType eventType);
    
    Task<NotificationProviderDto?> GetProviderByIdAsync(Guid id);
    
    Task InvalidateCacheAsync();
}
