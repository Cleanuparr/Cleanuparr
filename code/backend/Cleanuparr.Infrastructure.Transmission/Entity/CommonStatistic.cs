#nullable enable
using System.Text.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Transmission.API.RPC.Entity
{
    /// <summary>
    /// Common statistic
    /// </summary>
    public class CommonStatistic
    {
        /// <summary>
        /// Uploaded bytes
        /// </summary>
        [JsonPropertyName("uploadedBytes")]
        public long? UploadedBytes{ get; set; }
        
        /// <summary>
        /// Downloaded bytes
        /// </summary>
        [JsonPropertyName("downloadedBytes")]
        public long? DownloadedBytes{ get; set; }

        /// <summary>
        /// Files added
        /// </summary>
        [JsonPropertyName("filesAdded")]
        public long? FilesAdded{ get; set; }

        /// <summary>
        /// Session count
        /// </summary>
        [JsonPropertyName("SessionCount")]
        public long? SessionCount{ get; set; }

        /// <summary>
        /// Seconds active
        /// </summary>
        [JsonPropertyName("SecondsActive")]
        public long? SecondsActive{ get; set; }
    }
}
