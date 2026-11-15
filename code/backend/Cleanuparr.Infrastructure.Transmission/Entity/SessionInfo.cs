#nullable enable
using System.Text.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using Transmission.API.RPC.Arguments;

namespace Transmission.API.RPC.Entity
{
    /// <summary>
    /// Session information
    /// </summary>
    public class SessionInfo
    {
        /// <summary>
        /// Max global download speed (KBps)
        /// </summary>
        [JsonPropertyName("alt-speed-down")]
        public long? AlternativeSpeedDown { get; set; }

        /// <summary>
        /// True means use the alt speeds
        /// </summary>
        [JsonPropertyName("alt-speed-enabled")]
        public bool? AlternativeSpeedEnabled { get; set; }

        /// <summary>
        /// When to turn on alt speeds (units: minutes after midnight)
        /// </summary>
        [JsonPropertyName("alt-speed-time-begin")]
        public long? AlternativeSpeedTimeBegin { get; set; }

        /// <summary>
        /// True means the scheduled on/off times are used
        /// </summary>
        [JsonPropertyName("alt-speed-time-enabled")]
        public bool? AlternativeSpeedTimeEnabled { get; set; }

        /// <summary>
        /// When to turn off alt speeds
        /// </summary>
        [JsonPropertyName("alt-speed-time-end")]
        public long? AlternativeSpeedTimeEnd { get; set; }

        /// <summary>
        /// What day(s) to turn on alt speeds
        /// </summary>
        [JsonPropertyName("alt-speed-time-day")]
        public long? AlternativeSpeedTimeDay { get; set; }

        /// <summary>
        /// Max global upload speed (KBps)
        /// </summary>
        [JsonPropertyName("alt-speed-up")]
        public long? AlternativeSpeedUp { get; set; }

        /// <summary>
        /// Location of the blocklist to use for "blocklist-update"
        /// </summary>
        [JsonPropertyName("blocklist-url")]
        public string? BlocklistUrl { get; set; }

        /// <summary>
        /// True means enabled
        /// </summary>
        [JsonPropertyName("blocklist-enabled")]
        public bool? BlocklistEnabled { get; set; }

        /// <summary>
        /// Number of rules in the blocklist
        /// </summary>
        [JsonPropertyName("blocklist-size")]
        public long? BlocklistSize { get; set; }

        /// <summary>
        /// Maximum size of the disk cache (MB)
        /// </summary>
        [JsonPropertyName("cache-size-mb")]
        public long? CacheSizeMb { get; set; }

        /// <summary>
        /// Default announce URLs, one per line, and a blank line between tiers
        /// </summary>
        [JsonPropertyName("default-trackers")]
        public string? DefaultTrackers { get; set; }

        /// <summary>
        /// Allow DHT in public torrents
        /// </summary>
        [JsonPropertyName("dht-enabled")]
        public bool? DhtEnabled { get; set; }

        /// <summary>
        /// Default path to download torrents
        /// </summary>
        [JsonPropertyName("download-dir")]
        public string? DownloadDirectory { get; set; }

        /// <summary>
        /// Free space in download dir
        /// </summary>
        [JsonPropertyName("download-dir-free-space")]
        [Obsolete("Obsolete since Transmission 4.0.0. Use the free-space method instead.")]
        public long? DownloadDirectoryFreeSpace { get; set; }

        /// <summary>
        /// Max number of torrents to download at once (see download-queue-enabled)
        /// </summary>
        [JsonPropertyName("download-queue-size")]
        public long? DownloadQueueSize { get; set; }

        /// <summary>
        /// If true, limit how many torrents can be downloaded at once
        /// </summary>
        [JsonPropertyName("download-queue-enabled")]
        public bool? DownloadQueueEnabled { get; set; }

        /// <summary>
        /// "required", "preferred", "tolerated"
        /// </summary>
        [JsonPropertyName("encryption")]
        public string? Encryption { get; set; }

        /// <summary>
        /// Torrents we're seeding will be stopped if they're idle for this long
        /// </summary>
        [JsonPropertyName("idle-seeding-limit")]
        public long? IdleSeedingLimit { get; set; }

        /// <summary>
        /// True if the seeding inactivity limit is honored by default
        /// </summary>
        [JsonPropertyName("idle-seeding-limit-enabled")]
        public bool? IdleSeedingLimitEnabled { get; set; }

        /// <summary>
        /// Path for incomplete torrents, when enabled
        /// </summary>
        [JsonPropertyName("incomplete-dir")]
        public string? IncompleteDirectory { get; set; }

        /// <summary>
        /// True means keep torrents in incomplete-dir until done
        /// </summary>
        [JsonPropertyName("incomplete-dir-enabled")]
        public bool? IncompleteDirectoryEnabled { get; set; }

        /// <summary>
        /// True means allow Local Peer Discovery in public torrents
        /// </summary>
        [JsonPropertyName("lpd-enabled")]
        public bool? LpdEnabled { get; set; }

        /// <summary>
        /// Maximum global number of peers
        /// </summary>
        [JsonPropertyName("peer-limit-global")]
        public long? PeerLimitGlobal { get; set; }

        /// <summary>
        /// Maximum global number of peers
        /// </summary>
        [JsonPropertyName("peer-limit-per-torrent")]
        public long? PeerLimitPerTorrent { get; set; }

        /// <summary>
        /// True means allow pex in public torrents
        /// </summary>
        [JsonPropertyName("pex-enabled")]
        public bool? PexEnabled { get; set; }

        /// <summary>
        /// Port number
        /// </summary>
        [JsonPropertyName("peer-port")]
        public long? PeerPort { get; set; }

