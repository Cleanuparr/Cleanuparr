namespace Cleanuparr.Api.Features.Notifications.Contracts.Responses;

public sealed record NotificationProvidersResponse
{
    public List<NotificationProviderResponse> Providers { get; init; } = [];
}
