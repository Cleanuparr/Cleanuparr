using System;
using System.Collections.Generic;
using System.Linq;

namespace QBittorrent.Client
{
    /// <summary>
    /// Request to add new torrents using torrent files and/or URLs.
    /// </summary>
    [ApiLevel(ApiLevel.V2)]
    public class AddTorrentsRequest : AddTorrentRequestBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AddTorrentsRequest"/> class.
        /// </summary>
        public AddTorrentsRequest()
            : this(Enumerable.Empty<string>(), Enumerable.Empty<Uri>())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AddTorrentsRequest"/> class.
        /// </summary>
        /// <param name="torrentFiles">The torrent files' paths.</param>
        public AddTorrentsRequest(IEnumerable<string> torrentFiles)
            : this(torrentFiles, Enumerable.Empty<Uri>())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AddTorrentsRequest"/> class.
        /// </summary>
        /// <param name="torrentUrls">The URLs of the torrents to add.</param>
        public AddTorrentsRequest(IEnumerable<Uri> torrentUrls)
            : this(Enumerable.Empty<string>(), torrentUrls)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AddTorrentsRequest"/> class.
        /// </summary>
        /// <param name="torrentFiles">The torrent files' paths.</param>
        /// <param name="torrentUrls">The URLs of the torrents to add.</param>
        public AddTorrentsRequest(IEnumerable<string> torrentFiles, IEnumerable<Uri> torrentUrls)
        {
            TorrentFiles = new List<string>(torrentFiles);
            TorrentUrls = new List<Uri>(torrentUrls);
        }

        /// <summary>
        /// The list of torrent URLs.
        /// </summary>
        public ICollection<Uri> TorrentUrls { get; }

        /// <summary>
        /// The list of torrent files' paths to add.
        /// </summary>
        public ICollection<string> TorrentFiles { get; }
    }
}
