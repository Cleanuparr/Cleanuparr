using Newtonsoft.Json;

namespace Cleanuparr.Infrastructure.Features.Notifications.Telegram;

public sealed class TelegramPayload
{
    [JsonProperty("chat_id")]
    public string ChatId { get; init; } = string.Empty;

    [JsonProperty("text")]
    public string Text { get; init; } = string.Empty;

    [JsonProperty("message_thread_id")]
    public int? MessageThreadId { get; init; }

    [JsonProperty("disable_notification")]
    public bool DisableNotification { get; init; }
}
