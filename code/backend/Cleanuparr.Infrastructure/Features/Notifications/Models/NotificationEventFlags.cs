namespace Cleanuparr.Infrastructure.Features.Notifications.Models;

public record NotificationEventFlags
{
    public bool OnFailedImportStrike { get; init; }
    
    public bool OnStalledStrike { get; init; }
    
    public bool OnSlowStrike { get; init; }
    
    public bool OnQueueItemDeleted { get; init; }
    
    public bool OnDownloadCleaned { get; init; }
    
    public bool OnDownloadStopped { get; init; }
    
    public bool OnCategoryChanged { get; init; }

    public bool OnSearchTriggered { get; init; }

    public bool OnSearchItemGrabbed { get; init; }

    public bool OnForceImported { get; init; }

    public bool OnErroredStrike { get; init; }

    public bool OnSeekerCycleCompleted { get; init; }
}
