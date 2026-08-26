namespace Cleanuparr.Domain.Enums;

/// <summary>
/// The member every persisted enum keeps for a value written by a newer version.
/// </summary>
public static class EnumSentinel
{
    /// <summary>
    /// Name of the member standing in for a value written by a newer version of the app.
    /// </summary>
    public const string Unknown = "Unknown";

    /// <summary>
    /// Value every sentinel member takes.
    /// Pinned so a member added later cannot shift it.
    /// </summary>
    public const int UnknownValue = 999;

    /// <summary>
    /// Whether a value came from text this build does not recognise.
    /// Never usable in a SQL predicate: the column holds the original text.
    /// Filter in memory.
    /// </summary>
    public static bool IsUnknown<TEnum>(TEnum value)
        where TEnum : struct, Enum =>
        value.ToString() == Unknown;

    /// <summary>
    /// Reads a stored member name, falling back to the sentinel.
    /// For raw SQL, which never passes through the EF value converters.
    /// Throws unless the enum declares the sentinel.
    /// </summary>
    public static TEnum ParseOrUnknown<TEnum>(string? value)
        where TEnum : struct, Enum =>
        LowercaseEnumName.TryParse(value, out TEnum parsed)
            ? parsed
            : Enum.Parse<TEnum>(Unknown);

    /// <summary>
    /// Reads a member a user asked to filter by, refusing the sentinel.
    /// The column keeps the text a newer version wrote.
    /// A query naming the sentinel throws.
    /// </summary>
    public static bool TryParseSelectable<TEnum>(string? value, out TEnum parsed)
        where TEnum : struct, Enum
    {
        if (LowercaseEnumName.TryParse(value, out parsed) && !IsUnknown(parsed))
        {
            return true;
        }

        parsed = default;
        return false;
    }

    /// <summary>
    /// Members a user can pick from, without the sentinel.
    /// </summary>
    public static List<TEnum> SelectableValues<TEnum>()
        where TEnum : struct, Enum =>
        Enum.GetValues<TEnum>().Where(value => !IsUnknown(value)).ToList();

    /// <summary>
    /// Member names a user can pick from, without the sentinel.
    /// </summary>
    public static List<string> SelectableNames<TEnum>()
        where TEnum : struct, Enum =>
        Enum.GetNames<TEnum>().Where(name => name != Unknown).ToList();
}
