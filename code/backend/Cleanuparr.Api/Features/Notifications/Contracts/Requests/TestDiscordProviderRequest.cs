namespace Cleanuparr.Api.Features.Notifications.Contracts.Requests;

public record TestDiscordProviderRequest : TestNotificationProviderRequestBase
{
    public string WebhookUrl { get; init; } = string.Empty;

    public string Username { get; init; } = string.Empty;

    public string AvatarUrl { get; init; } = string.Empty;
}
