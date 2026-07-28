using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Cleanuparr.Infrastructure.Json;

/// <summary>
/// The options that the application uses for JSON.
/// </summary>
public static class CleanuparrJsonOptions
{
    /// <summary>
    /// The options to read a response from an external API.
    /// These options do not make a property necessary, because an external API can omit a field.
    /// A field that is absent or null gets the default value of the property.
    /// </summary>
    public static readonly JsonSerializerOptions ExternalApiRead = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        RespectRequiredConstructorParameters = false,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver
        {
            Modifiers = { IgnoreRequiredProperties, IgnoreNullForNonNullable },
        },
    };

    /// <summary>
    /// The options to write a request to an external API.
    /// </summary>
    public static readonly JsonSerializerOptions Outbound = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// The options to write a payload for a notification provider.
    /// </summary>
    public static readonly JsonSerializerOptions Notification = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// The options to write a payload for a notification provider that needs the null fields.
    /// </summary>
    public static readonly JsonSerializerOptions NotificationIncludeNulls = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static void IgnoreRequiredProperties(JsonTypeInfo typeInfo)
    {
        if (typeInfo.Kind is not JsonTypeInfoKind.Object)
        {
            return;
        }

        foreach (JsonPropertyInfo property in typeInfo.Properties)
        {
            property.IsRequired = false;
        }
    }

    /// <summary>
    /// Keeps the default value of a property that is not nullable if the JSON field is null.
    /// </summary>
    /// <param name="typeInfo">The metadata of the type to change.</param>
    public static void IgnoreNullForNonNullable(JsonTypeInfo typeInfo)
    {
        if (typeInfo.Kind is not JsonTypeInfoKind.Object)
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
