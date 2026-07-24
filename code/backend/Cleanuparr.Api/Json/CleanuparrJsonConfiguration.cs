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
}
