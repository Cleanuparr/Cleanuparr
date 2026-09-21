using Cleanuparr.Domain.Enums;

namespace Cleanuparr.Infrastructure.Helpers;

public static class CacheKeys
{
    public static string BlocklistType(InstanceType instanceType) => $"{instanceType.ToString()}_type";
    public static string BlocklistPatterns(InstanceType instanceType) => $"{instanceType.ToString()}_patterns";
    public static string BlocklistRegexes(InstanceType instanceType) => $"{instanceType.ToString()}_regexes";

    public static string IgnoredDownloads(string name) => $"{name}_ignored";
    
    public static string DownloadMarkedForRemoval(string hash, Uri url) => $"remove_{hash.ToLowerInvariant()}_{url}";

    public static string ForceImportPending(Uri url) => $"force_import_pending_{url}";

    public static string ForceImportGaveUp(string hash, Uri url) => $"force_import_gave_up_{hash.ToLowerInvariant()}_{url}";

    public static string ForceImportFirstSeen(string hash, Uri url) => $"force_import_seen_{hash.ToLowerInvariant()}_{url}";

    public static string ForceImportTries(string hash, Uri url) => $"force_import_tries_{hash.ToLowerInvariant()}_{url}";
    
    public static class UTorrent
    {
        public static string GetAuthTokenKey(string clientId) => $"utorrent:auth:token:{clientId}";
        public static string GetGuidCookieKey(string clientId) => $"utorrent:auth:cookie:{clientId}";
    }
}