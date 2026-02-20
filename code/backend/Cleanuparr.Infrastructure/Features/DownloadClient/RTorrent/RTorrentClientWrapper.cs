using Cleanuparr.Domain.Entities.RTorrent.Response;

namespace Cleanuparr.Infrastructure.Features.DownloadClient.RTorrent;

public sealed class RTorrentClientWrapper : IRTorrentClientWrapper
{
    private readonly RTorrentClient _client;

    public RTorrentClientWrapper(RTorrentClient client)
    {
        _client = client;
    }

    public Task<string> GetVersionAsync()
        => _client.GetVersionAsync();

    public Task<List<RTorrentTorrent>> GetAllTorrentsAsync()
        => _client.GetAllTorrentsAsync();

    public Task<RTorrentTorrent?> GetTorrentAsync(string hash)
        => _client.GetTorrentAsync(hash);

    public Task<List<RTorrentFile>> GetTorrentFilesAsync(string hash)
        => _client.GetTorrentFilesAsync(hash);

    public Task<List<string>> GetTrackersAsync(string hash)
        => _client.GetTrackersAsync(hash);

    public Task DeleteTorrentAsync(string hash)
        => _client.DeleteTorrentAsync(hash);

    public Task SetFilePriorityAsync(string hash, int fileIndex, int priority)
        => _client.SetFilePriorityAsync(hash, fileIndex, priority);

    public Task<string?> GetLabelAsync(string hash)
        => _client.GetLabelAsync(hash);

    public Task SetLabelAsync(string hash, string label)
        => _client.SetLabelAsync(hash, label);
}
