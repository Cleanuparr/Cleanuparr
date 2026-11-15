using System;

namespace QBittorrent.Client
{
    /// <summary>
    /// The QBittorrent log entry severity.
    /// </summary>
    [Flags]
    public enum TorrentLogSeverity
    {
        /// <summary>
        /// The normal severity
        /// </summary>
        Normal = 1,

        /// <summary>
        /// The information severity
        /// </summary>
        Info = 2,

        /// <summary>
        /// The warning severity
        /// </summary>
        Warning = 4,

        /// <summary>
        /// The critical error severity
        /// </summary>
        Critical = 8,

        /// <summary>
        /// All severities. For use in filters only.
        /// </summary>
        All = Normal | Info | Warning | Critical
    }
}
