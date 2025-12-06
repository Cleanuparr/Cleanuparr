using QBittorrent.Client;

namespace Cleanuparr.Infrastructure.Features.DownloadClient.QBittorrent;

/// <summary>
/// Concrete wrapper implementation for QBittorrentClient
/// </summary>
public sealed class QBittorrentClientWrapper : IQBittorrentClientWrapper
{
    private readonly QBittorrentClient _client;

    public QBittorrentClientWrapper(QBittorrentClient client)
    {
        _client = client;
    }

    public Task LoginAsync(string username, string password)
        => _client.LoginAsync(username, password);

    public Task<ApiVersion> GetApiVersionAsync()
        => _client.GetApiVersionAsync();

    public Task<IReadOnlyList<TorrentInfo>> GetTorrentListAsync(TorrentListQuery query)
        => _client.GetTorrentListAsync(query);

    public Task<TorrentProperties?> GetTorrentPropertiesAsync(string hash)
        => _client.GetTorrentPropertiesAsync(hash);

    public Task<IReadOnlyList<TorrentContent>> GetTorrentContentsAsync(string hash)
        => _client.GetTorrentContentsAsync(hash);

    public Task<IReadOnlyList<TorrentTracker>> GetTorrentTrackersAsync(string hash)
        => _client.GetTorrentTrackersAsync(hash);

    public Task<IReadOnlyDictionary<string, Category>> GetCategoriesAsync()
        => _client.GetCategoriesAsync();

    public Task AddCategoryAsync(string category)
        => _client.AddCategoryAsync(category);

    public Task DeleteAsync(IEnumerable<string> hashes, bool deleteDownloadedData)
        => _client.DeleteAsync(hashes, deleteDownloadedData);

    public Task AddTorrentTagAsync(IEnumerable<string> hashes, string tag)
        => _client.AddTorrentTagAsync(hashes, tag);

    public Task SetTorrentCategoryAsync(IEnumerable<string> hashes, string category)
        => _client.SetTorrentCategoryAsync(hashes, category);

    public Task SetFilePriorityAsync(string hash, int fileIndex, TorrentContentPriority priority)
        => _client.SetFilePriorityAsync(hash, fileIndex, priority);

    public Task SetPreferencesAsync(Preferences preferences)
        => _client.SetPreferencesAsync(preferences);

    public void Dispose()
        => _client.Dispose();
}
