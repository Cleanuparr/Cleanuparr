namespace Cleanuparr.Shared.Helpers;

/// <summary>
/// Helpers for making user-controlled values safe to write to the logs.
/// </summary>
public static class LogSanitizer
{
    /// <summary>
    /// Strips control characters from a user-controlled value before it reaches the logs.
    /// Sinks render the message verbatim, so a line break or an escape sequence in the value
    /// would let a caller forge log entries.
    /// </summary>
    public static string SanitizeForLog(this string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        if (!value.Any(char.IsControl))
        {
            return value;
        }

        return new string(value.Where(character => !char.IsControl(character)).ToArray());
    }
}
