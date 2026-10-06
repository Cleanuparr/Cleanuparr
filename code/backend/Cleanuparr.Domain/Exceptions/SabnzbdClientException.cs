namespace Cleanuparr.Domain.Exceptions;

public class SabnzbdClientException : Exception
{
    public SabnzbdClientException(string message) : base(message)
    {
    }

    public SabnzbdClientException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
