namespace Cleanuparr.Domain.Exceptions;

/// <summary>
/// Thrown when a notification provider connectivity test fails. Maps to HTTP 400 so a failed
/// test is reported as a bad request rather than an unexpected server error.
/// </summary>
public sealed class NotificationTestException : Exception
{
    public NotificationTestException(string message) : base(message)
    {
    }

    public NotificationTestException(string message, Exception inner) : base(message, inner)
    {
    }
}
