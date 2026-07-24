using System.Text.Json.Serialization;

namespace Cleanuparr.Domain.Entities.Deluge.Response;

public sealed record DelugeTorrentExtended : DelugeTorrent
{
    [JsonPropertyName("total_done")]
    public long TotalDone { get; set; }

    [JsonPropertyName("total_payload_download")]
    public long TotalPayloadDownload { get; set; }

    [JsonPropertyName("total_uploaded")]
    public long TotalUploaded { get; set; }

    [JsonPropertyName("next_announce")]
    public int NextAnnounce { get; set; }

    [JsonPropertyName("tracker_status")]
    public string TrackerStatus { get; set; }

    [JsonPropertyName("num_pieces")]
    public int NumPieces { get; set; }

    [JsonPropertyName("piece_length")]
    public long PieceLength { get; set; }

    [JsonPropertyName("is_auto_managed")]
    public bool IsAutoManaged { get; set; }

    [JsonPropertyName("active_time")]
    public long ActiveTime { get; set; }

    [JsonPropertyName("seeding_time")]
    public long SeedingTime { get; set; }

    [JsonPropertyName("time_since_transfer")]
    public long TimeSinceTransfer { get; set; }

    [JsonPropertyName("seed_rank")]
    public int SeedRank { get; set; }

    [JsonPropertyName("last_seen_complete")]
    public long LastSeenComplete { get; set; }

    [JsonPropertyName("completed_time")]
    public long CompletedTime { get; set; }

    [JsonPropertyName("owner")] public string Owner { get; set; }

    [JsonPropertyName("public")]
    public bool Public { get; set; }

    [JsonPropertyName("shared")]
    public bool Shared { get; set; }

    [JsonPropertyName("queue")] public int Queue { get; set; }

    [JsonPropertyName("total_wanted")]
    public long TotalWanted { get; set; }

    [JsonPropertyName("state")] public string State { get; set; }

    [JsonPropertyName("progress")]
    public float Progress { get; set; }

    [JsonPropertyName("num_seeds")]
    public int NumSeeds { get; set; }

    [JsonPropertyName("total_seeds")]
    public int TotalSeeds { get; set; }

    [JsonPropertyName("num_peers")]
    public int NumPeers { get; set; }

    [JsonPropertyName("total_peers")]
    public int TotalPeers { get; set; }

    [JsonPropertyName("download_payload_rate")]
    public long DownloadPayloadRate { get; set; }

    [JsonPropertyName("upload_payload_rate")]
    public long UploadPayloadRate { get; set; }

    [JsonPropertyName("eta")] public long Eta { get; set; }

    [JsonPropertyName("distributed_copies")]
    public float DistributedCopies { get; set; }

    [JsonPropertyName("time_added")]
    public int TimeAdded { get; set; }

    [JsonPropertyName("tracker_host")]
    public string TrackerHost { get; set; }

    [JsonPropertyName("download_location")]
    public string DownloadLocation { get; set; }

    [JsonPropertyName("total_remaining")]
    public long TotalRemaining { get; set; }

    [JsonPropertyName("max_download_speed")]
    public long MaxDownloadSpeed { get; set; }

    [JsonPropertyName("max_upload_speed")]
    public long MaxUploadSpeed { get; set; }

    [JsonPropertyName("seeds_peers_ratio")]
    public float SeedsPeersRatio { get; set; }
}