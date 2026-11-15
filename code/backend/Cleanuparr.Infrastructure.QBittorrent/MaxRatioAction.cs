using System;
using System.Collections.Generic;
using System.Text;

namespace QBittorrent.Client
{
    /// <summary>
    /// The action that is performed when <see cref="Preferences.MaxRatio"/> is reached.
    /// </summary>
    public enum MaxRatioAction
    {
        /// <summary>
        /// Pause torrent.
        /// </summary>
        Pause = 0,

        /// <summary>
        /// Remove torrent.
        /// </summary>
        Remove = 1
    }
}
