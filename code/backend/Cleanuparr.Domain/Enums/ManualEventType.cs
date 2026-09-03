namespace Cleanuparr.Domain.Enums;

public enum ManualEventType
{
    RecurringDownload,
    SearchNotTriggered,

    /// <summary>
    /// Text this build does not know.
    /// </summary>
    Unknown = EnumSentinel.UnknownValue,
}
