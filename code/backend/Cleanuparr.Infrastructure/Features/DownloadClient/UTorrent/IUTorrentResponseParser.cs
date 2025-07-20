using Cleanuparr.Domain.Entities.UTorrent.Response;

namespace Cleanuparr.Infrastructure.Features.DownloadClient.UTorrent;

/// <summary>
/// Interface for parsing µTorrent API responses
/// Provides endpoint-specific parsing methods for different response types
/// </summary>
public interface IUTorrentResponseParser
{
    /// <summary>
    /// Parses a torrent list response from JSON
    /// </summary>
    /// <param name="json">Raw JSON response from the API</param>
    /// <returns>Parsed torrent list response</returns>
    TorrentListResponse ParseTorrentList(string json);

    /// <summary>
    /// Parses a file list response from JSON
    /// </summary>
    /// <param name="json">Raw JSON response from the API</param>
    /// <returns>Parsed file list response</returns>
    FileListResponse ParseFileList(string json);

    /// <summary>
    /// Parses a properties response from JSON
    /// </summary>
    /// <param name="json">Raw JSON response from the API</param>
    /// <returns>Parsed properties response</returns>
    PropertiesResponse ParseProperties(string json);

    /// <summary>
    /// Parses a label list response from JSON
    /// </summary>
    /// <param name="json">Raw JSON response from the API</param>
    /// <returns>Parsed label list response</returns>
    LabelListResponse ParseLabelList(string json);
}
