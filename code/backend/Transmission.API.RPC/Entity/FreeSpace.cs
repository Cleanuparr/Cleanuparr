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
    /// FreeSpace
    /// </summary>
    public class FreeSpace
    {
        /// <summary>
        /// Path of the queried directory
        /// </summary>
        [JsonPropertyName("path")]
        public string? Path { get; set; }

        /// <summary>
        /// The size, in bytes, of the free space in that directory
        /// </summary>
        [JsonPropertyName("size-bytes")]
        public long? SizeBytes { get; set; }

        /// <summary>
        /// the total capacity, in bytes, of that directory
        /// </summary>
        [JsonPropertyName("total_size")]
        public long? TotalSize { get; set; }
    }
}
