using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Domain.Exceptions;
using Cleanuparr.Shared.Attributes;

namespace Cleanuparr.Persistence.Models.Configuration;

/// <summary>
/// Configuration for a specific download client
/// </summary>
[Table("download_clients")]
public sealed record DownloadClientConfig
{
    /// <summary>
    /// Unique identifier for this client
    /// </summary>
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; } = Guid.NewGuid();
    
    /// <summary>
    /// Whether this client is enabled
    /// </summary>
    public bool Enabled { get; set; } = false;
    
    /// <summary>
    /// Friendly name for this client
    /// </summary>
    public required string Name { get; set; }
    
    /// <summary>
    /// Type name of download client
    /// </summary>
    public required DownloadClientTypeName TypeName { get; set; }
    
    /// <summary>
    /// Type of download client
    /// </summary>
    public required DownloadClientType Type { get; set; }
    
    /// <summary>
    /// Host address for the download client
    /// </summary>
    public Uri? Host { get; set; }
    
    /// <summary>
    /// Username for authentication
    /// </summary>
    public string? Username { get; set; }
    
    /// <summary>
    /// Password for authentication
    /// </summary>
    [SensitiveData]
    public string? Password { get; set; }

    /// <summary>
    /// API key for authentication, for clients that use one instead of a username/password.
    /// </summary>
    [SensitiveData]
    public string? ApiKey { get; set; }

    /// <summary>
    /// HTTP authentication scheme the client answers a reverse proxy's challenge with.
    /// </summary>
    public HttpAuthType HttpAuthType { get; set; } = HttpAuthType.Basic;

    /// <summary>
    /// Reverse proxy username for qBittorrent, Deluge and SABnzbd.
    /// Other clients reuse Username.
    /// </summary>
    public string? HttpAuthUsername { get; set; }

    /// <summary>
    /// Reverse proxy password for qBittorrent, Deluge and SABnzbd.
    /// </summary>
    [SensitiveData]
    public string? HttpAuthPassword { get; set; }

    /// <summary>
    /// The base URL path component, used by clients like Transmission and Deluge
    /// </summary>
    public string? UrlBase { get; set; }

    /// <summary>
    /// Optional external URL for notifications when internal Docker URLs are not reachable externally
    /// </summary>
    public Uri? ExternalUrl { get; set; }

    /// <summary>
    /// The computed full URL for the client
    /// </summary>
    [NotMapped]
    [JsonIgnore]
    public Uri Url => new($"{Host?.ToString().TrimEnd('/')}/{UrlBase?.TrimStart('/').TrimEnd('/')}");

    /// <summary>
    /// Returns ExternalUrl if set, otherwise falls back to computed Url
    /// </summary>
    [NotMapped]
    [JsonIgnore]
    public Uri ExternalOrInternalUrl => ExternalUrl ?? Url;

    /// <summary>
    /// The path prefix reported by the download client (e.g., "/downloads").
    /// Replaced with <see cref="DownloadDirectoryTarget"/> when resolving file paths across all features.
    /// </summary>
    public string? DownloadDirectorySource { get; set; }

    /// <summary>
    /// The actual local mount path (e.g., "/data/downloads").
    /// Replaces <see cref="DownloadDirectorySource"/> in file paths for hardlink checking and orphan detection.
    /// </summary>
    public string? DownloadDirectoryTarget { get; set; }

    /// <summary>
    /// Validates the configuration
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            throw new CustomValidationException($"Client name cannot be empty");
        }

        if (Host is null)
        {
            throw new CustomValidationException($"Host cannot be empty");
        }

        if (Host.IsAbsoluteUri && Host.UserInfo.Length > 0)
        {
            throw new CustomValidationException(
                "Credentials in the URL are not supported. Use the HTTP authentication fields instead.");
        }

        if (!string.IsNullOrWhiteSpace(DownloadDirectorySource) != !string.IsNullOrWhiteSpace(DownloadDirectoryTarget))
        {
            throw new CustomValidationException("Both download directory source and target must be set, or both must be empty");
        }

        if (TypeName.RequiresAuthField(DownloadClientAuthField.ApiKey) && string.IsNullOrWhiteSpace(ApiKey))
        {
            throw new CustomValidationException("API key cannot be empty");
        }

        if (TypeName.SupportsApiKeyAndCredentials() && !string.IsNullOrWhiteSpace(ApiKey) &&
            (!string.IsNullOrWhiteSpace(Username) || !string.IsNullOrWhiteSpace(Password)))
        {
            throw new CustomValidationException("Use either an API key or a username and password, not both");
        }

        if (TypeName.ApiKeyConflictsWithHttpAuth() && !string.IsNullOrWhiteSpace(ApiKey) &&
            !string.IsNullOrWhiteSpace(HttpAuthUsername))
        {
            throw new CustomValidationException(
                "API key can't be combined with HTTP authentication. Use qBittorrent username/password instead.");
        }

        if (HttpAuthType == HttpAuthType.Digest && string.IsNullOrWhiteSpace(DigestUsername))
        {
            throw new CustomValidationException("HTTP Digest authentication requires a username");
        }
    }

    /// <summary>
    /// Username sent for Digest auth.
    /// </summary>
    private string? DigestUsername => TypeName.UsesDedicatedHttpAuthFields() ? HttpAuthUsername : Username;
}
