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

    /// <summary>
    /// Reads a value of a row as a number.
    /// </summary>
    /// <remarks>
    /// µTorrent sends each torrent as a row of values. A build can send a value
    /// with a type that this parser does not expect. A bad value gives 0, because
    /// an error here stops the parse of the full torrent list.
    /// </remarks>
    private static long AsInt64(JsonElement element) =>
        element.ValueKind switch
        {
            JsonValueKind.Number => element.TryGetInt64(out long number) ? number : (long)element.GetDouble(),
            JsonValueKind.String => long.TryParse(element.GetString(), out long text) ? text : 0,
            JsonValueKind.True => 1,
            _ => 0,
        };

    private static int AsInt32(JsonElement element)
    {
        long value = AsInt64(element);

        return value switch
        {
            > int.MaxValue => int.MaxValue,
            < int.MinValue => int.MinValue,
            _ => (int)value,
        };
    }

    /// <summary>
    /// Reads a value of a row as text.
    /// </summary>
    /// <remarks>
    /// A number or a boolean gives its JSON text. A null value gives an empty
    /// string.
    /// </remarks>
    private static string AsString(JsonElement element) =>
        element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Null or JsonValueKind.Undefined => string.Empty,
            _ => element.ToString(),
        };

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
                            Hash = AsString(data[0]),
                            Status = AsInt32(data[1]),
                            Name = AsString(data[2]),
                            Size = AsInt64(data[3]),
                            Progress = AsInt32(data[4]),
                            Downloaded = AsInt64(data[5]),
                            Uploaded = AsInt64(data[6]),
                            RatioRaw = AsInt32(data[7]),
                            UploadSpeed = AsInt32(data[8]),
                            DownloadSpeed = AsInt32(data[9]),
                            ETA = AsInt32(data[10]),
                            Label = AsString(data[11]),
                            PeersConnected = AsInt32(data[12]),
                            PeersInSwarm = AsInt32(data[13]),
                            SeedsConnected = AsInt32(data[14]),
                            SeedsInSwarm = AsInt32(data[15]),
                            Availability = AsInt32(data[16]),
                            QueueOrder = AsInt32(data[17]),
                            Remaining = AsInt64(data[18]),
                            DownloadUrl = AsString(data[19]),
                            RssFeedUrl = AsString(data[20]),
                            StatusMessage = AsString(data[21]),
                            StreamId = AsString(data[22]),
                            DateAdded = AsInt64(data[23]),
                            DateCompleted = AsInt64(data[24]),
                            AppUpdateUrl = AsString(data[25]),
                            SavePath = AsString(data[26])
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
                        string labelName = AsString(labelData[0]);

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
                response.Hash = AsString(rawResponse.FilesRaw[0]);

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
                                    Name = AsString(fileData[0]),
                                    Size = AsInt64(fileData[1]),
                                    Downloaded = AsInt64(fileData[2]),
                                    Priority = AsInt32(fileData[3]),
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
                        string labelName = AsString(labelData[0]);
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
