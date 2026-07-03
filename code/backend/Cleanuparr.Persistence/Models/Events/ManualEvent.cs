using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Persistence.Models.State;
using Microsoft.EntityFrameworkCore;

namespace Cleanuparr.Persistence.Models.Events;

/// <summary>
/// Events that need manual interaction from the user
/// </summary>
[Index(nameof(Timestamp), IsDescending = [true])]
[Index(nameof(Severity))]
[Index(nameof(Message))]
[Index(nameof(IsResolved))]
[Index(nameof(JobRunId))]
[Index(nameof(InstanceType))]
public class ManualEvent
{
    [Key]
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    [Required]
    [MaxLength(1000)]
    public string Message { get; set; } = string.Empty;

    [Required]
    public required EventSeverity Severity { get; set; }

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
    /// Strike number at the time of the event, when applicable
    /// </summary>
    public int? StrikeCount { get; set; }

    public bool IsResolved { get; set; }

    public Guid? JobRunId { get; set; }

    [JsonIgnore]
    public JobRun? JobRun { get; set; }

    /// <summary>
    /// The type of arr instance that generated this event
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

    public bool IsDryRun { get; set; }
}