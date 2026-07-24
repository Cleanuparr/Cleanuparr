namespace Cleanuparr.Domain.Exceptions;

public class DelugeClientException : Exception
{
    public DelugeClientException(string message) : base(message)
    {
    }

    public DelugeClientException(string message, Exception innerException) : base(message, innerException)
    {
    }
}