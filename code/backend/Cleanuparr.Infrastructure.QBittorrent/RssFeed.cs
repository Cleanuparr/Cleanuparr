using System;
using System.Collections.Generic;
using System.Diagnostics;
using Newtonsoft.Json;

namespace QBittorrent.Client
{
    /// <summary>
    /// Represents an RSS Feed.
    /// </summary>
    /// <seealso cref="QBittorrent.Client.RssItem" />
    [DebuggerDisplay("Feed {Name} : {Url}")]
    public class RssFeed : RssItem
    {
        /// <summary>
        /// Gets or sets the uid.
        /// </summary>
        /// <value>
        /// The uid.
        /// </value>
        [JsonProperty("uid")]
        public Guid Uid { get; set; }

        /// <summary>
        /// Gets or sets the feed URL.
        /// </summary>
        /// <value>
        /// The feed URL.
        /// </value>
        [JsonProperty("url")]
        public Uri Url { get; set; }

        /// <summary>
        /// Gets or sets the feed title.
        /// </summary>
        /// <value>
        /// The title.
        /// </value>
        [JsonProperty("title")]
        public string Title { get; set; }

        /// <summary>
        /// Gets or sets the last build date.
        /// </summary>
        /// <value>
        /// The last build date.
        /// </value>
        [JsonProperty("lastBuildDate")]
        public DateTimeOffset? LastBuildDate { get; set; }

        /// <summary>
        /// Gets or sets the value indicating whether the feed is loading.
        /// </summary>
        [JsonProperty("isLoading")]
        public bool? IsLoading { get; set; }

        /// <summary>
        /// Gets or sets the value indicating whether the feed has an error.
        /// </summary>
        [JsonProperty("hasError")]
        public bool? HasError { get; set; }

        /// <summary>
        /// Gets or sets the articles.
        /// </summary>
        /// <value>
        /// The articles.
        /// </value>
        [JsonProperty("articles")]
        public IList<RssArticle> Articles { get; set; }
    }
}
