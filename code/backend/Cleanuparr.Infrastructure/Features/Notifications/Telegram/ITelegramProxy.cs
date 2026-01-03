namespace Cleanuparr.Infrastructure.Features.Notifications.Telegram;

public interface ITelegramProxy
{
    Task SendNotification(TelegramPayload payload, string botToken);
}
