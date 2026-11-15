using System;
using System.Collections.Generic;
using System.Text;
using Transmission.API.RPC.Common;

namespace Transmission.API.RPC.Arguments
{
    /// <summary>
    /// Bandwidth group settings for editing bandwidth groups
    /// </summary>
    public class BandwidthGroupSettings : ArgumentsBase
    {
        /// <summary>
        /// Session limits are honored
        /// </summary>
        public bool HonorsSessionLimits { get { return GetValue<bool>("honorsSessionLimits"); } set { this["honorsSessionLimits"] = value; } }

        /// <summary>
        /// Name of the bandwidth group
        /// </summary>
        public string Name { get { return GetValue<string>("name"); } set { this["name"] = value; } }

        /// <summary>
        /// Max global download speed of this bandwidth group (KBps)
        /// </summary>
        public long SpeedLimitDown { get { return GetValue<long>("speed-limit-down"); } set { this["speed-limit-down"] = value; } }

        /// <summary>
        /// True means enabled
        /// </summary>
        public bool SpeedLimitDownEnabled { get { return GetValue<bool>("speed-limit-down-enabled"); } set { this["speed-limit-down-enabled"] = value; } }

        /// <summary>
        /// Max global upload speed of this bandwidth group (KBps)
        /// </summary>
        public long SpeedLimitUp { get { return GetValue<long>("speed-limit-up"); } set { this["speed-limit-up"] = value; } }

        /// <summary>
        /// True means enabled
        /// </summary>
        public bool SpeedLimitUpEnabled { get { return GetValue<bool>("speed-limit-up-enabled"); } set { this["speed-limit-up-enabled"] = value; } }
    }
}
