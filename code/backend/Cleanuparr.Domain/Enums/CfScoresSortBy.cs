using System.Text.Json.Serialization;

namespace Cleanuparr.Domain.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CfScoresSortBy
{
    Title,
    CurrentScore,
    CutoffScore,
    QualityProfile,
    LastSyncedAt,
    LastUpgradedAt,
}
