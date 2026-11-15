using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using QBittorrent.Client;
using Shouldly;

namespace Cleanuparr.Infrastructure.QBittorrent.Tests;

public class PreferencesTests
{
    [Fact]
    public void SerializeToEmpty()
    {
        JsonConvert.SerializeObject(new Preferences()).ShouldBe("{}");
    }

    [Fact]
    public void Serialize_RemovesOnlyKeysInsertedByHook()
    {
        Preferences preferences = new()
        {
            WebUIPassword = "secret",
            AddTorrentPaused = true,
            AdditionalData = new Dictionary<string, JToken> { { "excluded_file_names", "*.lnk" } }
        };

        string json = JsonConvert.SerializeObject(preferences);

        JObject posted = JObject.Parse(json);
        posted["web_ui_password"]!.Value<string>().ShouldBe("secret");
        posted["add_stopped_enabled"]!.Value<bool>().ShouldBeTrue();
        posted["excluded_file_names"]!.Value<string>().ShouldBe("*.lnk");
        preferences.AdditionalData.Keys.ShouldBe(["excluded_file_names"]);
    }

    [Fact]
    public void Serialize_CallerSuppliedKey_SurvivesSerialization()
    {
        Preferences preferences = new()
        {
            WebUIPassword = "secret",
            AdditionalData = new Dictionary<string, JToken> { { "web_ui_password", "caller" } }
        };

        JsonConvert.SerializeObject(preferences);

        preferences.AdditionalData.ContainsKey("web_ui_password").ShouldBeTrue();
    }
}
