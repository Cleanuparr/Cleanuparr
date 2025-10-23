using System.Globalization;
using Cleanuparr.Domain.Entities.Arr.Queue;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Infrastructure.Features.Notifications.Models;
using Cleanuparr.Infrastructure.Interceptors;
using Cleanuparr.Persistence.Models.Configuration.Arr;
using Cleanuparr.Persistence.Models.Configuration.QueueCleaner;
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
            _logger.LogError(ex, "Failed to notify queue item deleted");
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
            _logger.LogError(ex, "Failed to notify download cleaned");
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
            _logger.LogError(ex, "Failed to notify category changed");
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

        NotificationContext context = new()
        {
            EventType = eventType,
            Title = $"Strike received with reason: {strikeType}",
            Description = record.Title,
            Severity = EventSeverity.Warning,
            Image = imageUrl,
            Data = new Dictionary<string, string>
            {
                ["Strike type"] = strikeType.ToString(),
                ["Strike count"] = strikeCount.ToString(),
                ["Hash"] = record.DownloadId.ToLowerInvariant(),
                ["Instance type"] = instanceType.ToString(),
                ["Url"] = instanceUrl.ToString(),
            }
        };

        if (strikeType is StrikeType.Stalled or StrikeType.SlowSpeed or StrikeType.SlowTime)
        {
            var rule = ContextProvider.Get<QueueRule>();
            context.Data.Add("Rule name", rule.Name);
        }

        return context;
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
            Image = imageUrl,
            Data = new Dictionary<string, string>
            {
                ["Reason"] = reason.ToString(),
                ["Removed from client?"] = removeFromClient.ToString(),
                ["Hash"] = record.DownloadId.ToLowerInvariant(),
                ["Instance type"] = instanceType.ToString(),
                ["Url"] = instanceUrl.ToString(),
            }
        };
    }

    private static NotificationContext BuildDownloadCleanedContext(double ratio, TimeSpan seedingTime, string categoryName, CleanReason reason)
    {
        var downloadName = ContextProvider.Get<string>("downloadName");
        var hash = ContextProvider.Get<string>("hash");

        return new NotificationContext
        {
            EventType = NotificationEventType.DownloadCleaned,
            Title = $"Cleaned item from download client with reason: {reason}",
            Description = downloadName,
            Severity = EventSeverity.Important,
            Data = new Dictionary<string, string>
            {
                ["Hash"] = hash.ToLowerInvariant(),
                ["Category"] = categoryName.ToLowerInvariant(),
                ["Ratio"] = ratio.ToString(CultureInfo.InvariantCulture),
                ["Seeding hours"] = Math.Round(seedingTime.TotalHours, 0).ToString(CultureInfo.InvariantCulture)
            }
        };
    }

    private NotificationContext BuildCategoryChangedContext(string oldCategory, string newCategory, bool isTag)
    {
        string downloadName = ContextProvider.Get<string>("downloadName");

        NotificationContext context = new()
        {
            EventType = NotificationEventType.CategoryChanged,
            Title = isTag ? "Tag added" : "Category changed",
            Description = downloadName,
            Severity = EventSeverity.Information,
            Data = new Dictionary<string, string>
            {
                ["hash"] = ContextProvider.Get<string>("hash").ToLowerInvariant()
            }
        };
        
        if (isTag)
        {
            context.Data.Add("Tag", newCategory);
        }
        else
        {
            context.Data.Add("Old category", oldCategory);
            context.Data.Add("New category", newCategory);
        }

        return context;
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
            _logger.LogWarning("No poster found for {title}", record.Title);
        }

        return image;
    }
}
