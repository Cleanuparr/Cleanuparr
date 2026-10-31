using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cleanuparr.Infrastructure.Features.Notifications.Webhook;

/// <summary>
/// Writes an enum as its camelCase member name.
/// A <see cref="JsonConverterAttribute"/> cannot pass a naming policy to <see cref="JsonStringEnumConverter{TEnum}"/>.
/// </summary>
public sealed class CamelCaseEnumConverter<TEnum> : JsonStringEnumConverter<TEnum>
    where TEnum : struct, Enum
{
    public CamelCaseEnumConverter() : base(JsonNamingPolicy.CamelCase)
    {
    }
}
