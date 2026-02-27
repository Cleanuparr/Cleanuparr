using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Persistence.Models.Configuration;
using Cleanuparr.Shared.Attributes;
using ValidationException = Cleanuparr.Domain.Exceptions.ValidationException;

namespace Cleanuparr.Persistence.Models.Configuration.Notification;

public sealed record AppriseConfig : IConfig
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; init; } = Guid.NewGuid();

    [Required]
    [ExcludeFromCodeCoverage]
    public Guid NotificationConfigId { get; init; }

    public NotificationConfig NotificationConfig { get; init; } = null!;

    /// <summary>
    /// The mode of operation: Api (external apprise-api container) or Cli (bundled apprise CLI)
    /// </summary>
    public AppriseMode Mode { get; init; } = AppriseMode.Api;

    // API mode fields
    [MaxLength(500)]
    public string Url { get; init; } = string.Empty;

    [MaxLength(255)]
    [SensitiveData]
    public string Key { get; init; } = string.Empty;

    [MaxLength(255)]
    public string? Tags { get; init; }

    // CLI mode fields
    /// <summary>
    /// Apprise service URLs for CLI mode (one per line).
    /// Example: discord://webhook_id/webhook_token
    /// </summary>
    [MaxLength(4000)]
    [SensitiveData(SensitiveDataType.AppriseUrl)]
    public string? ServiceUrls { get; init; }

    [NotMapped]
    public Uri? Uri
    {
        get
        {
            try
            {
                return string.IsNullOrWhiteSpace(Url) ? null : new Uri(Url, UriKind.Absolute);
            }
            catch
            {
                return null;
            }
        }
    }

    public bool IsValid()
    {
        return Mode switch
        {
            AppriseMode.Api => Uri != null && !string.IsNullOrWhiteSpace(Key),
            AppriseMode.Cli => !string.IsNullOrWhiteSpace(ServiceUrls),
            _ => false
        };
    }

    public void Validate()
    {
        if (Mode is AppriseMode.Api)
        {
            ValidateApiMode();
            return;
        }

        ValidateCliMode();
    }

    private void ValidateApiMode()
    {
        if (string.IsNullOrWhiteSpace(Url))
        {
            throw new ValidationException("Apprise server URL is required for API mode");
        }

        if (Uri is null)
        {
            throw new ValidationException("Apprise server URL must be a valid HTTP or HTTPS URL");
        }

        if (string.IsNullOrWhiteSpace(Key))
        {
            throw new ValidationException("Apprise configuration key is required for API mode");
        }

        if (Key.Length < 2)
        {
            throw new ValidationException("Apprise configuration key must be at least 2 characters long");
        }
    }

    private void ValidateCliMode()
    {
        if (string.IsNullOrWhiteSpace(ServiceUrls))
        {
            throw new ValidationException("At least one service URL is required for CLI mode");
        }
    }
}
