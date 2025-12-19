using Transmission.API.RPC;
using Transmission.API.RPC.Arguments;
using Transmission.API.RPC.Entity;

namespace Cleanuparr.Infrastructure.Features.DownloadClient.Transmission;

/// <summary>
/// Concrete wrapper implementation for Transmission Client
/// </summary>
public sealed class TransmissionClientWrapper : ITransmissionClientWrapper
{
    private readonly Client _client;

    public TransmissionClientWrapper(Client client)
    {
        _client = client;
    }

    public Task<SessionInfo?> GetSessionInformationAsync()
        => _client.GetSessionInformationAsync();

    public Task<TransmissionTorrents?> TorrentGetAsync(string[] fields, string? hash = null)
    {
        if (hash is null)
        {
            return _client.TorrentGetAsync(fields);
        }
        
        return _client.TorrentGetAsync(fields, hash);
    }

    public Task TorrentSetAsync(TorrentSettings settings)
        => _client.TorrentSetAsync(settings);

    public Task TorrentSetLocationAsync(long[] ids, string location, bool move)
        => _client.TorrentSetLocationAsync(ids, location, move);

    public Task TorrentRemoveAsync(long[] ids, bool deleteLocalData)
        => _client.TorrentRemoveAsync(ids, deleteLocalData);
}
