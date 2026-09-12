using Cleanuparr.Persistence.Models.Configuration.QueueCleaner;

namespace Cleanuparr.Api.Features.QueueCleaner.Contracts.Responses;

public sealed record QueueCleanerConfigResponse
{
    public bool Enabled { get; init; }

    public required string CronExpression { get; init; }

    public bool UseAdvancedScheduling { get; init; }

    public required FailedImportConfigResponse FailedImport { get; init; }

    public required IReadOnlyList<string> IgnoredDownloads { get; init; }

    public bool ProcessNoContentId { get; init; }

    public ushort DownloadingMetadataMaxStrikes { get; init; }

    public static QueueCleanerConfigResponse From(QueueCleanerConfig config) => new()
    {
        Enabled = config.Enabled,
        CronExpression = config.CronExpression,
        UseAdvancedScheduling = config.UseAdvancedScheduling,
        FailedImport = FailedImportConfigResponse.From(config.FailedImport),
        IgnoredDownloads = config.IgnoredDownloads,
        ProcessNoContentId = config.ProcessNoContentId,
        DownloadingMetadataMaxStrikes = config.DownloadingMetadataMaxStrikes,
    };
}
