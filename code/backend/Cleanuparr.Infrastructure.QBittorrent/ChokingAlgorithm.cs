namespace QBittorrent.Client
{
    /// <summary>
    /// Defines the algorithm to use to determine which peers to unchoke.
    /// </summary>
    public enum ChokingAlgorithm
    {
        /// <summary>
        /// The traditional choker with a fixed number of unchoke slots.
        /// </summary>
        FixedSlots = 0,

        /// <summary>
        /// Opens up unchoke slots based on the upload rate achieved to peers.
        /// The more slots that are opened, the marginal upload rate required to open up another slot increases.
        /// </summary>
        RateBased = 1
    }
}
