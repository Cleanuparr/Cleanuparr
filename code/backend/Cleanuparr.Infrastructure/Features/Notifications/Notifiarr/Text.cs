namespace Cleanuparr.Infrastructure.Features.Notifications.Notifiarr;

/// <summary>
/// Text content and formatting for a Notifiarr notification.
/// </summary>
public class Text
{
    /// <summary>
    /// Notification title.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Icon name or emoji code for the notification.
    /// </summary>
    public string Icon { get; set; } = string.Empty;

    /// <summary>
    /// Main message content.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Secondary description text.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Additional fields displayed in the notification.
    /// </summary>
    public List<Field> Fields { get; set; } = new List<Field>();

    /// <summary>
    /// Footer text shown at the bottom.
    /// </summary>
    public string Footer { get; set; } = string.Empty;
}
