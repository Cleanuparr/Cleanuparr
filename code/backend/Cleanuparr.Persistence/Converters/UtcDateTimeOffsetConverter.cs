using System.Globalization;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Cleanuparr.Persistence.Converters;

/// <summary>
/// Persists <see cref="DateTimeOffset"/> as a sortable, offset-less UTC string.
///
/// EF Core's SQLite provider refuses <c>DateTimeOffset</c> in <c>ORDER BY</c>/comparisons, so we
/// store the value as TEXT instead. The chosen format (<c>yyyy-MM-dd HH:mm:ss.fffffff</c>, UTC, no
/// offset) is byte-for-byte compatible with the legacy <c>DateTime</c> storage, so existing rows
/// need no data migration and keep sorting chronologically (everything is UTC, fixed width).
/// </summary>
public class UtcDateTimeOffsetConverter : ValueConverter<DateTimeOffset, string>
{
    private const string Format = "yyyy-MM-dd HH:mm:ss.fffffff";

    public UtcDateTimeOffsetConverter() : base(
        v => v.UtcDateTime.ToString(Format, CultureInfo.InvariantCulture),
        v => DateTimeOffset.Parse(
            v,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal)
    ) {}
}
