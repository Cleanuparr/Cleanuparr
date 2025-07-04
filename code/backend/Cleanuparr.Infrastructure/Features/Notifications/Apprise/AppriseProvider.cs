using System.Text;
using Cleanuparr.Infrastructure.Features.Notifications.Models;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration.Notification;
using Infrastructure.Verticals.Notifications;

namespace Cleanuparr.Infrastructure.Features.Notifications.Apprise;

public sealed class AppriseProvider : NotificationProvider<AppriseConfig>
{
    private readonly IAppriseProxy _proxy;
    
    public override string Name => "Apprise";
    
    public AppriseProvider(DataContext dataContext, IAppriseProxy proxy)
        : base(dataContext.AppriseConfigs)
    {
        _proxy = proxy;
    }

    public override async Task OnFailedImportStrike(FailedImportStrikeNotification notification)
    {
        await _proxy.SendNotification(BuildPayload(notification, NotificationType.Warning), Config);
    }
    
    public override async Task OnStalledStrike(StalledStrikeNotification notification)
    {
        await _proxy.SendNotification(BuildPayload(notification, NotificationType.Warning), Config);
    }
    
    public override async Task OnSlowStrike(SlowStrikeNotification notification)
    {
        await _proxy.SendNotification(BuildPayload(notification, NotificationType.Warning), Config);
    }
    
    public override async Task OnQueueItemDeleted(QueueItemDeletedNotification notification)
    {
        await _proxy.SendNotification(BuildPayload(notification, NotificationType.Warning), Config);
    }

    public override async Task OnDownloadCleaned(DownloadCleanedNotification notification)
    {
        await _proxy.SendNotification(BuildPayload(notification, NotificationType.Warning), Config);
    }
    
    public override async Task OnCategoryChanged(CategoryChangedNotification notification)
    {
        await _proxy.SendNotification(BuildPayload(notification, NotificationType.Warning), Config);
    }
    
    private ApprisePayload BuildPayload(ArrNotification notification, NotificationType notificationType)
    {
        StringBuilder body = new();
        body.AppendLine(notification.Description);
        body.AppendLine();
        body.AppendLine($"Instance type: {notification.InstanceType.ToString()}");
        body.AppendLine($"Url: {notification.InstanceUrl}");
        body.AppendLine($"Download hash: {notification.Hash}");

        foreach (NotificationField field in notification.Fields ?? [])
        {
            body.AppendLine($"{field.Title}: {field.Text}");
        }
        
        ApprisePayload payload = new()
        {
            Title = notification.Title,
            Body = body.ToString(),
            Type = notificationType.ToString().ToLowerInvariant(),
            Tags = Config.Tags,
        };
        
        return payload;
    }
    
    private ApprisePayload BuildPayload(Notification notification, NotificationType notificationType)
    {
        StringBuilder body = new();
        body.AppendLine(notification.Description);
        body.AppendLine();

        foreach (NotificationField field in notification.Fields ?? [])
        {
            body.AppendLine($"{field.Title}: {field.Text}");
        }
        
        ApprisePayload payload = new()
        {
            Title = notification.Title,
            Body = body.ToString(),
            Type = notificationType.ToString().ToLowerInvariant(),
            Tags = Config.Tags,
        };
        
        return payload;
    }
}