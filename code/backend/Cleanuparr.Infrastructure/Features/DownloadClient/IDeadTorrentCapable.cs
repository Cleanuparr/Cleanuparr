namespace Cleanuparr.Infrastructure.Features.DownloadClient;

/// <summary>
/// Torrent clients that report a seeder count, so dead-torrent detection applies.
/// rTorrent does not.
/// </summary>
public interface IDeadTorrentCapable : ISeedingCleanupCapable, IUnlinkedCapable
{
}
