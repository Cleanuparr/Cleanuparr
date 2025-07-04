namespace Cleanuparr.Infrastructure.Features.Notifications.Apprise;

public class AppriseException : Exception
{
    public AppriseException(string message) : base(message)
    {
    }

    public AppriseException(string message, Exception innerException) : base(message, innerException)
    {
    }
}