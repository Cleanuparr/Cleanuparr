using System.ComponentModel.DataAnnotations;
using Cleanuparr.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Cleanuparr.Persistence.Models.State;

[Index(nameof(DownloadItemId), nameof(Type))]
[Index(nameof(CreatedAt))]
[Index(nameof(JobRunId))]
public class Strike
{
    [Key]
    public Guid Id { get; set; } = Guid.CreateVersion7();

    [Required]
    public Guid DownloadItemId { get; set; }

    public DownloadItem DownloadItem { get; set; } = null!;

    [Required]
    public Guid JobRunId { get; set; }

    public JobRun JobRun { get; set; } = null!;

    [Required]
    public required StrikeType Type { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public long? LastDownloadedBytes { get; set; }
}
