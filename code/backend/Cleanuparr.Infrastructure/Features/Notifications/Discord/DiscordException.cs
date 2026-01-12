namespace Cleanuparr.Infrastructure.Features.Notifications.Discord;

public class DiscordException : Exception
{
    public DiscordException(string message) : base(message)
    {
    }

    public DiscordException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
