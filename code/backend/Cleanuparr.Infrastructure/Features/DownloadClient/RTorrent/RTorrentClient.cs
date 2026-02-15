using System.Net.Http.Headers;
using System.Text;
using System.Xml.Linq;
using Cleanuparr.Domain.Entities.RTorrent.Response;
using Cleanuparr.Domain.Exceptions;
using Cleanuparr.Persistence.Models.Configuration;

namespace Cleanuparr.Infrastructure.Features.DownloadClient.RTorrent;

/// <summary>
/// Low-level XML-RPC client for communicating with rTorrent
/// </summary>
public sealed class RTorrentClient
{
    private readonly DownloadClientConfig _config;
    private readonly HttpClient _httpClient;

    // Fields to request when fetching torrent data via d.multicall2
    private static readonly string[] TorrentFields =
    [
        "d.hash=",
        "d.name=",
        "d.is_private=",
        "d.size_bytes=",
        "d.completed_bytes=",
        "d.down.rate=",
        "d.ratio=",
        "d.state=",
        "d.complete=",
        "d.timestamp.finished=",
        "d.custom1=",
        "d.base_path="
    ];

    // Fields to request when fetching file data via f.multicall
    private static readonly string[] FileFields =
    [
        "f.path=",
        "f.size_bytes=",
        "f.priority=",
        "f.completed_chunks=",
        "f.size_chunks="
    ];

    public RTorrentClient(DownloadClientConfig config, HttpClient httpClient)
    {
        _config = config;
        _httpClient = httpClient;
    }

    /// <summary>
    /// Gets the rTorrent client version for health check
    /// </summary>
    public async Task<string> GetVersionAsync()
    {
        var response = await CallAsync("system.client_version");
        return ParseStringValue(response);
    }

    /// <summary>
    /// Gets all torrents with their status information
    /// </summary>
    public async Task<List<RTorrentTorrent>> GetAllTorrentsAsync()
    {
        var response = await CallAsync("d.multicall2", "", "main", TorrentFields);
        return ParseTorrentList(response);
    }

