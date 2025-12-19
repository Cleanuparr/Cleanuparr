using Transmission.API.RPC.Arguments;
using Transmission.API.RPC.Entity;

namespace Cleanuparr.Infrastructure.Features.DownloadClient.Transmission;

/// <summary>
/// Wrapper interface for Transmission Client to enable testing
/// </summary>
public interface ITransmissionClientWrapper
{
    Task<SessionInfo?> GetSessionInformationAsync();
    Task<TransmissionTorrents?> TorrentGetAsync(string[] fields, string? hash = null);
    Task TorrentSetAsync(TorrentSettings settings);
    Task TorrentSetLocationAsync(long[] ids, string location, bool move);
    Task TorrentRemoveAsync(long[] ids, bool deleteLocalData);
}
