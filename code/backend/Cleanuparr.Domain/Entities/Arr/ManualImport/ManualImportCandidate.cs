using System.Text.Json;

namespace Cleanuparr.Domain.Entities.Arr.ManualImport;

/// <summary>
/// One file an arr found for a download, with the ids it maps to.
/// </summary>
/// <remarks>
/// Quality and languages stay raw JSON so the arr's own values go back unchanged.
/// </remarks>
public sealed record ManualImportCandidate
{
    public string? Path { get; init; }

    public string? RelativePath { get; init; }

    public string? FolderName { get; init; }

    public string? DownloadId { get; init; }

    public string? ReleaseGroup { get; init; }

    public int IndexerFlags { get; init; }

    public string? ReleaseType { get; init; }

    public JsonElement? Quality { get; init; }

    public JsonElement? Languages { get; init; }

    public List<ManualImportRejection>? Rejections { get; init; }

    // Sonarr, Sportarr and Whisparr v2
    public ManualImportRef? Series { get; init; }

    public List<ManualImportRef>? Episodes { get; init; }

    // Radarr and Whisparr v3
    public ManualImportRef? Movie { get; init; }

    /// <summary>
    /// Radarr 6 sends the movie id flat, older builds nest it under <see cref="Movie"/>.
    /// </summary>
    public long? MovieId { get; init; }

    /// <summary>
    /// The movie the candidate maps to, wherever the arr put the id.
    /// </summary>
    public long? ResolvedMovieId => Movie?.Id ?? MovieId;
}
