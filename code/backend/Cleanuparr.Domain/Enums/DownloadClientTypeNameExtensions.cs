namespace Cleanuparr.Domain.Enums;

public static class DownloadClientTypeNameExtensions
{
    private static readonly Dictionary<DownloadClientTypeName, DownloadClientAuthField[]> AuthFields = new()
    {
        [DownloadClientTypeName.qBittorrent] = [DownloadClientAuthField.Username, DownloadClientAuthField.Password],
        [DownloadClientTypeName.Deluge] = [DownloadClientAuthField.Password],
        [DownloadClientTypeName.Transmission] = [DownloadClientAuthField.Username, DownloadClientAuthField.Password],
        [DownloadClientTypeName.uTorrent] = [DownloadClientAuthField.Username, DownloadClientAuthField.Password],
        [DownloadClientTypeName.rTorrent] = [DownloadClientAuthField.Username, DownloadClientAuthField.Password],
        [DownloadClientTypeName.Sabnzbd] = [DownloadClientAuthField.ApiKey],
    };

    /// <summary>
    /// Whether the client type accepts this credential field.
    /// </summary>
    public static bool SupportsAuthField(this DownloadClientTypeName typeName, DownloadClientAuthField field) =>
        AuthFields.TryGetValue(typeName, out var fields) && fields.Contains(field);

    /// <summary>
    /// Maps a client type name to its download protocol, so callers never pass this independently.
    /// </summary>
    public static DownloadClientType ClientType(this DownloadClientTypeName typeName) => typeName switch
    {
        DownloadClientTypeName.qBittorrent => DownloadClientType.Torrent,
        DownloadClientTypeName.Deluge => DownloadClientType.Torrent,
        DownloadClientTypeName.Transmission => DownloadClientType.Torrent,
        DownloadClientTypeName.uTorrent => DownloadClientType.Torrent,
        DownloadClientTypeName.rTorrent => DownloadClientType.Torrent,
        DownloadClientTypeName.Sabnzbd => DownloadClientType.Usenet,
        _ => throw new ArgumentOutOfRangeException(nameof(typeName), typeName, "Unknown download client type name"),
    };

    /// <summary>
    /// True when the field is the client's only credential, so leaving it blank means it cannot authenticate at all.
    /// </summary>
    public static bool RequiresAuthField(this DownloadClientTypeName typeName, DownloadClientAuthField field) =>
        AuthFields.TryGetValue(typeName, out var fields) && fields is [var only] && only == field;

    private static readonly DownloadClientCapability[] BaseTorrentCapabilities =
    [
        DownloadClientCapability.QueueCheck,
        DownloadClientCapability.FileBlocking,
        DownloadClientCapability.OrphanClaims,
        DownloadClientCapability.SeedingCleanup,
        DownloadClientCapability.Unlinked,
    ];

    private static readonly DownloadClientCapability[] UsenetCapabilities =
    [
        DownloadClientCapability.QueueCheck,
        DownloadClientCapability.FileBlocking,
        DownloadClientCapability.OrphanClaims,
    ];

    /// <summary>
    /// Every torrent client reports a seeder count and supports min-seeders filtering, except rTorrent.
    /// </summary>
    private static readonly DownloadClientCapability[] SeedersCapableTorrentCapabilities =
    [
        ..BaseTorrentCapabilities,
        DownloadClientCapability.SeedersFiltering,
        DownloadClientCapability.DeadTorrent,
    ];

    private static readonly Dictionary<DownloadClientTypeName, DownloadClientCapability[]> Capabilities = new()
    {
        [DownloadClientTypeName.qBittorrent] =
        [
            ..SeedersCapableTorrentCapabilities,
            DownloadClientCapability.TagFiltering,
        ],
        [DownloadClientTypeName.Deluge] = SeedersCapableTorrentCapabilities,
        [DownloadClientTypeName.Transmission] =
        [
            ..SeedersCapableTorrentCapabilities,
            DownloadClientCapability.TagFiltering,
        ],
        [DownloadClientTypeName.uTorrent] = SeedersCapableTorrentCapabilities,
        [DownloadClientTypeName.rTorrent] = BaseTorrentCapabilities,
        [DownloadClientTypeName.Sabnzbd] = UsenetCapabilities,
    };

    /// <summary>
    /// Whether the client type's service implements the capability's <c>I*Capable</c> interface.
    /// </summary>
    public static bool SupportsCapability(this DownloadClientTypeName typeName, DownloadClientCapability capability) =>
        Capabilities.TryGetValue(typeName, out var capabilities) && capabilities.Contains(capability);
}
