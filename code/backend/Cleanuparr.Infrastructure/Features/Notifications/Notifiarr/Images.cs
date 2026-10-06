namespace Cleanuparr.Infrastructure.Features.Notifications.Notifiarr;

/// <summary>
/// Images for a Notifiarr notification.
/// </summary>
public class Images
{
    /// <summary>
    /// Small thumbnail image URL.
    /// </summary>
    public Uri? Thumbnail { get; set; }

    /// <summary>
    /// Large image URL.
    /// </summary>
    public Uri? Image { get; set; }
}
