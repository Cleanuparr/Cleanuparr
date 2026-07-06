using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Persistence.Models.State;
using Microsoft.EntityFrameworkCore;

namespace Cleanuparr.Persistence.Models.Events;

/// <summary>
/// Represents an event in the system
/// </summary>
[Index(nameof(Timestamp), IsDescending = [true])]
[Index(nameof(EventType))]
[Index(nameof(Severity))]
[Index(nameof(JobRunId))]
[Index(nameof(ArrInstanceId))]
[Index(nameof(CycleId))]
[Index(nameof(DeleteReason))]
public class AppEvent : IEvent
{
    [Key]
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    [Required]
    public EventType EventType { get; set; }

    [Required]
    [MaxLength(1000)]
    public string Message { get; set; } = string.Empty;

    [Required]
    public required EventSeverity Severity { get; set; }

    /// <summary>
    /// Optional correlation ID to link related events
    /// </summary>
    public Guid? TrackingId { get; set; }

    public Guid? StrikeId { get; set; }

    [JsonIgnore]
    public Strike? Strike { get; set; }

    public Guid? JobRunId { get; set; }

    [JsonIgnore]
    public JobRun? JobRun { get; set; }

    /// <summary>
    /// The ID of the arr instance that generated this event
    /// </summary>
    public Guid? ArrInstanceId { get; set; }

    /// <summary>
    /// The ID of the download client involved in this event
    /// </summary>
    public Guid? DownloadClientId { get; set; }

    /// <summary>
    /// Status of the search command (only set for SearchTriggered events)
    /// </summary>
    public SearchCommandStatus? SearchStatus { get; set; }

    /// <summary>
    /// When the search command completed (only set for SearchTriggered events)
    /// </summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>
    /// The Seeker cycle ID associated with this event (only set for SearchTriggered events)
    /// </summary>
    public Guid? CycleId { get; set; }

    public bool IsDryRun { get; set; }

    // Item context (most events)

    /// <summary>
    /// Title of the download item this event refers to
    /// </summary>
    [MaxLength(500)]
    public string? ItemTitle { get; set; }

    /// <summary>
    /// Hash / download ID of the item this event refers to
    /// </summary>
    [MaxLength(100)]
    public string? ItemHash { get; set; }

    /// <summary>
    /// Strike number at the time of the event (strike / reset events)
    /// </summary>
    public int? StrikeCount { get; set; }

    /// <summary>
    /// Import failure reasons (FailedImportStrike events)
    /// </summary>
    public List<string> FailedImportReasons { get; set; } = [];

    /// <summary>
    /// Reason a queue item was deleted (QueueItemDeleted events)
    /// </summary>
    public DeleteReason? DeleteReason { get; set; }

    /// <summary>
    /// Whether the item was also removed from the download client (QueueItemDeleted events)
    /// </summary>
    public bool? RemoveFromClient { get; set; }

    /// <summary>
    /// Reason a download was cleaned (DownloadCleaned events)
    /// </summary>
    public CleanReason? CleanReason { get; set; }

    /// <summary>
    /// Category of the cleaned download (DownloadCleaned events)
    /// </summary>
    [MaxLength(200)]
    public string? CleanedCategory { get; set; }

    /// <summary>
    /// Seed ratio at the time of cleaning (DownloadCleaned events)
    /// </summary>
    public double? SeedRatio { get; set; }

    /// <summary>
    /// Seeding time in hours at the time of cleaning (DownloadCleaned events)
    /// </summary>
    public double? SeedingTimeHours { get; set; }

    /// <summary>
    /// Previous category (CategoryChanged events)
    /// </summary>
    [MaxLength(200)]
    public string? OldCategory { get; set; }

    /// <summary>
    /// New category or tag (CategoryChanged events)
    /// </summary>
    [MaxLength(200)]
    public string? NewCategory { get; set; }

    /// <summary>
    /// Whether the category change was a tag rather than a category (CategoryChanged events)
    /// </summary>
    public bool? IsCategoryTag { get; set; }

    /// <summary>
    /// Type of search (SearchTriggered events)
    /// </summary>
    public SeekerSearchType? SearchType { get; set; }

    /// <summary>
    /// Reason a search was triggered (SearchTriggered events)
    /// </summary>
    public SeekerSearchReason? SearchReason { get; set; }

    /// <summary>
    /// Titles of items grabbed after search completion, populated by SeekerCommandMonitor (SearchTriggered events)
    /// </summary>
    public List<string> GrabbedItems { get; set; } = [];

    // Used only for notifications

    [NotMapped]
    public InstanceType? InstanceType { get; set; }

    [NotMapped]
    public string? InstanceUrl { get; set; }

    [NotMapped]
    public DownloadClientTypeName? DownloadClientType { get; set; }

    [NotMapped]
    public string? DownloadClientName { get; set; }
}
