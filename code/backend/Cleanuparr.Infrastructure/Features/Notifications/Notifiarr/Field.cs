namespace Cleanuparr.Infrastructure.Features.Notifications.Notifiarr;

/// <summary>
/// Field in a Notifiarr notification message.
/// </summary>
public class Field
{
    /// <summary>
    /// Field name or label.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Field content or value.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Whether the field displays inline with other fields.
    /// </summary>
    public bool Inline { get; set; }
}
