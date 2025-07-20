namespace Cleanuparr.Domain.Exceptions;

/// <summary>
/// Exception thrown when µTorrent response parsing fails
/// </summary>
public class UTorrentParsingException : UTorrentException
{
    /// <summary>
    /// The raw response that failed to parse
    /// </summary>
    public string RawResponse { get; }

    public UTorrentParsingException(string message, string rawResponse) : base(message)
    {
        RawResponse = rawResponse;
    }
    
    public UTorrentParsingException(string message, string rawResponse, Exception innerException) : base(message, innerException)
    {
        RawResponse = rawResponse;
    }
}
