namespace Cleanuparr.Infrastructure.Features.Notifications.Pushover;

public sealed class PushoverException : Exception
{
    public PushoverException(string message) : base(message)
    {
    }

    public PushoverException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
