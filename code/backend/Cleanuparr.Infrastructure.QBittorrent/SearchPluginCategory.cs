using Newtonsoft.Json;

namespace QBittorrent.Client
{
    /// <summary>
    /// Search plugin category.
    /// </summary>
    public class SearchPluginCategory
    {
        /// <summary>
        /// Initializes a new instance of <see cref="SearchPluginCategory"/>.
        /// </summary>
        public SearchPluginCategory()
        {
        }

        /// <summary>
        /// Initializes a new instance of <see cref="SearchPluginCategory"/>.
        /// </summary>
        /// <param name="idAndName">The ID and name of search plugin category.</param>
        public SearchPluginCategory(string idAndName) : this(idAndName, idAndName)
        {
        }

        /// <summary>
        /// Initializes a new instance of <see cref="SearchPluginCategory"/>.
        /// </summary>
        /// <param name="id">The ID of search plugin category.</param>
        /// <param name="name">The name of search plugin category.</param>
        public SearchPluginCategory(string id, string name)
        {
            Id = id;
            Name = name;
        }

        /// <summary>
        /// Gets or sets the ID of search plugin category.
        /// </summary>
        [JsonProperty("id")]
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets the name of search plugin category.
        /// </summary>
        [JsonProperty("name")]
        public string Name { get; set; }
    }
}
