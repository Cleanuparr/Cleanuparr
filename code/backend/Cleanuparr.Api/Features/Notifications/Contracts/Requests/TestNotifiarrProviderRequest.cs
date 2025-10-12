namespace Cleanuparr.Api.Features.Notifications.Contracts.Requests;

public record TestNotifiarrProviderRequest
{
    public string ApiKey { get; init; } = string.Empty;
    
    public string ChannelId { get; init; } = string.Empty;
}
