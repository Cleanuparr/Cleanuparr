using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.RegularExpressions;
using Cleanuparr.Domain.Enums;
using ValidationException = Cleanuparr.Domain.Exceptions.ValidationException;

namespace Cleanuparr.Persistence.Models.Configuration.Notification;

public sealed partial record PushoverConfig : IConfig
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; init; } = Guid.NewGuid();

    [Required]
    public Guid NotificationConfigId { get; init; }

    public NotificationConfig NotificationConfig { get; init; } = null!;

    /// <summary>
    /// Application API token (30 characters, [A-Za-z0-9])
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string ApiToken { get; init; } = string.Empty;

    /// <summary>
    /// User/group key (30 characters, [A-Za-z0-9])
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string UserKey { get; init; } = string.Empty;

    /// <summary>
    /// Target specific devices (comma-separated when sent to API)
    /// </summary>
    public List<string> Devices { get; init; } = [];

    /// <summary>
    /// Notification priority (-2 to 2)
    /// </summary>
    [Required]
    public PushoverPriority Priority { get; init; } = PushoverPriority.Normal;

    /// <summary>
    /// Notification sound (built-in or custom)
    /// </summary>
    [MaxLength(50)]
    public string? Sound { get; init; }

    /// <summary>
    /// Retry interval in seconds for emergency priority (min 30)
    /// </summary>
    public int? Retry { get; init; }

    /// <summary>
    /// Expiration time in seconds for emergency priority (max 10800)
    /// </summary>
    public int? Expire { get; init; }

    /// <summary>
    /// Tags for receipt tracking and batch cancellation
    /// </summary>
    public List<string> Tags { get; init; } = [];

    public bool IsValid()
    {
        if (string.IsNullOrWhiteSpace(ApiToken) || string.IsNullOrWhiteSpace(UserKey))
            return false;

        if (Priority == PushoverPriority.Emergency)
        {
            if (!Retry.HasValue || Retry.Value < 30)
                return false;
            if (!Expire.HasValue || Expire.Value < 1 || Expire.Value > 10800)
                return false;
        }

        return true;
    }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ApiToken))
            throw new ValidationException("Pushover API token is required");

        if (string.IsNullOrWhiteSpace(UserKey))
            throw new ValidationException("Pushover user key is required");

        if (Priority == PushoverPriority.Emergency)
        {
            if (!Retry.HasValue || Retry.Value < 30)
                throw new ValidationException("Retry interval must be at least 30 seconds for emergency priority");

            if (!Expire.HasValue || Expire.Value < 1)
                throw new ValidationException("Expire time is required for emergency priority");

            if (Expire.Value > 10800)
                throw new ValidationException("Expire time cannot exceed 10800 seconds (3 hours)");
        }

        // Validate device names if provided
        foreach (var device in Devices.Where(d => !string.IsNullOrWhiteSpace(d)))
        {
            if (device.Length > 25)
                throw new ValidationException($"Device name '{device}' exceeds 25 character limit");

            if (!DeviceNameRegex().IsMatch(device))
                throw new ValidationException($"Device name '{device}' contains invalid characters. Only letters, numbers, underscores, and hyphens are allowed.");
        }
    }

    [GeneratedRegex(@"^[A-Za-z0-9_-]+$")]
    private static partial Regex DeviceNameRegex();
}
