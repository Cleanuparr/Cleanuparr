using Cleanuparr.Domain.Entities.UTorrent.Response;

namespace Cleanuparr.Infrastructure.Features.DownloadClient.UTorrent;

public sealed class UTorrentClientWrapper : IUTorrentClientWrapper
{
    private readonly UTorrentClient _client;

    public UTorrentClientWrapper(UTorrentClient client)
    {
        _client = client;
    }

    public Task<bool> LoginAsync()
        => _client.LoginAsync();

    public Task<bool> TestConnectionAsync()
        => _client.TestConnectionAsync();

    public Task<List<UTorrentItem>> GetTorrentsAsync()
        => _client.GetTorrentsAsync();

    public Task<UTorrentItem?> GetTorrentAsync(string hash)
        => _client.GetTorrentAsync(hash);

    public Task<List<UTorrentFile>?> GetTorrentFilesAsync(string hash)
        => _client.GetTorrentFilesAsync(hash);

    public Task<UTorrentProperties> GetTorrentPropertiesAsync(string hash)
        => _client.GetTorrentPropertiesAsync(hash);

    public Task<List<string>> GetLabelsAsync()
        => _client.GetLabelsAsync();

    public Task SetTorrentLabelAsync(string hash, string label)
        => _client.SetTorrentLabelAsync(hash, label);

    public Task SetFilesPriorityAsync(string hash, List<int> fileIndexes, int priority)
        => _client.SetFilesPriorityAsync(hash, fileIndexes, priority);

    public Task RemoveTorrentsAsync(List<string> hashes)
        => _client.RemoveTorrentsAsync(hashes);
}
