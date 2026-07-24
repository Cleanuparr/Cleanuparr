using System.Text.Json.Serialization;

namespace Cleanuparr.Infrastructure.Features.DownloadClient.Deluge.Extensions;

internal static class DelugeExtensions
{
    public static List<string> GetAllJsonPropertyFromType(this Type t)
    {
        Type type = typeof(JsonPropertyNameAttribute);
        List<System.Reflection.PropertyInfo> props = t.GetProperties()
            .Where(prop => Attribute.IsDefined(prop, type))
            .ToList();

        return props
            .Select(x => x.GetCustomAttributes(type, true).Single())
            .Cast<JsonPropertyNameAttribute>()
            .Select(x => x.Name)
            .ToList();
    }
}
