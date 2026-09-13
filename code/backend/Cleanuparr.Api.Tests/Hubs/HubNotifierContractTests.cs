using System.Text.Json;
using Cleanuparr.Api.Features.Events.Contracts.Responses;
using Cleanuparr.Api.Features.Logging.Contracts.Responses;
using Cleanuparr.Api.Features.Strikes.Contracts.Responses;
using Cleanuparr.Api.Hubs;
using Cleanuparr.Api.Tests.TestHelpers;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Health;
using Cleanuparr.Infrastructure.Logging;
using Cleanuparr.Infrastructure.Models;
using Cleanuparr.Persistence.Models.Events;
using Shouldly;
using Xunit;

namespace Cleanuparr.Api.Tests.Hubs;

/// <summary>
/// Records the message name and wire shape of every push the hub adapters emit.
/// </summary>
public class HubNotifierContractTests
{
    private readonly HubMessage<AppHub> _appHub = new();
    private readonly HubMessage<HealthStatusHub> _healthHub = new();

    [Fact]
    public async Task NotifyEventAsync_SendsEventReceivedWithTheDocumentedKeys()
    {
        await new EventNotifier(_appHub.Context, TimeProvider.System).NotifyEventAsync(new AppEvent
        {
            EventType = EventType.QueueItemDeleted,
            Message = "deleted",
            Severity = EventSeverity.Important,
        });

        (string method, object? payload) = _appHub.Single();

        method.ShouldBe("EventReceived");
        payload.ShouldBeOfType<EventListItem>();
        ResponseContract.Keys(ResponseContract.Payload(payload)).ShouldBe(
        [
            "arrInstanceId",
            "cleanReason",
            "cleanedCategory",
            "completedAt",
            "cycleId",
            "deleteReason",
            "downloadClientId",
            "eventType",
            "failedImportReasons",
            "grabbedItems",
            "id",
            "isCategoryTag",
            "isDryRun",
            "itemHash",
            "itemTitle",
            "jobRunId",
            "message",
            "newCategory",
            "oldCategory",
            "removeFromClient",
            "searchReason",
            "searchStatus",
            "searchType",
            "seedRatio",
            "seedingTimeHours",
            "severity",
            "strikeCount",
            "strikeId",
            "timestamp",
            "trackingId",
        ]);
    }

    [Fact]
    public async Task NotifyManualEventAsync_SendsManualEventReceivedWithTheDocumentedKeys()
    {
        await new EventNotifier(_appHub.Context, TimeProvider.System).NotifyManualEventAsync(new ManualEvent
        {
            Message = "needs attention",
            Severity = EventSeverity.Warning,
            Type = ManualEventType.SearchNotTriggered,
        });

        (string method, object? payload) = _appHub.Single();

        method.ShouldBe("ManualEventReceived");
        payload.ShouldBeOfType<ManualEventResponse>();
        ResponseContract.Keys(ResponseContract.Payload(payload)).ShouldBe(
        [
            "downloadClientName",
            "downloadClientType",
            "id",
            "instanceType",
            "instanceUrl",
            "isDryRun",
            "isResolved",
            "itemHash",
            "itemTitle",
            "jobRunId",
            "message",
            "resolvedAt",
            "severity",
            "strikeCount",
            "timestamp",
            "type",
        ]);
    }

