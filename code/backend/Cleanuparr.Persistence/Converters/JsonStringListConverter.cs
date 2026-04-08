using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Text.Json;

namespace Cleanuparr.Persistence.Converters;

public class JsonStringListConverter : ValueConverter<List<string>, string>
{
    public JsonStringListConverter() : base(
        v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
        v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>())
    {
    }
}
