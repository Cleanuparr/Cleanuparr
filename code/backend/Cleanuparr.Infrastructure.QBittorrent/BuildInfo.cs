using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace QBittorrent.Client
{
    /// <summary>
    /// Describes qBittorrent build information.
    /// </summary>
    public class BuildInfo
    {
        /// <summary>
        /// The version of Qt.
        /// </summary>
        [JsonProperty("qt")]
        public string QtVersion { get; set; }
        
        /// <summary>
        /// The version of libtorrent.
        /// </summary>
        [JsonProperty("libtorrent")]
        public string LibtorrentVersion { get; set; }

        /// <summary>
        /// The version of Boost.
        /// </summary>
        [JsonProperty("boost")]
        public string BoostVersion { get; set; }

        /// <summary>
        /// The version of OpenSSL.
        /// </summary>
        [JsonProperty("openssl")]
        public string OpenSslVersion { get; set; }

        /// <summary>
        /// The version of zlib.
        /// </summary>
        [JsonProperty("zlib")]
        public string ZlibVersion { get; set; }

        /// <summary>
        /// QBittorrent bitness.
        /// </summary>
        [JsonProperty("bitness")]
        public int Bitness { get; set; }

        /// <summary>
        /// The platform qBittorrent is running on.
        /// </summary>
        [JsonProperty("platform")]
        public string Platform { get; set; }

        /// <summary>
        /// Additional properties not handled by this library.
        /// </summary>
        [JsonExtensionData]
        public IDictionary<string, JToken> AdditionalData { get; set; }
    }
}
