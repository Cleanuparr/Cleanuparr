using System.Text.Json.Serialization;

namespace Cleanuparr.Infrastructure.Features.Notifications.Gotify;

/// <summary>
/// Extra metadata fields for a Gotify notification.
/// </summary>
public class GotifyExtras
{
    /// <summary>
    /// Client display configuration for rendering the notification.
    /// </summary>
    [JsonPropertyName("client::display")]
    public GotifyClientDisplay? ClientDisplay { get; set; }
}
