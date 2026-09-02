namespace Cleanuparr.Domain.Exceptions;

/// <summary>
/// Thrown when a value read from text this build does not know reaches the database.
/// </summary>
public class UnknownEnumValueException : InvalidOperationException
{
    public UnknownEnumValueException(string message) : base(message)
    {
    }

    public UnknownEnumValueException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
