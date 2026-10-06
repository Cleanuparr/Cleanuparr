using System.Text.Json.Serialization;

namespace Cleanuparr.Domain.Entities.Sabnzbd;

/// <summary>
/// A single job that has left the SABnzbd queue, successfully or not.
/// </summary>
public sealed record SabnzbdHistorySlot
{
    [JsonPropertyName("nzo_id")]
    public string? NzoId { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>
    /// "Completed" or "Failed" once a job leaves the queue.
    /// </summary>
    [JsonPropertyName("status")]
    public string? Status { get; init; }

    /// <summary>
    /// The final output directory on disk.
    /// </summary>
    [JsonPropertyName("storage")]
    public string? Storage { get; init; }

    [JsonPropertyName("fail_message")]
    public string? FailMessage { get; init; }

    [JsonPropertyName("bytes")]
    public long Bytes { get; init; }

    [JsonPropertyName("category")]
    public string? Category { get; init; }
}
