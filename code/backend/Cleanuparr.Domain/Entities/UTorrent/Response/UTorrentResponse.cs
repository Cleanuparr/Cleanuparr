using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Cleanuparr.Domain.Entities.UTorrent.Response;

/// <summary>
/// Base response wrapper for µTorrent Web UI API calls
/// </summary>
public sealed record UTorrentResponse<T>
{
    [JsonProperty(PropertyName = "build")]
    public int Build { get; set; }

    [JsonProperty(PropertyName = "label")]
    public object[][]? Labels { get; set; }

    [JsonProperty(PropertyName = "torrents")]
    public T? Torrents { get; set; }

    [JsonProperty(PropertyName = "torrentp")]
    public object[]? TorrentProperties { get; set; }

    [JsonProperty(PropertyName = "files")]
    public object[]? FilesDto { get; set; }
    
    [JsonIgnore]
    public List<UTorrentFile>? Files
    {
        get
        {
            if (FilesDto is null || FilesDto.Length < 2)
            {
                return null;
            }

            var files = new List<UTorrentFile>();
            
            if (FilesDto[1] is JArray jArray)
            {
                foreach (var jToken in jArray)
                {
                    var fileTokenArray = (JArray)jToken;
                    var fileArray = fileTokenArray.ToObject<object[]>() ?? [];
                    files.Add(new UTorrentFile
                    {
                        Name = fileArray[0].ToString() ?? string.Empty,
                        Size = Convert.ToInt64(fileArray[1]),
                        Downloaded = Convert.ToInt64(fileArray[2]),
                        Priority = Convert.ToInt32(fileArray[3]),
                    });
                }
                
            }

            return files;
        }
    }

    [JsonProperty(PropertyName = "props")]
    public UTorrentProperties[]? Properties { get; set; }
} 