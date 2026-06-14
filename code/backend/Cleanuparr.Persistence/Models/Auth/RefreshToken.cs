using System.ComponentModel.DataAnnotations;
using Cleanuparr.Shared.Attributes;

namespace Cleanuparr.Persistence.Models.Auth;

public class RefreshToken
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    [Required]
    [SensitiveData]
    public required string TokenHash { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    public User User { get; set; } = null!;
}
