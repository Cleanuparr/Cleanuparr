using System.ComponentModel.DataAnnotations;
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
[Index(nameof(InstanceType))]
[Index(nameof(DownloadClientType))]
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

    public Strike? Strike { get; set; }

    public Guid? JobRunId { get; set; }

    public JobRun? JobRun { get; set; }

    /// <summary>
    /// The type of arr instance that generated this event (e.g., Sonarr, Radarr)
    /// </summary>
    public InstanceType? InstanceType { get; set; }

    /// <summary>
    /// The URL of the arr instance that generated this event
    /// </summary>
    [MaxLength(500)]
    public string? InstanceUrl { get; set; }

    /// <summary>
    /// The type of download client involved in this event
    /// </summary>
    public DownloadClientTypeName? DownloadClientType { get; set; }

    /// <summary>
    /// The name of the download client involved in this event
    /// </summary>
    [MaxLength(200)]
    public string? DownloadClientName { get; set; }
} 