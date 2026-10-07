using Cleanuparr.Domain.Entities;
using Cleanuparr.Persistence.Models.Configuration.DownloadCleaner;

namespace Cleanuparr.Infrastructure.Features.DownloadClient;

/// <summary>
/// Download clients whose files can be checked for hardlinks, so unlinked downloads can be moved to a target category.
/// Implemented by the torrent clients only; the *arrs move (never hardlink) usenet downloads on import.
/// </summary>
public interface IUnlinkedCapable : IDownloadService
{
    /// <summary>
    /// Filters downloads that should have their category changed.
    /// </summary>
    /// <param name="downloads">The downloads to filter.</param>
    /// <param name="unlinkedConfig">The unlinked config for this download client.</param>
    /// <returns>A list of downloads for the provided categories.</returns>
    List<ITorrentItemWrapper>? FilterDownloadsToChangeCategoryAsync(List<ITorrentItemWrapper>? downloads, UnlinkedConfig unlinkedConfig);

    /// <summary>
    /// Changes the category for downloads that have no hardlinks.
    /// </summary>
    /// <param name="downloads">The downloads to change.</param>
    /// <param name="unlinkedConfig">The unlinked config for this download client.</param>
    Task ChangeCategoryForNoHardLinksAsync(List<ITorrentItemWrapper>? downloads, UnlinkedConfig unlinkedConfig);

    /// <summary>
    /// Moves a single torrent to the target category, or adds it as a tag/label when <paramref name="useTag"/> is set.
    /// </summary>
    /// <param name="torrent">The torrent to move.</param>
    /// <param name="targetCategory">The target category/tag.</param>
    /// <param name="useTag">When true, add a tag/label instead of changing the category (qBittorrent and Transmission).</param>
    Task ChangeTorrentCategoryAsync(ITorrentItemWrapper torrent, string targetCategory, bool useTag);

    /// <summary>
    /// Creates a category.
    /// </summary>
    /// <param name="name">The category name.</param>
    Task CreateCategoryAsync(string name);
}
