using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace QBittorrent.Client
{
    /// <summary>
    /// Represents an RSS article.
    /// </summary>
    public class RssArticle
    {
        /// <summary>
        /// The article id.
        /// </summary>
        [JsonProperty("id")]
        public string Id { get; set; }

        /// <summary>
        /// The article date/time.
        /// </summary>
        [JsonProperty("date")]
        public DateTimeOffset Date { get; set; }

        /// <summary>
        /// The title.
        /// </summary>
        [JsonProperty("title")]
        public string Title { get; set; }

        /// <summary>
        /// The author.
        /// </summary>
        [JsonProperty("author")]
        public string Author { get; set; }

        /// <summary>
        /// The description.
        /// </summary>
        [JsonProperty("description")]
        public string Description { get; set; }

        /// <summary>
        /// The torrent URI.
        /// </summary>
        [JsonProperty("torrentURL")]
        public Uri TorrentUri { get; set; }

        /// <summary>
        /// The torrent link.
        /// </summary>
        [JsonProperty("link")]
        public Uri Link { get; set; }

        /// <summary>
        /// The value indicating whether the article has been read.
        /// </summary>
        [JsonProperty("isRead")]
        public bool IsRead { get; set; }

        /// <summary>
        /// Additional properties not handled by this library.
        /// </summary>
        [JsonExtensionData]
        public IDictionary<string, JToken> AdditionalData { get; set; }

        [OnError]
        internal void OnError(StreamingContext context, ErrorContext errorContext)
        {
            var member = errorContext.Member as string;
            if (member == "torrentURL" || member == "link")
            {
                errorContext.Handled = true;
            }
        }
    }
}
