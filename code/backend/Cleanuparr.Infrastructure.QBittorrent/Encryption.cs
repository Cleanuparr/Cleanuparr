using System;
using System.Collections.Generic;
using System.Text;

namespace QBittorrent.Client
{
    /// <summary>
    /// Torrent transfer encryption preferences.
    /// </summary>
    public enum Encryption
    {
        /// <summary>
        /// Prefer encrypted connections, but allow using unencrypted connections too.
        /// </summary>
        Prefer = 0,

        /// <summary>
        /// Force encrypted connections.
        /// </summary>
        ForceOn = 1,

        /// <summary>
        /// Force unencrypted connections.
        /// </summary>
        ForceOff = 2
    }
}
