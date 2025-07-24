namespace Cleanuparr.Domain.Exceptions;

public class UTorrentException : Exception
{
    public UTorrentException(string message) : base(message)
    {
    }
    
    public UTorrentException(string message, Exception innerException) : base(message, innerException)
    {
    }
}