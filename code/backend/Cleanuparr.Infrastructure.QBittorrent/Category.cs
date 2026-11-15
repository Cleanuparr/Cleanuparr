using System.Collections.Generic;
using System.Runtime.Serialization;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace QBittorrent.Client
{
    /// <summary>
    /// Describes torrent category and the corresponding save path.
    /// </summary>
    public class Category
    {
        /// <summary>
        /// Category name.
        /// </summary>
        [JsonProperty("name")]
        public string Name { get; set; }

        /// <summary>
        /// Category save path.
        /// </summary>
        [JsonProperty("savePath")]
        public string SavePath { get; set; }

        /// <summary>
        /// Additional properties not handled by this library.
        /// </summary>
        [JsonExtensionData]
        public IDictionary<string, JToken> AdditionalData { get; set; }

        [OnDeserialized]
        [UsedImplicitly]
        private void OnDeserialized(StreamingContext context)
        {
            if (AdditionalData != null && AdditionalData.TryGetValue("save_path", out JToken savePath) && savePath.Type == JTokenType.String)
            {
                SavePath = savePath.ToObject<string>();
                AdditionalData = null;
            }
        }
    }
}
