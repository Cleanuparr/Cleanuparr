using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

using Cleanuparr.Shared.Attributes;
using Cleanuparr.Shared.Helpers;

namespace Cleanuparr.Api.Json;

/// <summary>
/// JSON type info resolver that masks properties decorated with <see cref="SensitiveDataAttribute"/>
/// by replacing their serialized values with the appropriate placeholder during serialization.
/// </summary>
public sealed class SensitiveDataResolver : IJsonTypeInfoResolver
{
    private readonly IJsonTypeInfoResolver _innerResolver;

    public SensitiveDataResolver(IJsonTypeInfoResolver innerResolver)
    {
        _innerResolver = innerResolver;
    }

    public JsonTypeInfo? GetTypeInfo(Type type, JsonSerializerOptions options)
    {
        var typeInfo = _innerResolver.GetTypeInfo(type, options);

        if (typeInfo?.Kind != JsonTypeInfoKind.Object)
            return typeInfo;

        foreach (var property in typeInfo.Properties)
        {
            if (property.AttributeProvider is not PropertyInfo propertyInfo)
                continue;

            var sensitiveAttr = propertyInfo.GetCustomAttribute<SensitiveDataAttribute>();
            if (sensitiveAttr is null)
                continue;

            ApplyMasking(property, sensitiveAttr.Type);
        }

        return typeInfo;
    }

    private static void ApplyMasking(JsonPropertyInfo property, SensitiveDataType maskType)
    {
        var originalGet = property.Get;
        if (originalGet is null)
        {
            return;
        }

        property.Get = maskType switch
        {
            SensitiveDataType.Full => obj =>
            {
                var value = originalGet(obj);
                return value is string ? SensitiveDataHelper.Placeholder : value;
            },

            SensitiveDataType.AppriseUrl => obj =>
            {
                var value = originalGet(obj);
                return value is string s ? SensitiveDataHelper.MaskAppriseUrls(s) : value;
            },

            _ => originalGet,
        };
    }
}
