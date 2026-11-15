using System;
using System.Collections.Generic;
using System.Text;

namespace QBittorrent.Client
{
    /// <summary>
    /// qBittorrent API level.
    /// </summary>
    public enum ApiLevel
    {
        /// <summary>
        /// The API level is detected automatically.
        /// </summary>
        Auto = 0,

        /// <summary>
        /// The API Level 1. Supported by qBittorrent v3.2.0-v4.0.4.
        /// </summary>
        V1 = 1_000_000,

        /// <summary>
        /// The API Level 2. Supported by qBittorrent v4.1.0 or later.
        /// </summary>
        V2 = 2_000_000
    }
}
