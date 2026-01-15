using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ValidationException = Cleanuparr.Domain.Exceptions.ValidationException;

namespace Cleanuparr.Persistence.Models.Configuration.Notification;

public sealed record GotifyConfig : IConfig
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
    public string ServerUrl { get; init; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string ApplicationToken { get; init; } = string.Empty;

    public int Priority { get; init; } = 5;

    public bool IsValid()
    {
        return !string.IsNullOrWhiteSpace(ServerUrl) && !string.IsNullOrWhiteSpace(ApplicationToken);
    }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ServerUrl))
        {
            throw new ValidationException("Gotify server URL is required");
        }

        if (!Uri.TryCreate(ServerUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != "http" && uri.Scheme != "https"))
        {
            throw new ValidationException("Gotify server URL must be a valid HTTP or HTTPS URL");
        }

        if (string.IsNullOrWhiteSpace(ApplicationToken))
        {
            throw new ValidationException("Gotify application token is required");
        }

        if (Priority < 0 || Priority > 10)
        {
            throw new ValidationException("Priority must be between 0 and 10");
        }
    }
}
