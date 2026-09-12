using Cleanuparr.Domain.Enums;
using Cleanuparr.Persistence.Models.Configuration;
using Cleanuparr.Shared.Attributes;

namespace Cleanuparr.Api.Features.DownloadClient.Contracts.Responses;

public sealed record DownloadClientConfigResponse
{
    public Guid Id { get; init; }

    public bool Enabled { get; init; }

    public required string Name { get; init; }

    public DownloadClientTypeName TypeName { get; init; }

    public DownloadClientType Type { get; init; }

    public Uri? Host { get; init; }

    public string? Username { get; init; }

    /// <summary>
    /// The resolver masks by attribute on the serialized type, so this has to stay on the DTO.
    /// </summary>
    [SensitiveData]
    public string? Password { get; init; }

    public string? UrlBase { get; init; }

    public Uri? ExternalUrl { get; init; }

    public string? DownloadDirectorySource { get; init; }

    public string? DownloadDirectoryTarget { get; init; }

    public static DownloadClientConfigResponse From(DownloadClientConfig config) => new()
    {
        Id = config.Id,
        Enabled = config.Enabled,
        Name = config.Name,
        TypeName = config.TypeName,
        Type = config.Type,
        Host = config.Host,
        Username = config.Username,
        Password = config.Password,
        UrlBase = config.UrlBase,
        ExternalUrl = config.ExternalUrl,
        DownloadDirectorySource = config.DownloadDirectorySource,
        DownloadDirectoryTarget = config.DownloadDirectoryTarget,
    };
}
