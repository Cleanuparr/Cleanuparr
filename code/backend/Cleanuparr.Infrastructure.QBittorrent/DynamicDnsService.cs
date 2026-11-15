using System.Diagnostics.CodeAnalysis;

namespace QBittorrent.Client
{
    /// <summary>
    /// Dynamic DNS Service
    /// </summary>
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public enum DynamicDnsService
    {
        /// <summary>
        /// None
        /// </summary>
        None = -1,

        /// <summary>
        /// DynDSN service <see href="https://dyn.com/dns/"/>
        /// </summary>
        DynDNS = 0,

        /// <summary>
        /// No-IP service <see href="https://www.noip.com"/>
        /// </summary>
        NoIP = 1
    }
}
