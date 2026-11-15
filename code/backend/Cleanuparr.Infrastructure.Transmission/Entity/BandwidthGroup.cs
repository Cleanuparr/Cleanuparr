using System.Text.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Text;

namespace Transmission.API.RPC.Entity
{
    /// <summary>
    /// Contains settings for a bandwidth group
    /// </summary>
    public class BandwidthGroup
    {
        /// <summary>
        /// Session limits are honored
        /// </summary>
        [JsonPropertyName("honorsSessionLimits")]
        public bool? HonorsSessionLimits { get; set; }

        /// <summary>
        /// Name of the bandwidth group
        /// </summary>
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        /// <summary>
        /// Max global download speed of this bandwidth group (KBps)
        /// </summary>
        [JsonPropertyName("speed-limit-down")]
        public long? SpeedLimitDown { get; set; }

        /// <summary>
        /// True means enabled
        /// </summary>
        [JsonPropertyName("speed-limit-down-enabled")]
        public bool? SpeedLimitDownEnabled { get; set; }

        /// <summary>
        /// Max global upload speed of this bandwidth group (KBps)
        /// </summary>
        [JsonPropertyName("speed-limit-up")]
        public long? SpeedLimitUp { get; set; }

        /// <summary>
        /// True means enabled
        /// </summary>
        [JsonPropertyName("speed-limit-up-enabled")]
        public bool? SpeedLimitUpEnabled { get; set; }
    }
}
