using Cleanuparr.Domain.Enums;

namespace Cleanuparr.Infrastructure.Features.DownloadClient;

public sealed record SeedingCheckResult
{
    public bool ShouldClean { get; set; }
    
    public CleanReason Reason { get; set; }    
}