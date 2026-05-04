using Cleanuparr.Domain.Entities.Deluge.Response;
using Cleanuparr.Domain.Enums;
using Newtonsoft.Json;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.DownloadClient;

public class DownloadStatusDeserializationTests
{
    private const string BaseDelugePayload = """
        {
            "hash": "abc123",
            "name": "Some Torrent",
            "eta": 0,
            "private": false,
            "total_size": 1024,
            "total_done": 1024,
            "is_finished": true,
            "label": "movies",
            "seeding_time": 0,
            "ratio": 1.0,
            "trackers": [],
            "download_payload_rate": 0,
            "download_location": "/downloads"
        }
        """;

    [Fact]
    public void UnknownStateValue_DeserializesToUnknown_AndPreservesOtherFields()
    {
        var json = BaseDelugePayload.Replace("\"hash\": \"abc123\",", "\"hash\": \"abc123\", \"state\": \"FutureNewState\",");

        var result = JsonConvert.DeserializeObject<DownloadStatus>(json);

        result.ShouldNotBeNull();
        result.State.ShouldBe(DelugeState.Unknown);
        result.Hash.ShouldBe("abc123");
        result.IsFinished.ShouldBeTrue();
        result.Size.ShouldBe(1024L);
    }

    [Theory]
    [InlineData("Seeding")]
    [InlineData("seeding")]
    [InlineData("SEEDING")]
    public void KnownState_DeserializesCaseInsensitively(string wireValue)
    {
        var json = BaseDelugePayload.Replace("\"hash\": \"abc123\",", $"\"hash\": \"abc123\", \"state\": \"{wireValue}\",");

        var result = JsonConvert.DeserializeObject<DownloadStatus>(json);

        result.ShouldNotBeNull();
        result.State.ShouldBe(DelugeState.Seeding);
    }

    [Fact]
    public void MissingStateField_DeserializesToUnknown()
    {
        var result = JsonConvert.DeserializeObject<DownloadStatus>(BaseDelugePayload);

        result.ShouldNotBeNull();
        result.State.ShouldBe(DelugeState.Unknown);
        result.Hash.ShouldBe("abc123");
    }

    [Fact]
    public void NullStateField_DeserializesToUnknown()
    {
        var json = BaseDelugePayload.Replace("\"hash\": \"abc123\",", "\"hash\": \"abc123\", \"state\": null,");

        var result = JsonConvert.DeserializeObject<DownloadStatus>(json);

        result.ShouldNotBeNull();
        result.State.ShouldBe(DelugeState.Unknown);
    }
}
