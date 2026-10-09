namespace Cleanuparr.Domain.Enums;

/// <summary>
/// A capability a download client service may implement, mirrored by one of the
/// <c>I*Capable</c> interfaces under <c>Infrastructure/Features/DownloadClient</c>.
/// </summary>
public enum DownloadClientCapability
{
    /// <summary>
    /// Can report whether an *arr queue item should be removed. Backs <c>IQueueCheckCapable</c>.
    /// </summary>
    QueueCheck,

    /// <summary>
    /// Can block unwanted files from completing. Backs <c>IFileBlockingCapable</c>.
    /// </summary>
    FileBlocking,

    /// <summary>
    /// Can report the on-disk paths its downloads claim. Backs <c>IOrphanClaimsCapable</c>.
    /// </summary>
    OrphanClaims,

    /// <summary>
    /// Downloads seed, so seeding rules apply. Backs <c>ISeedingCleanupCapable</c>.
    /// </summary>
    SeedingCleanup,

    /// <summary>
    /// Files can be checked for hardlinks, so unlinked downloads can be moved. Backs <c>IUnlinkedCapable</c>.
    /// </summary>
    Unlinked,

    /// <summary>
    /// Seeding rules can filter by tag/label. Backs <c>ITagFilterable</c> on the client's seeding rule type.
    /// </summary>
    TagFiltering,

    /// <summary>
    /// Seeding rules can filter by minimum seeders. Backs <c>ISeedersFilterable</c> on the client's seeding rule type.
    /// </summary>
    SeedersFiltering,

    /// <summary>
    /// Reports a seeder count, so dead-torrent detection applies. Backs <c>IDeadTorrentCapable</c>.
    /// </summary>
    DeadTorrent,
}
