using System.Text.Json;

namespace Cleanuparr.Domain.Entities.Arr.ManualImport;

/// <summary>
/// One file in a ManualImport command, built from a verified candidate.
/// </summary>
/// <remarks>
/// Each arr reads only its own ids, so the fields it does not know stay null.
/// </remarks>
public sealed record ManualImportFile
{
    public string? Path { get; init; }

    public string? FolderName { get; init; }

    public string? DownloadId { get; init; }

    public string? ReleaseGroup { get; init; }

    public int IndexerFlags { get; init; }

    public JsonElement? Quality { get; init; }

    public JsonElement? Languages { get; init; }

    // Sonarr, Sportarr and Whisparr v2
    public long? SeriesId { get; init; }

    public List<long>? EpisodeIds { get; init; }

    public string? ReleaseType { get; init; }

    // Radarr and Whisparr v3
    public long? MovieId { get; init; }
}
