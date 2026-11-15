using Newtonsoft.Json;
using QBittorrent.Client;
using Shouldly;

namespace Cleanuparr.Infrastructure.QBittorrent.Tests;

public class RssAutoDownloadingRuleTests
{
    [Fact]
    public void Deserialize()
    {
        const string ruleText = """
            {
                "enabled": false,
                "mustContain": "The *Punisher*",
                "mustNotContain": "",
                "useRegex": false,
                "episodeFilter": "1x01-;",
                "smartFilter": false,
                "previouslyMatchedEpisodes": [],
                "affectedFeeds": ["http://showrss.info/user/134567.rss?magnets=true"],
                "ignoreDays": 1,
                "lastMatch": "20 Nov 2017 09:05:11",
                "addPaused": true,
                "assignedCategory": "",
                "savePath": "C:/Users/JohnDoe/Downloads/Punisher"
            }
            """;

        RssAutoDownloadingRule rule = JsonConvert.DeserializeObject<RssAutoDownloadingRule>(ruleText)!;

        rule.Enabled.ShouldBeFalse();
        rule.MustContain.ShouldBe("The *Punisher*");
        rule.MustNotContain.ShouldBe("");
        rule.UseRegex.ShouldBeFalse();
        rule.EpisodeFilter.ShouldBe("1x01-;");
        rule.SmartFilter.ShouldBeFalse();
        rule.PreviouslyMatchedEpisodes.ShouldBeEmpty();
        rule.AffectedFeeds.ShouldBe([new Uri("http://showrss.info/user/134567.rss?magnets=true")]);
        rule.IgnoreDays.ShouldBe(1);
        rule.LastMatch.ShouldBe(new DateTime(2017, 11, 20, 9, 5, 11));
        rule.AddPaused.ShouldBe(true);
        rule.AssignedCategory.ShouldBe("");
        rule.SavePath.ShouldBe("C:/Users/JohnDoe/Downloads/Punisher");
    }
}
