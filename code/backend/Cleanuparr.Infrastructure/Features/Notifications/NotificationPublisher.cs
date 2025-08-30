using System.Globalization;
using Cleanuparr.Domain.Entities.Arr.Queue;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Infrastructure.Features.Notifications.Models;
using Cleanuparr.Persistence.Models.Configuration.Arr;
using Infrastructure.Interceptors;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Features.Notifications;

public class NotificationPublisher : INotificationPublisher
{
    private readonly ILogger<NotificationPublisher> _logger;
    private readonly IDryRunInterceptor _dryRunInterceptor;
    private readonly INotificationConfigurationService _configurationService;
    private readonly INotificationProviderFactory _providerFactory;

    public NotificationPublisher(
        ILogger<NotificationPublisher> logger,
        IDryRunInterceptor dryRunInterceptor,
        INotificationConfigurationService configurationService,
        INotificationProviderFactory providerFactory)
    {
        _logger = logger;
        _dryRunInterceptor = dryRunInterceptor;
        _configurationService = configurationService;
        _providerFactory = providerFactory;
    }

    public virtual async Task NotifyStrike(StrikeType strikeType, int strikeCount)
    {
        try
        {
            var eventType = MapStrikeTypeToEventType(strikeType);
            var context = BuildStrikeNotificationContext(strikeType, strikeCount, eventType);
            
            await SendNotificationAsync(eventType, context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "failed to notify strike");
        }
    }

    public virtual async Task NotifyQueueItemDeleted(bool removeFromClient, DeleteReason reason)
    {
        try
        {
            var context = BuildQueueItemDeletedContext(removeFromClient, reason);
            await SendNotificationAsync(NotificationEventType.QueueItemDeleted, context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "failed to notify queue item deleted");
        }
    }

    public virtual async Task NotifyDownloadCleaned(double ratio, TimeSpan seedingTime, string categoryName, CleanReason reason)
    {
        try
        {
            var context = BuildDownloadCleanedContext(ratio, seedingTime, categoryName, reason);
            await SendNotificationAsync(NotificationEventType.DownloadCleaned, context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "failed to notify download cleaned");
        }
    }

    public virtual async Task NotifyCategoryChanged(string oldCategory, string newCategory, bool isTag = false)
    {
        try
        {
            var context = BuildCategoryChangedContext(oldCategory, newCategory, isTag);
            await SendNotificationAsync(NotificationEventType.CategoryChanged, context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "failed to notify category changed");
        }
    }

    private async Task SendNotificationAsync(NotificationEventType eventType, NotificationContext context)
    {
        await _dryRunInterceptor.InterceptAsync(SendNotificationInternalAsync, (eventType, context));
    }

    private async Task SendNotificationInternalAsync((NotificationEventType eventType, NotificationContext context) parameters)
    {
        var (eventType, context) = parameters;
        var providers = await _configurationService.GetProvidersForEventAsync(eventType);

        if (!providers.Any())
        {
            _logger.LogDebug("No providers configured for event type {eventType}", eventType);
            return;
        }

        var tasks = providers.Select(async providerConfig =>
        {
            try
            {
                var provider = _providerFactory.CreateProvider(providerConfig);
                await provider.SendNotificationAsync(context);
                _logger.LogDebug("Notification sent successfully via {providerName}", provider.Name);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send notification via provider {providerName}", providerConfig.Name);
            }
        });

        await Task.WhenAll(tasks);
    }

    private NotificationContext BuildStrikeNotificationContext(StrikeType strikeType, int strikeCount, NotificationEventType eventType)
    {
        var record = ContextProvider.Get<QueueRecord>(nameof(QueueRecord));
        var instanceType = (InstanceType)ContextProvider.Get<object>(nameof(InstanceType));
        var instanceUrl = ContextProvider.Get<Uri>(nameof(ArrInstance) + nameof(ArrInstance.Url));
        var imageUrl = GetImageFromContext(record, instanceType);

        return new NotificationContext
        {
            EventType = eventType,
            Title = $"Strike received with reason: {strikeType}",
            Description = record.Title,
            Severity = EventSeverity.Warning,
            Data = new Dictionary<string, object>
            {
                ["strikeType"] = strikeType.ToString(),
                ["strikeCount"] = strikeCount,
                ["hash"] = record.DownloadId.ToLowerInvariant(),
                ["instanceType"] = instanceType,
                ["instanceUrl"] = instanceUrl,
                ["image"] = imageUrl?.ToString() ?? string.Empty
            }
        };
    }

