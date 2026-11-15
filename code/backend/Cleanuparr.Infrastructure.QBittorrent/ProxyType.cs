using System;
using System.Collections.Generic;
using System.Text;

namespace QBittorrent.Client
{
    /// <summary>
    /// Proxy server type.
    /// </summary>
    public enum ProxyType
    {
        /// <summary>
        /// Proxy is not used.
        /// </summary>
        None = -1,

        /// <summary>
        /// HTTP proxy without authentication
        /// </summary>
        Http = 1,

        /// <summary>
        /// SOCKS5 proxy without authentication
        /// </summary>
        Socks5 = 2,

        /// <summary>
        /// HTTP proxy with authentication
        /// </summary>
        HttpAuth = 3,

        /// <summary>
        /// SOCKS5 proxy with authentication
        /// </summary>
        Socks5Auth = 4,

        /// <summary>
        /// SOCKS4 proxy without authentication
        /// </summary>
        Socks4 = 5 
    }
}
