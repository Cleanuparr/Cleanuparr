namespace Cleanuparr.Infrastructure.Features.Notifications.Notifiarr;

/// <summary>
/// Root notification object sent to Notifiarr.
/// </summary>
public class NotifiarrNotification
{
    /// <summary>
    /// Whether to update an existing notification instead of creating new.
    /// </summary>
    public bool Update { get; set; }

    /// <summary>
    /// Application name reported to Notifiarr.
    /// </summary>
    public string Name => "Cleanuparr";

    /// <summary>
    /// Event type identifier for classification by Notifiarr.
    /// </summary>
    public int? Event { get; set; }
}
