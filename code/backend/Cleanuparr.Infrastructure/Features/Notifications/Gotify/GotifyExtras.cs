using System.Text.Json.Serialization;

namespace Cleanuparr.Infrastructure.Features.Notifications.Gotify;

public class GotifyExtras
{
    [JsonPropertyName("client::display")]
    public GotifyClientDisplay? ClientDisplay { get; set; }
}
