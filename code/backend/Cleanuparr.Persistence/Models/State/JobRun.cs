using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Persistence.Models.Events;
using Microsoft.EntityFrameworkCore;

namespace Cleanuparr.Persistence.Models.State;

[Index(nameof(StartedAt), IsDescending = [true])]
[Index(nameof(Type))]
public class JobRun
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public required JobType Type { get; set; }

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAt { get; set; }

    public JobRunStatus? Status { get; set; }

    [JsonIgnore]
    public List<Strike> Strikes { get; set; } = [];

    [JsonIgnore]
    public List<AppEvent> Events { get; set; } = [];

    [JsonIgnore]
    public List<ManualEvent> ManualEvents { get; set; } = [];
}
