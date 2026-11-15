using System;
using System.Collections.Generic;
using System.Text;

namespace QBittorrent.Client
{
    /// <summary>
    /// Specificies when .torrent file must be deleted
    /// </summary>
    public enum TorrentFileAutoDeleteMode
    {
        /// <summary>
        /// Never
        /// </summary>
        Never = 0,

        /// <summary>
        /// After the torrent has been added
        /// </summary>
        IfAdded = 1,

        /// <summary>
        /// Always
        /// </summary>
        Always = 2
    }
}
