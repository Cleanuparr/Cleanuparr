namespace Cleanuparr.Infrastructure.Features.Notifications.Ntfy;

public sealed class NtfyException : Exception
{
    public NtfyException(string message) : base(message)
    {
    }

    public NtfyException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
