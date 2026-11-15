using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace QBittorrent.Client
{
    /// <summary>
    /// Represents full or partial data returned by <see cref="QBittorrentClient.GetPartialDataAsync"/> method.
    /// </summary>
    public class PartialData
    {
        /// <summary>
        /// Gets or sets the response identifier.
        /// </summary>
        /// <value>
        /// The response identifier.
        /// </value>
        [JsonProperty("rid")]
        public int ResponseId { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this object contains all data.
        /// </summary>
        /// <value>
        ///   <see langword="true" /> if this object contains all data; 
        ///   <see langword="false" /> if this object contains only changes of data since the previous request.
        /// </value>
        [JsonProperty("full_update")]
        public bool FullUpdate { get; set; }

        /// <summary>
        /// Gets or sets the list of changed or added torrents.
        /// </summary>
        [JsonProperty("torrents")]
        public IReadOnlyDictionary<string, TorrentPartialInfo> TorrentsChanged { get; set; }

        /// <summary>
        /// Gets or sets the list of removed torrents.
        /// </summary>
        [JsonProperty("torrents_removed")]
        public IReadOnlyList<string> TorrentsRemoved { get; set; }

        /// <summary>
        /// Gets or sets the list of added categories.
        /// </summary>
        /// <remarks>
        /// Starting from API v2.1.0 this property will always be <see langword="null"/>.
        /// You should use <see cref="CategoriesChanged"/> property instead.
        /// </remarks>
        /// <seealso cref="CategoriesChanged"/>
        [Obsolete("Use CategoriesChanged property instead.")]
        [JsonIgnore]
        public IReadOnlyList<string> CategoriesAdded { get; set; }

        /// <summary>
        /// Gets or sets the changed categories with their save paths.
        /// </summary>
        [JsonIgnore]
        public IReadOnlyDictionary<string, Category> CategoriesChanged { get; set; }

        /// <summary>
        /// Gets or sets the list of removed categories.
        /// </summary>
        [JsonProperty("categories_removed")]
        public IReadOnlyList<string> CategoriesRemoved { get; set; }
        
        /// <summary>
        /// Get or sets the list of added tags.
        /// </summary>
        [JsonProperty("tags")]
        public IReadOnlyList<string> TagsAdded { get; set; }

        /// <summary>
        /// Get or sets the list of removed tags.
        /// </summary>
        [JsonProperty("tags_removed")]
        public IReadOnlyList<string> TagsRemoved { get; set; }

        /// <summary>
        /// Priority system usage flag
        /// </summary>
        [JsonProperty("queueing")]
        public bool Queueing { get; set; }

        /// <summary>
        /// Gets or sets the state of the server.
        /// </summary>
        [JsonProperty("server_state")]
        public GlobalTransferExtendedInfo ServerState { get; set; }

        /// <summary>
        /// Additional properties not handled by this library.
        /// </summary>
        [JsonExtensionData]
        public IDictionary<string, JToken> AdditionalData { get; set; }

        [OnDeserialized]
        [UsedImplicitly]
        private void OnDeserialized(StreamingContext context)
        {
            const string categoriesKey = "categories";
            if (AdditionalData != null &&
                AdditionalData.TryGetValue(categoriesKey, out JToken categories))
            {
#pragma warning disable 618
                if (categories.Type == JTokenType.Array)
                {
                    CategoriesAdded = categories.ToObject<List<string>>();
                    CategoriesChanged = CategoriesAdded.ToDictionary(
                        name => name, 
                        name => new Category {Name = name, SavePath = string.Empty});
                    AdditionalData.Remove(categoriesKey);
                }
                else if (categories.Type == JTokenType.Object)
                {
                    CategoriesChanged = categories.ToObject<Dictionary<string, Category>>();
                    if (FullUpdate)
                    {
                        CategoriesAdded = CategoriesChanged.Keys.ToList();
                    }
                    AdditionalData.Remove(categoriesKey);
                }
#pragma warning restore 618
            }
        }
    }
}
