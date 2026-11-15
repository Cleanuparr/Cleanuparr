using System;
using System.Collections.Generic;
using System.Linq;

namespace QBittorrent.Client
{
    /// <summary>
    /// Request to add new torrents using torrent URLs.
    /// </summary>
    /// <seealso cref="AddTorrentRequestBase" />
    public class AddTorrentUrlsRequest : AddTorrentRequestBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AddTorrentUrlsRequest"/> class.
        /// </summary>
        public AddTorrentUrlsRequest() : this(Enumerable.Empty<Uri>())
        {
        }
        
        /// <summary>
        /// Initializes a new instance of the <see cref="AddTorrentUrlsRequest"/> class.
        /// </summary>
        /// <param name="url">The URL of the torrent to add.</param>
        public AddTorrentUrlsRequest(Uri url)
        {
            TorrentUrls = new List<Uri>(1) {url};
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AddTorrentUrlsRequest"/> class.
        /// </summary>
        /// <param name="urls">The URLs of the torrents to add.</param>
        public AddTorrentUrlsRequest(IEnumerable<Uri> urls)
        {
            TorrentUrls = new List<Uri>(urls);
        }

        /// <summary>
        /// The list of torrent URLs.
        /// </summary>
        public ICollection<Uri> TorrentUrls { get; }
    }
}
