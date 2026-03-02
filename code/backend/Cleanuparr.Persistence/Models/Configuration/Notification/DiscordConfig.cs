using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Cleanuparr.Persistence.Models.Configuration;
using Cleanuparr.Shared.Attributes;
using ValidationException = Cleanuparr.Domain.Exceptions.ValidationException;

namespace Cleanuparr.Persistence.Models.Configuration.Notification;

public sealed record DiscordConfig : IConfig
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; init; } = Guid.NewGuid();

    [Required]
    public Guid NotificationConfigId { get; init; }

    [ForeignKey(nameof(NotificationConfigId))]
    public NotificationConfig NotificationConfig { get; init; } = null!;

    [Required]
    [MaxLength(500)]
    [SensitiveData]
    public string WebhookUrl { get; init; } = string.Empty;

    [MaxLength(80)]
    public string Username { get; init; } = string.Empty;

    [MaxLength(500)]
    public string AvatarUrl { get; init; } = string.Empty;

    public bool IsValid()
    {
        return !string.IsNullOrWhiteSpace(WebhookUrl);
    }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(WebhookUrl))
        {
            throw new ValidationException("Discord webhook URL is required");
        }

        if (!WebhookUrl.StartsWith("https://discord.com/api/webhooks/", StringComparison.OrdinalIgnoreCase) &&
            !WebhookUrl.StartsWith("https://discordapp.com/api/webhooks/", StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException("Discord webhook URL must be a valid Discord webhook URL");
        }

        if (!string.IsNullOrWhiteSpace(AvatarUrl) &&
            !Uri.TryCreate(AvatarUrl, UriKind.Absolute, out var uri))
        {
            throw new ValidationException("Avatar URL must be a valid URL");
        }
    }
}
