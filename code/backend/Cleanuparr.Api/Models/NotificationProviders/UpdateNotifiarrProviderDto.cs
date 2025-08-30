namespace Cleanuparr.Api.Models.NotificationProviders;

public sealed record UpdateNotifiarrProviderDto : CreateNotificationProviderBaseDto
{
    public string ApiKey { get; init; } = string.Empty;
    
    public string ChannelId { get; init; } = string.Empty;
}
