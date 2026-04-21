using System.Text.Json.Serialization;

namespace Cleanuparr.Domain.Enums;

/// <summary>
/// Sorting fields available for custom format score listings.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CfScoresSortBy
{
    /// <summary>Sort by item title.</summary>
    Title,
    /// <summary>Sort by the item's current custom format score.</summary>
    CurrentScore,
    /// <summary>Sort by the quality profile's configured cutoff score.</summary>
    CutoffScore,
    /// <summary>Sort by quality profile name.</summary>
    QualityProfile,
    /// <summary>Sort by the timestamp of the last score sync.</summary>
    LastSyncedAt,
    /// <summary>Sort by the timestamp of the most recent score upgrade.</summary>
    LastUpgradedAt,
}
