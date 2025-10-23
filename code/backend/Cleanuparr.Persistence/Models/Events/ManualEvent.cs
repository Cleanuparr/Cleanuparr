using System.ComponentModel.DataAnnotations;
using Cleanuparr.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Cleanuparr.Persistence.Models.Events;

/// <summary>
/// Events that need manual interaction from the user
/// </summary>
[Index(nameof(Timestamp), IsDescending = [true])]
[Index(nameof(Severity))]
[Index(nameof(Message))]
[Index(nameof(IsResolved))]
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
}