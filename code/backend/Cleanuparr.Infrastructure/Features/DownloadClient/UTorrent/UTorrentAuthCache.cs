namespace Cleanuparr.Infrastructure.Features.DownloadClient.UTorrent;

/// <summary>
/// Represents cached authentication data for a µTorrent client instance
/// </summary>
public sealed class UTorrentAuthCache
{
    public string AuthToken { get; init; } = string.Empty;
    public string GuidCookie { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }
    
    /// <param name="now">The current time, from the caller's clock.</param>
    public bool IsValid(DateTimeOffset now) => now < ExpiresAt &&
                                               !string.IsNullOrEmpty(AuthToken) &&
                                               !string.IsNullOrEmpty(GuidCookie);
}
