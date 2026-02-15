using System.ComponentModel.DataAnnotations;
using Cleanuparr.Shared.Attributes;

namespace Cleanuparr.Persistence.Models.Auth;

public class RecoveryCode
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    [Required]
    [SensitiveData]
    public required string CodeHash { get; set; }

    public bool IsUsed { get; set; }

    public DateTime? UsedAt { get; set; }

    public User User { get; set; } = null!;
}
