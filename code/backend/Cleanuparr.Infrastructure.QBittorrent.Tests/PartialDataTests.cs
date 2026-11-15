using Newtonsoft.Json;
using QBittorrent.Client;
using Shouldly;

#pragma warning disable 618

namespace Cleanuparr.Infrastructure.QBittorrent.Tests;

public class PartialDataTests
{
    [Fact]
    public void LegacyCategories()
    {
        const string data = """
            { "rid": 1, "categories": ["cat1", "cat2"] }
            """;

        PartialData partialData = JsonConvert.DeserializeObject<PartialData>(data)!;

        partialData.CategoriesAdded.ShouldBe(["cat1", "cat2"], ignoreOrder: true);
        partialData.CategoriesChanged.Keys.ShouldBe(["cat1", "cat2"], ignoreOrder: true);
        partialData.CategoriesChanged["cat1"].Name.ShouldBe("cat1");
        partialData.CategoriesChanged["cat1"].SavePath.ShouldBe("");
        partialData.CategoriesChanged["cat2"].Name.ShouldBe("cat2");
        partialData.CategoriesChanged["cat2"].SavePath.ShouldBe("");
    }

    [Fact]
    public void ModernCategories()
    {
        const string data = """
            {
                "rid": 1,
                "categories": {
                    "cat1": {"name": "cat1", "savePath": ""},
                    "cat2": {"name": "cat2", "savePath": "/home/test/cat2"}
                }
            }
            """;

        PartialData partialData = JsonConvert.DeserializeObject<PartialData>(data)!;

        partialData.CategoriesAdded.ShouldBeNull();
        partialData.CategoriesChanged.Keys.ShouldBe(["cat1", "cat2"], ignoreOrder: true);
        partialData.CategoriesChanged["cat1"].SavePath.ShouldBe("");
        partialData.CategoriesChanged["cat2"].SavePath.ShouldBe("/home/test/cat2");
    }
}
