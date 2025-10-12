namespace Cleanuparr.Api.Features.Notifications.Contracts.Requests;

public record UpdateNotifiarrProviderRequest : UpdateNotificationProviderRequestBase
{
    public string ApiKey { get; init; } = string.Empty;
    
    public string ChannelId { get; init; } = string.Empty;
}
