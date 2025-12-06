using QBittorrent.Client;

namespace Cleanuparr.Infrastructure.Features.DownloadClient.QBittorrent;

/// <summary>
/// Wrapper interface for QBittorrentClient to enable testing
/// </summary>
public interface IQBittorrentClientWrapper : IDisposable
{
    Task LoginAsync(string username, string password);
    Task<ApiVersion> GetApiVersionAsync();
    Task<IReadOnlyList<TorrentInfo>> GetTorrentListAsync(TorrentListQuery query);
    Task<TorrentProperties?> GetTorrentPropertiesAsync(string hash);
    Task<IReadOnlyList<TorrentContent>> GetTorrentContentsAsync(string hash);
    Task<IReadOnlyList<TorrentTracker>> GetTorrentTrackersAsync(string hash);
    Task<IReadOnlyDictionary<string, Category>> GetCategoriesAsync();
    Task AddCategoryAsync(string category);
    Task DeleteAsync(IEnumerable<string> hashes, bool deleteDownloadedData);
    Task AddTorrentTagAsync(IEnumerable<string> hashes, string tag);
    Task SetTorrentCategoryAsync(IEnumerable<string> hashes, string category);
    Task SetFilePriorityAsync(string hash, int fileIndex, TorrentContentPriority priority);
    Task SetPreferencesAsync(Preferences preferences);
}
