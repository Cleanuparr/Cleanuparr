using Newtonsoft.Json;

namespace Cleanuparr.Infrastructure.Features.DownloadClient.Deluge.Extensions;

internal static class DelugeExtensions
{
    public static List<String?> GetAllJsonPropertyFromType(this Type t)
    {
        var type = typeof(JsonPropertyAttribute);
        var props = t.GetProperties()
            .Where(prop => Attribute.IsDefined(prop, type))
            .ToList();
        
        return props
            .Select(x => x.GetCustomAttributes(type, true).Single())
            .Cast<JsonPropertyAttribute>()
            .Select(x => x.PropertyName)
            .ToList();
    }
}