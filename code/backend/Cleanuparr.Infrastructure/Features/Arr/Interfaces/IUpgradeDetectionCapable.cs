using Cleanuparr.Domain.Entities.Arr.Queue;

namespace Cleanuparr.Infrastructure.Features.Arr.Interfaces;

/// <summary>
/// An arr whose queue tells an upgrade from a new download.
/// </summary>
public interface IUpgradeDetectionCapable : IArrClient
{
    /// <summary>
    /// Checks whether every record of a download has a file.
    /// </summary>
    /// <param name="records">The queue records that share the same download, for example a season pack.</param>
    /// <returns>True when every record has a file.</returns>
    bool IsUpgrade(IReadOnlyList<QueueRecord> records);
}
