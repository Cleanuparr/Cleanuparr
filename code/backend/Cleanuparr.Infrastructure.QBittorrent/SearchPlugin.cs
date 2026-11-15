using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace QBittorrent.Client
{
    /// <summary>
    /// Represents information about a search plugin.
    /// </summary>
    public class SearchPlugin
    {
        /// <summary>
        /// Name that can be used to perform search using all plugins.
        /// </summary>
        public const string All = "all";

        /// <summary>
        /// Name that can be used to perform search using all enabled plugins.
        /// </summary>
        public const string Enabled = "enabled";

        /// <summary>
        /// Whether the plugin is enabled.
        /// </summary>
        [JsonProperty("enabled")]
        public bool IsEnabled { get; set; }

        /// <summary>
        /// Full name of the plugin
        /// </summary>
        [JsonProperty("fullName")]
        public string FullName { get; set; }

        /// <summary>
        /// Short name of the plugin
        /// </summary>
        [JsonProperty("name")]
        public string Name { get; set; }

        /// <summary>
        /// List of supported categories
        /// </summary>
        [Obsolete("Use " + nameof(Categories) + " property instead.")]
        [JsonIgnore]
        public IReadOnlyList<string> SupportedCategories { get; set; }

        /// <summary>
        /// List of supported categories
        /// </summary>
        [JsonIgnore]
        public IReadOnlyList<SearchPluginCategory> Categories { get; set; }

        /// <summary>
        /// URL of the torrent site
        /// </summary>
        [JsonProperty("url")]
        public Uri Url { get; set; }

        /// <summary>
        /// Installed version of the plugin
        /// </summary>
        [JsonProperty("version")]
        public Version Version { get; set; }

        /// <summary>
        /// Additional properties not handled by this library.
        /// </summary>
        [JsonExtensionData]
        public IDictionary<string, JToken> AdditionalData { get; set; }

#pragma warning disable 618
        [OnDeserialized]
        [UsedImplicitly]
        private void OnDeserialized(StreamingContext context)
        {
            const string supportedCategoriesKey = "supportedCategories";
            if (AdditionalData != null &&
                AdditionalData.TryGetValue(supportedCategoriesKey, out var token) &&
                token is JArray { Count: > 0 } array )
            {
                if (array.First.Type == JTokenType.String)
                {
                    SupportedCategories = array.ToObject<List<string>>();
                    Categories = SupportedCategories.Select(c => new SearchPluginCategory(c)).ToArray();
                }
                else if (array.First.Type == JTokenType.Object)
                {
                    Categories = array.ToObject<List<SearchPluginCategory>>();
                    SupportedCategories = Categories.Select(c => c.Id).ToArray();
                }
            }

            SupportedCategories ??= Array.Empty<string>();
            Categories ??= Array.Empty<SearchPluginCategory>();
        }
#pragma warning restore 618
    }
}
