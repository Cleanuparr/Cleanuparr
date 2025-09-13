using Cleanuparr.Shared.Helpers;

namespace Cleanuparr.Infrastructure.Helpers;

public class FileReader
{
    private readonly HttpClient _httpClient;
    
    public FileReader(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient(Constants.HttpClientWithRetryName);
    }
    
    /// <summary>
    /// Reads content from either a local file or HTTP(S) URL
    /// Extracted from BlocklistProvider.ReadContentAsync for reuse
    /// </summary>
    /// <param name="path">File path or URL</param>
    /// <returns>Array of lines from the content</returns>
    public async Task<string[]> ReadContentAsync(string path)
    {
        if (Uri.TryCreate(path, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            return await ReadFromUrlAsync(path);
        }

        if (File.Exists(path))
        {
            // local file path
            return await File.ReadAllLinesAsync(path);
        }

        throw new ArgumentException($"File not found: {path}");
    }

    private async Task<string[]> ReadFromUrlAsync(string url)
    {
        using HttpResponseMessage response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();
        
        return (await response.Content.ReadAsStringAsync())
            .Split(['\r','\n'], StringSplitOptions.RemoveEmptyEntries);
    }
}