namespace Cleanuparr.Domain.Exceptions;

public class RTorrentClientException : Exception
{
    public RTorrentClientException(string message) : base(message)
    {
    }

    public RTorrentClientException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
