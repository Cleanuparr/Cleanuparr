using Cleanuparr.Domain.Enums;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Cleanuparr.Persistence.Converters;

/// <summary>
/// Stores an enum as lowercase text, reading an unrecognised value as the Unknown member.
/// For columns that say what a row is, so a rollback loads the row instead of throwing.
/// Writing it throws: the column keeps the text a newer version wrote.
/// </summary>
public class SentinelLowercaseEnumConverter<TEnum> : ValueConverter<TEnum, string>
    where TEnum : struct, Enum
{
    private static readonly TEnum Sentinel = Enum.Parse<TEnum>(EnumSentinel.Unknown);

    public SentinelLowercaseEnumConverter() : base(
        v => Write(v),
        v => Read(v))
    {
    }

    private static string Write(TEnum value) =>
        EnumSentinel.IsUnknown(value)
            ? throw new InvalidOperationException(
                $"{typeof(TEnum).Name}.{EnumSentinel.Unknown} is not a database value: filter it in memory.")
            : value.ToString().ToLowerInvariant();

    private static TEnum Read(string? value) =>
        LowercaseEnumName.TryParse(value, out TEnum parsed) ? parsed : Sentinel;
}
