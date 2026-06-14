using System.ComponentModel.DataAnnotations;

namespace Cleanuparr.Persistence.Models.Auth;

public class UserFeatureView
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    [Required]
    public required string FeatureId { get; set; }

    public DateTime FirstSeenAt { get; set; }

    public User User { get; set; } = null!;
}
