using Cleanuparr.Domain.Entities.RTorrent.Response;

namespace Cleanuparr.Infrastructure.Features.DownloadClient.RTorrent;

public interface IRTorrentClientWrapper
{
    Task<string> GetVersionAsync();
    Task<List<RTorrentTorrent>> GetAllTorrentsAsync();
    Task<RTorrentTorrent?> GetTorrentAsync(string hash);
    Task<List<RTorrentFile>> GetTorrentFilesAsync(string hash);
    Task<List<string>> GetTrackersAsync(string hash);
    Task DeleteTorrentAsync(string hash);
    Task SetFilePriorityAsync(string hash, int fileIndex, int priority);
    Task<string?> GetLabelAsync(string hash);
    Task SetLabelAsync(string hash, string label);
}
