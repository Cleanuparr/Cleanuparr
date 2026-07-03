using System.ComponentModel.DataAnnotations;
using Cleanuparr.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Cleanuparr.Persistence.Models.Events;

/// <summary>
/// Archived <see cref="AppEvent"/> moved out of the hot events table after the retention window.
/// Flat snapshot of the event's persisted fields (no navigations); the original <see cref="AppEvent.Id"/>
/// is preserved for traceability.
/// </summary>
[Index(nameof(Timestamp), IsDescending = [true])]
[Index(nameof(EventType))]
[Index(nameof(Severity))]
[Index(nameof(ArchivedAt), IsDescending = [true])]
[Index(nameof(DeleteReason))]
public class EventHistory
{
    /// <summary>
    /// Preserved from the originating <see cref="AppEvent.Id"/>.
    /// </summary>
    [Key]
    public Guid Id { get; set; }

    public DateTimeOffset Timestamp { get; set; }

    [Required]
    public EventType EventType { get; set; }

    [Required]
    [MaxLength(1000)]
    public string Message { get; set; } = string.Empty;

    [Required]
    public required EventSeverity Severity { get; set; }

    /// <summary>
    /// When the event was archived to history.
    /// </summary>
    public DateTimeOffset ArchivedAt { get; set; } = DateTimeOffset.UtcNow;

    public Guid? TrackingId { get; set; }

    // Snapshot values (no navigation FKs)
    public Guid? StrikeId { get; set; }

    public Guid? JobRunId { get; set; }

    public Guid? ArrInstanceId { get; set; }

    public Guid? DownloadClientId { get; set; }

    public SearchCommandStatus? SearchStatus { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public Guid? CycleId { get; set; }

    public bool IsDryRun { get; set; }

    // Typed payload (mirrors AppEvent)
    [MaxLength(500)]
    public string? ItemTitle { get; set; }

    [MaxLength(100)]
    public string? ItemHash { get; set; }

    public int? StrikeCount { get; set; }

    public List<string> FailedImportReasons { get; set; } = [];

    public DeleteReason? DeleteReason { get; set; }

    public bool? RemoveFromClient { get; set; }

    public CleanReason? CleanReason { get; set; }

    [MaxLength(200)]
    public string? CleanedCategory { get; set; }

    public double? SeedRatio { get; set; }

    public double? SeedingTimeHours { get; set; }

    [MaxLength(200)]
    public string? OldCategory { get; set; }

    [MaxLength(200)]
    public string? NewCategory { get; set; }

    public bool? IsCategoryTag { get; set; }

    public SeekerSearchType? SearchType { get; set; }

    public SeekerSearchReason? SearchReason { get; set; }

    public List<string> GrabbedItems { get; set; } = [];
}
