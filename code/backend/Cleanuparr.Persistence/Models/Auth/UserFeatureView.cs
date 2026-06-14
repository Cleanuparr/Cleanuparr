using System.ComponentModel.DataAnnotations;

namespace Cleanuparr.Persistence.Models.Auth;

/// <summary>
/// Records the moment a user first saw a given feature, used to drive the "NEW" feature badges.
/// </summary>
public class UserFeatureView
{
    /// <summary>
    /// Primary key.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Owning user.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Stable identifier of the feature, as declared in the frontend registry.
    /// </summary>
    [Required]
    [MaxLength(64)]
    public required string FeatureId { get; set; }

    /// <summary>
    /// UTC timestamp the user first saw the feature.
    /// </summary>
    public DateTime FirstSeenAt { get; set; }

    /// <summary>
    /// Navigation to the owning user.
    /// </summary>
    public User User { get; set; } = null!;
}
