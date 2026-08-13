using System.Text.Json;
using Cleanuparr.Domain.Entities.Arr;
using Cleanuparr.Domain.Entities.Arr.Queue;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Json;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Json;

public class ExternalApiReadTests
{
    [Fact]
    public void QueueRecord_WithoutDownloadId_Deserializes()
    {
        const string payload = """
        {
            "totalRecords": 1,
            "records": [
                {
                    "id": 42,
                    "seriesId": 7,
                    "title": "Some Release",
                    "status": "delay",
                    "protocol": "torrent"
                }
            ]
        }
        """;

        QueueListResponse result = JsonSerializer.Deserialize<QueueListResponse>(payload, CleanuparrJsonOptions.ExternalApiRead)!;

        result.TotalRecords.ShouldBe(1);
        result.Records.Count.ShouldBe(1);
        result.Records[0].Id.ShouldBe(42);
        result.Records[0].DownloadId.ShouldBeEmpty();
    }

    [Fact]
    public void QueueRecord_WithExplicitNulls_FallsBackToDefaults()
    {
        const string payload = """
        {
            "totalRecords": 1,
            "records": [{ "id": 1, "title": "T", "downloadId": null, "protocol": null, "trackedDownloadState": null }]
        }
        """;

        QueueListResponse result = JsonSerializer.Deserialize<QueueListResponse>(payload, CleanuparrJsonOptions.ExternalApiRead)!;

        result.Records[0].DownloadId.ShouldBeEmpty();
        result.Records[0].Protocol.ShouldBeEmpty();
        result.Records[0].TrackedDownloadState.ShouldBeEmpty();
    }

    [Fact]
    public void QueueRecord_WithExplicitNullOnNullableProperty_StaysNull()
    {
        const string payload = """
        {
            "totalRecords": 1,
            "records": [{ "id": 1, "title": "T", "downloadId": "ABC", "downloadClient": null }]
        }
        """;

        QueueListResponse result = JsonSerializer.Deserialize<QueueListResponse>(payload, CleanuparrJsonOptions.ExternalApiRead)!;

        result.Records[0].DownloadClient.ShouldBeNull();
    }

    [Fact]
    public void Tag_WithoutLabel_UsesEmptyDefault()
    {
        List<Tag> result = JsonSerializer.Deserialize<List<Tag>>("""[{"id": 3}]""", CleanuparrJsonOptions.ExternalApiRead)!;

        result[0].Id.ShouldBe(3);
        result[0].Label.ShouldBeEmpty();
    }

    [Fact]
    public void QueueRecord_WithAllProperties_StillDeserializes()
    {
        const string payload = """
        {
            "totalRecords": 1,
            "records": [{ "id": 1, "title": "T", "downloadId": "ABC", "protocol": "torrent", "sizeleft": 100 }]
        }
        """;

        QueueListResponse result = JsonSerializer.Deserialize<QueueListResponse>(payload, CleanuparrJsonOptions.ExternalApiRead)!;

        result.Records[0].DownloadId.ShouldBe("ABC");
        result.Records[0].Title.ShouldBe("T");
        result.Records[0].SizeLeft.ShouldBe(100);
    }

    [Fact]
    public void QueueRecord_WithFractionalSizeLeft_Deserializes()
    {
        // Sizeleft is a decimal in each arr. Sonarr writes its JSON with Newtonsoft.
        // A whole value then gets a decimal point: 4467066880.0.
        const string payload = """
        {
            "totalRecords": 1,
            "records": [{ "id": 1, "title": "T", "downloadId": "ABC", "protocol": "torrent", "sizeleft": 4467066880.0 }]
        }
        """;

        QueueListResponse result = JsonSerializer.Deserialize<QueueListResponse>(payload, CleanuparrJsonOptions.ExternalApiRead)!;

        result.Records[0].SizeLeft.ShouldBe(4467066880);
    }

    [Fact]
    public void EmptyQueueResponse_Deserializes()
    {
        QueueListResponse result = JsonSerializer.Deserialize<QueueListResponse>("{}", CleanuparrJsonOptions.ExternalApiRead)!;

        result.TotalRecords.ShouldBe(0);
        result.Records.ShouldBeEmpty();
    }

    [Fact]
    public void PositionalRecord_WithMissingConstructorParameter_Deserializes()
    {
        ArrCommandStatus result = JsonSerializer.Deserialize<ArrCommandStatus>("""{"id": 5}""", CleanuparrJsonOptions.ExternalApiRead)!;

        result.Id.ShouldBe(5);
        result.Status.ShouldBe(ArrCommandState.Unknown);
    }

    [Theory]
    [InlineData("queued", ArrCommandState.Queued)]
    [InlineData("started", ArrCommandState.Started)]
    [InlineData("completed", ArrCommandState.Completed)]
    [InlineData("failed", ArrCommandState.Failed)]
    [InlineData("aborted", ArrCommandState.Aborted)]
    [InlineData("cancelled", ArrCommandState.Cancelled)]
    [InlineData("orphaned", ArrCommandState.Orphaned)]
    [InlineData("something-new", ArrCommandState.Unknown)]
    [InlineData("3", ArrCommandState.Unknown)]
    [InlineData("999", ArrCommandState.Unknown)]
    [InlineData("completed,failed", ArrCommandState.Unknown)]
    public void CommandStatus_DeserializesState(string wireValue, ArrCommandState expected)
    {
        ArrCommandStatus result = JsonSerializer.Deserialize<ArrCommandStatus>(
            $$"""{"id": 5, "status": "{{wireValue}}"}""", CleanuparrJsonOptions.ExternalApiRead)!;

        result.Status.ShouldBe(expected);
    }

    [Fact]
    public void CommandStatus_WithNullState_DeserializesToUnknown()
    {
        ArrCommandStatus result = JsonSerializer.Deserialize<ArrCommandStatus>(
            """{"id": 5, "status": null}""", CleanuparrJsonOptions.ExternalApiRead)!;

        result.Status.ShouldBe(ArrCommandState.Unknown);
    }
}
