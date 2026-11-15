using System;
using System.Collections.Generic;
using System.Text;

namespace Transmission.API.RPC.Arguments
{
    /// <summary>
    /// Which protocol was used for the port test
    /// </summary>
    public enum PortTestProtocol
    {
        /// <summary>
        /// IPv4
        /// </summary>
        IPv4 = 0,
        /// <summary>
        /// IPv6
        /// </summary>
        IPV6,
        /// <summary>
        /// Could not be determined
        /// </summary>
        Unknown,
    }
}
