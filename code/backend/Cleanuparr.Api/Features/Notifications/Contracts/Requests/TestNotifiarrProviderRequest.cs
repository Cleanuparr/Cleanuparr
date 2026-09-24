namespace Cleanuparr.Api.Features.Notifications.Contracts.Requests;

public record TestNotifiarrProviderRequest : TestNotificationProviderRequestBase
{
    public string ApiKey { get; init; } = string.Empty;

    public string ChannelId { get; init; } = string.Empty;
}
