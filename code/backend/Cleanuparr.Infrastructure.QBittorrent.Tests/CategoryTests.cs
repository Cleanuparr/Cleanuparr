using Newtonsoft.Json;
using QBittorrent.Client;
using Shouldly;

namespace Cleanuparr.Infrastructure.QBittorrent.Tests;

public class CategoryTests
{
    [Fact]
    public void Deserialize_SnakeCaseSavePath_KeepsOtherAdditionalData()
    {
        Category category = JsonConvert.DeserializeObject<Category>("""{"name":"tv","save_path":"/tv","extra":1}""")!;

        category.SavePath.ShouldBe("/tv");
        category.AdditionalData.ShouldNotBeNull();
        category.AdditionalData.ContainsKey("save_path").ShouldBeFalse();
        category.AdditionalData.ContainsKey("extra").ShouldBeTrue();
    }
}
