namespace Cleanuparr.Domain.Enums;

public static class LowercaseEnumName
{
    /// <summary>
    /// Whether a stored value looks like an enum member name rather than a number.
    /// Enum.TryParse accepts numeric strings and yields undefined values.
    /// Older migrations left integers in enum columns.
    /// </summary>
    public static bool IsName(string? value) =>
        !string.IsNullOrWhiteSpace(value) && !char.IsAsciiDigit(value[0]) && value[0] != '-';

    /// <summary>
    /// Parses a stored member name, rejecting anything that is not one.
    /// </summary>
    public static bool TryParse<TEnum>(string? value, out TEnum parsed)
        where TEnum : struct, Enum
    {
        if (IsName(value) && Enum.TryParse(value, true, out parsed))
        {
            return true;
        }

        parsed = default;
        return false;
    }
}
