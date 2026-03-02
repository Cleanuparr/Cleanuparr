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
    public Guid Id { get; init; } = Guid.NewGuid();
    
    /// <summary>
    /// Whether this client is enabled
    /// </summary>
    public bool Enabled { get; init; } = false;
    
    /// <summary>
    /// Friendly name for this client
    /// </summary>
    public required string Name { get; init; }
    
    /// <summary>
    /// Type name of download client
    /// </summary>
    public required DownloadClientTypeName TypeName { get; init; }
    
    /// <summary>
    /// Type of download client
    /// </summary>
    public required DownloadClientType Type { get; init; }
    
    /// <summary>
    /// Host address for the download client
    /// </summary>
    public Uri? Host { get; init; }
    
    /// <summary>
    /// Username for authentication
    /// </summary>
    public string? Username { get; init; }
    
    /// <summary>
    /// Password for authentication
    /// </summary>
    [SensitiveData]
    public string? Password { get; init; }
    
    /// <summary>
    /// The base URL path component, used by clients like Transmission and Deluge
    /// </summary>
    public string? UrlBase { get; init; }

    /// <summary>
    /// Optional external URL for notifications when internal Docker URLs are not reachable externally
    /// </summary>
    public Uri? ExternalUrl { get; init; }

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
    /// Validates the configuration
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            throw new ValidationException($"Client name cannot be empty");
        }
        
        if (Host is null)
        {
            throw new ValidationException($"Host cannot be empty");
        }
    }
}
