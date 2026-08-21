using Cleanuparr.Domain.Entities;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Domain.Entities.LazyLibrarian;
using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Infrastructure.Features.DownloadClient;
using Cleanuparr.Persistence.Models.Configuration;
using Cleanuparr.Persistence.Models.Configuration.Arr;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Features.LazyLibrarian;

/// <summary>
/// Asks the torrent clients what to do with each queued item.
/// The subclass supplies the question its job asks.
/// </summary>
public abstract class LazyLibrarianJobEvaluator : ILazyLibrarianEvaluator
{
    protected readonly ILogger _logger;
    private readonly ILazyLibrarianService _lazyLibrarianService;

    protected LazyLibrarianJobEvaluator(ILogger logger, ILazyLibrarianService lazyLibrarianService)
    {
        _logger = logger;
        _lazyLibrarianService = lazyLibrarianService;
    }

    /// <summary>
    /// A download client's verdict, normalised across the two jobs.
    /// </summary>
    protected readonly record struct ClientVerdict(
        bool Found,
        bool ShouldRemove,
        bool RemoveFromClient,
        DeleteReason DeleteReason,
        ITorrentItemWrapper? Torrent
    );

    protected abstract Task<ClientVerdict> CheckAsync(
        IDownloadService downloadService,
        string hash,
        IReadOnlyList<string> ignoredDownloads
    );

    public async Task<IReadOnlyList<LazyLibrarianRemovalDecision>> EvaluateAsync(
        ArrInstance instance,
        IReadOnlyList<IDownloadService> downloadServices,
        IReadOnlyList<string> ignoredDownloads
    )
    {
        IReadOnlyList<LazyLibrarianQueueItem> items = await _lazyLibrarianService.GetQueueAsync(instance);

        List<IDownloadService> torrentClients = downloadServices
            .Where(x => x.ClientConfig.Type is DownloadClientType.Torrent)
            .ToList();

        if (torrentClients.Count is 0)
        {
            _logger.LogDebug("No torrent clients enabled");
            return [];
        }

        List<LazyLibrarianRemovalDecision> decisions = new();

        foreach (LazyLibrarianQueueItem item in items)
        {
            if (ignoredDownloads.Any(ignored => item.DownloadId.Equals(ignored, StringComparison.OrdinalIgnoreCase)))
            {
                _logger.LogInformation("skip | download is ignored | {title}", item.Title);
                continue;
            }

            _logger.LogDebug("processing | {title} | {id}", item.Title, item.DownloadId);

            // The striker fires inside the download service and notifies from context.
            ContextProvider.Set(ContextProvider.Keys.ItemName, item.Title);
            ContextProvider.Set(ContextProvider.Keys.Hash, item.DownloadId);

            LazyLibrarianRemovalDecision? decision = await EvaluateItemAsync(item, torrentClients, ignoredDownloads);

            if (decision is not null)
            {
                decisions.Add(decision);
            }
        }

        return decisions;
    }

    private async Task<LazyLibrarianRemovalDecision?> EvaluateItemAsync(
        LazyLibrarianQueueItem item,
        List<IDownloadService> torrentClients,
        IReadOnlyList<string> ignoredDownloads
    )
    {
        ClientVerdict check = default;
        DownloadClientConfig? foundInClient = null;
        IDownloadService? foundInService = null;

        foreach (IDownloadService downloadService in torrentClients)
        {
            try
            {
                check = await CheckAsync(downloadService, item.DownloadId, ignoredDownloads);

                if (check.Found)
                {
                    foundInClient = downloadService.ClientConfig;
                    foundInService = downloadService;
                    break;
                }
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Error checking download {dName} with download client {cName}",
                    item.Title, downloadService.ClientConfig.Name);
            }
        }

        if (!check.Found)
        {
            _logger.LogWarning("Download not found in any torrent client | {title}", item.Title);
            return null;
        }

        if (!check.ShouldRemove)
        {
            return null;
        }

        return new LazyLibrarianRemovalDecision
        {
            Item = item,
            DeleteReason = check.DeleteReason,
            RemoveFromClient = check.RemoveFromClient,
            DownloadClient = foundInClient,
            DownloadService = foundInService,
            Torrent = check.Torrent,
        };
    }
}
