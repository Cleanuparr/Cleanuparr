namespace QBittorrent.Client
{
    /// <summary>
    /// Defines the layout of torrent content.
    /// </summary>
    public enum TorrentContentLayout
    {
        /// <summary>
        /// Original layout
        /// </summary>
        Original,

        /// <summary>
        /// Create subfolder
        /// </summary>
        Subfolder,

        /// <summary>
        /// Do not create subfolder
        /// </summary>
        NoSubfolder
    }
}
