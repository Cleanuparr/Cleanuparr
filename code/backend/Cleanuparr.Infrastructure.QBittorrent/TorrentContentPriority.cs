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
        /// The minimal priority (1). 
        /// </summary>
        Minimal = 1,

        /// <summary>
        /// The very low priority (2).
        /// </summary>
        VeryLow = 2,

        /// <summary>
        /// The low priority (3).
        /// </summary>
        Low = 3,

        /// <summary>
        /// The normal priority (4).
        /// </summary>
        Normal = 4,

        /// <summary>
        /// The high priority (5).
        /// </summary>
        High = 5,

        /// <summary>
        /// The very high priority (6).
        /// </summary>
        VeryHigh = 6,
        
        /// <summary>
        /// The maximal priority (7).
        /// </summary>
        Maximal = 7
    }
}
