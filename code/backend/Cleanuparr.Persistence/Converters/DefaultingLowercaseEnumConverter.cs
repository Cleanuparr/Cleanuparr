using Cleanuparr.Domain.Enums;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Cleanuparr.Persistence.Converters;

/// <summary>
/// Stores an enum as lowercase text, reading an unrecognised value as a fallback.
/// For a setting whose row has to exist, since hiding it leaves the app unconfigured.
/// The fallback is the value declared on the property.
/// </summary>
public class DefaultingLowercaseEnumConverter<TEnum> : ValueConverter<TEnum, string>
    where TEnum : struct, Enum
{
    public DefaultingLowercaseEnumConverter(TEnum fallback) : base(
        v => v.ToString().ToLowerInvariant(),
        v => Parse(v, fallback))
    {
    }

    private static TEnum Parse(string? value, TEnum fallback) =>
        LowercaseEnumName.TryParse(value, out TEnum parsed) ? parsed : fallback;
}
