using Cleanuparr.Domain.Enums;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Cleanuparr.Persistence.Converters;

/// <summary>
/// Stores a nullable enum as lowercase text, reading an unrecognised value as null.
/// Only safe where null already means absent.
/// A value from a newer version degrades to a blank field.
/// </summary>
public class NullableLowercaseEnumConverter<TEnum> : ValueConverter<TEnum?, string?>
    where TEnum : struct, Enum
{
    public NullableLowercaseEnumConverter() : base(
        v => v == null ? null : v.Value.ToString().ToLowerInvariant(),
        v => Parse(v))
    {
    }

    private static TEnum? Parse(string? value) =>
        LowercaseEnumName.TryParse(value, out TEnum parsed) ? parsed : null;
}
