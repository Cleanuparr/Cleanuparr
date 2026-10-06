using System.Text.Json.Serialization;

namespace Cleanuparr.Domain.Entities.Sabnzbd;

public sealed record SabnzbdCategoriesResponse
{
    [JsonPropertyName("categories")]
    public IReadOnlyList<string> Categories { get; init; } = [];
}
