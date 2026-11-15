using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Transmission.API.RPC.Entity
{
    /// <summary>
    /// Torrent fields
    /// </summary>
    public sealed class TorrentFields
    {
        private TorrentFields() { }

        /// <summary>
        /// activityDate
        /// </summary>
        public const string ACTIVITY_DATE = "activityDate";

        /// <summary>
        /// addedDate
        /// </summary>
        public const string ADDED_DATE = "addedDate";

        /// <summary>
        /// availability
        /// </summary>
        public const string AVAILABILITY = "availability";

        /// <summary>
        /// bandwidthPriority
        /// </summary>
        public const string BANDWIDTH_PRIORITY = "bandwidthPriority";

        /// <summary>
        /// comment
        /// </summary>
        public const string COMMENT = "comment";

        /// <summary>
        /// corruptEver
        /// </summary>
        public const string CORRUPT_EVER = "corruptEver";

        /// <summary>
        /// creator
        /// </summary>
        public const string CREATOR = "creator";

        /// <summary>
        /// dateCreated
        /// </summary>
        public const string DATE_CREATED = "dateCreated";

        /// <summary>
        /// desiredAvailable
        /// </summary>
        public const string DESIRED_AVAILABLE = "desiredAvailable";

        /// <summary>
        /// doneDate
        /// </summary>
        public const string DONE_DATE = "doneDate";

        /// <summary>
        /// downloadDir
        /// </summary>
        public const string DOWNLOAD_DIR = "downloadDir";

        /// <summary>
        /// downloadedEver
        /// </summary>
        public const string DOWNLOADED_EVER = "downloadedEver";

        /// <summary>
        /// downloadLimit
        /// </summary>
        public const string DOWNLOAD_LIMIT = "downloadLimit";

        /// <summary>
        /// downloadLimited
        /// </summary>
        public const string DOWNLOAD_LIMITED = "downloadLimited";

        /// <summary>
        /// editDate
        /// </summary>
        public const string EDIT_DATE = "editDate";
        
        /// <summary>
        /// error
        /// </summary>
        public const string ERROR = "error";

        /// <summary>
        /// errorString
        /// </summary>
        public const string ERROR_STRING = "errorString";

        /// <summary>
        /// eta
        /// </summary>
        public const string ETA = "eta";

        /// <summary>
        /// etaIdle
        /// </summary>
        public const string ETA_IDLE = "etaIdle";

        /// <summary>
        /// file-count
        /// </summary>
        public const string FILE_COUNT = "file-count";

        /// <summary>
        /// files
        /// </summary>
        public const string FILES = "files";

        /// <summary>
        /// fileStats
        /// </summary>
        public const string FILE_STATS = "fileStats";

        /// <summary>
        /// group
        /// </summary>
        public const string GROUP = "group";

        /// <summary>
        /// hashString
        /// </summary>
        public const string HASH_STRING = "hashString";

        /// <summary>
        /// haveUnchecked
        /// </summary>
        public const string HAVE_UNCHECKED = "haveUnchecked";

        /// <summary>
        /// haveValid
        /// </summary>
        public const string HAVE_VALID = "haveValid";

        /// <summary>
        /// honorsSessionLimits
        /// </summary>
        public const string HONORS_SESSION_LIMITS = "honorsSessionLimits";

        /// <summary>
        /// id
        /// </summary>
        public const string ID = "id";

        /// <summary>
        /// isFinished
        /// </summary>
        public const string IS_FINISHED = "isFinished";

        /// <summary>
        /// isPrivate
        /// </summary>
        public const string IS_PRIVATE = "isPrivate";

        /// <summary>
        /// isStalled
        /// </summary>
        public const string IS_STALLED = "isStalled";

        /// <summary>
        /// labels
        /// </summary>
        public const string LABELS = "labels";

        /// <summary>
        /// leftUntilDone
        /// </summary>
        public const string LEFT_UNTIL_DONE = "leftUntilDone";

        /// <summary>
        /// magnetLink
        /// </summary>
        public const string MAGNET_LINK = "magnetLink";

        /// <summary>
        /// manualAnnounceTime
        /// </summary>
        public const string MANUAL_ANNOUNCE_TIME = "manualAnnounceTime";

        /// <summary>
        /// maxConnectedPeers
        /// </summary>
        public const string MAX_CONNECTED_PEERS = "maxConnectedPeers";

        /// <summary>
        /// metadataPercentComplete
        /// </summary>
        public const string METADATA_PERCENT_COMPLETE = "metadataPercentComplete";

        /// <summary>
        /// name
        /// </summary>
        public const string NAME = "name";

        /// <summary>
        /// peer-limit
        /// </summary>
        public const string PEER_LIMIT = "peer-limit";

        /// <summary>
        /// peers
        /// </summary>
        public const string PEERS = "peers";

        /// <summary>
        /// peersConnected
        /// </summary>
        public const string PEERS_CONNECTED = "peersConnected";

        /// <summary>
        /// peersFrom
        /// </summary>
        public const string PEERS_FROM = "peersFrom";

        /// <summary>
        /// peersGettingFromUs
        /// </summary>
        public const string PEERS_GETTING_FROM_US = "peersGettingFromUs";

        /// <summary>
        /// peersSendingToUs
        /// </summary>
        public const string PEERS_SENDING_TO_US = "peersSendingToUs";

        /// <summary>
        /// percentComplete
        /// </summary>
        public const string PERCENT_COMPLETE = "percentComplete";

        /// <summary>
        /// percentDone
        /// </summary>
        public const string PERCENT_DONE = "percentDone";

        /// <summary>
        /// pieces
        /// </summary>
        public const string PIECES = "pieces";

        /// <summary>
        /// pieceCount
        /// </summary>
        public const string PIECE_COUNT = "pieceCount";

        /// <summary>
        /// pieceSize
        /// </summary>
        public const string PIECE_SIZE = "pieceSize";

        /// <summary>
        /// priorities
        /// </summary>
        public const string PRIORITIES = "priorities";

        /// <summary>
        /// primary-mime-type
        /// </summary>
        public const string PRIMARY_MIME_TYPE = "primary-mime-type";

        /// <summary>
        /// queuePosition
        /// </summary>
        public const string QUEUE_POSITION = "queuePosition";

        /// <summary>
        /// rateDownload
        /// </summary>
        public const string RATE_DOWNLOAD = "rateDownload";

        /// <summary>
        /// rateUpload
        /// </summary>
        public const string RATE_UPLOAD = "rateUpload";

        /// <summary>
        /// recheckProgress
        /// </summary>
        public const string RECHECK_PROGRESS = "recheckProgress";

        /// <summary>
        /// secondsDownloading
        /// </summary>
        public const string SECONDS_DOWNLOADING = "secondsDownloading";

        /// <summary>
        /// secondsSeeding
        /// </summary>
        public const string SECONDS_SEEDING = "secondsSeeding";

        /// <summary>
        /// seedIdleLimit
        /// </summary>
        public const string SEED_IDLE_LIMIT = "seedIdleLimit";

        /// <summary>
        /// seedIdleMode
        /// </summary>
        public const string SEED_IDLE_MODE = "seedIdleMode";

        /// <summary>
        /// seedRatioLimit
        /// </summary>
        public const string SEED_RATIO_LIMIT = "seedRatioLimit";

        /// <summary>
        /// seedRatioMode
        /// </summary>
        public const string SEED_RATIO_MODE = "seedRatioMode";

        /// <summary>
        /// sequentialDownload
        /// </summary>
        public const string SEQUENTIAL_DOWNLOAD = "sequentialDownload";

        /// <summary>
        /// sizeWhenDone
        /// </summary>
        public const string SIZE_WHEN_DONE = "sizeWhenDone";

        /// <summary>
        /// startDate
        /// </summary>
        public const string START_DATE = "startDate";

        /// <summary>
        /// status
        /// </summary>
        public const string STATUS = "status";

        /// <summary>
        /// trackers
        /// </summary>
        public const string TRACKERS = "trackers";

        /// <summary>
        /// trackerList
        /// </summary>
        public const string TRACKER_LIST = "trackerList";

        /// <summary>
        /// trackerStats
        /// </summary>
        public const string TRACKER_STATS = "trackerStats";

        /// <summary>
        /// totalSize
        /// </summary>
        public const string TOTAL_SIZE = "totalSize";

        /// <summary>
        /// torrentFile
        /// </summary>
        public const string TORRENT_FILE = "torrentFile";

        /// <summary>
        /// uploadedEver
        /// </summary>
        public const string UPLOADED_EVER = "uploadedEver";

        /// <summary>
        /// uploadLimit
        /// </summary>
        public const string UPLOAD_LIMIT = "uploadLimit";

        /// <summary>
        /// uploadLimited
        /// </summary>
        public const string UPLOAD_LIMITED = "uploadLimited";

        /// <summary>
        /// uploadRatio
        /// </summary>
        public const string UPLOAD_RATIO = "uploadRatio";

        /// <summary>
        /// wanted
        /// </summary>
        public const string WANTED = "wanted";

        /// <summary>
        /// webseeds
        /// </summary>
        public const string WEB_SEEDS = "webseeds";

        /// <summary>
        /// webseedsSendingToUs
        /// </summary>
        public const string WEB_SEEDS_SENDING_TO_US = "webseedsSendingToUs";

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
                    ACTIVITY_DATE,
                    ADDED_DATE,
                    AVAILABILITY,
                    BANDWIDTH_PRIORITY,
                    COMMENT,
                    CORRUPT_EVER,
                    CREATOR,
                    DATE_CREATED,
                    DESIRED_AVAILABLE,
                    DONE_DATE,
                    DOWNLOAD_DIR,
                    DOWNLOADED_EVER,
                    DOWNLOAD_LIMIT,
                    DOWNLOAD_LIMITED,
                    EDIT_DATE,
                    ERROR,
                    ERROR_STRING,
                    ETA,
                    ETA_IDLE,
                    FILE_COUNT,
                    FILES,
                    FILE_STATS,
                    GROUP,
                    HASH_STRING,
                    HAVE_UNCHECKED,
                    HAVE_VALID,
                    HONORS_SESSION_LIMITS,
                    ID,
                    IS_FINISHED,
                    IS_PRIVATE,
                    IS_STALLED,
                    LABELS,
                    LEFT_UNTIL_DONE,
                    MAGNET_LINK,
                    MANUAL_ANNOUNCE_TIME,
                    MAX_CONNECTED_PEERS,
                    METADATA_PERCENT_COMPLETE,
                    NAME,
                    PEER_LIMIT,
                    PEERS,
                    PEERS_CONNECTED,
                    PEERS_FROM,
                    PEERS_GETTING_FROM_US,
                    PEERS_SENDING_TO_US,
                    PERCENT_COMPLETE,
                    PERCENT_DONE,
                    PIECES,
                    PIECE_COUNT,
                    PIECE_SIZE,
                    PRIORITIES,
                    PRIMARY_MIME_TYPE,
                    QUEUE_POSITION,
                    RATE_DOWNLOAD,
                    RATE_UPLOAD,
                    RECHECK_PROGRESS,
                    SECONDS_DOWNLOADING,
                    SECONDS_SEEDING,
                    SEED_IDLE_LIMIT,
                    SEED_IDLE_MODE,
                    SEED_RATIO_LIMIT,
                    SEED_RATIO_MODE,
                    SEQUENTIAL_DOWNLOAD,
                    SIZE_WHEN_DONE,
                    START_DATE,
                    STATUS,
                    TRACKERS,
                    TRACKER_LIST,
                    TRACKER_STATS,
                    TOTAL_SIZE,
                    TORRENT_FILE,
                    UPLOADED_EVER,
                    UPLOAD_LIMIT,
                    UPLOAD_LIMITED,
                    UPLOAD_RATIO,
                    WANTED,
                    WEB_SEEDS,
                    WEB_SEEDS_SENDING_TO_US,
                    #endregion
                };
            }
        }
    }
}
