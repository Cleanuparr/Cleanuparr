using System.Text.Json.Serialization;

namespace Cleanuparr.Domain.Entities.Sabnzbd;

/// <summary>
/// The subset of SABnzbd's <c>misc</c> config section Cleanuparr reads.
/// </summary>
public sealed record SabnzbdMiscConfig
{
    /// <summary>
    /// The folder SABnzbd downloads in-progress jobs into, before they move to <c>complete_dir</c>.
    /// </summary>
    [JsonPropertyName("download_dir")]
    public string? DownloadDir { get; init; }

    /// <summary>
    /// The folder SABnzbd moves a job into once it finishes downloading and post-processing.
    /// </summary>
    [JsonPropertyName("complete_dir")]
    public string? CompleteDir { get; init; }
}
