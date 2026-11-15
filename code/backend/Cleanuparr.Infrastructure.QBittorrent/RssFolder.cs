using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace QBittorrent.Client
{
    /// <summary>
    /// Represents an RSS folder.
    /// </summary>
    /// <seealso cref="QBittorrent.Client.RssItem" />
    [DebuggerDisplay("Folder {Name} with {Items.Count} items")]
    public class RssFolder : RssItem
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RssFolder"/> class.
        /// </summary>
        public RssFolder() : this(null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RssFolder"/> class.
        /// </summary>
        /// <param name="items">The folder items.</param>
        public RssFolder(IEnumerable<RssItem> items) : this(string.Empty, items)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="RssFolder"/> class.</summary>
        /// <param name="name">The folder name.</param>
        /// <param name="items">The folder items.</param>
        public RssFolder(string name, IEnumerable<RssItem> items)
        {
            Name = name;
            Items = new List<RssItem>(items ?? Enumerable.Empty<RssItem>());
        }

        /// <summary>
        /// Gets all folder items (feeds and folders).
        /// </summary>
        /// <seealso cref="Feeds"/>
        /// <seealso cref="Folders"/>
        public IList<RssItem> Items { get; }

        /// <summary>
        /// Gets all subfolders.
        /// </summary>
        /// <seealso cref="Items"/>
        /// <seealso cref="Feeds"/>
        public IEnumerable<RssFolder> Folders => Items?.OfType<RssFolder>();

        /// <summary>
        /// Gets all feeds in the folder.
        /// </summary>
        /// <seealso cref="Items"/>
        /// <seealso cref="Folders"/>
        public IEnumerable<RssFeed> Feeds => Items?.OfType<RssFeed>();
    }
}
