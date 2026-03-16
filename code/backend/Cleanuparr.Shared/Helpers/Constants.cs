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
    public const ushort MaxSearchIntervalMinutes = 30;

    public const string LogoUrl = "https://cdn.jsdelivr.net/gh/Cleanuparr/Cleanuparr@main/Logo/48.png";
}