using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ValidationException = Cleanuparr.Domain.Exceptions.ValidationException;

namespace Cleanuparr.Persistence.Models.Configuration.BlacklistSync;

/// <summary>
/// Configuration for Blacklist Synchronization to qBittorrent
/// </summary>
public sealed record BlacklistSyncConfig : IJobConfig
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; } = Guid.NewGuid();

    public bool Enabled { get; set; }

    public string CronExpression { get; set; } = "0 0 * * * ?";
    
    public string? BlacklistPath { get; set; }

    public void Validate()
    {
        if (!Enabled)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(BlacklistPath))
        {
            throw new ValidationException("Blacklist sync is enabled but the path is not configured");
        }

        bool isValidPath = Uri.TryCreate(BlacklistPath, UriKind.Absolute, out var uri) &&
                           (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps) ||
                           File.Exists(BlacklistPath);

        if (!isValidPath)
        {
            throw new ValidationException("Blacklist path must be a valid URL or an existing local file path");
        }
    }
}
