using Cleanuparr.Infrastructure.QBittorrent.Tests.TestHelpers;
using Newtonsoft.Json.Linq;
using QBittorrent.Client;
using Shouldly;

namespace Cleanuparr.Infrastructure.QBittorrent.Tests;

public class QBittorrentClientPreferencesTests
{
    [Fact]
    public async Task SetPreferencesAsync_OnlyExcludedFileNames_PostsOnlyThatKey()
    {
        using FakeQBittorrentServer server = new();
        Preferences preferences = new()
        {
            AdditionalData = new Dictionary<string, JToken>
            {
                { "excluded_file_names", "*.lnk\n*.exe" }
            }
        };

        await server.Client.SetPreferencesAsync(preferences);

        RecordedRequest request = server.SingleApiRequest;
        request.Method.ShouldBe(HttpMethod.Post);
        request.Uri.AbsolutePath.ShouldBe("/api/v2/app/setPreferences");
        request.Body.ShouldStartWith("json=");
        string json = Uri.UnescapeDataString(request.Body!["json=".Length..].Replace('+', ' '));
        json.ShouldBe("""{"excluded_file_names":"*.lnk\n*.exe"}""");
    }

    [Fact]
    public async Task SetPreferencesAsync_EmptyExcludedFileNames_PostsEmptyString()
    {
        using FakeQBittorrentServer server = new();
        Preferences preferences = new()
        {
            AdditionalData = new Dictionary<string, JToken>
            {
                { "excluded_file_names", "" }
            }
        };

        await server.Client.SetPreferencesAsync(preferences);

        string json = Uri.UnescapeDataString(server.SingleApiRequest.Body!["json=".Length..]);
        json.ShouldBe("""{"excluded_file_names":""}""");
    }
}
