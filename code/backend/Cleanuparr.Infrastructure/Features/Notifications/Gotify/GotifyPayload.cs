namespace Cleanuparr.Infrastructure.Features.Notifications.Gotify;

public class GotifyPayload
{
    public string Title { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public int Priority { get; set; } = 5;

    public GotifyExtras? Extras { get; set; }
}
