namespace Cleanuparr.Api.Features.Webhooks.Contracts;

/// <summary>
/// Minimal, tolerant projection of the Sonarr/Radarr "On Grab" Webhook payload. Only the fields used
/// to trigger a targeted MalwareBlocker scan are bound; all other fields are ignored.
/// </summary>
public sealed record ArrWebhookPayload
{
    /// <summary>"Grab" to act on; "Test" is sent when the connection's Test button is clicked.</summary>
    public string? EventType { get; init; }

    /// <summary>Torrent infohash (or NZB id) identifying the download in the download client.</summary>
    public string? DownloadId { get; init; }

    /// <summary>Present on Sonarr payloads; carries the series content id.</summary>
    public ArrWebhookContent? Series { get; init; }

    /// <summary>Present on Radarr payloads; carries the movie content id.</summary>
    public ArrWebhookContent? Movie { get; init; }
}

public sealed record ArrWebhookContent
{
    public long Id { get; init; }
}
