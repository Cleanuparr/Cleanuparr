using Cleanuparr.Domain.Enums;

namespace Cleanuparr.Infrastructure.Features.Jobs;

/// <summary>
/// Identifies a single download that should be scanned by the MalwareBlocker as the result of an
/// *arr "On Grab" webhook, instead of iterating the whole queue. Carried through the Quartz
/// JobDataMap and surfaced to the handler via <see cref="Context.ContextProvider"/>.
/// </summary>
public sealed record WebhookScanTarget(Guid InstanceId, string DownloadId, long ContentId, InstanceType Type)
{
    public const string InstanceIdKey = "webhook.instanceId";
    public const string DownloadIdKey = "webhook.downloadId";
    public const string ContentIdKey = "webhook.contentId";
    public const string InstanceTypeKey = "webhook.instanceType";
}
