namespace Cleanuparr.Api.Features.Notifications.Contracts.Requests;

public record UpdateGotifyProviderRequest : UpdateNotificationProviderRequestBase
{
    public string ServerUrl { get; init; } = string.Empty;

    public string ApplicationToken { get; init; } = string.Empty;

    public int Priority { get; init; } = 5;
}
