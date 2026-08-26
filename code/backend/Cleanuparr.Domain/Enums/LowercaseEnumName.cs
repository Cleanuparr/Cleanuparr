namespace Cleanuparr.Domain.Enums;

/// <summary>
/// Reads the lowercase member names enum columns hold.
/// </summary>
public static class LowercaseEnumName
{
    /// <summary>
    /// Whether a stored value looks like an enum member name rather than a number.
    /// Enum.TryParse takes numbers, whitespace and a leading sign.
    /// </summary>
    public static bool IsName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        char first = value.AsSpan().TrimStart()[0];

        return !char.IsAsciiDigit(first) && first != '-' && first != '+';
    }

    /// <summary>
    /// Parses a stored member name, rejecting anything that is not one.
    /// </summary>
    public static bool TryParse<TEnum>(string? value, out TEnum parsed)
        where TEnum : struct, Enum
    {
        if (IsName(value) && Enum.TryParse(value, true, out parsed) && Enum.IsDefined(parsed))
        {
            return true;
        }

        parsed = default;
        return false;
    }
}