    [Fact]
    public async Task NotifyStrikeAsync_SendsStrikeReceivedWithTheDocumentedKeys()
    {
        await new EventNotifier(_appHub.Context, TimeProvider.System)
            .NotifyStrikeAsync(Guid.NewGuid(), StrikeType.FailedImport, "HASH", "title", isDryRun: true);

        (string method, object? payload) = _appHub.Single();

        method.ShouldBe("StrikeReceived");
        RecentStrikeDto strike = payload.ShouldBeOfType<RecentStrikeDto>();
        strike.CreatedAt.ShouldBe(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
        ResponseContract.Keys(ResponseContract.Payload(payload)).ShouldBe(
        [
            "createdAt",
            "downloadId",
            "id",
            "isDryRun",
            "title",
            "type",
        ]);
    }

    [Fact]
    public void NotifyLog_SendsLogReceivedWithTheDocumentedKeys()
    {
        new LogNotifier(_appHub.Context).NotifyLog(new LogEntry
        {
            Timestamp = DateTimeOffset.UnixEpoch,
            Level = "Information",
            Message = "hello",
        });

        (string method, object? payload) = _appHub.Single();

        method.ShouldBe("LogReceived");
        payload.ShouldBeOfType<LogEntryResponse>();
        ResponseContract.Keys(ResponseContract.Payload(payload)).ShouldBe(
        [
            "category",
            "downloadClientName",
            "downloadClientType",
            "exception",
            "instanceName",
            "jobName",
            "jobRunId",
            "level",
            "message",
            "timestamp",
        ]);
    }

    [Fact]
    public async Task NotifyAppStatusAsync_SendsAppStatusUpdatedWithTheDocumentedKeys()
    {
        await new StatusNotifier(_appHub.Context).NotifyAppStatusAsync(new AppStatus("1.0.0", "1.1.0"));

        (string method, object? payload) = _appHub.Single();

        method.ShouldBe("AppStatusUpdated");
        payload.ShouldBeOfType<AppStatus>();
        ResponseContract.Keys(ResponseContract.Payload(payload)).ShouldBe(["currentVersion", "latestVersion"]);
    }

    [Fact]
    public async Task NotifySearchStatsUpdatedAsync_SendsSearchStatsUpdatedWithoutAPayload()
    {
        await new StatusNotifier(_appHub.Context).NotifySearchStatsUpdatedAsync();

        _appHub.SingleWithoutPayload().ShouldBe("SearchStatsUpdated");
    }

    [Fact]
    public async Task NotifyCustomFormatScoresUpdatedAsync_SendsCfScoresUpdatedWithoutAPayload()
    {
        await new StatusNotifier(_appHub.Context).NotifyCustomFormatScoresUpdatedAsync();

        _appHub.SingleWithoutPayload().ShouldBe("CfScoresUpdated");
    }

    [Theory]
    [InlineData("HealthStatusChanged")]
    [InlineData("ClientDegraded")]
    [InlineData("ClientRecovered")]
    public async Task HealthMessages_CarryTheDocumentedKeys(string expectedMethod)
    {
        HealthNotifier notifier = new(_healthHub.Context);
        HealthStatus status = new()
        {
            ClientId = Guid.NewGuid(),
            ClientName = "qbit",
            ClientTypeName = DownloadClientTypeName.qBittorrent,
            IsHealthy = true,
            LastChecked = DateTimeOffset.UnixEpoch,
        };

        await (expectedMethod switch
        {
            "ClientDegraded" => notifier.NotifyClientDegradedAsync(status),
            "ClientRecovered" => notifier.NotifyClientRecoveredAsync(status),
            _ => notifier.NotifyHealthStatusChangedAsync(status),
        });

        (string method, object? payload) = _healthHub.Single();

        method.ShouldBe(expectedMethod);
        payload.ShouldBeOfType<HealthStatus>();
        ResponseContract.Keys(ResponseContract.Payload(payload)).ShouldBe(
        [
            "clientId",
            "clientName",
            "clientTypeName",
            "errorMessage",
            "isHealthy",
            "lastChecked",
            "responseTime",
        ]);
    }

    [Fact]
    public async Task NotifyClientRemovedAsync_SendsABareGuid()
    {
        Guid clientId = Guid.NewGuid();

        await new HealthNotifier(_healthHub.Context).NotifyClientRemovedAsync(clientId);

        (string method, object? payload) = _healthHub.Single();

        method.ShouldBe("ClientRemoved");
        payload.ShouldBe(clientId);
        ResponseContract.Payload(payload!).ValueKind.ShouldBe(JsonValueKind.String);
    }

    [Fact]
    public async Task NotifyArrInstanceRemovedAsync_SendsABareGuid()
    {
        Guid instanceId = Guid.NewGuid();

        await new HealthNotifier(_healthHub.Context).NotifyArrInstanceRemovedAsync(instanceId);

        (string method, object? payload) = _healthHub.Single();

        method.ShouldBe("ArrInstanceRemoved");
        payload.ShouldBe(instanceId);
        ResponseContract.Payload(payload!).ValueKind.ShouldBe(JsonValueKind.String);
    }
}
