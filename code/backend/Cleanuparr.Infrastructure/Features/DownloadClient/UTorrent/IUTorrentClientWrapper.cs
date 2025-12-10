using Cleanuparr.Domain.Entities.UTorrent.Response;

namespace Cleanuparr.Infrastructure.Features.DownloadClient.UTorrent;

public interface IUTorrentClientWrapper
{
    Task<bool> LoginAsync();
    Task<bool> TestConnectionAsync();
    Task<List<UTorrentItem>> GetTorrentsAsync();
    Task<UTorrentItem?> GetTorrentAsync(string hash);
    Task<List<UTorrentFile>?> GetTorrentFilesAsync(string hash);
    Task<UTorrentProperties> GetTorrentPropertiesAsync(string hash);
    Task<List<string>> GetLabelsAsync();
    Task SetTorrentLabelAsync(string hash, string label);
    Task SetFilesPriorityAsync(string hash, List<int> fileIndexes, int priority);
    Task RemoveTorrentsAsync(List<string> hashes);
}
