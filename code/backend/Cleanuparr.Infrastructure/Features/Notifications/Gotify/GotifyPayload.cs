using Newtonsoft.Json;

namespace Cleanuparr.Infrastructure.Features.Notifications.Gotify;

public class GotifyPayload
{
    public string Title { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public int Priority { get; set; } = 5;

    public GotifyExtras? Extras { get; set; }
}

public class GotifyExtras
{
    [JsonProperty("client::display")]
    public GotifyClientDisplay? ClientDisplay { get; set; }
}

public class GotifyClientDisplay
{
    public string? ContentType { get; set; }
}
