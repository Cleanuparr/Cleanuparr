using Cleanuparr.Domain.Enums;
using Newtonsoft.Json;

namespace Cleanuparr.Domain.Entities.Deluge.Response;

public sealed record DownloadStatus
{
    public string? Hash { get; init; }

    public DelugeState State { get; init; }

    public string? Name { get; init; }

    public ulong Eta { get; init; }

    [JsonProperty("download_payload_rate")]
    public long DownloadSpeed { get; init; }

    public bool Private { get; init; }

    [JsonProperty("total_size")]
    public long Size { get; init; }

    [JsonProperty("total_done")]
    public long TotalDone { get; init; }

    [JsonProperty("is_finished")]
    public bool IsFinished { get; init; }
    
    public string? Label { get; set; }
    
    [JsonProperty("seeding_time")]
    public long SeedingTime { get; init; }
    
    public float Ratio { get; init; }
    
    public required IReadOnlyList<Tracker> Trackers { get; init; }
    
    [JsonProperty("download_location")]
    public required string DownloadLocation { get; init; }
}

public sealed record Tracker
{
    public required string Url { get; init; }
}