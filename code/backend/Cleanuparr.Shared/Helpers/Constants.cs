using Microsoft.Extensions.Caching.Memory;

namespace Cleanuparr.Shared.Helpers;

public static class Constants
{
    public static readonly TimeSpan TriggerMaxLimit  = TimeSpan.FromHours(6);
    public static readonly TimeSpan TriggerMinLimit = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan SeekerMinLimit = TimeSpan.FromMinutes(1);

    public const string HttpClientWithRetryName = "retry";

    public static readonly MemoryCacheEntryOptions DefaultCacheEntryOptions = new()
    {
        SlidingExpiration = TimeSpan.FromMinutes(10)
    };

    public const ushort DefaultSearchIntervalMinutes = 3;
    public const ushort MinSearchIntervalMinutes = 2;
    public const ushort MaxSearchIntervalMinutes = 360;

    public const string LogoUrl = "https://cdn.jsdelivr.net/gh/Cleanuparr/Cleanuparr@main/Logo/48.png";

    public const string CustomFormatScoreSyncerCron = "0 0/30 * * * ?";

    /// <summary>
    /// Quartz JobKey for webhook-triggered MalwareBlocker runs. Distinct from the cron JobKey
    /// ("MalwareBlocker") so the two have independent DisallowConcurrentExecution locks.
    /// </summary>
    public const string MalwareBlockerWebhookJobKey = "MalwareBlockerWebhook";

    /// <summary>
    /// Delays (relative to receiving the "On Grab" webhook) at which the targeted MalwareBlocker scan
    /// is run. The first run is immediate; later runs catch torrents whose file metadata was not yet
    /// available. Once a download is acted on, retries become safe no-ops.
    /// </summary>
    public static readonly IReadOnlyList<TimeSpan> MalwareBlockerWebhookRetryDelays =
    [
        TimeSpan.Zero,
        TimeSpan.FromSeconds(30),
        TimeSpan.FromSeconds(120),
    ];
}