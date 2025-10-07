using Cleanuparr.Domain.Entities.UTorrent.Response;
using Cleanuparr.Domain.Exceptions;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Cleanuparr.Infrastructure.Features.DownloadClient.UTorrent;

/// <summary>
/// Implementation of µTorrent response parser
/// Handles endpoint-specific parsing of API responses with proper error handling
/// </summary>
public class UTorrentResponseParser : IUTorrentResponseParser
{
    private readonly ILogger<UTorrentResponseParser> _logger;

    public UTorrentResponseParser(ILogger<UTorrentResponseParser> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public TorrentListResponse ParseTorrentList(string json)
    {
        try
        {
            var response = JsonConvert.DeserializeObject<TorrentListResponse>(json);
            
            if (response == null)
            {
                throw new UTorrentParsingException("Failed to deserialize torrent list response", json);
            }

            // Parse torrents
            if (response.TorrentsRaw != null)
            {
                foreach (var data in response.TorrentsRaw)
                {
                    if (data is { Length: >= 27 })
                    {
                        response.Torrents.Add(new UTorrentItem
                        {
                            Hash = data[0].ToString() ?? string.Empty,
                            Status = Convert.ToInt32(data[1]),
                            Name = data[2].ToString() ?? string.Empty,
                            Size = Convert.ToInt64(data[3]),
                            Progress = Convert.ToInt32(data[4]),
                            Downloaded = Convert.ToInt64(data[5]),
                            Uploaded = Convert.ToInt64(data[6]),
                            RatioRaw = Convert.ToInt32(data[7]),
                            UploadSpeed = Convert.ToInt32(data[8]),
                            DownloadSpeed = Convert.ToInt32(data[9]),
                            ETA = Convert.ToInt32(data[10]),
                            Label = data[11].ToString() ?? string.Empty,
                            PeersConnected = Convert.ToInt32(data[12]),
                            PeersInSwarm = Convert.ToInt32(data[13]),
                            SeedsConnected = Convert.ToInt32(data[14]),
                            SeedsInSwarm = Convert.ToInt32(data[15]),
                            Availability = Convert.ToInt32(data[16]),
                            QueueOrder = Convert.ToInt32(data[17]),
                            Remaining = Convert.ToInt64(data[18]),
                            DownloadUrl = data[19].ToString() ?? string.Empty,
                            RssFeedUrl = data[20].ToString() ?? string.Empty,
                            StatusMessage = data[21].ToString() ?? string.Empty,
                            StreamId = data[22].ToString() ?? string.Empty,
                            DateAdded = Convert.ToInt64(data[23]),
                            DateCompleted = Convert.ToInt64(data[24]),
                            AppUpdateUrl = data[25].ToString() ?? string.Empty,
                            SavePath = data[26].ToString() ?? string.Empty
                        });
                    }
                }
            }

            // Parse labels
            if (response.LabelsRaw != null)
            {
                foreach (var labelData in response.LabelsRaw)
                {
                    if (labelData is { Length: > 0 })
                    {
                        var labelName = labelData[0].ToString();
                        
                        if (!string.IsNullOrEmpty(labelName))
                        {
                            response.Labels.Add(labelName);
                        }
                    }
                }
            }

            return response;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse torrent list JSON response");
            throw new UTorrentParsingException($"Failed to parse torrent list response: {ex.Message}", json, ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error parsing torrent list response");
            throw new UTorrentParsingException($"Unexpected error parsing torrent list response: {ex.Message}", json, ex);
        }
    }

    /// <inheritdoc/>
    public FileListResponse ParseFileList(string json)
    {
        try
        {
            var rawResponse = JsonConvert.DeserializeObject<FileListResponse>(json);
            
            if (rawResponse == null)
            {
                throw new UTorrentParsingException("Failed to deserialize file list response", json);
            }

            var response = new FileListResponse();

            // Parse files from the nested array structure
            if (rawResponse.FilesRaw is { Length: >= 2 })
            {
                response.Hash = rawResponse.FilesRaw[0].ToString() ?? string.Empty;
                
                if (rawResponse.FilesRaw[1] is JArray jArray)
                {
                    foreach (var jToken in jArray)
                    {
                        if (jToken is JArray fileArray)
                        {
                            var fileData = fileArray.ToObject<object[]>() ?? Array.Empty<object>();
                            
                            if (fileData.Length >= 4)
                            {
                                response.Files.Add(new UTorrentFile
                                {
                                    Name = fileData[0]?.ToString() ?? string.Empty,
                                    Size = Convert.ToInt64(fileData[1]),
                                    Downloaded = Convert.ToInt64(fileData[2]),
                                    Priority = Convert.ToInt32(fileData[3]),
                                });
                            }
                        }
                    }
                }
            }

            return response;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse file list JSON response");
            throw new UTorrentParsingException($"Failed to parse file list response: {ex.Message}", json, ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error parsing file list response");
            throw new UTorrentParsingException($"Unexpected error parsing file list response: {ex.Message}", json, ex);
        }
    }

    /// <inheritdoc/>
    public PropertiesResponse ParseProperties(string json)
    {
        try
        {
            var rawResponse = JsonConvert.DeserializeObject<PropertiesResponse>(json);
            
            if (rawResponse == null)
            {
                throw new UTorrentParsingException("Failed to deserialize properties response", json);
            }

            var response = new PropertiesResponse();

            // Parse properties from the array structure
            if (rawResponse.PropertiesRaw is { Length: > 0 })
            {
                response.Properties = JsonConvert.DeserializeObject<UTorrentProperties>(rawResponse.PropertiesRaw.FirstOrDefault()?.ToString());
            }

            return response;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse properties JSON response");
            throw new UTorrentParsingException($"Failed to parse properties response: {ex.Message}", json, ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error parsing properties response");
            throw new UTorrentParsingException($"Unexpected error parsing properties response: {ex.Message}", json, ex);
        }
    }

    /// <inheritdoc/>
    public LabelListResponse ParseLabelList(string json)
    {
        try
        {
            var response = JsonConvert.DeserializeObject<LabelListResponse>(json);
            
            if (response == null)
            {
                throw new UTorrentParsingException("Failed to deserialize label list response", json);
            }

            // Parse labels
            if (response.LabelsRaw != null)
            {
                foreach (var labelData in response.LabelsRaw)
                {
                    if (labelData is { Length: > 0 })
                    {
                        var labelName = labelData[0]?.ToString();
                        if (!string.IsNullOrEmpty(labelName))
                        {
                            response.Labels.Add(labelName);
                        }
                    }
                }
            }

            return response;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse label list JSON response");
            throw new UTorrentParsingException($"Failed to parse label list response: {ex.Message}", json, ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error parsing label list response");
            throw new UTorrentParsingException($"Unexpected error parsing label list response: {ex.Message}", json, ex);
        }
    }
}
