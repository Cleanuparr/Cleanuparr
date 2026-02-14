using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;

namespace Cleanuparr.Persistence.Models.State;

[Index(nameof(DownloadId), IsUnique = true)]
public class DownloadItem
{
    [Key]
    public Guid Id { get; set; } = Guid.CreateVersion7();

    [Required]
    [MaxLength(100)]
    public required string DownloadId { get; set; }

    [Required]
    [MaxLength(500)]
    public required string Title { get; set; }

    public bool IsMarkedForRemoval { get; set; }
    public bool IsRemoved { get; set; }
    public bool IsReturning { get; set; }

    [JsonIgnore]
    public List<Strike> Strikes { get; set; } = [];
}
