using System.Runtime.Serialization;

namespace QBittorrent.Client
{
    /// <summary>
    /// The torrent state.
    /// </summary>
    public enum TorrentState
    {
        /// <summary>
        /// Unknown
        /// </summary>
        [EnumMember(Value = "unknown")]
        Unknown,

        /// <summary>
        /// Some error occurred, applies to paused torrents
        /// </summary>
        [EnumMember(Value = "error")]
        Error,

        /// <summary>
        /// Torrent is paused and has finished downloading
        /// </summary>
        [EnumMember(Value = "pausedUP")]
        PausedUpload,

        /// <summary>
        /// Torrent is paused and has NOT finished downloading
        /// </summary>
        [EnumMember(Value = "pausedDL")]
        PausedDownload,

        /// <summary>
        /// Queuing is enabled and torrent is queued for upload
        /// </summary>
        [EnumMember(Value = "queuedUP")]
        QueuedUpload,

        /// <summary>
        /// Queuing is enabled and torrent is queued for download
        /// </summary>
        [EnumMember(Value = "queuedDL")]
        QueuedDownload,

        /// <summary>
        /// Torrent is being seeded and data is being transferred
        /// </summary>
        [EnumMember(Value = "uploading")]
        Uploading,

        /// <summary>
        /// Torrent is being seeded, but no connection were made
        /// </summary>
        [EnumMember(Value = "stalledUP")]
        StalledUpload,

        /// <summary>
        /// Torrent has finished downloading and is being checked; 
        /// this status also applies to preallocation (if enabled) and checking resume data on qBt startup
        /// </summary>
        [EnumMember(Value = "checkingUP")]
        CheckingUpload,

        /// <summary>
        /// Torrent is being checked
        /// </summary>
        [EnumMember(Value = "checkingDL")]
        CheckingDownload,

        /// <summary>
        /// Torrent is being downloaded and data is being transferred
        /// </summary>
        [EnumMember(Value = "downloading")]
        Downloading,

        /// <summary>
        /// Torrent is being downloaded, but no connection were made
        /// </summary>
        [EnumMember(Value = "stalledDL")]
        StalledDownload,
        
        /// <summary>
        /// Torrent has just started downloading and is fetching metadata
        /// </summary>
        [EnumMember(Value = "metaDL")]
        FetchingMetadata,
        
        /// <summary>
        /// Torrent has just started downloading and is fetching metadata
        /// </summary>
        [EnumMember(Value = "forcedMetaDL")]
        ForcedFetchingMetadata,

        /// <summary>
        /// 
        /// </summary>
        [EnumMember(Value = "forcedUP")]
        ForcedUpload,

        /// <summary>
        /// 
        /// </summary>
        [EnumMember(Value = "forcedDL")]
        ForcedDownload,

        /// <summary>
        /// The files are missing
        /// </summary>
        [EnumMember(Value = "missingFiles")]
        MissingFiles,

        /// <summary>
        /// Allocating space on disk
        /// </summary>
        [EnumMember(Value = "allocating")]
        Allocating,

        /// <summary>
        /// Queued for checking
        /// </summary>
        [EnumMember(Value = "queuedForChecking")]
        QueuedForChecking,

        /// <summary>
        /// Resume data is being checked
        /// </summary>
        [EnumMember(Value = "checkingResumeData")]
        CheckingResumeData,

        /// <summary>
        /// Data is being moved from the temporary folder
        /// </summary>
        [EnumMember(Value = "moving")]
        Moving
    }
}
