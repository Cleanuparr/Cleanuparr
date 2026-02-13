using System.ComponentModel.DataAnnotations;
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

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [Required]
    [MaxLength(1000)]
    public string Message { get; set; } = string.Empty;

    public string? Data { get; set; }

    [Required]
    public required EventSeverity Severity { get; set; }

    public bool IsResolved { get; set; }

    public Guid? JobRunId { get; set; }

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
}