using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Transmission.API.RPC.Common;

namespace Transmission.API.RPC.Arguments
{
    /// <summary>
    /// Torrent settings
    /// </summary>
    public class TorrentSettings : ArgumentsBase
    {
        /// <summary>
        /// This torrent's bandwidth tr_priority_t
        /// </summary>
        public long BandwidthPriority { get { return GetValue<long>("bandwidthPriority"); } set { this["bandwidthPriority"] = value; } }

        /// <summary>
        /// Maximum download speed (KBps)
        /// </summary>
        public long DownloadLimit { get { return GetValue<long>("downloadLimit"); } set { this["downloadLimit"] = value; } }

        /// <summary>
        /// Download limit is honored
        /// </summary>
        public bool DownloadLimited { get { return GetValue<bool>("downloadLimited"); } set { this["downloadLimited"] = value; } }

        /// <summary>
        /// The name of this torrent's bandwidth group
        /// </summary>
        public string? Group { get { return GetValue<string>("group"); } set { this["group"] = value; } }

        /// <summary>
        /// Session upload limits are honored
        /// </summary>
        public bool HonorsSessionLimits { get { return GetValue<bool>("honorsSessionLimits"); } set { this["honorsSessionLimits"] = value; } }

        /// <summary>
        /// Torrent id array
        /// </summary>
        public object[]? Ids { get { return GetValue<object[]>("ids"); } set { this["ids"] = value; } }

        /// <summary>
        /// Array of strings containing the labels assigned to the torrent
        /// </summary>
        public string[]? Labels { get { return GetValue<string[]>("labels"); } set { this["labels"] = value; } }

        /// <summary>
        /// New location of the torrent's content
        /// </summary>
        public string? Location { get { return GetValue<string>("location"); } set { this["location"] = value; } }

        /// <summary>
        /// Maximum number of peers
        /// </summary>
        public long PeerLimit { get { return GetValue<long>("peer-limit"); } set { this["peer-limit"] = value; } }

        /// <summary>
        /// Position of this torrent in its queue [0...n)
        /// </summary>
        public long QueuePosition { get { return GetValue<long>("queuePosition"); } set { this["queuePosition"] = value; } }

        /// <summary>
        /// Torrent-level number of minutes of seeding inactivity
        /// </summary>
        public long SeedIdleLimit { get { return GetValue<long>("seedIdleLimit"); } set { this["seedIdleLimit"] = value; } }

        /// <summary>
        /// Which seeding inactivity mode to use. 0=Global 1=Single 2=Unlimited
        /// </summary>
        public long SeedIdleMode { get { return GetValue<long>("seedIdleMode"); } set { this["seedIdleMode"] = value; } }

        /// <summary>
        /// Torrent-level seeding ratio
        /// </summary>
        public double SeedRatioLimit { get { return GetValue<double>("seedRatioLimit"); } set { this["seedRatioLimit"] = value; } }

        /// <summary>
        /// Which ratio mode to use. 0=Global 1=Single 2=Unlimited
        /// </summary>
        public long SeedRatioMode { get { return GetValue<long>("seedRatioMode"); } set { this["seedRatioMode"] = value; } }

        /// <summary>
        /// Whether to download the torrent sequentially
        /// </summary>
        public bool SequentialDownload { get { return GetValue<bool>("sequentialDownload"); } set { this["sequentialDownload"] = value; } }

        /// <summary>
        /// Maximum upload speed (KBps)
        /// </summary>
        public long UploadLimit { get { return GetValue<long>("uploadLimit"); } set { this["uploadLimit"] = value; } }

        /// <summary>
        /// Upload limit is honored
        /// </summary>
        public bool UploadLimited { get { return GetValue<bool>("uploadLimited"); } set { this["uploadLimited"] = value; } }

        /// <summary>
        /// strings of announce URLs to add
        /// </summary>
        [Obsolete("TrackerAdd is obsolete since Transmission 4.0.0, use TrackerList instead.")]
		public string[]? TrackerAdd { get { return GetValue<string[]>("trackerAdd"); } set { this["trackerAdd"] = value; } }

        /// <summary>
        /// Ids of trackers to remove
        /// </summary>
        [Obsolete("TrackerRemove is obsolete since Transmission 4.0.0, use TrackerList instead.")]
		public long[]? TrackerRemove { get { return GetValue<long[]>("trackerRemove"); } set { this["trackerRemove"] = value; } }

        /// <summary>
        /// Pairs of IDs of announce URLs to replace along with their new value
        /// </summary>
        [Obsolete("TrackerReplace is obsolete since Transmission 4.0.0, use TrackerList instead.")]
        public KeyValuePair<int, string>[]? TrackerReplace
        {
            get
            {
                object[]? flat = GetValue<object[]>("trackerReplace");
                if (flat == null)
                {
                    return null;
                }

                KeyValuePair<int, string>[] pairs = new KeyValuePair<int, string>[flat.Length / 2];
                for (int i = 0; i < pairs.Length; i++)
                {
                    pairs[i] = new KeyValuePair<int, string>(Convert.ToInt32(flat[2 * i]), (string)flat[2 * i + 1]);
                }

                return pairs;
            }
            set
            {
                this["trackerReplace"] = value?.SelectMany(pair => new object[] { pair.Key, pair.Value }).ToArray();
            }
        }

        /// <summary>
        /// string of announce URLs, one per line, with a blank line between tiers
        /// </summary>
        public string? TrackerList { get { return GetValue<string>("trackerList"); } set { this["trackerList"] = value; } }

		/// <summary>
		/// Files wanted
		/// </summary>
		public long[]? FilesWanted { get { return GetValue<long[]>("files-wanted"); } set { this["files-wanted"] = value; } }

		/// <summary>
		/// Files unwanted
		/// </summary>
		public long[]? FilesUnwanted { get { return GetValue<long[]>("files-unwanted"); } set { this["files-unwanted"] = value; } }

		/// <summary>
		/// High priority files
		/// </summary>
		public long[]? PriorityHigh { get { return GetValue<long[]>("priority-high"); } set { this["priority-high"] = value; } }

		/// <summary>
		/// Low priority files
		/// </summary>
		public long[]? PriorityLow { get { return GetValue<long[]>("priority-low"); } set { this["priority-low"] = value; } }

		/// <summary>
		/// Normal priority files
		/// </summary>
		public long[]? PriorityNormal { get { return GetValue<long[]>("priority-normal"); } set { this["priority-normal"] = value; } }
	}
}
