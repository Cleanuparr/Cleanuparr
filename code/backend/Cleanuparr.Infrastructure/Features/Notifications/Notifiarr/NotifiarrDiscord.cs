namespace Cleanuparr.Infrastructure.Features.Notifications.Notifiarr;

/// <summary>
/// Discord-specific fields for a Notifiarr notification.
/// </summary>
public class NotifiarrDiscord
{
    /// <summary>
    /// Hex color code for the embed sidebar.
    /// </summary>
    public string Color { get; set; } = string.Empty;

    /// <summary>
    /// User and role mentions to ping in the notification.
    /// </summary>
    public Ping Ping { get; set; }

    /// <summary>
    /// Thumbnail and image URLs for the notification.
    /// </summary>
    public Images Images { get; set; }

    /// <summary>
    /// Text content and formatting for the notification.
    /// </summary>
    public Text Text { get; set; }

    /// <summary>
    /// IDs for routing the notification to Discord.
    /// </summary>
    public Ids Ids { get; set; }
}
