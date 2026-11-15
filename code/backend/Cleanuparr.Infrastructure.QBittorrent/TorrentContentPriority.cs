namespace QBittorrent.Client
{
    /// <summary>
    /// Torrent content file download priority
    /// </summary>
    public enum TorrentContentPriority
    {
        /// <summary>
        /// Do not download the file (0).
        /// </summary>
        Skip = 0,

        /// <summary>
        /// Sent as 1, which qBittorrent treats as normal priority.
        /// </summary>
        Minimal = 1,

        /// <summary>
        /// Read-only (2). The client rejects it when setting.
        /// </summary>
        VeryLow = 2,

        /// <summary>
        /// Read-only (3). The client rejects it when setting.
        /// </summary>
        Low = 3,

        /// <summary>
        /// Read-only (4). The client rejects it when setting.
        /// </summary>
        Normal = 4,

        /// <summary>
        /// Read-only (5). The client rejects it when setting.
        /// </summary>
        High = 5,

        /// <summary>
        /// Sent as 6, which qBittorrent treats as high priority.
        /// </summary>
        VeryHigh = 6,
        
        /// <summary>
        /// Sent as 7, which qBittorrent treats as maximal priority.
        /// </summary>
        Maximal = 7
    }
}
