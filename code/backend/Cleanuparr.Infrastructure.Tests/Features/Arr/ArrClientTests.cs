using System.Net;
using System.Text;
using Cleanuparr.Domain.Entities.Arr;
using Cleanuparr.Domain.Entities.Arr.Queue;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Arr;
using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Infrastructure.Features.ItemStriker;
using Cleanuparr.Infrastructure.Interceptors;
using Cleanuparr.Infrastructure.Tests.TestHelpers;
using Cleanuparr.Persistence.Models.Configuration.Arr;
using Cleanuparr.Persistence.Models.Configuration.QueueCleaner;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.Arr;

public class ArrClientTests
{
    private readonly ILogger<ArrClient> _logger;
    private readonly IStriker _striker;
    private readonly IDryRunInterceptor _dryRunInterceptor;
    private readonly FakeHttpMessageHandler _httpMessageHandler;
    private readonly TestArrClient _client;
    private readonly ArrInstance _arrInstance;

    public ArrClientTests()
    {
        _logger = Substitute.For<ILogger<ArrClient>>();
        _striker = Substitute.For<IStriker>();
        _dryRunInterceptor = Substitute.For<IDryRunInterceptor>();
        _httpMessageHandler = new FakeHttpMessageHandler();

        var httpClient = new HttpClient(_httpMessageHandler);
        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient(Arg.Any<string>()).Returns(httpClient);

        _client = new TestArrClient(_logger, httpClientFactory, _striker, _dryRunInterceptor);
        _arrInstance = new ArrInstance
        {
            Name = "test",
            Url = new Uri("http://localhost:8989/"),
            ApiKey = "secret-key",
        };

        // Default: dry-run disabled, pass-through delegate invocation
        _dryRunInterceptor.IsDryRunEnabled().Returns(false);
        _dryRunInterceptor
            .InterceptAsync<HttpResponseMessage>(Arg.Any<Func<Task<HttpResponseMessage>>>(), Arg.Any<string?>())
            .Returns(async ci =>
            {
                Func<Task<HttpResponseMessage>> action = ci.Arg<Func<Task<HttpResponseMessage>>>();
                return await action();
            });
    }

    #region GetQueueItemsAsync

    [Fact]
    public async Task GetQueueItemsAsync_SendsGetWithApiKeyAndExpectedUri()
    {
        // Arrange
        _httpMessageHandler.SetupResponse((_, _) => Task.FromResult(JsonResponse(
            new QueueListResponse { TotalRecords = 0, Records = Array.Empty<QueueRecord>() })));

        // Act
        var result = await _client.GetQueueItemsAsync(_arrInstance, 2);

        // Assert
        result.ShouldNotBeNull();
        var request = _httpMessageHandler.CapturedRequests.ShouldHaveSingleItem();
        request.Method.ShouldBe(HttpMethod.Get);
        request.RequestUri!.AbsolutePath.ShouldBe("/api/v1/queue");
        request.RequestUri.Query.ShouldBe("?page=2&pageSize=100");
        request.Headers.GetValues("x-api-key").ShouldHaveSingleItem().ShouldBe("secret-key");
    }

    [Fact]
    public async Task GetQueueItemsAsync_NonSuccessStatus_Throws()
    {
        // Arrange
        _httpMessageHandler.SetupResponse(HttpStatusCode.InternalServerError);

        // Act / Assert
        await Should.ThrowAsync<HttpRequestException>(() => _client.GetQueueItemsAsync(_arrInstance, 1));
    }

    [Fact]
    public async Task GetQueueItemsAsync_NullDeserialization_Throws()
    {
        // Arrange — body "null" deserializes to null
        _httpMessageHandler.SetupResponse((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("null", Encoding.UTF8, "application/json"),
        }));

