namespace QBittorrent.Client
{
    /// <summary>
    /// Torrent piece state.
    /// </summary>
    public enum TorrentPieceState
    {
        /// <summary>
        /// The download has not been started yet.
        /// </summary>
        NotDownloaded = 0,
        
        /// <summary>
        /// The piece is being downloaded.
        /// </summary>
        Downloading = 1,

        /// <summary>
        /// The piece has been downloaded.
        /// </summary>
        Downloaded = 2
    }
}
