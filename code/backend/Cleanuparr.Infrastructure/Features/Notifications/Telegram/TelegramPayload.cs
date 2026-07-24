using System.Text.Json.Serialization;

namespace Cleanuparr.Infrastructure.Features.Notifications.Telegram;

public sealed class TelegramPayload
{
    [JsonPropertyName("chat_id")]
    public string ChatId { get; init; } = string.Empty;

    [JsonPropertyName("text")]
    public string Text { get; init; } = string.Empty;

    [JsonPropertyName("photo")]
    public string? PhotoUrl { get; init; }

    [JsonPropertyName("message_thread_id")]
    public int? MessageThreadId { get; init; }

    [JsonPropertyName("disable_notification")]
    public bool DisableNotification { get; init; }
}
