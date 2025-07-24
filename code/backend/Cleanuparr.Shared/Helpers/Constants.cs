using Microsoft.Extensions.Caching.Memory;

namespace Cleanuparr.Shared.Helpers;

public static class Constants
{
    public static readonly TimeSpan TriggerMaxLimit  = TimeSpan.FromHours(6);
    public static readonly TimeSpan TriggerMinLimit = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan CacheLimitBuffer = TimeSpan.FromHours(2);

    public const string HttpClientWithRetryName = "retry";
    
    public static readonly MemoryCacheEntryOptions DefaultCacheEntryOptions = new()
    {
        SlidingExpiration = TimeSpan.FromMinutes(10)
    };
}