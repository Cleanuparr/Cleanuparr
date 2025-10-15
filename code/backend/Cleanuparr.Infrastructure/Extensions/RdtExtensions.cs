using Cleanuparr.Infrastructure.Features.DownloadClient.RdtClient;
using Cleanuparr.Infrastructure.Services;

namespace Cleanuparr.Infrastructure.Extensions;

public static class RdtExtensions
{
    public static bool ShouldIgnore(this RdtService.RdtTorrentInfo download, IReadOnlyList<string> ignoredDownloads)
    {
        foreach (string value in ignoredDownloads)
        {
            if (download.Hash?.Equals(value, StringComparison.InvariantCultureIgnoreCase) ?? false)
            {
                return true;
            }

            if (download.Category?.Equals(value, StringComparison.InvariantCultureIgnoreCase) ?? false)
            {
                return true;
            }

            if (!string.IsNullOrEmpty(download.Tags))
            {
                var tags = download.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (tags.Any(tag => tag.Equals(value, StringComparison.InvariantCultureIgnoreCase)))
                {
                    return true;
                }
            }
        }

        return false;
    }

}
