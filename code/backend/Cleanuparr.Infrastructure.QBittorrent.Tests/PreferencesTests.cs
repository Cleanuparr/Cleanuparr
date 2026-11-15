using Newtonsoft.Json;
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
}
