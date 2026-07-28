using System.Text.Json;
using Cleanuparr.Domain.Entities.Arr;
using Cleanuparr.Domain.Entities.Arr.Queue;
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
        result.Records[0].DownloadId.ShouldBeNull();
    }

    [Fact]
    public void QueueRecord_WithNullDownloadId_Deserializes()
    {
        const string payload = """
        {
            "totalRecords": 1,
            "records": [{ "id": 1, "title": "T", "downloadId": null, "protocol": "torrent" }]
        }
        """;

        QueueListResponse result = JsonSerializer.Deserialize<QueueListResponse>(payload, CleanuparrJsonOptions.ExternalApiRead)!;

        result.Records[0].DownloadId.ShouldBeNull();
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
    public void EmptyQueueResponse_Deserializes()
    {
        QueueListResponse result = JsonSerializer.Deserialize<QueueListResponse>("{}", CleanuparrJsonOptions.ExternalApiRead)!;

        result.TotalRecords.ShouldBe(0);
        result.Records.ShouldBeNull();
    }

    [Fact]
    public void PositionalRecord_WithMissingConstructorParameter_Deserializes()
    {
        ArrCommandStatus result = JsonSerializer.Deserialize<ArrCommandStatus>("""{"id": 5}""", CleanuparrJsonOptions.ExternalApiRead)!;

        result.Id.ShouldBe(5);
        result.Status.ShouldBeNull();
    }

    [Fact]
    public void Tag_WithoutLabel_Deserializes()
    {
        List<Tag> result = JsonSerializer.Deserialize<List<Tag>>("""[{"id": 3}]""", CleanuparrJsonOptions.ExternalApiRead)!;

        result[0].Id.ShouldBe(3);
        result[0].Label.ShouldBeNull();
    }
}
