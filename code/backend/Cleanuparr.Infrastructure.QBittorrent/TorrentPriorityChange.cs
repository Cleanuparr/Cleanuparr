namespace QBittorrent.Client
{
    /// <summary>
    /// Represent requested value for torrent priority.
    /// </summary>
    public enum TorrentPriorityChange
    {
        /// <summary>
        /// Set the minimal priority.
        /// </summary>
        Minimal,

        /// <summary>
        /// Increase priority.
        /// </summary>
        Increase,

        /// <summary>
        /// Decrease priority.
        /// </summary>
        Decrease,

        /// <summary>
        /// Set the maximal priority.
        /// </summary>
        Maximal
    }
}
