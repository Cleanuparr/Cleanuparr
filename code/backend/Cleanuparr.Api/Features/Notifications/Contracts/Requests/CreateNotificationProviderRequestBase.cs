namespace Cleanuparr.Api.Features.Notifications.Contracts.Requests;

public abstract record CreateNotificationProviderRequestBase
{
    public string Name { get; init; } = string.Empty;
    
    public bool IsEnabled { get; init; } = true;
    
    public bool OnFailedImportStrike { get; init; }
    
    public bool OnStalledStrike { get; init; }
    
    public bool OnSlowStrike { get; init; }
    
    public bool OnQueueItemDeleted { get; init; }
    
    public bool OnDownloadCleaned { get; init; }
    
    public bool OnCategoryChanged { get; init; }
}
