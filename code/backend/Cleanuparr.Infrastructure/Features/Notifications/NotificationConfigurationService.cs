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
            var providers = await _dataContext.Set<NotificationConfig>()
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

    private static NotificationProviderDto MapToDto(NotificationConfig config)
    {
        var events = new NotificationEventFlags
        {
            OnFailedImportStrike = config.OnFailedImportStrike,
            OnStalledStrike = config.OnStalledStrike,
            OnSlowStrike = config.OnSlowStrike,
            OnQueueItemDeleted = config.OnQueueItemDeleted,
            OnDownloadCleaned = config.OnDownloadCleaned,
            OnCategoryChanged = config.OnCategoryChanged
        };

        var configuration = config.Type switch
        {
            NotificationProviderType.Notifiarr => config.NotifiarrConfiguration,
            NotificationProviderType.Apprise => config.AppriseConfiguration,
            _ => new object()
        };

        return new NotificationProviderDto
        {
            Id = config.Id,
            Name = config.Name,
            Type = config.Type,
            IsEnabled = config.IsEnabled && config.IsConfigured && config.HasAnyEventEnabled,
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
