using Cleanuparr.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Features.DownloadClient.RdtClient;

public partial class RdtService
{
    /// <inheritdoc/>
    public override async Task<DownloadCheckResult> ShouldRemoveFromArrQueueAsync(string hash, IReadOnlyList<string> ignoredDownloads)
    {
        DownloadCheckResult result = new();

        try
        {
            var torrents = await GetTorrentListAsync();
            RdtTorrentInfo? download = torrents.FirstOrDefault(x => x.Hash == hash);

            if (download is null)
            {
                _logger.LogDebug("failed to find torrent {hash} in the {name} download client", hash, _downloadClientConfig.Name);
                return result;
            }

            result.Found = true;

            // RDT-Client doesn't have a concept of private torrents in the same way
            result.IsPrivate = false;

            _logger.LogDebug("Queue check for RDT-Client torrent {name} - basic implementation", download.Name);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking torrent {hash} in RDT-Client", hash);
            return result;
        }
    }
}