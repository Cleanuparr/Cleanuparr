using Cleanuparr.Domain.Enums;
using Cleanuparr.Persistence.Models.Events;

namespace Cleanuparr.Api.Features.Events.Contracts.Responses;

public sealed record ManualEventResponse
{
    public Guid Id { get; init; }

    public DateTimeOffset Timestamp { get; init; }

    public required string Message { get; init; }

    public EventSeverity Severity { get; init; }

    public ManualEventType Type { get; init; }

    public string? ItemTitle { get; init; }

    public string? ItemHash { get; init; }

    public int? StrikeCount { get; init; }

    public bool IsResolved { get; init; }

    public DateTimeOffset? ResolvedAt { get; init; }

    public Guid? JobRunId { get; init; }

    public InstanceType? InstanceType { get; init; }

    public string? InstanceUrl { get; init; }

    public DownloadClientTypeName? DownloadClientType { get; init; }

    public string? DownloadClientName { get; init; }

    public bool IsDryRun { get; init; }

    public static ManualEventResponse From(ManualEvent manualEvent) => new()
    {
        Id = manualEvent.Id,
        Timestamp = manualEvent.Timestamp,
        Message = manualEvent.Message,
        Severity = manualEvent.Severity,
        Type = manualEvent.Type,
        ItemTitle = manualEvent.ItemTitle,
        ItemHash = manualEvent.ItemHash,
        StrikeCount = manualEvent.StrikeCount,
        IsResolved = manualEvent.IsResolved,
        ResolvedAt = manualEvent.ResolvedAt,
        JobRunId = manualEvent.JobRunId,
        InstanceType = manualEvent.InstanceType,
        InstanceUrl = manualEvent.InstanceUrl,
        DownloadClientType = manualEvent.DownloadClientType,
        DownloadClientName = manualEvent.DownloadClientName,
        IsDryRun = manualEvent.IsDryRun,
    };
}
