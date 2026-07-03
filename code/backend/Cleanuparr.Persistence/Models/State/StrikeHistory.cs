using System.ComponentModel.DataAnnotations;
using Cleanuparr.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Cleanuparr.Persistence.Models.State;

/// <summary>
/// Archived record of a strike after it leaves the active enforcement table.
/// Denormalized snapshot: it holds no navigation FKs so it survives cleanup of the
/// originating <see cref="DownloadItem"/> and <see cref="JobRun"/>.
/// </summary>
[Index(nameof(ArchivedAt), IsDescending = [true])]
[Index(nameof(Outcome))]
[Index(nameof(Type))]
public class StrikeHistory
{
    [Key]
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>
    /// Id of the originating download item (plain snapshot value, no FK).
    /// </summary>
    public Guid DownloadItemId { get; set; }

    [MaxLength(500)]
    public string? ItemTitle { get; set; }

    [MaxLength(100)]
    public string? ItemHash { get; set; }

    [Required]
    public required StrikeType Type { get; set; }

    [Required]
    public required StrikeOutcome Outcome { get; set; }

    /// <summary>
    /// Why the strike left the active table (e.g. progress, speed, eta, seeders, inactivity, maxStrikes).
    /// </summary>
    [MaxLength(100)]
    public string? Reason { get; set; }

    /// <summary>
    /// When the original strike was created.
    /// </summary>
    public DateTimeOffset StruckAt { get; set; }

    /// <summary>
    /// When the strike was archived to history.
    /// </summary>
    public DateTimeOffset ArchivedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Id of the job run that produced the strike (plain snapshot value, no FK).
    /// </summary>
    public Guid JobRunId { get; set; }

    public long? LastDownloadedBytes { get; set; }

    public bool IsDryRun { get; set; }
}
