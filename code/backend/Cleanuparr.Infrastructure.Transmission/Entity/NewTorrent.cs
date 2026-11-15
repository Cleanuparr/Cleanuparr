using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Transmission.API.RPC.Common;

namespace Transmission.API.RPC.Entity
{
    /// <summary>
    /// Information about the torrent file, that will be added
    /// </summary>
    public class NewTorrent : ArgumentsBase
    {
        /// <summary>
        /// Pointer to a string of one or more cookies.
        /// </summary>
        public string? Cookies { get { return GetValue<string>("cookies"); } set { this["cookies"] = value; } }

        /// <summary>
        /// Path to download the torrent to
        /// </summary>
        public string? DownloadDirectory { get { return GetValue<string>("download-dir"); } set { this["download-dir"] = value; } }

        /// <summary>
		/// filename (relative to the server) or URL of the .torrent file (Priority than the metadata)
        /// </summary>
        public string? Filename { get { return GetValue<string>("filename"); } set { this["filename"] = value; } }

        /// <summary>
        /// Array of labels applied to the torrent
        /// </summary>
        public string[]? Labels { get { return GetValue<string[]>("labels"); } set { this["labels"] = value; } }

        /// <summary>
        /// base64-encoded .torrent content
        /// </summary>
        public string? Metainfo { get { return GetValue<string>("metainfo"); } set { this["metainfo"] = value; } }

        /// <summary>
        /// if true, don't start the torrent
        /// </summary>
        public bool Paused { get { return GetValue<bool>("paused"); } set { this["paused"] = value; } }

        /// <summary>
        /// maximum number of peers
        /// </summary>
        public long PeerLimit { get { return GetValue<long>("peer-limit"); } set { this["peer-limit"] = value; } }

        /// <summary>
        /// Torrent's bandwidth priority
        /// </summary>
        public long BandwidthPriority { get { return GetValue<long>("bandwidthPriority"); } set { this["bandwidthPriority"] = value; } }

        /// <summary>
        /// Indices of file(s) to download
        /// </summary>
        public long[]? FilesWanted { get { return GetValue<long[]>("files-wanted"); } set { this["files-wanted"] = value; } }

        /// <summary>
        /// Indices of file(s) to download
        /// </summary>
        public long[]? FilesUnwanted { get { return GetValue<long[]>("files-unwanted"); } set { this["files-unwanted"] = value; } }

        /// <summary>
        /// Indices of high-priority file(s)
        /// </summary>
        public long[]? PriorityHigh { get { return GetValue<long[]>("priority-high"); } set { this["priority-high"] = value; } }

        /// <summary>
        /// Indices of low-priority file(s)
        /// </summary>
        public long[]? PriorityLow { get { return GetValue<long[]>("priority-low"); } set { this["priority-low"] = value; } }

        /// <summary>
        /// Indices of normal-priority file(s)
        /// </summary>
        public long[]? PriorityNormal { get { return GetValue<long[]>("priority-normal"); } set { this["priority-normal"] = value; } }
    }
}
