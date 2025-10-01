using System.Text.Json.Serialization;

namespace Cleanuparr.Infrastructure.Models;

public sealed record AppStatus(
    [property: JsonPropertyName("currentVersion")] string? CurrentVersion,
    [property: JsonPropertyName("latestVersion")] string? LatestVersion
)
{
    public static readonly AppStatus Empty = new(null, null);
}
