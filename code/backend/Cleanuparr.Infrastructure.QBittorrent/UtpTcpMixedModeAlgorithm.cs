namespace QBittorrent.Client
{
    /// <summary>
    /// Determines how to treat TCP connections when there are uTP connections.
    /// </summary>
    public enum UtpTcpMixedModeAlgorithm
    {
        /// <summary>
        /// Prefer TCP connections.
        /// </summary>
        PreferTcp = 0,

        /// <summary>
        /// Rate limit all TCP connections to their proportional share based on how many of the connections are TCP.
        /// </summary>
        PeerProportional = 1
    }
}