    /// <summary>
    /// Gets a single torrent by hash
    /// </summary>
    public async Task<RTorrentTorrent?> GetTorrentAsync(string hash)
    {
        try
        {
            var fields = TorrentFields.Select(f => f.TrimEnd('=')).ToArray();
            var tasks = fields.Select(field => CallAsync(field, hash)).ToArray();
            var responses = await Task.WhenAll(tasks);
            var values = responses.Select(ParseSingleValue).ToArray();

            return CreateTorrentFromValues(values);
        }
        catch (RTorrentClientException)
        {
            return null;
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    /// <summary>
    /// Gets all files for a torrent
    /// </summary>
    public async Task<List<RTorrentFile>> GetTorrentFilesAsync(string hash)
    {
        var response = await CallAsync("f.multicall", hash, "", FileFields);
        return ParseFileList(response);
    }

    /// <summary>
    /// Gets tracker URLs for a torrent
    /// </summary>
    public async Task<List<string>> GetTrackersAsync(string hash)
    {
        var response = await CallAsync("t.multicall", hash, "", "t.url=");
        return ParseTrackerList(response);
    }

    /// <summary>
    /// Deletes a torrent from rTorrent
    /// </summary>
    /// <param name="hash">Torrent hash</param>
    public async Task DeleteTorrentAsync(string hash)
    {
        await CallAsync("d.erase", hash);
    }

    /// <summary>
    /// Sets the priority for a file within a torrent
    /// </summary>
    /// <param name="hash">Torrent hash</param>
    /// <param name="fileIndex">File index (0-based)</param>
    /// <param name="priority">Priority: 0=skip, 1=normal, 2=high</param>
    public async Task SetFilePriorityAsync(string hash, int fileIndex, int priority)
    {
        // rTorrent uses hash:f<index> format for file commands
        await CallAsync("f.priority.set", $"{hash}:f{fileIndex}", priority);
    }

    /// <summary>
    /// Gets the label (category) for a torrent
    /// </summary>
    public async Task<string?> GetLabelAsync(string hash)
    {
        var response = await CallAsync("d.custom1", hash);
        var label = ParseStringValue(response);
        return string.IsNullOrEmpty(label) ? null : label;
    }

    /// <summary>
    /// Sets the label (category) for a torrent
    /// </summary>
    public async Task SetLabelAsync(string hash, string label)
    {
        await CallAsync("d.custom1.set", hash, label);
    }

    /// <summary>
    /// Sends an XML-RPC call to rTorrent
    /// </summary>
    private async Task<XElement> CallAsync(string method, params object[] parameters)
    {
        var requestXml = BuildXmlRpcRequest(method, parameters);
        var responseXml = await SendRequestAsync(requestXml);
        return ParseXmlRpcResponse(responseXml);
    }

    private string BuildXmlRpcRequest(string method, object[] parameters)
    {
        var doc = new XDocument(
            new XElement("methodCall",
                new XElement("methodName", method),
                new XElement("params",
                    parameters.Select(p => new XElement("param", SerializeValue(p)))
                )
            )
        );

        return doc.ToString(SaveOptions.DisableFormatting);
    }

    private XElement SerializeValue(object? value)
    {
        return value switch
        {
            null => new XElement("value", new XElement("string", "")),
            string s => new XElement("value", new XElement("string", s)),
            int i => new XElement("value", new XElement("i4", i)),
            long l => new XElement("value", new XElement("i8", l)),
            bool b => new XElement("value", new XElement("boolean", b ? "1" : "0")),
            double d => new XElement("value", new XElement("double", d)),
            string[] arr => new XElement("value",
                new XElement("array",
                    new XElement("data",
                        arr.Select(item => new XElement("value", new XElement("string", item)))
                    )
                )
            ),
            object[] arr => new XElement("value",
                new XElement("array",
                    new XElement("data",
                        arr.Select(item => SerializeValue(item))
                    )
                )
            ),
            _ => new XElement("value", new XElement("string", value.ToString()))
        };
    }

    private async Task<string> SendRequestAsync(string requestXml)
    {
        var content = new StringContent(requestXml, Encoding.UTF8, "text/xml");
        content.Headers.ContentType = new MediaTypeHeaderValue("text/xml");

        var response = await _httpClient.PostAsync(_config.Url, content);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync();
    }

    private XElement ParseXmlRpcResponse(string responseXml)
    {
        var doc = XDocument.Parse(responseXml);
        var root = doc.Root;

        if (root == null)
        {
            throw new RTorrentClientException("Invalid XML-RPC response: empty document");
        }

        // Check for fault response
        var fault = root.Element("fault");
        if (fault != null)
        {
            var faultValue = fault.Element("value");
            var faultStruct = faultValue?.Element("struct");
            var faultString = faultStruct?.Elements("member")
                .FirstOrDefault(m => m.Element("name")?.Value == "faultString")
                ?.Element("value")?.Value ?? "Unknown XML-RPC fault";

            throw new RTorrentClientException($"XML-RPC fault: {faultString}");
        }

        // Get the response value
        var paramsElement = root.Element("params");
        var param = paramsElement?.Element("param");
        var value = param?.Element("value");

        if (value == null)
        {
            throw new RTorrentClientException("Invalid XML-RPC response: missing value");
        }

        return value;
    }

    private static string ParseStringValue(XElement value)
    {
        // Value can be directly text or wrapped in <string>, <i4>, <i8>, etc.
        var stringEl = value.Element("string");
        if (stringEl != null) return stringEl.Value;

        var i4El = value.Element("i4");
        if (i4El != null) return i4El.Value;

        var i8El = value.Element("i8");
        if (i8El != null) return i8El.Value;

        // Direct text content
        if (!value.HasElements) return value.Value;

        return value.Elements().First().Value;
    }

    private static object? ParseSingleValue(XElement value)
    {
        var stringEl = value.Element("string");
        if (stringEl != null) return stringEl.Value;

        var i4El = value.Element("i4");
        if (i4El != null) return long.TryParse(i4El.Value, out var i4) ? i4 : 0L;

        var i8El = value.Element("i8");
        if (i8El != null) return long.TryParse(i8El.Value, out var i8) ? i8 : 0L;

        var intEl = value.Element("int");
        if (intEl != null) return long.TryParse(intEl.Value, out var intVal) ? intVal : 0L;

        var boolEl = value.Element("boolean");
        if (boolEl != null) return boolEl.Value == "1";

        var doubleEl = value.Element("double");
        if (doubleEl != null) return double.TryParse(doubleEl.Value, out var d) ? d : 0.0;

        // Direct text content
        if (!value.HasElements) return value.Value;

        return value.Elements().First().Value;
    }

    private List<RTorrentTorrent> ParseTorrentList(XElement value)
    {
        var result = new List<RTorrentTorrent>();
        var array = value.Element("array");
        var data = array?.Element("data");

        if (data == null) return result;

        foreach (var itemValue in data.Elements("value"))
        {
            var innerArray = itemValue.Element("array")?.Element("data");
            if (innerArray == null) continue;

            var values = innerArray.Elements("value").Select(ParseSingleValue).ToArray();
            var torrent = CreateTorrentFromValues(values);
            if (torrent != null)
            {
                result.Add(torrent);
            }
        }

        return result;
    }

    private static RTorrentTorrent? CreateTorrentFromValues(object?[] values)
    {
        if (values.Length < 12) return null;

        return new RTorrentTorrent
        {
            Hash = values[0]?.ToString() ?? "",
            Name = values[1]?.ToString() ?? "",
            IsPrivate = Convert.ToInt32(values[2] ?? 0),
            SizeBytes = Convert.ToInt64(values[3] ?? 0),
            CompletedBytes = Convert.ToInt64(values[4] ?? 0),
            DownRate = Convert.ToInt64(values[5] ?? 0),
            Ratio = Convert.ToInt64(values[6] ?? 0),
            State = Convert.ToInt32(values[7] ?? 0),
            Complete = Convert.ToInt32(values[8] ?? 0),
            TimestampFinished = Convert.ToInt64(values[9] ?? 0),
            Label = values[10]?.ToString(),
            BasePath = values[11]?.ToString()
        };
    }

    private List<RTorrentFile> ParseFileList(XElement value)
    {
        var result = new List<RTorrentFile>();
        var array = value.Element("array");
        var data = array?.Element("data");

        if (data == null) return result;

        int index = 0;
        foreach (var itemValue in data.Elements("value"))
        {
            var innerArray = itemValue.Element("array")?.Element("data");
            if (innerArray == null) continue;

            var values = innerArray.Elements("value").Select(ParseSingleValue).ToArray();
            if (values.Length >= 5)
            {
                result.Add(new RTorrentFile
                {
                    Index = index,
                    Path = values[0]?.ToString() ?? "",
                    SizeBytes = Convert.ToInt64(values[1] ?? 0),
                    Priority = Convert.ToInt32(values[2] ?? 1),
                    CompletedChunks = Convert.ToInt64(values[3] ?? 0),
                    SizeChunks = Convert.ToInt64(values[4] ?? 0)
                });
                index++;
            }
        }

        return result;
    }

    private List<string> ParseTrackerList(XElement value)
    {
        var result = new List<string>();
        var array = value.Element("array");
        var data = array?.Element("data");

        if (data == null) return result;

        foreach (var itemValue in data.Elements("value"))
        {
            var innerArray = itemValue.Element("array")?.Element("data");
            if (innerArray == null) continue;

            var url = innerArray.Elements("value").FirstOrDefault();
            if (url != null)
            {
                var trackerUrl = ParseStringValue(url);
                if (!string.IsNullOrEmpty(trackerUrl))
                {
                    result.Add(trackerUrl);
                }
            }
        }

        return result;
    }
}
