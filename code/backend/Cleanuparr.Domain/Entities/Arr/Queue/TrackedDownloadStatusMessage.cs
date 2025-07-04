namespace Cleanuparr.Domain.Entities.Arr.Queue;

public sealed record TrackedDownloadStatusMessage
{
    public string Title { get; set; }
    
    public List<string>? Messages { get; set; }
}