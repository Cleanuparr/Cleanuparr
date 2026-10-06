namespace Cleanuparr.Infrastructure.Features.Notifications.Apprise;

/// <summary>
/// Notification severity level for Apprise messages.
/// </summary>
public enum NotificationType
{
    /// <summary>
    /// Informational message.
    /// </summary>
    Info,

    /// <summary>
    /// Success notification.
    /// </summary>
    Success,

    /// <summary>
    /// Warning notification.
    /// </summary>
    Warning,

    /// <summary>
    /// Failure or error notification.
    /// </summary>
    Failure
}
