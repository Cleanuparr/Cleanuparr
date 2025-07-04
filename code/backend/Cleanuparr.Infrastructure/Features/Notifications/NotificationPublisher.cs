using System.Globalization;
using Cleanuparr.Domain.Entities.Arr.Queue;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Infrastructure.Features.Notifications.Models;
using Cleanuparr.Persistence.Models.Configuration.Arr;
using Infrastructure.Interceptors;
using Mapster;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Features.Notifications;

public class NotificationPublisher : INotificationPublisher
{
    private readonly ILogger<INotificationPublisher> _logger;
    private readonly IBus _messageBus;
    private readonly IDryRunInterceptor _dryRunInterceptor;

    public NotificationPublisher(ILogger<INotificationPublisher> logger, IBus messageBus, IDryRunInterceptor dryRunInterceptor)
    {
        _logger = logger;
        _messageBus = messageBus;
        _dryRunInterceptor = dryRunInterceptor;
    }
    
    public virtual async Task NotifyStrike(StrikeType strikeType, int strikeCount)
    {
        try
        {
            QueueRecord record = ContextProvider.Get<QueueRecord>(nameof(QueueRecord));
            InstanceType instanceType = (InstanceType)ContextProvider.Get<object>(nameof(InstanceType));
            Uri instanceUrl = ContextProvider.Get<Uri>(nameof(ArrInstance) + nameof(ArrInstance.Url));
            Uri? imageUrl = GetImageFromContext(record, instanceType);

            ArrNotification notification = new()
            {
                InstanceType = instanceType,
                InstanceUrl = instanceUrl,
                Hash = record.DownloadId.ToLowerInvariant(),
                Title = $"Strike received with reason: {strikeType}",
                Description = record.Title,
                Image = imageUrl,
                Fields = [new() { Title = "Strike count", Text = strikeCount.ToString() }]
            };
            
            switch (strikeType)
            {
                case StrikeType.Stalled:
                case StrikeType.DownloadingMetadata:
                    await NotifyInternal(notification.Adapt<StalledStrikeNotification>());
                    break;
                case StrikeType.FailedImport:
                    await NotifyInternal(notification.Adapt<FailedImportStrikeNotification>());
                    break;
                case StrikeType.SlowSpeed:
                case StrikeType.SlowTime:
                    await NotifyInternal(notification.Adapt<SlowStrikeNotification>());
                    break;
            }
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
            QueueRecord record = ContextProvider.Get<QueueRecord>(nameof(QueueRecord));
            InstanceType instanceType = (InstanceType)ContextProvider.Get<object>(nameof(InstanceType));
            Uri instanceUrl = ContextProvider.Get<Uri>(nameof(ArrInstance) + nameof(ArrInstance.Url));
            Uri? imageUrl = GetImageFromContext(record, instanceType);

            QueueItemDeletedNotification notification = new()
            {
                InstanceType = instanceType,
                InstanceUrl = instanceUrl,
                Hash = record.DownloadId.ToLowerInvariant(),
                Title = $"Deleting item from queue with reason: {reason}",
                Description = record.Title,
                Image = imageUrl,
                Fields = [new() { Title = "Removed from download client?", Text = removeFromClient ? "Yes" : "No" }]
            };

            await NotifyInternal(notification);
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
            DownloadCleanedNotification notification = new()
            {
                Title = $"Cleaned item from download client with reason: {reason}",
                Description = ContextProvider.Get<string>("downloadName"),
                Fields =
                [
                    new() { Title = "Hash", Text = ContextProvider.Get<string>("hash").ToLowerInvariant() },
                    new() { Title = "Category", Text = categoryName.ToLowerInvariant() },
                    new() { Title = "Ratio", Text = $"{ratio.ToString(CultureInfo.InvariantCulture)}%" },
                    new()
                    {
                        Title = "Seeding hours", Text = $"{Math.Round(seedingTime.TotalHours, 0).ToString(CultureInfo.InvariantCulture)}h"
                    }
                ],
                Level = NotificationLevel.Important
            };

            await NotifyInternal(notification);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "failed to notify download cleaned");
        }
    }
    
    public virtual async Task NotifyCategoryChanged(string oldCategory, string newCategory, bool isTag = false)
    {
        CategoryChangedNotification notification = new()
        {
            Title = isTag? "Tag added" : "Category changed",
            Description = ContextProvider.Get<string>("downloadName"),
            Fields =
            [
                new() { Title = "Hash", Text = ContextProvider.Get<string>("hash").ToLowerInvariant() }
            ],
            Level = NotificationLevel.Important
        };

        if (isTag)
        {
            notification.Fields.Add(new() { Title = "Tag", Text = newCategory });
        }
        else
        {
            notification.Fields.Add(new() { Title = "Old category", Text = oldCategory });
            notification.Fields.Add(new() { Title = "New category", Text = newCategory });
        }

        await NotifyInternal(notification);
    }
    
    private Task NotifyInternal<T>(T message) where T: notnull
    {
        return _dryRunInterceptor.InterceptAsync(Notify<T>, message);
    }

    private Task Notify<T>(T message) where T: notnull
    {
        return _messageBus.Publish(message);
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