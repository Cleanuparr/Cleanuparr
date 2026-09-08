namespace Cleanuparr.Shared.Helpers;

/// <summary>
/// Helpers for making user-controlled values safe to write to the logs.
/// </summary>
public static class LogSanitizer
{
    /// <summary>
    /// Strips control characters from a user-controlled value before it reaches the logs.
    /// Sinks render the message verbatim, so a line break would let a caller forge whole entries
    /// and an escape sequence would let one repaint a terminal tailing the log.
    /// </summary>
    public static string SanitizeForLog(this string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        string stripped = new string(value
            .Where(character => !char.IsControl(character) || character is '\r' or '\n')
            .ToArray());

        return stripped.Replace("\r", string.Empty).Replace("\n", string.Empty);
    }
}
