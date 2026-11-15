using System;
using System.Collections.Generic;
using System.Text;

namespace Transmission.API.RPC.Arguments
{
    /// <summary>
    /// Session fields
    /// </summary>
    public sealed class SessionFields
    {
        private SessionFields() { }
        /// <summary>
        /// alt-speed-down
        /// </summary>
        public const string ALT_SPEED_DOWN = "alt-speed-down";
        /// <summary>
        /// alt-speed-enabled
        /// </summary>
        public const string ALT_SPEED_ENABLED = "alt-speed-enabled";
        /// <summary>
        /// alt-speed-time-begin
        /// </summary>
        public const string ALT_SPEED_TIME_BEGIN = "alt-speed-time-begin";
        /// <summary>
        /// alt-speed-time-enabled
        /// </summary>
        public const string ALT_SPEED_TIME_ENABLED = "alt-speed-time-enabled";
        /// <summary>
        /// alt-speed-time-end
        /// </summary>
        public const string ALT_SPEED_TIME_END = "alt-speed-time-end";
        /// <summary>
        /// alt-speed-time-day
        /// </summary>
        public const string ALT_SPEED_TIME_DAY = "alt-speed-time-day";
        /// <summary>
        /// alt-speed-up
        /// </summary>
        public const string ALT_SPEED_UP = "alt-speed-up";
        /// <summary>
        /// blocklist-url
        /// </summary>
        public const string BLOCKLIST_URL = "blocklist-url";
        /// <summary>
        /// blocklist-enabled
        /// </summary>
        public const string BLOCKLIST_ENABLED = "blocklist-enabled";
        /// <summary>
        /// blocklist-size
        /// </summary>
        public const string BLOCKLIST_SIZE = "blocklist-size";
        /// <summary>
        /// cache-size-mb
        /// </summary>
        public const string CACHE_SIZE_MB = "cache-size-mb";
        /// <summary>
        /// default-trackers
        /// </summary>
        public const string DEFAULT_TRACKERS = "default-trackers";
        /// <summary>
        /// dht-enabled
        /// </summary>
        public const string DHT_ENABLED = "dht-enabled";
        /// <summary>
        /// download-dir
        /// </summary>
        public const string DOWNLOAD_DIR = "download-dir";
        /// <summary>
        /// download-dir-free-space
        /// </summary>
        public const string DOWNLOAD_DIR_FREE_SPACE = "download-dir-free-space";
        /// <summary>
        /// download-queue-size
        /// </summary>
        public const string DOWNLOAD_QUEUE_SIZE = "download-queue-size";
        /// <summary>
        /// download-queue-enabled
        /// </summary>
        public const string DOWNLOAD_QUEUE_ENABLED = "download-queue-enabled";
        /// <summary>
        /// encryption
        /// </summary>
        public const string ENCRYPTION = "encryption";
        /// <summary>
        /// idle-seeding-limit
        /// </summary>
        public const string IDLE_SEEDING_LIMIT = "idle-seeding-limit";
        /// <summary>
        /// idle-seeding-limit-enabled
        /// </summary>
        public const string IDLE_SEEDING_LIMIT_ENABLED = "idle-seeding-limit-enabled";
        /// <summary>
        /// incomplete-dir
        /// </summary>
        public const string INCOMPLETE_DIR = "incomplete-dir";
        /// <summary>
        /// incomplete-dir-enabled
        /// </summary>
        public const string INCOMPLETE_DIR_ENABLED = "incomplete-dir-enabled";
        /// <summary>
        /// lpd-enabled
        /// </summary>
        public const string LPD_ENABLED = "lpd-enabled";
        /// <summary>
        /// peer-limit-global
        /// </summary>
        public const string PEER_LIMIT_GLOBAL = "peer-limit-global";
        /// <summary>
        /// peer-limit-per-torrent
        /// </summary>
        public const string PEER_LIMIT_PER_TORRENT = "peer-limit-per-torrent";
        /// <summary>
        /// pex-enabled
        /// </summary>
        public const string PEX_ENABLED = "pex-enabled";
        /// <summary>
        /// peer-port
        /// </summary>
        public const string PEER_PORT = "peer-port";
        /// <summary>
        /// peer-port-random-on-start
        /// </summary>
        public const string PEER_PORT_RANDOM_ON_START = "peer-port-random-on-start";
        /// <summary>
        /// port-forwarding-enabled
        /// </summary>
        public const string PORT_FORWARDING_ENABLED = "port-forwarding-enabled";
        /// <summary>
        /// queue-stalled-enabled
        /// </summary>
        public const string QUEUE_STALLED_ENABLED = "queue-stalled-enabled";
        /// <summary>
        /// queue-stalled-minutes
        /// </summary>
        public const string QUEUE_STALLED_MINUTES = "queue-stalled-minutes";
        /// <summary>
        /// rename-partial-files
        /// </summary>
        public const string RENAME_PARTIAL_FILES = "rename-partial-files";
        /// <summary>
        /// session-id
        /// </summary>
        public const string SESSION_ID = "session-id";
        /// <summary>
        /// script-torrent-added-filename
        /// </summary>
        public const string SCRIPT_TORRENT_ADDED_FILENAME = "script-torrent-added-filename";
        /// <summary>
        /// script-torrent-added-enabled
        /// </summary>
        public const string SCRIPT_TORRENT_ADDED_ENABLED = "script-torrent-added-enabled";
        /// <summary>
        /// script-torrent-done-filename
        /// </summary>
        public const string SCRIPT_TORRENT_DONE_FILENAME = "script-torrent-done-filename";
        /// <summary>
        /// script-torrent-done-enabled
        /// </summary>
        public const string SCRIPT_TORRENT_DONE_ENABLED = "script-torrent-done-enabled";
        /// <summary>
        /// script-torrent-done-seeding-filename
        /// </summary>
        public const string SCRIPT_TORRENT_DONE_SEEDING_FILENAME = "script-torrent-done-seeding-filename";
        /// <summary>
        /// script-torrent-done-seeding-enabled
        /// </summary>
        public const string SCRIPT_TORRENT_DONE_SEEDING_ENABLED = "script-torrent-done-seeding-enabled";
        /// <summary>
        /// seedRatioLimit
        /// </summary>
        public const string SEED_RATIO_LIMIT = "seedRatioLimit";
        /// <summary>
        /// seedRatioLimited
        /// </summary>
        public const string SEED_RATIO_LIMITED = "seedRatioLimited";
        /// <summary>
        /// seed-queue-size
        /// </summary>
        public const string SEED_QUEUE_SIZE = "seed-queue-size";
        /// <summary>
        /// seed-queue-enabled
        /// </summary>
        public const string SEED_QUEUE_ENABLED = "seed-queue-enabled";
        /// <summary>
        /// speed-limit-down
        /// </summary>
        public const string SPEED_LIMIT_DOWN = "speed-limit-down";
        /// <summary>
        /// speed-limit-down-enabled
        /// </summary>
        public const string SPEED_LIMIT_DOWN_ENABLED = "speed-limit-down-enabled";
        /// <summary>
        /// speed-limit-up
        /// </summary>
        public const string SPEED_LIMIT_UP = "speed-limit-up";
        /// <summary>
        /// speed-limit-up-enabled
        /// </summary>
        public const string SPEED_LIMIT_UP_ENABLED = "speed-limit-up-enabled";
        /// <summary>
        /// start-added-torrents
        /// </summary>
        public const string START_ADDED_TORRENTS = "start-added-torrents";
        /// <summary>
        /// trash-original-torrent-files
        /// </summary>
        public const string TRASH_ORIGINAL_TORRENT_FILES = "trash-original-torrent-files";
        /// <summary>
        /// units
        /// </summary>
        public const string UNITS = "units";
        /// <summary>
        /// utp-enabled
        /// </summary>
        public const string UTP_ENABLED = "utp-enabled";
        /// <summary>
        /// config-dir
        /// </summary>
        public const string CONFIG_DIR = "config-dir";
        /// <summary>
        /// rpc-version
        /// </summary>
        public const string RPC_VERSION = "rpc-version";
        /// <summary>
        /// rpc-version-minimum
        /// </summary>
        public const string RPC_VERSION_MINIMUM = "rpc-version-minimum";
        /// <summary>
        /// rpc-version-semver
        /// </summary>
        public const string RPC_VERSION_SEMVER = "rpc-version-semver";
        /// <summary>
        /// version
        /// </summary>
        public const string VERSION = "version";

