namespace Cleanuparr.Domain.Exceptions;

public class DelugeClientException : Exception
{
    public DelugeClientException(string message) : base(message)
    {
    }
}