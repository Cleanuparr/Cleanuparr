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

    public OidcConfig Oidc { get; set; } = new();

    [Required]
    [SensitiveData]
    public required string ApiKey { get; set; }

    public bool SetupCompleted { get; set; }

    public int FailedLoginAttempts { get; set; }

    public DateTimeOffset? LockoutEnd { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public List<RecoveryCode> RecoveryCodes { get; set; } = [];

    public List<RefreshToken> RefreshTokens { get; set; } = [];

    /// <summary>
    /// Records of when this user first saw each feature, used to drive the "NEW" feature badges.
    /// </summary>
    public List<UserFeatureView> FeatureViews { get; set; } = [];
}
