using Cleanuparr.Domain.Entities;
using Cleanuparr.Infrastructure.Features.DownloadClient;

namespace Cleanuparr.Infrastructure.Features.DownloadCleaner.Services;

/// <summary>
/// Handles downloads that have lost their hard links by moving them to a
/// dedicated category or tag so they can be cleaned up separately.
/// </summary>
public interface IUnlinkedDownloadsService
{
    /// <summary>
    /// Re-categorises downloads with no hard links according to the supplied
    /// configuration.
    /// </summary>
    Task ProcessAsync(IDownloadService downloadService, List<ITorrentItemWrapper> clientDownloads);
}
