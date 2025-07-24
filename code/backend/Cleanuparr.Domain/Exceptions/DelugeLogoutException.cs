namespace Cleanuparr.Domain.Exceptions;

public sealed class DelugeLogoutException : DelugeClientException
{
    public DelugeLogoutException() : base("logout failed")
    {
    }
}