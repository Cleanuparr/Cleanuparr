namespace Cleanuparr.Domain.Exceptions;

public sealed class LazyLibrarianException : Exception
{
    public LazyLibrarianException(string message) : base(message)
    {
    }

    public LazyLibrarianException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
