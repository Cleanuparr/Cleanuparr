using Cleanuparr.Domain.Entities.UTorrent.Request;

namespace Cleanuparr.Infrastructure.Features.DownloadClient.UTorrent;

/// <summary>
/// Factory for creating type-safe UTorrent API requests
/// Provides specific methods for each supported API endpoint
/// </summary>
public static class UTorrentRequestFactory
{
    /// <summary>
    /// Creates a request to get the list of all torrents
    /// </summary>
    /// <returns>Request for torrent list API call</returns>
    public static UTorrentRequest CreateTorrentListRequest()
    {
        return UTorrentRequest.Create("list=1", string.Empty);
    }

    /// <summary>
    /// Creates a request to get files for a specific torrent
    /// </summary>
    /// <param name="hash">Torrent hash</param>
    /// <returns>Request for file list API call</returns>
    public static UTorrentRequest CreateFileListRequest(string hash)
    {
        return UTorrentRequest.Create("action=getfiles", string.Empty)
            .WithParameter("hash", hash);
    }

    /// <summary>
    /// Creates a request to get properties for a specific torrent
    /// </summary>
    /// <param name="hash">Torrent hash</param>
    /// <returns>Request for properties API call</returns>
    public static UTorrentRequest CreatePropertiesRequest(string hash)
    {
        return UTorrentRequest.Create("action=getprops", string.Empty)
            .WithParameter("hash", hash);
    }

    /// <summary>
    /// Creates a request to get all labels
    /// </summary>
    /// <returns>Request for label list API call</returns>
    public static UTorrentRequest CreateLabelListRequest()
    {
        return UTorrentRequest.Create("list=1", string.Empty);
    }

    /// <summary>
    /// Creates a request to remove a torrent and its data
    /// </summary>
    /// <param name="hash">Torrent hash</param>
    /// <returns>Request for remove torrent with data API call</returns>
    public static UTorrentRequest CreateRemoveTorrentWithDataRequest(string hash)
    {
        return UTorrentRequest.Create("action=removedatatorrent", string.Empty)
            .WithParameter("hash", hash);
    }

    /// <summary>
    /// Creates a request to set file priorities for a torrent
    /// </summary>
    /// <param name="hash">Torrent hash</param>
    /// <param name="fileIndexes"></param>
    /// <param name="filePriority"></param>
    /// <returns>Request for set file priorities API call</returns>
    public static UTorrentRequest CreateSetFilePrioritiesRequest(string hash, List<int> fileIndexes, int filePriority)
    {
        var request = UTorrentRequest.Create("action=setprio", string.Empty)
            .WithParameter("hash", hash)
            .WithParameter("p", filePriority.ToString());

        foreach (int fileIndex in fileIndexes)
        {
            request.WithParameter("f", fileIndex.ToString());
        }

        return request;
    }

    /// <summary>
    /// Creates a request to set a torrent's label
    /// </summary>
    /// <param name="hash">Torrent hash</param>
    /// <param name="label">Label to set</param>
    /// <returns>Request for set label API call</returns>
    public static UTorrentRequest CreateSetLabelRequest(string hash, string label)
    {
        return UTorrentRequest.Create("action=setprops", string.Empty)
            .WithParameter("hash", hash)
            .WithParameter("s", "label")
            .WithParameter("v", label);
    }
}
