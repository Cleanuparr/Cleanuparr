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
    
    public bool IsValid => DateTimeOffset.UtcNow < ExpiresAt && 
                          !string.IsNullOrEmpty(AuthToken) && 
                          !string.IsNullOrEmpty(GuidCookie);
}
