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
[Index(nameof(Message))]
[Index(nameof(StrikeId))]
[Index(nameof(JobRunId))]
[Index(nameof(ArrInstanceId))]
[Index(nameof(DownloadClientId))]
[Index(nameof(CycleId))]
public class AppEvent : IEvent
{
    [Key]
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [Required]
    public EventType EventType { get; set; }

    [Required]
    [MaxLength(1000)]
    public string Message { get; set; } = string.Empty;

    /// <inheritdoc/>
    public string? Data { get; set; }

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
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// The Seeker cycle ID associated with this event (only set for SearchTriggered events)
    /// </summary>
    public Guid? CycleId { get; set; }

    public bool IsDryRun { get; set; }

    public SearchEventData? SearchEventData { get; set; }

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
