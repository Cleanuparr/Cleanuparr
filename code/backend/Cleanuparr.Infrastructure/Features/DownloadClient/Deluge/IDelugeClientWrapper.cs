using Cleanuparr.Domain.Entities.Deluge.Response;

namespace Cleanuparr.Infrastructure.Features.DownloadClient.Deluge;

public interface IDelugeClientWrapper
{
    Task<bool> LoginAsync();
    Task<bool> IsConnected();
    Task<bool> Connect();
    Task<DownloadStatus?> GetTorrentStatus(string hash);
    Task<DelugeContents?> GetTorrentFiles(string hash);
    Task<DelugeTorrent?> GetTorrent(string hash);
    Task<DelugeTorrentExtended?> GetTorrentExtended(string hash);
    Task<List<DownloadStatus>?> GetStatusForAllTorrents();
    Task DeleteTorrents(List<string> hashes, bool removeData);
    Task ChangeFilesPriority(string hash, List<int> priorities);
    Task<IReadOnlyList<string>> GetLabels();
    Task CreateLabel(string label);
    Task SetTorrentLabel(string hash, string newLabel);
}
