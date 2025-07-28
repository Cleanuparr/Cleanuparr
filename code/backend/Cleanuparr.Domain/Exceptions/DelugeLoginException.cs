namespace Cleanuparr.Domain.Exceptions;

public sealed class DelugeLoginException : DelugeClientException
{
    public DelugeLoginException() : base("login failed")
    {
    }
}