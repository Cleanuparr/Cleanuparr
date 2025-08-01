using Cleanuparr.Domain.Enums;

namespace Cleanuparr.Infrastructure.Helpers;

public static class CacheKeys
{
    public static string Strike(StrikeType strikeType, string hash) => $"{strikeType.ToString()}_{hash}";

    public static string BlocklistType(InstanceType instanceType) => $"{instanceType.ToString()}_type";
    public static string BlocklistPatterns(InstanceType instanceType) => $"{instanceType.ToString()}_patterns";
    public static string BlocklistRegexes(InstanceType instanceType) => $"{instanceType.ToString()}_regexes";
    
    public static string KnownMalwarePatterns() => "KNOWN_MALWARE_PATTERNS";
    
    public static string StrikeItem(string hash, StrikeType strikeType) => $"item_{hash}_{strikeType.ToString()}";
    
    public static string IgnoredDownloads(string name) => $"{name}_ignored";
    
    public static string DownloadMarkedForRemoval(string hash, Uri url) => $"remove_{hash.ToLowerInvariant()}_{url}";
    
    public static class UTorrent
    {
        public static string GetAuthTokenKey(string clientId) => $"utorrent:auth:token:{clientId}";
        public static string GetGuidCookieKey(string clientId) => $"utorrent:auth:cookie:{clientId}";
    }
}