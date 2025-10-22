namespace Cleanuparr.Api.Features.Notifications.Contracts.Requests;

public record CreateAppriseProviderRequest : CreateNotificationProviderRequestBase
{
    public string Url { get; init; } = string.Empty;
    
    public string Key { get; init; } = string.Empty;
    
    public string Tags { get; init; } = string.Empty;
}