    private NotificationContext BuildQueueItemDeletedContext(bool removeFromClient, DeleteReason reason)
    {
        var record = ContextProvider.Get<QueueRecord>(nameof(QueueRecord));
        var instanceType = (InstanceType)ContextProvider.Get<object>(nameof(InstanceType));
        var instanceUrl = ContextProvider.Get<Uri>(nameof(ArrInstance) + nameof(ArrInstance.Url));
        var imageUrl = GetImageFromContext(record, instanceType);

        return new NotificationContext
        {
            EventType = NotificationEventType.QueueItemDeleted,
            Title = $"Deleting item from queue with reason: {reason}",
            Description = record.Title,
            Severity = EventSeverity.Important,
            Data = new Dictionary<string, object>
            {
                ["reason"] = reason.ToString(),
                ["removeFromClient"] = removeFromClient,
                ["hash"] = record.DownloadId.ToLowerInvariant(),
                ["instanceType"] = instanceType,
                ["instanceUrl"] = instanceUrl,
                ["image"] = imageUrl?.ToString() ?? string.Empty
            }
        };
    }

    private NotificationContext BuildDownloadCleanedContext(double ratio, TimeSpan seedingTime, string categoryName, CleanReason reason)
    {
        var downloadName = ContextProvider.Get<string>("downloadName");
        var hash = ContextProvider.Get<string>("hash");

        return new NotificationContext
        {
            EventType = NotificationEventType.DownloadCleaned,
            Title = $"Cleaned item from download client with reason: {reason}",
            Description = downloadName,
            Severity = EventSeverity.Important,
            Data = new Dictionary<string, object>
            {
                ["reason"] = reason.ToString(),
                ["hash"] = hash.ToLowerInvariant(),
                ["categoryName"] = categoryName.ToLowerInvariant(),
                ["ratio"] = ratio,
                ["seedingHours"] = Math.Round(seedingTime.TotalHours, 0)
            }
        };
    }

    private NotificationContext BuildCategoryChangedContext(string oldCategory, string newCategory, bool isTag)
    {
        var downloadName = ContextProvider.Get<string>("downloadName");
        var hash = ContextProvider.Get<string>("hash");

        return new NotificationContext
        {
            EventType = NotificationEventType.CategoryChanged,
            Title = isTag ? "Tag added" : "Category changed",
            Description = downloadName,
            Severity = EventSeverity.Information,
            Data = new Dictionary<string, object>
            {
                ["oldCategory"] = oldCategory,
                ["newCategory"] = newCategory,
                ["isTag"] = isTag,
                ["hash"] = hash.ToLowerInvariant()
            }
        };
    }

    private static NotificationEventType MapStrikeTypeToEventType(StrikeType strikeType)
    {
        return strikeType switch
        {
            StrikeType.Stalled => NotificationEventType.StalledStrike,
            StrikeType.DownloadingMetadata => NotificationEventType.StalledStrike,
            StrikeType.FailedImport => NotificationEventType.FailedImportStrike,
            StrikeType.SlowSpeed => NotificationEventType.SlowSpeedStrike,
            StrikeType.SlowTime => NotificationEventType.SlowTimeStrike,
            _ => throw new ArgumentOutOfRangeException(nameof(strikeType), strikeType, null)
        };
    }

    private Uri? GetImageFromContext(QueueRecord record, InstanceType instanceType)
    {
        Uri? image = instanceType switch
        {
            InstanceType.Sonarr => record.Series?.Images?.FirstOrDefault(x => x.CoverType == "poster")?.RemoteUrl,
            InstanceType.Radarr => record.Movie?.Images?.FirstOrDefault(x => x.CoverType == "poster")?.RemoteUrl,
            InstanceType.Lidarr => record.Album?.Images?.FirstOrDefault(x => x.CoverType == "cover")?.Url,
            InstanceType.Readarr => record.Book?.Images?.FirstOrDefault(x => x.CoverType == "cover")?.Url,
            InstanceType.Whisparr => record.Series?.Images?.FirstOrDefault(x => x.CoverType == "poster")?.RemoteUrl,
            _ => throw new ArgumentOutOfRangeException(nameof(instanceType))
        };

        if (image is null)
        {
            _logger.LogWarning("no poster found for {title}", record.Title);
        }

        return image;
    }
}
