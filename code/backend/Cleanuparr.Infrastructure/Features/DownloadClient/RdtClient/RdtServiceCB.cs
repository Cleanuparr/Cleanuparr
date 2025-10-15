using Cleanuparr.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Features.DownloadClient.RdtClient;

public partial class RdtService
{
    /// <inheritdoc/>
    public override async Task<BlockFilesResult> BlockUnwantedFilesAsync(string hash, IReadOnlyList<string> ignoredDownloads)
    {
        BlockFilesResult result = new();

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

            _logger.LogDebug("File blocking for RDT-Client is limited - torrent {name}", download.Name);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error blocking files for torrent {hash} in RDT-Client", hash);
            return result;
        }
    }
}