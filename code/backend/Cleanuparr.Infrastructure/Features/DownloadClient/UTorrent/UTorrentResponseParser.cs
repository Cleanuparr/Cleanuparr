using System.Text.Json;
using Cleanuparr.Domain.Entities.UTorrent.Response;
using Cleanuparr.Domain.Exceptions;
using Cleanuparr.Infrastructure.Json;
using Microsoft.Extensions.Logging;

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
            TorrentListResponse? response = JsonSerializer.Deserialize<TorrentListResponse>(json, CleanuparrJsonOptions.ExternalApiRead);

            if (response == null)
            {
                throw new UTorrentParsingException("Failed to deserialize torrent list response", json);
            }

            // Parse torrents
            if (response.TorrentsRaw != null)
            {
                foreach (JsonElement[] data in response.TorrentsRaw)
                {
                    if (data is { Length: >= 27 })
                    {
                        response.Torrents.Add(new UTorrentItem
                        {
                            Hash = data[0].GetString() ?? string.Empty,
                            Status = data[1].GetInt32(),
                            Name = data[2].GetString() ?? string.Empty,
                            Size = data[3].GetInt64(),
                            Progress = data[4].GetInt32(),
                            Downloaded = data[5].GetInt64(),
                            Uploaded = data[6].GetInt64(),
                            RatioRaw = data[7].GetInt32(),
                            UploadSpeed = data[8].GetInt32(),
                            DownloadSpeed = data[9].GetInt32(),
                            ETA = data[10].GetInt32(),
                            Label = data[11].GetString() ?? string.Empty,
                            PeersConnected = data[12].GetInt32(),
                            PeersInSwarm = data[13].GetInt32(),
                            SeedsConnected = data[14].GetInt32(),
                            SeedsInSwarm = data[15].GetInt32(),
                            Availability = data[16].GetInt32(),
                            QueueOrder = data[17].GetInt32(),
                            Remaining = data[18].GetInt64(),
                            DownloadUrl = data[19].GetString() ?? string.Empty,
                            RssFeedUrl = data[20].GetString() ?? string.Empty,
                            StatusMessage = data[21].GetString() ?? string.Empty,
                            StreamId = data[22].GetString() ?? string.Empty,
                            DateAdded = data[23].GetInt64(),
                            DateCompleted = data[24].GetInt64(),
                            AppUpdateUrl = data[25].GetString() ?? string.Empty,
                            SavePath = data[26].GetString() ?? string.Empty
                        });
                    }
                }
            }

            // Parse labels
            if (response.LabelsRaw != null)
            {
                foreach (JsonElement[] labelData in response.LabelsRaw)
                {
                    if (labelData is { Length: > 0 })
                    {
                        string? labelName = labelData[0].GetString();

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
            FileListResponse? rawResponse = JsonSerializer.Deserialize<FileListResponse>(json, CleanuparrJsonOptions.ExternalApiRead);

            if (rawResponse == null)
            {
                throw new UTorrentParsingException("Failed to deserialize file list response", json);
            }

            FileListResponse response = new();

            // Parse files from the nested array structure
            if (rawResponse.FilesRaw is { Length: >= 2 })
            {
                response.Hash = rawResponse.FilesRaw[0].GetString() ?? string.Empty;

                JsonElement filesElement = rawResponse.FilesRaw[1];

                if (filesElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement fileEntry in filesElement.EnumerateArray())
                    {
                        if (fileEntry.ValueKind == JsonValueKind.Array)
                        {
                            JsonElement[] fileData = fileEntry.EnumerateArray().ToArray();

                            if (fileData.Length >= 4)
                            {
                                response.Files.Add(new UTorrentFile
                                {
                                    Name = fileData[0].GetString() ?? string.Empty,
                                    Size = fileData[1].GetInt64(),
                                    Downloaded = fileData[2].GetInt64(),
                                    Priority = fileData[3].GetInt32(),
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
            PropertiesResponse? rawResponse = JsonSerializer.Deserialize<PropertiesResponse>(json, CleanuparrJsonOptions.ExternalApiRead);

            if (rawResponse == null)
            {
                throw new UTorrentParsingException("Failed to deserialize properties response", json);
            }

            PropertiesResponse response = new();

            // Parse properties from the array structure
            if (rawResponse.PropertiesRaw is { Length: > 0 })
            {
                response.Properties = rawResponse.PropertiesRaw[0]
                    .Deserialize<UTorrentProperties>(CleanuparrJsonOptions.ExternalApiRead) ?? new UTorrentProperties();
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
            LabelListResponse? response = JsonSerializer.Deserialize<LabelListResponse>(json, CleanuparrJsonOptions.ExternalApiRead);

            if (response == null)
            {
                throw new UTorrentParsingException("Failed to deserialize label list response", json);
            }

            // Parse labels
            if (response.LabelsRaw != null)
            {
                foreach (JsonElement[] labelData in response.LabelsRaw)
                {
                    if (labelData is { Length: > 0 })
                    {
                        string? labelName = labelData[0].GetString();
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