        // Act / Assert
        await Should.ThrowAsync<Exception>(() => _client.GetQueueItemsAsync(_arrInstance, 1));
    }

    #endregion

    #region GetActiveDownloadCountAsync

    [Fact]
    public async Task GetActiveDownloadCountAsync_EmptyQueue_ReturnsZero()
    {
        // Arrange
        _httpMessageHandler.SetupResponse((_, _) => Task.FromResult(JsonResponse(
            new QueueListResponse { TotalRecords = 0, Records = Array.Empty<QueueRecord>() })));

        // Act
        var count = await _client.GetActiveDownloadCountAsync(_arrInstance);

        // Assert
        count.ShouldBe(0);
    }

    [Fact]
    public async Task GetActiveDownloadCountAsync_CountsRecordsWithSizeLeftAboveZero()
    {
        // Arrange — 3 records, only 2 have SizeLeft > 0
        var records = new[]
        {
            BuildRecord(1, sizeLeft: 100),
            BuildRecord(2, sizeLeft: 0),
            BuildRecord(3, sizeLeft: 50),
        };
        _httpMessageHandler.SetupResponse((_, _) => Task.FromResult(JsonResponse(
            new QueueListResponse { TotalRecords = records.Length, Records = records })));

        // Act
        var count = await _client.GetActiveDownloadCountAsync(_arrInstance);

        // Assert
        count.ShouldBe(2);
    }

    [Fact]
    public async Task GetActiveDownloadCountAsync_MultiPage_AccumulatesAcrossPages()
    {
        // Arrange — page 1: 2 active of 2; page 2: 1 active of 2; total = 4 records
        int call = 0;
        _httpMessageHandler.SetupResponse((_, _) =>
        {
            call++;
            QueueRecord[] records = call switch
            {
                1 => new[] { BuildRecord(1, sizeLeft: 10), BuildRecord(2, sizeLeft: 10) },
                2 => new[] { BuildRecord(3, sizeLeft: 0), BuildRecord(4, sizeLeft: 5) },
                _ => Array.Empty<QueueRecord>(),
            };
            return Task.FromResult(JsonResponse(new QueueListResponse { TotalRecords = 4, Records = records }));
        });

        // Act
        var count = await _client.GetActiveDownloadCountAsync(_arrInstance);

        // Assert
        count.ShouldBe(3);
        _httpMessageHandler.CapturedRequests.Count.ShouldBe(2);
    }

    #endregion

    #region DeleteQueueItemAsync

    [Fact]
    public async Task DeleteQueueItemAsync_RemoveFromClient_SendsDeleteWithExpectedQuery()
    {
        // Arrange
        _httpMessageHandler.SetupResponse(HttpStatusCode.OK);

        // Act
        await _client.DeleteQueueItemAsync(_arrInstance, BuildRecord(42), removeFromClient: true, changeCategory: false, DeleteReason.FailedImport);

        // Assert
        var request = _httpMessageHandler.CapturedRequests.ShouldHaveSingleItem();
        request.Method.ShouldBe(HttpMethod.Delete);
        request.RequestUri!.AbsolutePath.ShouldBe("/api/v1/queue/42");
        request.RequestUri.Query.ShouldBe("?blocklist=true&skipRedownload=true&changeCategory=false&removeFromClient=true");
        request.Headers.GetValues("x-api-key").ShouldHaveSingleItem().ShouldBe("secret-key");
    }

    [Fact]
    public async Task DeleteQueueItemAsync_ChangeCategory_BuildsCategoryQuery()
    {
        // Arrange
        _httpMessageHandler.SetupResponse(HttpStatusCode.OK);

        // Act
        await _client.DeleteQueueItemAsync(_arrInstance, BuildRecord(7), removeFromClient: true, changeCategory: true, DeleteReason.FailedImport);

        // Assert
        var request = _httpMessageHandler.CapturedRequests.ShouldHaveSingleItem();
        request.RequestUri!.Query.ShouldBe("?blocklist=true&skipRedownload=true&changeCategory=true&removeFromClient=false");
    }

    [Fact]
    public async Task DeleteQueueItemAsync_NoRemoveNoChangeCategory_BuildsBareQuery()
    {
        // Arrange
        _httpMessageHandler.SetupResponse(HttpStatusCode.OK);

        // Act
        await _client.DeleteQueueItemAsync(_arrInstance, BuildRecord(7), removeFromClient: false, changeCategory: false, DeleteReason.Stalled);

        // Assert
        var request = _httpMessageHandler.CapturedRequests.ShouldHaveSingleItem();
        request.RequestUri!.Query.ShouldBe("?blocklist=true&skipRedownload=true&changeCategory=false&removeFromClient=false");
    }

    [Fact]
    public async Task DeleteQueueItemAsync_ServerError_Throws()
    {
        // Arrange
        _httpMessageHandler.SetupResponse(HttpStatusCode.BadGateway);

        // Act / Assert
        await Should.ThrowAsync<HttpRequestException>(() =>
            _client.DeleteQueueItemAsync(_arrInstance, BuildRecord(1), removeFromClient: true, changeCategory: false, DeleteReason.Stalled));
    }

    [Fact]
    public async Task DeleteQueueItemAsync_DryRunReturnsNull_DoesNotThrow()
    {
        // Arrange — interceptor short-circuits and returns null; method should still log "removed"
        _dryRunInterceptor
            .InterceptAsync<HttpResponseMessage>(Arg.Any<Func<Task<HttpResponseMessage>>>(), Arg.Any<string?>())
            .Returns((HttpResponseMessage?)null);

        // Act
        await _client.DeleteQueueItemAsync(_arrInstance, BuildRecord(1), removeFromClient: false, changeCategory: false, DeleteReason.Stalled);

        // Assert — no HTTP call was actually made because the interceptor was substituted to return null
        _httpMessageHandler.CapturedRequests.ShouldBeEmpty();
    }

    #endregion

    #region SearchItemAsync

    [Fact]
    public async Task SearchItemAsync_DryRun_ReturnsFirstOrDefault()
    {
        // Arrange — TestArrClient.SearchItemsAsync returns empty; dry-run path uses FirstOrDefault
        _dryRunInterceptor.IsDryRunEnabled().Returns(true);

        // Act
        var result = await _client.SearchItemAsync(_arrInstance, new SearchItem { Id = 1 });

        // Assert
        result.ShouldBe(0);
    }

    [Fact]
    public async Task SearchItemAsync_NotDryRun_EmptyIds_ThrowsInvalidOperation()
    {
        // Arrange — non-dry-run path uses .First() which throws on empty
        _dryRunInterceptor.IsDryRunEnabled().Returns(false);

        // Act / Assert
        await Should.ThrowAsync<InvalidOperationException>(() => _client.SearchItemAsync(_arrInstance, new SearchItem { Id = 1 }));
    }

    [Fact]
    public async Task SearchItemAsync_NotDryRun_ReturnsFirstId()
    {
        // Arrange — TestArrClient returns the id list it was given
        _client.SearchResultIds = [99L, 100L];
        _dryRunInterceptor.IsDryRunEnabled().Returns(false);

        // Act
        var result = await _client.SearchItemAsync(_arrInstance, new SearchItem { Id = 1 });

        // Assert
        result.ShouldBe(99);
    }

    #endregion

    #region IsRecordValid

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void IsRecordValid_NullOrEmptyDownloadId_ReturnsFalse(string? downloadId)
    {
        // Arrange
        var record = new QueueRecord
        {
            Id = 1,
            Title = "title",
            DownloadId = downloadId!,
            Protocol = "torrent",
        };

        // Act / Assert
        _client.IsRecordValid(record).ShouldBeFalse();
    }

    [Fact]
    public void IsRecordValid_NonEmptyDownloadId_ReturnsTrue()
    {
        // Arrange / Act / Assert
        _client.IsRecordValid(BuildRecord(1)).ShouldBeTrue();
    }

    #endregion

    #region HealthCheckAsync

    [Fact]
    public async Task HealthCheckAsync_SendsGetWithApiKey()
    {
        // Arrange
        _httpMessageHandler.SetupResponse(HttpStatusCode.OK);

        // Act
        await _client.HealthCheckAsync(_arrInstance);

        // Assert
        var request = _httpMessageHandler.CapturedRequests.ShouldHaveSingleItem();
        request.Method.ShouldBe(HttpMethod.Get);
        request.RequestUri!.AbsolutePath.ShouldBe("/api/v1/system/status");
        request.Headers.GetValues("x-api-key").ShouldHaveSingleItem().ShouldBe("secret-key");
    }

    [Fact]
    public async Task HealthCheckAsync_NonSuccess_Throws()
    {
        // Arrange
        _httpMessageHandler.SetupResponse(HttpStatusCode.Unauthorized);

        // Act / Assert
        await Should.ThrowAsync<HttpRequestException>(() => _client.HealthCheckAsync(_arrInstance));
    }

    #endregion

    #region GetCommandStatusAsync

    [Fact]
    public async Task GetCommandStatusAsync_DeserializesResponse()
    {
        // Arrange
        _httpMessageHandler.SetupResponse((_, _) => Task.FromResult(JsonResponse(
            new ArrCommandStatus(42, "completed", "ok"))));

        // Act
        var status = await _client.GetCommandStatusAsync(_arrInstance, 42);

        // Assert
        status.Id.ShouldBe(42);
        status.Status.ShouldBe("completed");
        status.Message.ShouldBe("ok");
        var request = _httpMessageHandler.CapturedRequests.ShouldHaveSingleItem();
        request.RequestUri!.AbsolutePath.ShouldBe("/api/v3/command/42");
    }

    [Fact]
    public async Task GetCommandStatusAsync_NullResponseBody_ReturnsUnknownDefault()
    {
        // Arrange — body deserializes to null
        _httpMessageHandler.SetupResponse((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("null", Encoding.UTF8, "application/json"),
        }));

        // Act
        var status = await _client.GetCommandStatusAsync(_arrInstance, 99);

        // Assert
        status.Id.ShouldBe(99);
        status.Status.ShouldBe("unknown");
        status.Message.ShouldBeNull();
    }

    #endregion

    #region ShouldRemoveFromQueue

    [Fact]
    public async Task ShouldRemoveFromQueue_IgnorePrivateAndIsPrivate_ReturnsFalse()
    {
        // Arrange
        SetQueueCleanerConfig(new QueueCleanerConfig
        {
            FailedImport = new FailedImportConfig { IgnorePrivate = true, MaxStrikes = 3, PatternMode = PatternMode.Exclude },
        });
        var record = BuildRecord(1, trackedStatus: "warning", trackedState: "importBlocked");

        // Act
        var result = await _client.ShouldRemoveFromQueue(InstanceType.Sonarr, record, isPrivateDownload: true, arrMaxStrikes: -1);

        // Assert
        result.ShouldBeFalse();
        await _striker.DidNotReceive().StrikeAndCheckLimit(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ushort>(), Arg.Any<StrikeType>(), Arg.Any<long?>());
    }

    [Fact]
    public async Task ShouldRemoveFromQueue_NotFailedImport_ReturnsFalse()
    {
        // Arrange — tracked download is "downloading" with no failed-import message
        SetQueueCleanerConfig(new QueueCleanerConfig
        {
            FailedImport = new FailedImportConfig { MaxStrikes = 3, PatternMode = PatternMode.Exclude },
        });
        var record = BuildRecord(1, trackedStatus: "ok", trackedState: "downloading");

        // Act
        var result = await _client.ShouldRemoveFromQueue(InstanceType.Sonarr, record, isPrivateDownload: false, arrMaxStrikes: -1);

        // Assert
        result.ShouldBeFalse();
        await _striker.DidNotReceive().StrikeAndCheckLimit(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ushort>(), Arg.Any<StrikeType>(), Arg.Any<long?>());
    }

    [Fact]
    public async Task ShouldRemoveFromQueue_ArrMaxStrikesZero_ReturnsFalse()
    {
        // Arrange
        SetQueueCleanerConfig(new QueueCleanerConfig
        {
            FailedImport = new FailedImportConfig { MaxStrikes = 3, PatternMode = PatternMode.Exclude },
        });
        var record = BuildRecord(1, trackedStatus: "warning", trackedState: "importBlocked",
            statusMessages: new List<TrackedDownloadStatusMessage>
            {
                new() { Title = "import failed", Messages = ["bad file"] },
            });

        // Act
        var result = await _client.ShouldRemoveFromQueue(InstanceType.Sonarr, record, isPrivateDownload: false, arrMaxStrikes: 0);

        // Assert
        result.ShouldBeFalse();
        await _striker.DidNotReceive().StrikeAndCheckLimit(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ushort>(), Arg.Any<StrikeType>(), Arg.Any<long?>());
    }

    [Fact]
    public async Task ShouldRemoveFromQueue_WarnImportBlocked_StrikesAndPropagatesLimitResult()
    {
        // Arrange — exclude-mode + non-matching pattern means we WILL strike
        SetQueueCleanerConfig(new QueueCleanerConfig
        {
            FailedImport = new FailedImportConfig
            {
                MaxStrikes = 3,
                PatternMode = PatternMode.Exclude,
                Patterns = new List<string> { "should not match" },
            },
        });
        var record = BuildRecord(1, trackedStatus: "warning", trackedState: "importBlocked",
            statusMessages: new List<TrackedDownloadStatusMessage>
            {
                new() { Title = "failed", Messages = ["import error"] },
            });
        _striker.StrikeAndCheckLimit(record.DownloadId, record.Title, (ushort)3, StrikeType.FailedImport)
            .Returns(true);

        // Act
        var result = await _client.ShouldRemoveFromQueue(InstanceType.Sonarr, record, isPrivateDownload: false, arrMaxStrikes: -1);

        // Assert
        result.ShouldBeTrue();
        await _striker.Received(1).StrikeAndCheckLimit(record.DownloadId, record.Title, (ushort)3, StrikeType.FailedImport);
    }

    [Fact]
    public async Task ShouldRemoveFromQueue_ArrMaxStrikesOverridesConfigMax()
    {
        // Arrange — config says 3, arr override says 7
        SetQueueCleanerConfig(new QueueCleanerConfig
        {
            FailedImport = new FailedImportConfig
            {
                MaxStrikes = 3,
                PatternMode = PatternMode.Exclude,
                Patterns = new List<string> { "no match" },
            },
        });
        var record = BuildRecord(1, trackedStatus: "warning", trackedState: "importFailed",
            statusMessages: new List<TrackedDownloadStatusMessage>
            {
                new() { Title = "failed", Messages = ["import error"] },
            });
        _striker.StrikeAndCheckLimit(record.DownloadId, record.Title, (ushort)7, StrikeType.FailedImport)
            .Returns(false);

        // Act
        var result = await _client.ShouldRemoveFromQueue(InstanceType.Sonarr, record, isPrivateDownload: false, arrMaxStrikes: 7);

        // Assert
        result.ShouldBeFalse();
        await _striker.Received(1).StrikeAndCheckLimit(record.DownloadId, record.Title, (ushort)7, StrikeType.FailedImport);
    }

    [Fact]
    public async Task ShouldRemoveFromQueue_ExcludePatternMatched_DoesNotStrike()
    {
        // Arrange — exclude-mode with a matching pattern should skip
        SetQueueCleanerConfig(new QueueCleanerConfig
        {
            FailedImport = new FailedImportConfig
            {
                MaxStrikes = 3,
                PatternMode = PatternMode.Exclude,
                Patterns = new List<string> { "permission" },
            },
        });
        var record = BuildRecord(1, trackedStatus: "warning", trackedState: "importBlocked",
            statusMessages: new List<TrackedDownloadStatusMessage>
            {
                new() { Title = "failed", Messages = ["permission denied on import"] },
            });

        // Act
        var result = await _client.ShouldRemoveFromQueue(InstanceType.Sonarr, record, isPrivateDownload: false, arrMaxStrikes: -1);

        // Assert
        result.ShouldBeFalse();
        await _striker.DidNotReceive().StrikeAndCheckLimit(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ushort>(), Arg.Any<StrikeType>(), Arg.Any<long?>());
    }

    [Fact]
    public async Task ShouldRemoveFromQueue_IncludePatternNotMatched_DoesNotStrike()
    {
        // Arrange — include-mode with non-matching pattern should skip
        SetQueueCleanerConfig(new QueueCleanerConfig
        {
            FailedImport = new FailedImportConfig
            {
                MaxStrikes = 3,
                PatternMode = PatternMode.Include,
                Patterns = new List<string> { "specific reason" },
            },
        });
        var record = BuildRecord(1, trackedStatus: "warning", trackedState: "importBlocked",
            statusMessages: new List<TrackedDownloadStatusMessage>
            {
                new() { Title = "failed", Messages = ["something else entirely"] },
            });

        // Act
        var result = await _client.ShouldRemoveFromQueue(InstanceType.Sonarr, record, isPrivateDownload: false, arrMaxStrikes: -1);

        // Assert
        result.ShouldBeFalse();
        await _striker.DidNotReceive().StrikeAndCheckLimit(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ushort>(), Arg.Any<StrikeType>(), Arg.Any<long?>());
    }

    [Fact]
    public async Task ShouldRemoveFromQueue_NoStatusMessages_DoesNotStrike()
    {
        // Arrange
        SetQueueCleanerConfig(new QueueCleanerConfig
        {
            FailedImport = new FailedImportConfig { MaxStrikes = 3, PatternMode = PatternMode.Exclude },
        });
        var record = BuildRecord(1, trackedStatus: "warning", trackedState: "importBlocked", statusMessages: null);

        // Act
        var result = await _client.ShouldRemoveFromQueue(InstanceType.Sonarr, record, isPrivateDownload: false, arrMaxStrikes: -1);

        // Assert
        result.ShouldBeFalse();
        await _striker.DidNotReceive().StrikeAndCheckLimit(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ushort>(), Arg.Any<StrikeType>(), Arg.Any<long?>());
    }

    [Fact]
    public async Task ShouldRemoveFromQueue_LidarrFailedWithWarn_Strikes()
    {
        // Arrange — Lidarr-specific path: status=failed/completed + warning triggers strike
        SetQueueCleanerConfig(new QueueCleanerConfig
        {
            FailedImport = new FailedImportConfig
            {
                MaxStrikes = 3,
                PatternMode = PatternMode.Exclude,
                Patterns = new List<string> { "ignored" },
            },
        });
        var record = BuildRecord(1, status: "failed", trackedStatus: "warning", trackedState: "downloading",
            statusMessages: new List<TrackedDownloadStatusMessage>
            {
                new() { Title = "failed", Messages = ["import broke"] },
            });
        _striker.StrikeAndCheckLimit(record.DownloadId, record.Title, (ushort)3, StrikeType.FailedImport)
            .Returns(true);

        // Act
        var result = await _client.ShouldRemoveFromQueue(InstanceType.Lidarr, record, isPrivateDownload: false, arrMaxStrikes: -1);

        // Assert
        result.ShouldBeTrue();
        await _striker.Received(1).StrikeAndCheckLimit(record.DownloadId, record.Title, (ushort)3, StrikeType.FailedImport);
    }

    [Fact]
    public async Task ShouldRemoveFromQueue_DownloadingWithFailedImportMessage_Strikes()
    {
        // Arrange — IsEdgeCase: state=downloading + a message starting with "Unable to import automatically"
        SetQueueCleanerConfig(new QueueCleanerConfig
        {
            FailedImport = new FailedImportConfig
            {
                MaxStrikes = 3,
                PatternMode = PatternMode.Exclude,
                Patterns = new List<string> { "ignored" },
            },
        });
        var record = BuildRecord(1, trackedStatus: "ok", trackedState: "downloading",
            statusMessages: new List<TrackedDownloadStatusMessage>
            {
                new() { Title = "warn", Messages = ["Unable to import automatically, please try again"] },
            });
        _striker.StrikeAndCheckLimit(record.DownloadId, record.Title, (ushort)3, StrikeType.FailedImport)
            .Returns(true);

        // Act
        var result = await _client.ShouldRemoveFromQueue(InstanceType.Sonarr, record, isPrivateDownload: false, arrMaxStrikes: -1);

        // Assert
        result.ShouldBeTrue();
    }

    #endregion

    #region Helpers

    private static void SetQueueCleanerConfig(QueueCleanerConfig config)
    {
        ContextProvider.Set(config);
    }

    private static QueueRecord BuildRecord(
        long id,
        long sizeLeft = 0,
        string status = "downloading",
        string trackedStatus = "ok",
        string trackedState = "downloading",
        List<TrackedDownloadStatusMessage>? statusMessages = null)
    {
        return new QueueRecord
        {
            Id = id,
            Title = $"item-{id}",
            DownloadId = id.ToString(),
            Protocol = "torrent",
            SizeLeft = sizeLeft,
            Status = status,
            TrackedDownloadStatus = trackedStatus,
            TrackedDownloadState = trackedState,
            StatusMessages = statusMessages,
        };
    }

    private static HttpResponseMessage JsonResponse<T>(T body)
    {
        var json = Newtonsoft.Json.JsonConvert.SerializeObject(body);
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
    }

    #endregion

    private sealed class TestArrClient : ArrClient
    {
        public List<long> SearchResultIds { get; set; } = new();

        public TestArrClient(
            ILogger<ArrClient> logger,
            IHttpClientFactory httpClientFactory,
            IStriker striker,
            IDryRunInterceptor dryRunInterceptor)
            : base(logger, httpClientFactory, striker, dryRunInterceptor)
        {
        }

        protected override string GetSystemStatusUrlPath() => "/api/v1/system/status";

        protected override string GetQueueUrlPath() => "/api/v1/queue";

        protected override string GetQueueUrlQuery(int page) => $"page={page}&pageSize=100";

        protected override string GetQueueDeleteUrlPath(long recordId) => $"/api/v1/queue/{recordId}";

        public override Task<List<long>> SearchItemsAsync(ArrInstance arrInstance, HashSet<SearchItem>? items)
            => Task.FromResult(SearchResultIds);

        public override bool HasContentId(QueueRecord record) => true;

        public override Task<List<Tag>> GetAllTagsAsync(ArrInstance arrInstance) => Task.FromResult(new List<Tag>());
    }
}
