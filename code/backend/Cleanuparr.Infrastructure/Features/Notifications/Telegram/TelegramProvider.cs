using System.Text;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Notifications.Models;
using Cleanuparr.Persistence.Models.Configuration.Notification;

namespace Cleanuparr.Infrastructure.Features.Notifications.Telegram;

public sealed class TelegramProvider : NotificationProviderBase<TelegramConfig>
{
    private readonly ITelegramProxy _proxy;

    public TelegramProvider(
        string name,
        NotificationProviderType type,
        TelegramConfig config,
        ITelegramProxy proxy
    ) : base(name, type, config)
    {
        _proxy = proxy;
    }

    public override async Task SendNotificationAsync(NotificationContext context)
    {
        var payload = BuildPayload(context);
        await _proxy.SendNotification(payload, Config.BotToken);
    }

    private TelegramPayload BuildPayload(NotificationContext context)
    {
        return new TelegramPayload
        {
            ChatId = Config.ChatId.Trim(),
            MessageThreadId = ParseTopicId(Config.TopicId),
            DisableNotification = Config.SendSilently,
            Text = BuildMessage(context)
        };
    }

    private static string BuildMessage(NotificationContext context)
    {
        var builder = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(context.Title))
        {
            builder.AppendLine(context.Title.Trim());
            builder.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(context.Description))
        {
            builder.AppendLine(context.Description.Trim());
        }

        if (context.Data.Any())
        {
            builder.AppendLine();
            foreach ((string key, string value) in context.Data)
            {
                builder.AppendLine($"{key}: {value}");
            }
        }

        return builder.ToString().Trim();
    }

    private static int? ParseTopicId(string? topicId)
    {
        return int.TryParse(topicId, out int parsed) ? parsed : null;
    }
}
