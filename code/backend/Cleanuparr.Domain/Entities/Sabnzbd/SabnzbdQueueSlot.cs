using System.Text.Json.Serialization;

namespace Cleanuparr.Domain.Entities.Sabnzbd;

/// <summary>
/// A single job in the SABnzbd queue (not yet finished).
/// </summary>
public sealed record SabnzbdQueueSlot
{
    [JsonPropertyName("nzo_id")]
    public string? NzoId { get; init; }

    [JsonPropertyName("filename")]
    public string? Filename { get; init; }

    /// <summary>
    /// For example "Downloading", "Paused", "Queued", "Checking", "Extracting", "Verifying", "Repairing", "Moving", "Fetching", "QuickCheck".
    /// </summary>
    [JsonPropertyName("status")]
    public string? Status { get; init; }

    [JsonPropertyName("mb")]
    public double Mb { get; init; }

    [JsonPropertyName("mbleft")]
    public double MbLeft { get; init; }

    [JsonPropertyName("cat")]
    public string? Category { get; init; }
}
