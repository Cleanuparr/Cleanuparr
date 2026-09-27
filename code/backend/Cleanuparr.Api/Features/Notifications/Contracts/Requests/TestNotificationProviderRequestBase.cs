namespace Cleanuparr.Api.Features.Notifications.Contracts.Requests;

public abstract record TestNotificationProviderRequestBase
{
    public Guid? ProviderId { get; init; }
}
