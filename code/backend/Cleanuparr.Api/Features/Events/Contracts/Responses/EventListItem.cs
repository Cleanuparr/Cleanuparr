using System.Linq.Expressions;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Persistence.Models.Events;

namespace Cleanuparr.Api.Features.Events.Contracts.Responses;

public class EventListItem
{
    public Guid Id { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public EventType EventType { get; set; }
    public string Message { get; set; } = string.Empty;
    public EventSeverity Severity { get; set; }
    public Guid? TrackingId { get; set; }
    public Guid? StrikeId { get; set; }
    public Guid? JobRunId { get; set; }
    public Guid? ArrInstanceId { get; set; }
    public Guid? DownloadClientId { get; set; }
    public SearchCommandStatus? SearchStatus { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public Guid? CycleId { get; set; }
    public bool IsDryRun { get; set; }
    public string? ItemTitle { get; set; }
    public string? ItemHash { get; set; }
    public int? StrikeCount { get; set; }
    public List<string> FailedImportReasons { get; set; } = [];
    public DeleteReason? DeleteReason { get; set; }
    public bool? RemoveFromClient { get; set; }
    public CleanReason? CleanReason { get; set; }
    public string? CleanedCategory { get; set; }
    public double? SeedRatio { get; set; }
    public double? SeedingTimeHours { get; set; }
    public string? OldCategory { get; set; }
    public string? NewCategory { get; set; }
    public bool? IsCategoryTag { get; set; }
    public SeekerSearchType? SearchType { get; set; }
    public SeekerSearchReason? SearchReason { get; set; }
    public List<string> GrabbedItems { get; set; } = [];

    public static readonly Expression<Func<AppEvent, EventListItem>> FromEvent = e => new EventListItem
    {
        Id = e.Id,
        Timestamp = e.Timestamp,
        EventType = e.EventType,
        Message = e.Message,
        Severity = e.Severity,
        TrackingId = e.TrackingId,
        StrikeId = e.StrikeId,
        JobRunId = e.JobRunId,
        ArrInstanceId = e.ArrInstanceId,
        DownloadClientId = e.DownloadClientId,
        SearchStatus = e.SearchStatus,
        CompletedAt = e.CompletedAt,
        CycleId = e.CycleId,
        IsDryRun = e.IsDryRun,
        ItemTitle = e.ItemTitle,
        ItemHash = e.ItemHash,
        StrikeCount = e.StrikeCount,
        FailedImportReasons = e.FailedImportReasons,
        DeleteReason = e.DeleteReason,
        RemoveFromClient = e.RemoveFromClient,
        CleanReason = e.CleanReason,
        CleanedCategory = e.CleanedCategory,
        SeedRatio = e.SeedRatio,
        SeedingTimeHours = e.SeedingTimeHours,
        OldCategory = e.OldCategory,
        NewCategory = e.NewCategory,
        IsCategoryTag = e.IsCategoryTag,
        SearchType = e.SearchType,
        SearchReason = e.SearchReason,
        GrabbedItems = e.GrabbedItems,
    };
}
