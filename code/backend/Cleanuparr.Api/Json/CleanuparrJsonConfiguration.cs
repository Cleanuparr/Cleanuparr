using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Cleanuparr.Api.Json;

public static class CleanuparrJsonConfiguration
{
    public static void ConfigureCore(JsonSerializerOptions options)
    {
        options.PropertyNameCaseInsensitive = true;
        options.Converters.Add(new JsonStringEnumConverter());
        options.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    }

    public static void ConfigureApi(JsonSerializerOptions options)
    {
        ConfigureCore(options);
        options.TypeInfoResolver = new SensitiveDataResolver(
            options.TypeInfoResolver ?? new DefaultJsonTypeInfoResolver());
    }

    public static void ConfigureApiInbound(JsonSerializerOptions options)
    {
        ConfigureApi(options);
        options.TypeInfoResolver = options.TypeInfoResolver!.WithAddedModifier(IgnoreNullForNonNullable);
    }

    private static void IgnoreNullForNonNullable(JsonTypeInfo typeInfo)
    {
        if (typeInfo.Kind != JsonTypeInfoKind.Object)
        {
            return;
        }

        foreach (JsonPropertyInfo property in typeInfo.Properties)
        {
            if (property.Set is null || property.IsSetNullable)
            {
                continue;
            }

            Action<object, object?> originalSet = property.Set;
            property.Set = (obj, value) =>
            {
                if (value is not null)
                {
                    originalSet(obj, value);
                }
            };
        }
    }
}
