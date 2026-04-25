using System.Text.Json.Serialization;

namespace Cleanuparr.Domain.Enums;

/// <summary>
/// Sorting fields available for custom format score upgrades.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CfUpgradesSortBy
{
    /// <summary>Sort by the timestamp at which the upgrade was recorded.</summary>
    UpgradedAt,
    /// <summary>Sort by item title.</summary>
    Title,
    /// <summary>Sort by the score recorded after the upgrade.</summary>
    NewScore,
    /// <summary>Sort by the score recorded immediately before the upgrade.</summary>
    PreviousScore,
    /// <summary>Sort by the difference between the new and previous scores.</summary>
    ScoreDelta,
    /// <summary>Sort by the quality profile's configured cutoff score.</summary>
    CutoffScore,
}
