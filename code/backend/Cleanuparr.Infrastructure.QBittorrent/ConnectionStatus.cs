using System.Runtime.Serialization;

namespace QBittorrent.Client
{
    /// <summary>
    /// The connection status.
    /// </summary>
    public enum ConnectionStatus
    {
        /// <summary>
        /// The connected status.
        /// </summary>
        [EnumMember(Value = "connected")]
        Connected,

        /// <summary>
        /// The firewalled status.
        /// </summary>
        [EnumMember(Value = "firewalled")]
        Firewalled,

        /// <summary>
        /// The disconnected status.
        /// </summary>
        [EnumMember(Value = "disconnected")]
        Disconnected
    }
}
