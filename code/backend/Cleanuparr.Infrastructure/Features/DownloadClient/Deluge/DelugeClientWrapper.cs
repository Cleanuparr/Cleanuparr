using Cleanuparr.Domain.Entities.Deluge.Response;

namespace Cleanuparr.Infrastructure.Features.DownloadClient.Deluge;

public sealed class DelugeClientWrapper : IDelugeClientWrapper
{
    private readonly DelugeClient _client;

    public DelugeClientWrapper(DelugeClient client)
    {
        _client = client;
    }

    public Task<bool> LoginAsync()
        => _client.LoginAsync();

    public Task<bool> IsConnected()
        => _client.IsConnected();

    public Task<bool> Connect()
        => _client.Connect();

    public Task<DownloadStatus?> GetTorrentStatus(string hash)
        => _client.GetTorrentStatus(hash);

    public Task<DelugeContents?> GetTorrentFiles(string hash)
        => _client.GetTorrentFiles(hash);

    public Task<DelugeTorrent?> GetTorrent(string hash)
        => _client.GetTorrent(hash);

    public Task<DelugeTorrentExtended?> GetTorrentExtended(string hash)
        => _client.GetTorrentExtended(hash);

    public Task<List<DownloadStatus>?> GetStatusForAllTorrents()
        => _client.GetStatusForAllTorrents();

    public Task DeleteTorrents(List<string> hashes, bool removeData)
        => _client.DeleteTorrents(hashes, removeData);

    public Task ChangeFilesPriority(string hash, List<int> priorities)
        => _client.ChangeFilesPriority(hash, priorities);

    public Task<IReadOnlyList<string>> GetLabels()
        => _client.GetLabels();

    public Task CreateLabel(string label)
        => _client.CreateLabel(label);

    public Task SetTorrentLabel(string hash, string newLabel)
        => _client.SetTorrentLabel(hash, newLabel);
}
