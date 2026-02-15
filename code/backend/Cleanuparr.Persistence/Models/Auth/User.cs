using System.ComponentModel.DataAnnotations;
using Cleanuparr.Shared.Attributes;

namespace Cleanuparr.Persistence.Models.Auth;

public class User
{
    public Guid Id { get; set; }

    [Required]
    [MaxLength(50)]
    public required string Username { get; set; }

    [Required]
    [SensitiveData]
    public required string PasswordHash { get; set; }

    [Required]
    [SensitiveData]
    public required string TotpSecret { get; set; }

    public bool TotpEnabled { get; set; }

    [MaxLength(100)]
    public string? PlexAccountId { get; set; }

    [MaxLength(100)]
    public string? PlexUsername { get; set; }

    [MaxLength(200)]
    public string? PlexEmail { get; set; }

    [SensitiveData]
    public string? PlexAuthToken { get; set; }

    [Required]
    [SensitiveData]
    public required string ApiKey { get; set; }

    public bool SetupCompleted { get; set; }

    public int FailedLoginAttempts { get; set; }

    public DateTime? LockoutEnd { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public List<RecoveryCode> RecoveryCodes { get; set; } = [];

    public List<RefreshToken> RefreshTokens { get; set; } = [];
}
