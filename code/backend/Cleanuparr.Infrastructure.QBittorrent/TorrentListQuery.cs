using System.Collections;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace QBittorrent.Client
{
    /// <summary>
    /// Encapsulates the query parameters to get the list of torrents.
    /// </summary>
    public class TorrentListQuery
    {
        /// <summary>
        /// Gets or sets the filter.
        /// </summary>
        public TorrentListFilter Filter { get; set; } = TorrentListFilter.All;

        /// <summary>
        /// Gets or sets the category to filter by.
        /// </summary>
        public string Category { get; set; }

        /// <summary>
        /// Gets or sets the field to sort by.
        /// </summary>
        public string SortBy { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether sorting must be performed in the descending order.
        /// </summary>
        public bool ReverseSort { get; set; }

        /// <summary>
        /// Gets or sets the maximal number of torrents to return.
        /// </summary>
        /// <seealso cref="Offset"/>
        public int? Limit { get; set; }

        /// <summary>
        /// Gets or sets the offset from the beginning of the torrent list.
        /// </summary>
        /// <seealso cref="Limit" />
        public int? Offset { get; set; }

        /// <summary>
        /// Gets or sets the hashes of the torrents to display.
        /// </summary>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.0.1")]
        public IEnumerable<string> Hashes { get; set; }

        /// <summary>
        /// Get torrents with the given tag (empty string means "without tag"; no "tag" parameter means "any tag".
        /// Remember to URL-encode the category name. For example, <c>My tag</c> becomes <c>My%20tag</c>.
        /// </summary>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.8.3")]
        [CanBeNull]
        public string Tag { get; set; }
    }
}