        /// <summary>
        /// True means pick a random peer port on launch
        /// </summary>
        [JsonPropertyName("peer-port-random-on-start")]
        public bool? PeerPortRandomOnStart { get; set; }

        /// <summary>
        /// true means enabled
        /// </summary>
        [JsonPropertyName("port-forwarding-enabled")]
        public bool? PortForwardingEnabled { get; set; }

        /// <summary>
        /// Whether or not to consider idle torrents as stalled
        /// </summary>
        [JsonPropertyName("queue-stalled-enabled")]
        public bool? QueueStalledEnabled { get; set; }

        /// <summary>
        /// Torrents that are idle for N minuets aren't counted toward seed-queue-size or download-queue-size
        /// </summary>
        [JsonPropertyName("queue-stalled-minutes")]
        public long? QueueStalledMinutes { get; set; }

        /// <summary>
        /// True means append ".part" to incomplete files
        /// </summary>
        [JsonPropertyName("rename-partial-files")]
        public bool? RenamePartialFiles { get; set; }
        
        /// <summary>
        /// Session ID
        /// </summary>
        [JsonPropertyName("session-id")]
        public string? SessionId { get; set; }

        /// <summary>
        /// Filename of the script to run
        /// </summary>
        [JsonPropertyName("script-torrent-added-filename")]
        public string? ScriptTorrentAddedFilename { get; set; }

        /// <summary>
        /// Whether or not to call the "added" script
        /// </summary>
        [JsonPropertyName("script-torrent-added-enabled")]
        public bool? ScriptTorrentAddedEnabled { get; set; }

        /// <summary>
        /// Filename of the script to run
        /// </summary>
        [JsonPropertyName("script-torrent-done-filename")]
        public string? ScriptTorrentDoneFilename { get; set; }

        /// <summary>
        /// Whether or not to call the "done" script
        /// </summary>
        [JsonPropertyName("script-torrent-done-enabled")]
        public bool? ScriptTorrentDoneEnabled { get; set; }

        /// <summary>
        /// Filename of the script to run
        /// </summary>
        [JsonPropertyName("script-torrent-done-seeding-filename")]
        public string? ScriptTorrentDoneSeedingFilename { get; set; }

        /// <summary>
        /// Whether or not to call the "done seeding" script
        /// </summary>
        [JsonPropertyName("script-torrent-done-seeding-enabled")]
        public bool? ScriptTorrentDoneSeedingEnabled { get; set; }

        /// <summary>
        /// The default seed ratio for torrents to use
        /// </summary>
        [JsonPropertyName("seedRatioLimit")]
        public double? SeedRatioLimit { get; set; }

        /// <summary>
        /// True if seedRatioLimit is honored by default
        /// </summary>
        [JsonPropertyName("seedRatioLimited")]
        public bool? SeedRatioLimited { get; set; }

        /// <summary>
        /// Max number of torrents to uploaded at once (see seed-queue-enabled)
        /// </summary>
        [JsonPropertyName("seed-queue-size")]
        public long? SeedQueueSize { get; set; }

        /// <summary>
        /// If true, limit how many torrents can be uploaded at once
        /// </summary>
        [JsonPropertyName("seed-queue-enabled")]
        public bool? SeedQueueEnabled { get; set; }

        /// <summary>
        /// Max global download speed (KBps)
        /// </summary>
        [JsonPropertyName("speed-limit-down")]
        public long? SpeedLimitDown { get; set; }

        /// <summary>
        /// True means enabled
        /// </summary>
        [JsonPropertyName("speed-limit-down-enabled")]
        public bool? SpeedLimitDownEnabled { get; set; }

        /// <summary>
        ///  max global upload speed (KBps)
        /// </summary>
        [JsonPropertyName("speed-limit-up")]
        public long? SpeedLimitUp { get; set; }

        /// <summary>
        /// True means enabled
        /// </summary>
        [JsonPropertyName("speed-limit-up-enabled")]
        public bool? SpeedLimitUpEnabled { get; set; }

        /// <summary>
        /// True means added torrents will be started right away
        /// </summary>
        [JsonPropertyName("start-added-torrents")]
        public bool? StartAddedTorrents { get; set; }

        /// <summary>
        /// True means the .torrent file of added torrents will be deleted
        /// </summary>
        [JsonPropertyName("trash-original-torrent-files")]
        public bool? TrashOriginalTorrentFiles { get; set; }

        /// <summary>
        /// Units
        /// </summary>
        [JsonPropertyName("units")]
        public Units? Units { get; set; }

        /// <summary>
        /// True means allow utp
        /// </summary>
        [JsonPropertyName("utp-enabled")]
        public bool? UtpEnabled { get; set; }

        /// <summary>
        /// Location of transmission's configuration directory
        /// </summary>
        [JsonPropertyName("config-dir")]
        public string? ConfigDirectory{ get; set; }

        /// <summary>
        /// The current RPC API version
        /// </summary>
        [JsonPropertyName("rpc-version")]
        public long? RpcVersion{ get; set; }

        /// <summary>
        /// The minimum RPC API version supported
        /// </summary>
        [JsonPropertyName("rpc-version-minimum")]
        public long? RpcVersionMinimum { get; set; }

        /// <summary>
        /// Current RPC API version in a semver-compatible string
        /// </summary>
        [JsonPropertyName("rpc-version-semver")]
        public string? RpcVersionSemver { get; set; }

        /// <summary>
        /// long? version string "$version ($revision)"
        /// </summary>
        [JsonPropertyName("version")]
        public string? Version{ get; set; }
    }
}
