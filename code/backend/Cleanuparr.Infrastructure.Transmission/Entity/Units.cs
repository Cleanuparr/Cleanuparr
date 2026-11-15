using System.Text.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Transmission.API.RPC.Entity
{
    /// <summary>
    /// Units
    /// </summary>
    public class Units
    {
        /// <summary>
        /// Speed units
        /// </summary>
        [JsonPropertyName("speed-units")]
        public string[]? SpeedUnits { get; set; }

        /// <summary>
        /// Speed bytes
        /// </summary>
        [JsonPropertyName("speed-bytes")]
        public long? SpeedBytes { get; set; }

        /// <summary>
        /// Size units
        /// </summary>
        [JsonPropertyName("size-units")]
        public string[]? SizeUnits { get; set; }

        /// <summary>
        /// Size bytes
        /// </summary>
        [JsonPropertyName("size-bytes")]
        public long? SizeBytes { get; set; }

        /// <summary>
        /// Memory units
        /// </summary>
        [JsonPropertyName("memory-units")]
        public string[]? MemoryUnits { get; set; }

        /// <summary>
        /// Memory bytes
        /// </summary>
        [JsonPropertyName("memory-bytes")]
        public long? MemoryBytes { get; set; }
    }
}
