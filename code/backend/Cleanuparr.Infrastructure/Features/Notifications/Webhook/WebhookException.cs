namespace Cleanuparr.Infrastructure.Features.Notifications.Webhook;

public class WebhookException : Exception
{
    public WebhookException(string message) : base(message)
    {
    }

    public WebhookException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
