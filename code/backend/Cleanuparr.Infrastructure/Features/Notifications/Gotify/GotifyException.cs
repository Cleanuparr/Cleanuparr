namespace Cleanuparr.Infrastructure.Features.Notifications.Gotify;

public class GotifyException : Exception
{
    public GotifyException(string message) : base(message)
    {
    }

    public GotifyException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
