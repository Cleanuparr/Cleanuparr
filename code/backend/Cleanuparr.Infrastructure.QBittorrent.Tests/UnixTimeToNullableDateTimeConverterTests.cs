using Newtonsoft.Json;
using QBittorrent.Client.Converters;
using Shouldly;

namespace Cleanuparr.Infrastructure.QBittorrent.Tests;

public class UnixTimeToNullableDateTimeConverterTests
{
    [Fact]
    public void ReadJson_NormalSeconds_ReturnsExpectedDateTime()
    {
        // 1577836800 = 2020-01-01T00:00:00Z
        Deserialize("1577836800").ShouldBe(new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Unspecified));
    }

    [Fact]
    public void ReadJson_Zero_ReturnsNull()
    {
        Deserialize("0").ShouldBeNull();
    }

    [Fact]
    public void ReadJson_NegativeOne_ReturnsNull()
    {
        Deserialize("-1").ShouldBeNull();
    }

    [Fact]
    public void ReadJson_JsonNull_ReturnsNull()
    {
        Deserialize("null").ShouldBeNull();
    }

    [Fact]
    public void ReadJson_ValueAboveMaxSeconds_ReturnsNull()
    {
        Deserialize("1221109432555").ShouldBeNull();
    }

    [Fact]
    public void ReadJson_LongMaxValue_ReturnsNull()
    {
        Deserialize(long.MaxValue.ToString()).ShouldBeNull();
    }

    [Fact]
    public void ReadJson_LongMinValue_ReturnsNull()
    {
        Deserialize(long.MinValue.ToString()).ShouldBeNull();
    }

    private static DateTime? Deserialize(string value)
    {
        string json = $"{{\"Timestamp\": {value}}}";
        return JsonConvert.DeserializeObject<TestData>(json)!.Timestamp;
    }

    private class TestData
    {
        [JsonConverter(typeof(UnixTimeToNullableDateTimeConverter))]
        public DateTime? Timestamp { get; set; }
    }
}
