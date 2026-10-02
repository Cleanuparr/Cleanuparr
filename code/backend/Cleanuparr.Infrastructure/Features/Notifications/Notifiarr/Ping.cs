namespace Cleanuparr.Infrastructure.Features.Notifications.Notifiarr;

/// <summary>
/// Discord user and role mentions for Notifiarr notifications.
/// </summary>
public class Ping
{
    /// <summary>
    /// Discord user ID to mention in the notification.
    /// </summary>
    public string PingUser { get; set; }

    /// <summary>
    /// Discord role ID to mention in the notification.
    /// </summary>
    public string PingRole { get; set; }
}
