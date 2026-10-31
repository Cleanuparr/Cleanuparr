using System.Text.Json.Serialization;

namespace Cleanuparr.Domain.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum QueueRuleContentType
{
    All,
    New,
    Upgrade
}
