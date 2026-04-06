using System.Text.Json.Serialization;

namespace Cleanuparr.Domain.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SeekerSearchReason
{
    Missing,
    QualityCutoffNotMet,
    CustomFormatScoreBelowCutoff,
    Replacement,
}
