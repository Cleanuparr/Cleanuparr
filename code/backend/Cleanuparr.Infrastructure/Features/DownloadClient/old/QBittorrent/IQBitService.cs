namespace Cleanuparr.Infrastructure.Features.DownloadClient.QBittorrent;

public interface IQBitService : IDownloadService, IDisposable
{
    Task UpdateBlacklistAsync(string blacklistPath);
}