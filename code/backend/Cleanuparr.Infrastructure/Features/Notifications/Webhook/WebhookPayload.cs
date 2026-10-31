using System.Text.Json.Serialization;
using Cleanuparr.Domain.Enums;

namespace Cleanuparr.Infrastructure.Features.Notifications.Webhook;

/// <summary>
/// The JSON body Cleanuparr sends to a webhook.
/// Null values stay in the output so receivers see every key.
/// </summary>
public sealed class WebhookPayload
{
    [JsonConverter(typeof(CamelCaseEnumConverter<NotificationEventType>))]
    public NotificationEventType EventType { get; init; }

    public string Title { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    [JsonConverter(typeof(CamelCaseEnumConverter<EventSeverity>))]
    public EventSeverity Severity { get; init; }

    /// <summary>
    /// Event details keyed by camelCase label.
    /// Values stay strings, as the event built them.
    /// </summary>
    public Dictionary<string, string> Data { get; init; } = new();

    /// <summary>
    /// A plain image URL. Cleanuparr never fetches it.
    /// </summary>
    public string? Image { get; init; }

    public DateTimeOffset Timestamp { get; init; }
}
