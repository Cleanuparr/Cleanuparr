using Newtonsoft.Json;
using QBittorrent.Client;
using Shouldly;

namespace Cleanuparr.Infrastructure.QBittorrent.Tests;

public class TorrentPropertiesTests
{
    [Fact]
    public void Deserialize_CompletionDate_ParsedAndNotLeftInAdditionalData()
    {
        TorrentProperties? properties = JsonConvert.DeserializeObject<TorrentProperties>(
            """{"completion_date":1700003000,"is_private":true}""");

        properties!.CompletionDate.ShouldBe(DateTimeOffset.FromUnixTimeSeconds(1700003000).UtcDateTime);
        properties.AdditionalData.ContainsKey("completion_date").ShouldBeFalse();
        properties.AdditionalData["is_private"].ToObject<bool>().ShouldBeTrue();
    }
}
