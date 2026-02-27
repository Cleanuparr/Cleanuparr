namespace Cleanuparr.Api.Features.Notifications.Contracts.Requests;

public record TestGotifyProviderRequest
{
    public string ServerUrl { get; init; } = string.Empty;

    public string ApplicationToken { get; init; } = string.Empty;

    public int Priority { get; init; } = 5;

    public Guid? ProviderId { get; init; }
}
