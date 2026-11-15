namespace QBittorrent.Client
{
    /// <summary>
    /// Defines the seeding unchoke behavior.
    /// </summary>
    public enum SeedChokingAlgorithm
    {
        /// <summary>
        /// Rotates the peers that are unchoked when seeding.
        /// This distributes the upload bandwidth uniformly and fairly.
        /// It minimizes the ability for a peer to download everything without redistributing it.
        /// </summary>
        RoundRobin = 0,

        /// <summary>
        /// Unchokes the peers we can send to the fastest.
        /// This might be a bit more reliable in utilizing all available capacity.
        /// </summary>
        FastestUpload = 1,

        /// <summary>
        /// Prioritizes peers who have just started or are just about to finish the download.
        /// The intention is to force peers in the middle of the download to trade with each other.
        /// </summary>
        AntiLeech = 2
    }
}
