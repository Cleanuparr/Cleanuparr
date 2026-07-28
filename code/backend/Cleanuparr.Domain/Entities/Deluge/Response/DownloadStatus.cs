using Cleanuparr.Domain.Enums;
using System.Text.Json.Serialization;

namespace Cleanuparr.Domain.Entities.Deluge.Response;

/// <summary>
/// The status of a torrent in Deluge.
/// Deluge sends only the fields that the request asks for.
/// </summary>
public sealed record DownloadStatus
{
    /// <summary>
    /// The hash of the torrent.
    /// </summary>
    public string? Hash { get; init; }

    /// <summary>
    /// The state of the torrent, for example <see cref="DelugeState.Seeding"/>.
    /// </summary>
    public DelugeState State { get; init; }

    /// <summary>
    /// The name of the torrent.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// The number of seconds that the download needs to finish.
    /// </summary>
    public ulong Eta { get; init; }

    /// <summary>
    /// The download speed in bytes each second.
    /// </summary>
    [JsonPropertyName("download_payload_rate")]
    public long DownloadSpeed { get; init; }

    /// <summary>
    /// True if the tracker of the torrent is private.
    /// </summary>
    public bool Private { get; init; }

    /// <summary>
    /// The size of the torrent in bytes.
    /// </summary>
    [JsonPropertyName("total_size")]
    public long Size { get; init; }

    /// <summary>
    /// The number of bytes that Deluge has.
    /// </summary>
    [JsonPropertyName("total_done")]
    public long TotalDone { get; init; }

    /// <summary>
    /// True if the download is complete.
    /// </summary>
    [JsonPropertyName("is_finished")]
    public bool IsFinished { get; init; }

    /// <summary>
    /// The label of the torrent.
    /// </summary>
    public string? Label { get; set; }

    /// <summary>
    /// The number of seconds that the torrent has been in the seed state.
    /// </summary>
    [JsonPropertyName("seeding_time")]
    public long SeedingTime { get; init; }

    /// <summary>
    /// The number of bytes that Deluge sent, divided by the number of bytes that Deluge got.
    /// </summary>
    public float Ratio { get; init; }

    /// <summary>
    /// The number of seeds that the tracker reports.
    /// </summary>
    [JsonPropertyName("total_seeds")]
    public int TotalSeeds { get; init; }

    /// <summary>
    /// The trackers of the torrent.
    /// </summary>
    public IReadOnlyList<Tracker> Trackers { get; init; } = [];

    /// <summary>
    /// The directory that holds the data of the torrent.
    /// </summary>
    [JsonPropertyName("download_location")]
    public string DownloadLocation { get; init; } = string.Empty;
}

/// <summary>
/// A tracker of a torrent in Deluge.
/// </summary>
public sealed record Tracker
{
    /// <summary>
    /// The address of the tracker.
    /// </summary>
    public string Url { get; init; } = string.Empty;
}
