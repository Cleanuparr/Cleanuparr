using System.Reflection;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using QBittorrent.Client;
using QBittorrent.Client.Converters;
using Shouldly;

namespace Cleanuparr.Infrastructure.QBittorrent.Tests;

public class TorrentStateConverterTests
{
    [Theory]
    [MemberData(nameof(Values))]
    [InlineData("stoppedDL", TorrentState.PausedDownload)]
    [InlineData("stoppedUP", TorrentState.PausedUpload)]
    public void ReadJson(string stringValue, TorrentState expectedState)
    {
        string json = $"{{\"TorrentState\": \"{stringValue}\"}}";
        TestData? testData = JsonConvert.DeserializeObject<TestData>(json);
        testData!.TorrentState.ShouldBe(expectedState);
    }

    [Theory]
    [MemberData(nameof(Values))]
    public void WriteJson(string expectedString, TorrentState torrentState)
    {
        TestData testData = new() { TorrentState = torrentState };
        string json = JsonConvert.SerializeObject(testData, Formatting.None);
        json.ShouldBe($"{{\"TorrentState\":\"{expectedString}\"}}");
    }

    public static IEnumerable<object[]> Values()
    {
        foreach (FieldInfo field in typeof(TorrentState).GetFields(BindingFlags.Static | BindingFlags.Public))
        {
            EnumMemberAttribute attribute = field.GetCustomAttribute<EnumMemberAttribute>()!;
            yield return [attribute.Value!, field.GetValue(null)!];
        }
    }

    private class TestData
    {
        [JsonConverter(typeof(TorrentStateConverter))]
        public TorrentState TorrentState { get; set; }
    }
}
