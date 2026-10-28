namespace Cleanuparr.Domain.Enums;

public enum NotificationProviderType
{
    Notifiarr,
    Apprise,
    Ntfy,
    Pushover,
    Telegram,
    Discord,
    Gotify,
    Webhook,

    /// <summary>
    /// Text this build does not know.
    /// </summary>
    Unknown = EnumSentinel.UnknownValue,
}
