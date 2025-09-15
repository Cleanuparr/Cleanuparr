using System.ComponentModel.DataAnnotations.Schema;
using Cleanuparr.Persistence.Models.Configuration;

namespace Cleanuparr.Persistence.Models.State;

/// <summary>
/// Tracks which download clients have been synchronized for a specific blacklist content hash.
/// </summary>
public sealed record BlacklistSyncHistory
{
    /// <summary>
    /// Primary key
    /// </summary>
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>
    /// SHA-256 hash of the blacklist contents used during synchronization
    /// </summary>
    public required string Hash { get; init; }

    /// <summary>
    /// Foreign key to the download client this sync entry applies to
    /// </summary>
    public required Guid DownloadClientId { get; init; }
    
    /// <summary>
    /// Navigation property to the associated download client configuration
    /// </summary>
    public DownloadClientConfig DownloadClient { get; init; } = null!;
}
