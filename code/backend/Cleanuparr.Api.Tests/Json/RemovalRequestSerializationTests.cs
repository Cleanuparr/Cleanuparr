using System.Text.Json;
using Cleanuparr.Api.Json;
using Cleanuparr.Domain.Entities.Arr;
using Cleanuparr.Domain.Entities.Arr.Queue;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.DownloadRemover.Models;
using Cleanuparr.Persistence.Models.Configuration.Arr;
using Shouldly;
using Xunit;

namespace Cleanuparr.Api.Tests.Json;

/// <summary>
/// The in-memory bus serializes with these options, so a broken discriminator
/// only shows up at runtime.
/// </summary>
public class RemovalRequestSerializationTests
{
    private static readonly JsonSerializerOptions Options = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        JsonSerializerOptions options = new();
        CleanuparrJsonConfiguration.ConfigureCore(options);
        return options;
    }

    private static QueueItemRemoveRequest CreateRequest(SearchItem searchItem)
    {
        return new QueueItemRemoveRequest
        {
            Instance = new ArrInstance
            {
                Name = "Test Instance",
                Url = new Uri("http://sonarr.local"),
                ApiKey = "test-api-key",
                ArrConfig = new ArrConfig { Type = InstanceType.Sonarr },
            },
            Target = new ArrRemovalTarget
            {
                Record = new QueueRecord
                {
                    Id = 1,
                    Title = "Test Record",
                    Protocol = "torrent",
                    DownloadId = "ABC123",
                },
                SearchItem = searchItem,
                RemoveFromClient = true,
                ChangeCategory = false,
            },
            DeleteReason = DeleteReason.Stalled,
            JobRunId = Guid.NewGuid(),
        };
    }

    private static QueueItemRemoveRequest RoundTrip(QueueItemRemoveRequest request)
    {
        string json = JsonSerializer.Serialize(request, Options);
        return JsonSerializer.Deserialize<QueueItemRemoveRequest>(json, Options)!;
    }

    [Fact]
    public void ArrTarget_SurvivesRoundTrip()
    {
        QueueItemRemoveRequest result = RoundTrip(CreateRequest(new SearchItem { Id = 42 }));

        ArrRemovalTarget target = result.Target.ShouldBeOfType<ArrRemovalTarget>();
        target.Record.DownloadId.ShouldBe("ABC123");
        target.RemoveFromClient.ShouldBeTrue();
        result.DeleteReason.ShouldBe(DeleteReason.Stalled);
    }

    [Fact]
    public void BaseSearchItem_SurvivesRoundTrip()
    {
        QueueItemRemoveRequest result = RoundTrip(CreateRequest(new SearchItem { Id = 42 }));

        SearchItem item = ArrTargetOf(result).SearchItem;
        item.ShouldBeOfType<SearchItem>();
        item.Id.ShouldBe(42);
    }

    [Fact]
    public void SeriesSearchItem_KeepsItsDerivedType()
    {
        QueueItemRemoveRequest result = RoundTrip(CreateRequest(new SeriesSearchItem
        {
            Id = 100,
            SeriesId = 10,
            SearchType = SeriesSearchType.Episode,
        }));

        SeriesSearchItem item = ArrTargetOf(result).SearchItem.ShouldBeOfType<SeriesSearchItem>();
        item.Id.ShouldBe(100);
        item.SeriesId.ShouldBe(10);
        item.SearchType.ShouldBe(SeriesSearchType.Episode);
    }

    [Fact]
    public void Target_CarriesTheDiscriminator_AndOmitsDerivedMembers()
    {
        string json = JsonSerializer.Serialize(CreateRequest(new SearchItem { Id = 42 }), Options);

        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement target = document.RootElement.GetProperty("Target");

        target.GetProperty("$target").GetString().ShouldBe("arr");
        target.TryGetProperty("DownloadId", out _).ShouldBeFalse();
        target.TryGetProperty("Title", out _).ShouldBeFalse();
    }

    private static ArrRemovalTarget ArrTargetOf(QueueItemRemoveRequest request) =>
        (ArrRemovalTarget)request.Target;
}