        /// <summary>
        /// All fields
        /// </summary>
        public static string[] ALL_FIELDS
        {
            get
            {
                return new string[]
                {
                    #region ALL FIELDS
                    ALT_SPEED_DOWN,
                    ALT_SPEED_ENABLED,
                    ALT_SPEED_TIME_BEGIN,
                    ALT_SPEED_TIME_ENABLED,
                    ALT_SPEED_TIME_END,
                    ALT_SPEED_TIME_DAY,
                    ALT_SPEED_UP,
                    BLOCKLIST_URL,
                    BLOCKLIST_ENABLED,
                    BLOCKLIST_SIZE,
                    CACHE_SIZE_MB,
                    DEFAULT_TRACKERS,
                    DHT_ENABLED,
                    DOWNLOAD_DIR,
                    //DOWNLOAD_DIR_FREE_SPACE, // Deprecated
                    DOWNLOAD_QUEUE_SIZE,
                    DOWNLOAD_QUEUE_ENABLED,
                    ENCRYPTION,
                    IDLE_SEEDING_LIMIT,
                    IDLE_SEEDING_LIMIT_ENABLED,
                    INCOMPLETE_DIR,
                    INCOMPLETE_DIR_ENABLED,
                    LPD_ENABLED,
                    PEER_LIMIT_GLOBAL,
                    PEER_LIMIT_PER_TORRENT,
                    PEX_ENABLED,
                    PEER_PORT,
                    PEER_PORT_RANDOM_ON_START,
                    PORT_FORWARDING_ENABLED,
                    QUEUE_STALLED_ENABLED,
                    QUEUE_STALLED_MINUTES,
                    RENAME_PARTIAL_FILES,
                    SESSION_ID,
                    SCRIPT_TORRENT_ADDED_FILENAME,
                    SCRIPT_TORRENT_ADDED_ENABLED,
                    SCRIPT_TORRENT_DONE_FILENAME,
                    SCRIPT_TORRENT_DONE_ENABLED,
                    SCRIPT_TORRENT_DONE_SEEDING_FILENAME,
                    SCRIPT_TORRENT_DONE_SEEDING_ENABLED,
                    SEED_RATIO_LIMIT,
                    SEED_RATIO_LIMITED,
                    SEED_QUEUE_SIZE,
                    SEED_QUEUE_ENABLED,
                    SPEED_LIMIT_DOWN,
                    SPEED_LIMIT_DOWN_ENABLED,
                    SPEED_LIMIT_UP,
                    SPEED_LIMIT_UP_ENABLED,
                    START_ADDED_TORRENTS,
                    TRASH_ORIGINAL_TORRENT_FILES,
                    UNITS,
                    UTP_ENABLED,
                    CONFIG_DIR,
                    RPC_VERSION,
                    RPC_VERSION_MINIMUM,
                    RPC_VERSION_SEMVER,
                    VERSION,
                    #endregion
                };
            }
        }
    }
}
