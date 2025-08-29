using Cleanuparr.Domain.Enums;

namespace Cleanuparr.Api.Models;

public sealed record UpdateNotificationProviderDto
{
    public string Name { get; init; } = string.Empty;
    
    public NotificationProviderType Type { get; init; }
    
    public bool IsEnabled { get; init; } = true;
    
    public bool OnFailedImportStrike { get; init; }
    
    public bool OnStalledStrike { get; init; }
    
    public bool OnSlowStrike { get; init; }
    
    public bool OnQueueItemDeleted { get; init; }
    
    public bool OnDownloadCleaned { get; init; }
    
    public bool OnCategoryChanged { get; init; }
    
    public object? Configuration { get; init; }
}
