using System;
using System.Collections.Generic;
using System.Text;

namespace QBittorrent.Client
{
    /// <summary>
    /// Describes torrent tracker status
    /// </summary>
    public enum TorrentTrackerStatus
    {
        /// <summary>
        /// Tracker is disabled (used for DHT, PeX, and LSD)
        /// </summary>
        Disabled = 0,

        /// <summary>
        /// Tracker has not been contacted yet
        /// </summary>
        NotContacted = 1,

        /// <summary>
        /// Tracker has been contacted and is working
        /// </summary>
        Working = 2,

        /// <summary>
        /// Tracker is currently being updated
        /// </summary>
        Updating = 3,

        /// <summary>
        /// Tracker has been contacted, but it is not working (or doesn't send proper replies)
        /// </summary>
        NotWorking = 4,
    }
}
