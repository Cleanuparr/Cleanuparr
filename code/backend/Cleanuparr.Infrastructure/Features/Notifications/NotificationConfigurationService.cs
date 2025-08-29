using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Notifications.Models;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration.Notification;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Features.Notifications;

public sealed class NotificationConfigurationService : INotificationConfigurationService
{
    private readonly DataContext _dataContext;
    private readonly ILogger<NotificationConfigurationService> _logger;
    private List<NotificationProviderDto>? _cachedProviders;
    private readonly object _cacheLock = new();

    public NotificationConfigurationService(
        DataContext dataContext,
        ILogger<NotificationConfigurationService> logger)
    {
        _dataContext = dataContext;
        _logger = logger;
    }

    public async Task<List<NotificationProviderDto>> GetActiveProvidersAsync()
    {
        lock (_cacheLock)
        {
            if (_cachedProviders != null)
            {
                return _cachedProviders.Where(p => p.IsEnabled).ToList();
            }
        }

        await LoadProvidersAsync();
        
        lock (_cacheLock)
        {
            return _cachedProviders?.Where(p => p.IsEnabled).ToList() ?? new List<NotificationProviderDto>();
        }
    }

    public async Task<List<NotificationProviderDto>> GetProvidersForEventAsync(NotificationEventType eventType)
    {
        var activeProviders = await GetActiveProvidersAsync();
        
        return activeProviders.Where(provider => IsEventEnabled(provider.Events, eventType)).ToList();
    }

    public async Task<NotificationProviderDto?> GetProviderByIdAsync(Guid id)
    {
        var allProviders = await GetActiveProvidersAsync();
        return allProviders.FirstOrDefault(p => p.Id == id);
    }

    public void InvalidateCache()
    {
        lock (_cacheLock)
        {
            _cachedProviders = null;
        }
        
        _logger.LogDebug("Notification provider cache invalidated");
    }

    private async Task LoadProvidersAsync()
    {
        try
        {
            var providers = await _dataContext.Set<NotificationProvider>()
                .Include(p => p.NotifiarrConfiguration)
                .Include(p => p.AppriseConfiguration)
                .AsNoTracking()
                .ToListAsync();

            var dtos = providers.Select(MapToDto).ToList();

            lock (_cacheLock)
            {
                _cachedProviders = dtos;
            }

            _logger.LogDebug("Loaded {count} notification providers", dtos.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load notification providers");
            
            lock (_cacheLock)
            {
                _cachedProviders = new List<NotificationProviderDto>();
            }
        }
    }

    private static NotificationProviderDto MapToDto(NotificationProvider provider)
    {
        var events = new NotificationEventFlags
        {
            OnFailedImportStrike = provider.OnFailedImportStrike,
            OnStalledStrike = provider.OnStalledStrike,
            OnSlowStrike = provider.OnSlowStrike,
            OnQueueItemDeleted = provider.OnQueueItemDeleted,
            OnDownloadCleaned = provider.OnDownloadCleaned,
            OnCategoryChanged = provider.OnCategoryChanged
        };

        var configuration = provider.Type switch
        {
            NotificationProviderType.Notifiarr => provider.NotifiarrConfiguration,
            NotificationProviderType.Apprise => provider.AppriseConfiguration,
            _ => new object()
        };

        return new NotificationProviderDto
        {
            Id = provider.Id,
            Name = provider.Name,
            Type = provider.Type,
            IsEnabled = provider.IsEnabled && provider.IsConfigured && provider.HasAnyEventEnabled,
            Events = events,
            Configuration = configuration ?? new object()
        };
    }

    private static bool IsEventEnabled(NotificationEventFlags events, NotificationEventType eventType)
    {
        return eventType switch
        {
            NotificationEventType.FailedImportStrike => events.OnFailedImportStrike,
            NotificationEventType.StalledStrike => events.OnStalledStrike,
            NotificationEventType.SlowSpeedStrike or NotificationEventType.SlowTimeStrike => events.OnSlowStrike,
            NotificationEventType.QueueItemDeleted => events.OnQueueItemDeleted,
            NotificationEventType.DownloadCleaned => events.OnDownloadCleaned,
            NotificationEventType.CategoryChanged => events.OnCategoryChanged,
            NotificationEventType.Test => true,
            _ => false
        };
    }
}
