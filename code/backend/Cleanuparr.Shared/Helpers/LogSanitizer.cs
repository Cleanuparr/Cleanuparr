namespace Cleanuparr.Shared.Helpers;

/// <summary>
/// Helpers for making user-controlled values safe to write to the logs.
/// </summary>
public static class LogSanitizer
{
    /// <summary>
    /// Strips line breaks from a user-controlled value before it reaches the logs.
    /// Sinks render the message verbatim, so a line break in the value would let a caller forge log entries.
    /// </summary>
    public static string SanitizeForLog(this string? value)
    {
        return value is null
            ? string.Empty
            : value.Replace("\r", string.Empty).Replace("\n", string.Empty);
    }
}
