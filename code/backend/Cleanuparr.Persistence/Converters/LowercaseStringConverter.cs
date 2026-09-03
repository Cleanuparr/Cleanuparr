using Cleanuparr.Domain.Helpers;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Cleanuparr.Persistence.Converters;

/// <summary>
/// Stores a hash as lowercase text, and lowercases the query parameters compared against it.
/// That makes case-sensitive comparisons match whatever casing the caller happens to hold.
/// Null stays null, so the converter also fits an optional hash column.
/// </summary>
public class LowercaseStringConverter : ValueConverter<string?, string?>
{
    public LowercaseStringConverter() : base(
        v => v == null ? null : DownloadHash.Normalize(v),
        v => v)
    {
    }
}
