namespace Cleanuparr.Api.Models.NotificationProviders;

public sealed record CreateAppriseProviderDto : CreateNotificationProviderBaseDto
{
    public string Url { get; init; } = string.Empty;
    
    public string Key { get; init; } = string.Empty;
    
    public string Tags { get; init; } = string.Empty;
}
