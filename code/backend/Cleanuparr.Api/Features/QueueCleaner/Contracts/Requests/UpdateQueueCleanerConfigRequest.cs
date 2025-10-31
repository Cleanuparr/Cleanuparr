using Cleanuparr.Persistence.Models.Configuration.QueueCleaner;

namespace Cleanuparr.Api.Features.QueueCleaner.Contracts.Requests;

public sealed record UpdateQueueCleanerConfigRequest
{
    public bool Enabled { get; init; }

    public string CronExpression { get; init; } = "0 0/5 * * * ?";

    public bool UseAdvancedScheduling { get; init; }

    public FailedImportConfig FailedImport { get; init; } = new();

    public ushort DownloadingMetadataMaxStrikes { get; init; }
    
    public List<string> IgnoredDownloads { get; set; } = [];
}
