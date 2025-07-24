namespace Cleanuparr.Domain.Exceptions;

/// <summary>
/// Exception thrown when µTorrent authentication fails
/// </summary>
public class UTorrentAuthenticationException : UTorrentException
{
    public UTorrentAuthenticationException(string message) : base(message)
    {
    }
    
    public UTorrentAuthenticationException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
