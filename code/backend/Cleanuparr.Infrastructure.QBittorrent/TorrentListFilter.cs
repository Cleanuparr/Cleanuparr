namespace QBittorrent.Client
{
    /// <summary>
    /// The status filter for the torrent list.
    /// </summary>
    public enum TorrentListFilter
    {
        /// <summary>
        /// All torrents
        /// </summary>
        All,

        /// <summary>
        /// The torrents being downloaded
        /// </summary>
        Downloading,
        
        /// <summary>
        /// The completed torrents
        /// </summary>
        Completed,

        /// <summary>
        /// The paused torrents
        /// </summary>
        Paused,

        /// <summary>
        /// The active torrents
        /// </summary>
        Active,

        /// <summary>
        /// The inactive torrents
        /// </summary>
        Inactive,

        /// <summary>
        /// The torrent being seeded
        /// </summary>
        Seeding,

        /// <summary>
        /// The resumed torrents
        /// </summary>
        Resumed,

        /// <summary>
        /// The errored torrents
        /// </summary>
        Errored,

        /// <summary>
        /// The stalled torrents
        /// </summary>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.4.1")]
        Stalled,

        /// <summary>
        /// The stalled torrents being uploaded
        /// </summary>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.4.1")]
        StalledUploading,

        /// <summary>
        /// The stalled torrents being downloaded
        /// </summary>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.4.1")]
        StalledDownloading,
    }
}
