using System;
using System.Collections.Generic;

namespace QBittorrent.Client
{
    /// <summary>
    /// The base class for add torrent requests.
    /// </summary>
    public abstract class AddTorrentRequestBase
    {
        /// <summary>
        /// Download folder
        /// </summary>
        public string DownloadFolder { get; set; }

        /// <summary>
        /// Cookie sent to download the .torrent file
        /// </summary>
        public string Cookie { get; set; }

        /// <summary>
        /// Category for the torrent
        /// </summary>
        public string Category { get; set; }

        /// <summary>
        /// Skip hash checking.
        /// </summary>
        public bool SkipHashChecking { get; set; }

        /// <summary>
        /// Add torrents in the paused state.
        /// </summary>
        public bool Paused { get; set; }

        /// <summary>
        /// Create the root folder.
        /// </summary>
        /// <remarks>This value is ignored starting from API v2.7.0. Use <see cref="ContentLayout"/> for the API v2.7.0 and newer.</remarks>
        /// <seealso cref="ContentLayout"/>
        [ApiLevel(ApiLevel.V1)]
        [Deprecated("2.7.0")]
        public bool? CreateRootFolder { get; set; }

        /// <summary>
        /// Rename torrent
        /// </summary>
        public string Rename { get; set; }

        /// <summary>
        /// Set torrent upload speed limit
        /// </summary>
        public int? UploadLimit { get; set; }

        /// <summary>
        /// Set torrent download speed limit
        /// </summary>
        public int? DownloadLimit { get; set; }

        /// <summary>
        /// Enable sequential download
        /// </summary>
        public bool SequentialDownload { get; set; }

        /// <summary>
        /// Prioritize download of first and last pieces
        /// </summary>
        public bool FirstLastPiecePrioritized { get; set; }

        /// <summary>
        /// Enable/disable automatic torrent management for these torrents
        /// </summary>
        /// <remarks>This value is ignored until API v2.2.0</remarks>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.2.0")]
        public bool? AutomaticTorrentManagement { get; set; }

        /// <summary>
        /// Tags for the torrent
        /// </summary>
        /// <remarks>This value is ignored until API v2.6.2</remarks>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.6.2")]
        public IEnumerable<string> Tags { get; set; }

        /// <summary>
        /// Torrent content layout.
        /// </summary>
        /// <remarks>This value is ignored until API v2.7.0. Use <see cref="CreateRootFolder"/> for the previous versions.</remarks>
        /// <seealso cref="CreateRootFolder"/>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.7.0")]
        public TorrentContentLayout? ContentLayout { get; set; }

        /// <summary>
        /// Set torrent share ratio limit
        /// </summary>
        /// <remarks>This value is ignored until API v2.8.1</remarks>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.8.1")]
        public double? RatioLimit { get; set; }

        /// <summary>
        /// Set torrent seeding time limit.
        /// </summary>
        /// <remarks>This value is ignored until API v2.8.1</remarks>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.8.1")]
        public TimeSpan? SeedingTimeLimit { get; set; }
    }
}
