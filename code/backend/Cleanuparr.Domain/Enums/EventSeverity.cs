namespace Cleanuparr.Domain.Enums;

public enum EventSeverity
{
    Test,
    Information,
    Warning,
    Important,
    Error,

    /// <summary>Text this build does not know.</summary>
    Unknown = EnumSentinel.UnknownValue,
}
