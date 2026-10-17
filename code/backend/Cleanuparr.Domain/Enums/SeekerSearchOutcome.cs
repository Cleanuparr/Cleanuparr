using System.Text.Json.Serialization;

namespace Cleanuparr.Domain.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SeekerSearchOutcome
{
    Grabbed,
    NothingGrabbed,
    Failed,
}
