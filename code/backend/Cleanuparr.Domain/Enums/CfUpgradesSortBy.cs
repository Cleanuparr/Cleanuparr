using System.Text.Json.Serialization;

namespace Cleanuparr.Domain.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CfUpgradesSortBy
{
    UpgradedAt,
    Title,
    NewScore,
    PreviousScore,
    ScoreDelta,
    CutoffScore,
}
